package dev.bunnyloader.catalog

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.longOrNull
import kotlinx.serialization.json.put
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.File
import java.io.IOException
import java.net.URI
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.TimeUnit

/**
 * Os avisos do canal do Discord, como o tools/mods/discord_updates.py: um
 * embed em inglês por mod que mudou num commit do índice. Ficam numa fila no
 * aparelho até o Discord confirmar; o que falhar sai na próxima operação.
 */
class Discord(webhook: String, private val stateFile: File) {
    private val url: String? = validUrl(webhook)
    var log: (String) -> Unit = {}

    val configured get() = url != null

    private class State(
        val pending: MutableList<JsonObject>,
        var delivered: List<String>,
        var lastError: String,
        var retryAt: Long,
    )

    fun status(): JsonObject {
        val state = runCatching { load() }.getOrNull()
        return buildJsonObject {
            put("configured", configured)
            put("pending", state?.pending?.size ?: 0)
            put("error", state?.lastError ?: "")
            put("retryAt", state?.retryAt ?: 0L)
        }
    }

    /** Os eventos de um commit do índice: `before` e `after` são as listas de mods, com o manifesto. */
    fun announce(before: List<JsonObject>, after: List<JsonObject>, previousCommit: String, commit: String, paths: List<String>) {
        if (!configured) return
        val events = events(before, after, previousCommit, commit, paths)
        try {
            val state = load()
            val known = state.delivered.toHashSet() + state.pending.mapNotNull { it.text("id") }
            events.filter { it.text("id") !in known }.forEach { state.pending += it }
            save(state)
        } catch (e: Exception) {
            log("  Discord notifications could not be queued. The catalog operation succeeded.")
            return
        }
        flush()
    }

    /** O catalog_events() do Python: um evento (id + payload) por mod que entrou, mudou ou saiu. */
    fun events(before: List<JsonObject>, after: List<JsonObject>, previousCommit: String, commit: String,
               paths: List<String>): List<JsonObject> {
        val old = before.associateBy { it.text("uid") ?: "" }
        val new = after.associateBy { it.text("uid") ?: "" }
        val changed = HashMap<String, MutableList<String>>()
        for (path in paths) {
            val parts = path.split('/', limit = 3)
            if (parts.size == 3 && parts[0] == "mods") changed.getOrPut(parts[1]) { mutableListOf() } += parts[2]
        }
        return (old.keys + new.keys).sorted().mapNotNull { uid ->
            val previous = old[uid]
            var entry = new[uid]
            if (previous == entry && uid !in changed) return@mapNotNull null
            val kind = when {
                entry == null -> "removed".also { entry = previous }
                previous == null -> "published"
                previous.text("version") != entry!!.text("version") -> "updated"
                else -> "listing"
            }
            buildJsonObject {
                put("id", "$commit:$uid")
                put("payload", payload(kind, entry!!, previous, commit, previousCommit, changed[uid].orEmpty()))
            }
        }
    }

    /** Envia o que está na fila, até [MAX_PER_FLUSH] de uma vez, parando no primeiro erro. */
    fun flush() {
        val target = url ?: return
        val state = try {
            load()
        } catch (e: Exception) {
            log("  Discord notification history is unreadable; pending notifications were preserved.")
            return
        }
        if (state.pending.isEmpty()) return
        if (state.retryAt > System.currentTimeMillis()) {
            log("  Discord: waiting for the rate limit before retrying pending notifications.")
            return
        }
        repeat(minOf(state.pending.size, MAX_PER_FLUSH)) {
            val event = state.pending.first()
            try {
                send(target, event["payload"]!!.jsonObject)
            } catch (e: RateLimited) {
                state.retryAt = System.currentTimeMillis() + e.retryMs
                state.lastError = "Discord returned HTTP ${e.code}. Pending notifications will be retried."
                save(state)
                log("  " + state.lastError)
                return
            } catch (e: Exception) {
                state.lastError = "Discord could not confirm delivery. Pending notifications will be retried."
                save(state)
                log("  " + state.lastError)
                return
            }
            state.pending.removeAt(0)
            state.delivered = (state.delivered + (event.text("id") ?: "")).takeLast(2000)
            state.lastError = ""
            state.retryAt = 0
            save(state)
            log("  Discord notification sent.")
        }
        if (state.pending.isNotEmpty()) log("  Discord: ${state.pending.size} notification(s) pending.")
    }

    fun sendTest() {
        val target = url ?: throw Fail("Discord webhook is not configured.")
        val body = buildJsonObject {
            put("username", "Bunny Loader Updates")
            put("allowed_mentions", buildJsonObject { put("parse", JsonArray(emptyList())) })
            put("embeds", buildJsonArray {
                add(buildJsonObject {
                    put("title", "Catalog notifications connected")
                    put("description", "Bunny Loader will post confirmed package publications, version updates, catalog listing changes and removals in this channel.")
                    put("color", 0x5865F2)
                    put("footer", buildJsonObject { put("text", "Bunny Loader Catalog | Connection test") })
                    put("timestamp", now())
                })
            })
        }
        try {
            send(target, body)
        } catch (e: RateLimited) {
            throw Fail("Discord returned HTTP ${e.code}; check the webhook configuration.")
        } catch (e: IOException) {
            throw Fail("Discord could not be reached.")
        }
        log("Discord connection test sent.")
    }

    // ------------------------------------------------------------ envio

    private class RateLimited(val code: Int, val retryMs: Long) : IOException("HTTP $code")

    private fun send(target: String, body: JsonObject) {
        val request = Request.Builder().url(target)
            .header("User-Agent", "BunnyLoader-Catalog/1.0")
            .post(body.toString().toRequestBody("application/json".toMediaType()))
            .build()
        client.newCall(request).execute().use { response ->
            val text = response.body?.string().orEmpty()
            if (!response.isSuccessful) {
                val retry = if (response.code == 429) {
                    runCatching { Json.parseToJsonElement(text).jsonObject["retry_after"] as JsonPrimitive }
                        .getOrNull()?.doubleOrNull?.let { (maxOf(1.0, it) * 1000).toLong() } ?: 60_000L
                } else 0L
                throw RateLimited(response.code, retry)
            }
            val message = runCatching { Json.parseToJsonElement(text).jsonObject }.getOrNull()
            if (message?.text("id").isNullOrEmpty()) throw IOException("Discord did not confirm the notification.")
        }
    }

    // ------------------------------------------------------------ fila

    @Synchronized
    private fun load(): State {
        if (!stateFile.isFile) return State(mutableListOf(), emptyList(), "", 0)
        val o = Json.parseToJsonElement(stateFile.readText()).jsonObject
        return State(
            o["pending"]!!.jsonArray.map { it.jsonObject }.toMutableList(),
            o["delivered"]!!.jsonArray.mapNotNull { (it as? JsonPrimitive)?.contentOrNull },
            o.text("last_error") ?: "",
            (o["retry_at"] as? JsonPrimitive)?.longOrNull ?: 0L,
        )
    }

    @Synchronized
    private fun save(state: State) {
        stateFile.parentFile?.mkdirs()
        val temp = File(stateFile.path + ".tmp")
        temp.writeText(buildJsonObject {
            put("pending", JsonArray(state.pending))
            put("delivered", JsonArray(state.delivered.map(::JsonPrimitive)))
            put("last_error", state.lastError)
            put("retry_at", state.retryAt)
        }.toString())
        if (!temp.renameTo(stateFile)) {
            stateFile.delete()
            temp.renameTo(stateFile)
        }
    }

    // ------------------------------------------------------------ o embed

    private fun payload(kind: String, entry: JsonObject, previous: JsonObject?, commit: String,
                        previousCommit: String, changedFiles: List<String>): JsonObject {
        val (title, description, color) = when (kind) {
            "published" -> Triple("New package published", "A new package is now available in the Bunny Loader catalog.", 0x3BA55D)
            "updated" -> Triple("Package updated", "A new package version is now available in the Bunny Loader catalog.", 0x5865F2)
            "listing" -> Triple("Catalog listing updated", "The package's catalog information has been updated.", 0xE8B34B)
            else -> Triple("Package removed", "This package has been removed from the Bunny Loader catalog. Its existing GitHub releases remain available.", 0xED4245)
        }
        val manifest = entry["manifest"] as? JsonObject ?: JsonObject(emptyMap())
        val id = entry.text("id") ?: ""
        val version = entry.text("version") ?: ""
        val fields = mutableListOf(field("Version", clean(version, 100).ifEmpty { "Not specified" }, true))
        if (previous != null && previous.text("version") != entry.text("version")) {
            fields += field("Previous version", clean(previous.text("version"), 100).ifEmpty { "Not specified" }, true)
        }
        fields += field("Package type", packageType(manifest), true)
        fields += field("Authors", clean(Publisher.authorLine(manifest)).ifEmpty { "Not specified" }, false)
        val download = entry["download"] as? JsonObject
        (download?.get("size") as? JsonPrimitive)?.doubleOrNull?.takeIf { it >= 0 }?.let { size ->
            val text = if (size >= 1048576) String.format(Locale.US, "%.1f MB", size / 1048576)
            else String.format(Locale.US, "%.1f KB", size / 1024)
            fields += field("Package size", text, true)
        }
        fields += field("Package ID", clean(id, 100).ifEmpty { "Not specified" }, true)
        val release = "$REPOSITORY/releases/tag/mod-$id-v$version"
        val links = mutableListOf("[${if (kind == "removed") "Previous release" else "View release"}]($release)")
        if (kind != "removed") links += "[Download package](${download?.text("url") ?: ""})"
        fields += field("Links", links.joinToString("\n").take(1024), false)
        if (kind == "listing") {
            val changes = mutableListOf<String>()
            if (previous != null && previous["manifest"] != entry["manifest"]) changes += "Package details"
            if (changedFiles.any { f -> listOf("description", "changelog", "license").any(f::startsWith) }) {
                changes += "Description, release notes or license"
            }
            if (changedFiles.any { f -> listOf("icon.", "banner.", "authors/", "thumbnails/").any(f::startsWith) }) {
                changes += "Images or author profiles"
            }
            if (changes.isNotEmpty()) fields += field("Changes", changes.joinToString("; "), false)
        }
        val files = (entry["files"] as? JsonArray)?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull }.orEmpty()
        val icon = listOf("icon.gif", "icon.png").firstOrNull { it in files }
        return buildJsonObject {
            put("username", "Bunny Loader Updates")
            put("allowed_mentions", buildJsonObject { put("parse", JsonArray(emptyList())) })
            put("embeds", buildJsonArray {
                add(buildJsonObject {
                    put("title", clean("$title: ${entry.text("name")?.takeIf { it.isNotEmpty() } ?: id}", 256))
                    put("description", description)
                    put("url", release)
                    put("color", color)
                    put("fields", JsonArray(fields))
                    put("timestamp", now())
                    put("footer", buildJsonObject { put("text", "Bunny Loader Catalog") })
                    if (icon != null) {
                        val revision = if (kind == "removed") previousCommit else commit
                        put("thumbnail", buildJsonObject { put("url", "${Repo.RAW}/$revision/mods/${entry.text("uid")}/$icon") })
                    }
                })
            })
        }
    }

    private fun field(name: String, value: String, inline: Boolean) = buildJsonObject {
        put("name", name)
        put("value", value)
        put("inline", inline)
    }

    companion object {
        private const val MAX_PER_FLUSH = 10
        private const val REPOSITORY = "https://github.com/${Repo.OWNER}/${Repo.NAME}"
        private val PATH = Regex("/api/(?:v\\d+/)?webhooks/\\d+/[A-Za-z0-9._-]+")
        private val client = OkHttpClient.Builder()
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(10, TimeUnit.SECONDS)
            .build()

        /** O mesmo filtro do webhook_url() do discord_updates.py, com o `wait=true` que faz o Discord responder a mensagem. */
        fun validUrl(value: String): String? {
            val uri = runCatching { URI(value.trim()) }.getOrNull() ?: return null
            if (uri.scheme != "https" || uri.host != "discord.com" || uri.port != -1 || uri.rawFragment != null) return null
            if (!PATH.matches(uri.rawPath ?: "")) return null
            val query = uri.rawQuery?.split('&')?.filter { it.isNotEmpty() && !it.startsWith("wait=") }.orEmpty() + "wait=true"
            return "https://discord.com${uri.rawPath}?${query.joinToString("&")}"
        }

        /** O text() do Python: sem emoji nem controle, com o markdown escapado e cortado no limite. */
        fun clean(value: String?, limit: Int = 512): String {
            val noEmoji = (value ?: "").replace(Regex("<a?:\\w+:\\d+>|[0-9#*]️?⃣"), "")
            val sb = StringBuilder()
            var i = 0
            while (i < noEmoji.length) {
                val cp = noEmoji.codePointAt(i)
                i += Character.charCount(cp)
                val type = Character.getType(cp)
                if (type == Character.OTHER_SYMBOL.toInt() || type == Character.CONTROL.toInt() || type == Character.FORMAT.toInt()) continue
                if (cp in 0x1F000..0x1FFFF || cp == 0xFE0E || cp == 0xFE0F || cp == 0x20E3) continue
                sb.appendCodePoint(cp)
            }
            val escaped = sb.toString().trim().replace(Regex("([\\\\`*_~|\\[\\]])"), "\\\\$1")
            return if (escaped.length <= limit) escaped else escaped.take(limit - 3) + "..."
        }

        fun packageType(manifest: JsonObject): String = when ((manifest.text("type") ?: "mod").trim().lowercase()) {
            "font", "fonts", "fonte", "fontes" -> "Font pack"
            "texture", "textures", "textura", "texturas", "resourcepack", "resource-pack" -> "Texture pack"
            else -> "Mod"
        }

        private fun now(): String = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US)
            .apply { timeZone = TimeZone.getTimeZone("UTC") }.format(Date())

        private fun JsonObject.text(key: String): String? = (this[key] as? JsonPrimitive)?.contentOrNull
    }
}
