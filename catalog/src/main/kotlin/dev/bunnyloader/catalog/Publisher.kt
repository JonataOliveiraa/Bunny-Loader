package dev.bunnyloader.catalog

import android.os.Build
import android.util.Base64
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.put
import java.io.File
import java.io.IOException
import java.net.URLEncoder
import java.security.MessageDigest

/**
 * O publish.py e o manager.py em Kotlin, só com a API do GitHub: sem git no
 * celular, o índice vira um commit feito pela API de Git (blobs, árvore,
 * commit e o ref andando para a frente). A ordem é a mesma de lá: Release
 * primeiro, índice depois.
 */
class Publisher(token: String, private val uploads: File, private val discord: Discord,
                private val csharp: CSharpBuild) {
    private val gh = token.takeIf { it.isNotEmpty() }?.let(::GitHub)
    private var head: Head? = null
    private var account: JsonObject? = null
    private val storeManifests = HashMap<String, JsonObject>()
    private val storeJsonCache = HashMap<String, JsonObject>()
    var log: (String) -> Unit = {}
        set(value) {
            field = value
            discord.log = value
        }

    private class Head(
        val commit: String,
        val tree: String,
        val files: Map<String, String>,
        val index: JsonObject,
        val indexText: String,
        val commitDate: String = "",
        val fetchedAt: Long = System.currentTimeMillis(),
        val loadMs: Long = 0,
    )

    /** Tudo o que a tela mostra: a chave, a ponta do índice, a rede e os mods. */
    fun state(): JsonObject {
        val h = head ?: loadHead().also { head = it }
        return buildJsonObject {
            put("credential", account())
            put("remote", buildJsonObject {
                put("source", if (gh == null) "public" else "api")
                put("branch", Repo.BRANCH)
                put("commit", h.commit)
                put("commitDate", h.commitDate)
                put("tree", h.tree)
                put("files", h.files.size)
                put("format", (h.index["format"] as? JsonPrimitive)?.contentOrNull)
                put("indexBytes", h.indexText.toByteArray().size)
                put("fetchedAt", h.fetchedAt)
                put("loadMs", h.loadMs)
            })
            put("traffic", buildJsonObject {
                put("requests", Traffic.requests)
                put("sent", Traffic.sent)
                put("received", Traffic.received)
                put("latencyMs", Traffic.lastLatencyMs)
                put("quotaLimit", Traffic.quotaLimit)
                put("quotaRemaining", Traffic.quotaRemaining)
                put("quotaReset", Traffic.quotaReset)
            })
            put("app", buildJsonObject {
                put("version", BuildConfig.VERSION_NAME)
                put("build", BuildConfig.VERSION_CODE)
                put("blVersion", BlPackage.BL_VERSION)
                put("device", "${Build.MANUFACTURER} ${Build.MODEL}")
                put("android", "${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})")
            })
            val stats = storeJson(h, STATS)
            val downloads = stats["downloads"] as? JsonObject ?: JsonObject(emptyMap())
            val featured = featured(h)
            put("stats", buildJsonObject {
                put("updatedAt", stats.text("updatedAt"))
                put("total", downloads.values.sumOf { (it as? JsonPrimitive)?.contentOrNull?.toLongOrNull() ?: 0L })
            })
            put("discord", discord.status())
            put("featured", JsonArray(featured.map(::JsonPrimitive)))
            put("catalog", JsonArray(mods(h.index).map { mod ->
                val uid = mod.text("uid") ?: ""
                JsonObject(pageEntry(h, mod) + mapOf(
                    "downloads" to (downloads[uid] ?: JsonNull),
                    "featured" to JsonPrimitive(uid in featured),
                ))
            }))
        }
    }

    fun sync() {
        account = null
        head = loadHead()
        log(I18n.tr("Índice sincronizado: ${head!!.commit.take(7)} (${head!!.loadMs} ms)",
            "Index synced: ${head!!.commit.take(7)} (${head!!.loadMs} ms)"))
        discord.flush()
    }

    fun discordTest() = discord.sendTest()

    fun discordRetry() = discord.flush()

    // ------------------------------------------------------------ chave de acesso

    private fun account(): JsonObject {
        account?.let { return it }
        val g = gh ?: return buildJsonObject {
            put("present", false)
            put("error", I18n.tr("o APK foi gerado sem chave de acesso", "the APK was built without an access key"))
        }
        return try {
            val user = g.call("GET", "https://api.github.com/user")?.jsonObject
            val repo = g.call("GET", Repo.API)?.jsonObject
            buildJsonObject {
                put("present", true)
                put("valid", true)
                put("login", user?.text("login"))
                put("push", (repo?.obj("permissions")?.get("push") as? JsonPrimitive)?.booleanOrNull == true)
                put("repo", "${Repo.OWNER}/${Repo.NAME}")
                put("checkedAt", System.currentTimeMillis())
            }.also { account = it }
        } catch (e: Fail) {
            if (": 401 " in (e.message ?: "")) buildJsonObject {
                put("present", true)
                put("valid", false)
                put("error", I18n.tr("o servidor recusou a chave embutida (vencida ou revogada?): gere o APK de novo",
                    "the server rejected the embedded key (expired or revoked?): rebuild the APK"))
            } else buildJsonObject {
                put("present", true)
                put("error", e.message)
            }
        } catch (e: IOException) {
            buildJsonObject {
                put("present", true)
                put("offline", true)
            }
        }
    }

    // ------------------------------------------------------------ leitura

    private fun loadHead(): Head {
        val started = System.currentTimeMillis()
        val g = gh ?: run {
            val text = String(GitHub.download("${Repo.RAW}/${Repo.BRANCH}/$INDEX_PATH"), Charsets.UTF_8)
            return Head(Repo.BRANCH, "", emptyMap(), Json.parseToJsonElement(text).jsonObject, text,
                loadMs = System.currentTimeMillis() - started)
        }
        val ref = g.call("GET", "${Repo.API}/git/ref/heads/${Repo.BRANCH}")?.jsonObject
            ?: throw Fail(I18n.tr("o branch ${Repo.BRANCH} não existe no servidor", "branch ${Repo.BRANCH} does not exist on the server"))
        val commit = ref.obj("object").text("sha")!!
        val commitInfo = g.call("GET", "${Repo.API}/git/commits/$commit")!!.jsonObject
        val tree = commitInfo.obj("tree").text("sha")!!
        val listing = g.call("GET", "${Repo.API}/git/trees/$tree?recursive=1")!!.jsonObject
        if ((listing["truncated"] as? JsonPrimitive)?.booleanOrNull == true) {
            throw Fail(I18n.tr("a árvore do ${Repo.BRANCH} é grande demais para a API", "the ${Repo.BRANCH} tree is too large for the API"))
        }
        val files = listing["tree"]!!.jsonArray.map { it.jsonObject }
            .filter { it.text("type") == "blob" }
            .associate { it.text("path")!! to it.text("sha")!! }
        val indexText = files[INDEX_PATH]?.let { sha ->
            val blob = g.call("GET", "${Repo.API}/git/blobs/$sha")!!.jsonObject
            String(Base64.decode(blob.text("content"), Base64.DEFAULT), Charsets.UTF_8)
        } ?: ""
        val index = if (indexText.isBlank()) buildJsonObject {
            put("format", FORMAT)
            put("mods", JsonArray(emptyList()))
        } else Json.parseToJsonElement(indexText).jsonObject
        return Head(commit, tree, files, index, indexText,
            commitDate = commitInfo.obj("committer").text("date") ?: "",
            loadMs = System.currentTimeMillis() - started)
    }

    private fun pageEntry(h: Head, mod: JsonObject): JsonObject {
        val manifest = mod["manifest"] as? JsonObject ?: storeManifest(h, mod.text("uid") ?: "")
        val files = (mod["files"] as? JsonArray)?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull } ?: emptyList()
        val url = mod.obj("download").text("url") ?: ""
        val icon = ICONS.firstOrNull { it in files }?.let { "${Repo.RAW}/${h.commit}/${Repo.STORE}mods/${mod.text("uid")}/$it" }
        return JsonObject(mod + mapOf(
            "id" to JsonPrimitive(mod.text("id") ?: manifest.text("id") ?: ""),
            "name" to JsonPrimitive(mod.text("name") ?: manifest.text("name") ?: ""),
            "manifest" to manifest,
            "authors" to JsonPrimitive(authorLine(manifest)),
            "category" to JsonPrimitive(manifest.text("category") ?: ""),
            "summary" to JsonPrimitive(manifest.text("summary") ?: ""),
            "icon" to (icon?.let(::JsonPrimitive) ?: JsonNull),
            "tag" to JsonPrimitive(url.substringAfter("/releases/download/").substringBefore('/')),
            "asset" to JsonPrimitive(url.substringAfterLast('/')),
        ))
    }

    /** Entradas antigas do índice não levam o manifesto: lê o da vitrine, como o manager.py. */
    private fun storeManifest(h: Head, uid: String): JsonObject {
        val g = gh ?: return JsonObject(emptyMap())
        val sha = h.files["${Repo.STORE}mods/$uid/manifest.json"] ?: return JsonObject(emptyMap())
        return storeManifests.getOrPut(sha) {
            runCatching { Json.parseToJsonElement(blobText(g, sha)).jsonObject }.getOrDefault(JsonObject(emptyMap()))
        }
    }

    /** Um JSON de store/ na mesma ponta (stats.json da Action, featured.json); sem ele, vazio. */
    private fun storeJson(h: Head, name: String): JsonObject {
        val g = gh
        val path = Repo.STORE + name
        val key = if (g == null) "public:${h.fetchedAt}:$name" else h.files[path] ?: return JsonObject(emptyMap())
        storeJsonCache[key]?.let { return it }
        val parsed = runCatching {
            val text = if (g == null) String(GitHub.download("${Repo.RAW}/${Repo.BRANCH}/$path"), Charsets.UTF_8)
            else blobText(g, key)
            Json.parseToJsonElement(text.removePrefix("\uFEFF")).jsonObject
        }.getOrDefault(JsonObject(emptyMap()))
        storeJsonCache[key] = parsed
        return parsed
    }

    private fun featured(h: Head): List<String> =
        (storeJson(h, FEATURED)["mods"] as? JsonArray)?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull }.orEmpty()

    private fun blobText(g: GitHub, sha: String): String {
        val blob = g.call("GET", "${Repo.API}/git/blobs/$sha")!!.jsonObject
        return String(Base64.decode(blob.text("content"), Base64.DEFAULT), Charsets.UTF_8).removePrefix("\uFEFF")
    }

    // ------------------------------------------------------------ pacote enviado

    fun saveUpload(name: String, base64: String): File {
        val target = uploadFile(name)
        target.parentFile?.mkdirs()
        target.writeBytes(Base64.decode(base64, Base64.DEFAULT))
        return target
    }

    fun uploadFile(name: String): File {
        val clean = name.substringAfterLast('/').substringAfterLast('\\').replace(Regex("[^A-Za-z0-9._ -]"), "_")
        if (!clean.lowercase().let { it.endsWith(".bl") || it.endsWith(".bmod") || it.endsWith(".zip") }) {
            throw Fail(I18n.tr("o arquivo precisa ser .bl, .bmod ou .zip", "the file must be .bl, .bmod or .zip"))
        }
        return File(uploads, clean)
    }

    fun check(file: File): JsonObject {
        val built = csharp.prepare(file, log)
        val pkg = BlPackage(file)
        val store = pkg.storeFiles()
        pkg.warnings.forEach { log(I18n.tr("aviso: ", "warning: ") + it) }
        val h = head ?: loadHead().also { head = it }
        val (code, text) = status(h, pkg.uid, pkg.version, pkg.sha256)
        val published = mods(h.index).firstOrNull { it.text("uid") == pkg.uid }
        val icon = ICONS.firstOrNull { it in pkg.names }?.let {
            "data:image/${it.substringAfter('.')};base64," + Base64.encodeToString(pkg.read(it), Base64.NO_WRAP)
        }
        return buildJsonObject {
            put("manifest", pkg.manifest)
            put("entries", pkg.names.size)
            put("scripts", pkg.names.count { it.endsWith(".cs") })
            put("images", pkg.names.count { it.lowercase().let { n -> n.endsWith(".png") || n.endsWith(".gif") } })
            put("tag", pkg.tag)
            put("asset", pkg.asset)
            put("warnings", buildJsonArray { pkg.warnings.forEach { add(JsonPrimitive(it)) } })
            put("published", published?.let {
                buildJsonObject {
                    put("version", it.text("version"))
                    put("sha256", it.obj("download").text("sha256"))
                    put("size", (it.obj("download")["size"] as? JsonPrimitive)?.contentOrNull?.toLongOrNull())
                }
            } ?: JsonNull)
            put("file", file.name)
            put("uid", pkg.uid)
            put("id", pkg.id)
            put("name", pkg.name)
            put("version", pkg.version)
            put("authors", authorLine(pkg.manifest))
            put("category", pkg.manifest.text("category") ?: "")
            put("summary", pkg.manifest.text("summary") ?: "")
            put("size", pkg.size)
            put("sha256", pkg.sha256)
            put("store", buildJsonArray { store.forEach { add(JsonPrimitive(it)) } })
            put("icon", icon)
            put("status", code)
            put("statusText", text)
            put("csharp", built.info ?: JsonNull)
        }
    }

    private fun status(h: Head, uid: String, version: String, sha: String): Pair<String, String> {
        val pub = mods(h.index).firstOrNull { it.text("uid") == uid } ?: return "new" to I18n.tr("Ainda não publicado", "Not published yet")
        if (pub.obj("download").text("sha256") == sha) return "same" to I18n.tr("Em dia", "Up to date")
        val published = pub.text("version") ?: ""
        val c = compareVersions(version, published)
        return when {
            c > 0 -> "newer" to I18n.tr("Versão nova pronta (publicado: v$published)", "New version ready (published: v$published)")
            c == 0 -> "changed" to I18n.tr("Mudou, mas a versão é a mesma: suba a versão", "Changed, but the version is the same: bump the version")
            else -> "older" to I18n.tr("Local é mais velho que o publicado (v$published)", "Local is older than the published one (v$published)")
        }
    }

    // ------------------------------------------------------------ publicar e tirar

    /** `dry`: confere e monta tudo, como o --dry-run do publish.py, sem gravar nada no GitHub. */
    fun publish(file: File, dry: Boolean = false) {
        val g = gh ?: throw Fail(I18n.tr("sem chave: gere o APK com o tokenmodscsharp.txt", "no access key: build the APK with tokenmodscsharp.txt"))
        if (!file.isFile) throw Fail(I18n.tr("o arquivo enviado não está mais aqui; envie de novo", "the uploaded file is gone; upload it again"))
        csharp.prepare(file, log)
        val pkg = BlPackage(file)
        log("${pkg.name} v${pkg.version}  (${pkg.uid})")
        log("  ${pkg.size} bytes, sha256 ${pkg.sha256}")
        pkg.warnings.forEach { log(I18n.tr("  aviso: $it", "  warning: $it")) }
        val files = pkg.storeFiles()
        releaseFor(g, pkg, dry)
        commitIndex(g, "Publish ${pkg.name} v${pkg.version}", dry) { h ->
            val old = mods(h.index).firstOrNull { it.text("uid") == pkg.uid }
            val oldId = old?.obj("manifest")?.text("id")
            if (old != null && oldId != pkg.id) {
                log(I18n.tr("  aviso: o id mudou de '$oldId' para '${pkg.id}' (o uid manda)", "  warning: id changed from '$oldId' to '${pkg.id}' (uid wins)"))
            }
            val prefix = "${Repo.STORE}mods/${pkg.uid}/"
            val changes = mutableListOf<JsonObject>()
            for (f in files) {
                val data = pkg.read(f)
                if (h.files[prefix + f] != gitSha(data)) changes += blobEntry(g, prefix + f, data, dry)
            }
            h.files.keys.filter { it.startsWith(prefix) && it.removePrefix(prefix) !in files }
                .forEach { changes += deleteEntry(it) }
            val entry = buildJsonObject {
                put("uid", pkg.uid)
                put("version", pkg.version)
                put("download", buildJsonObject {
                    put("url", pkg.url)
                    put("sha256", pkg.sha256)
                    put("size", pkg.size)
                })
                put("files", buildJsonArray { files.forEach { add(JsonPrimitive(it)) } })
                put("manifest", Json.parseToJsonElement(pkg.manifestText))
                // Como o publish.py: a data em que o mod entrou, mantida nas versões novas.
                (if (old == null) utcNow() else old.text("publishedAt"))?.let { put("publishedAt", it) }
            }
            log(I18n.tr("  vitrine: ${files.joinToString(", ")}", "  store page: ${files.joinToString(", ")}"))
            changes to (mods(h.index).filter { it.text("uid") != pkg.uid } + entry)
        }
        if (!dry) file.delete()
    }

    fun remove(uid: String) {
        val g = gh ?: throw Fail(I18n.tr("sem chave: gere o APK com o tokenmodscsharp.txt", "no access key: build the APK with tokenmodscsharp.txt"))
        if (!UID.matches(uid)) throw Fail(I18n.tr("uid inválido", "invalid uid"))
        commitIndex(g, "Remove $uid from the catalog") { h ->
            val mods = mods(h.index)
            if (mods.none { it.text("uid") == uid }) throw Fail(I18n.tr("$uid não está no catálogo", "$uid is not in the catalog"))
            val changes = h.files.keys.filter { it.startsWith("${Repo.STORE}mods/$uid/") }.map(::deleteEntry).toMutableList()
            val featured = featured(h)
            if (uid in featured) {
                changes += textEntry(Repo.STORE + FEATURED, PyJson.dump(buildJsonObject {
                    put("format", FORMAT)
                    put("mods", JsonArray(featured.filter { it != uid }.map(::JsonPrimitive)))
                }))
            }
            changes to mods.filter { it.text("uid") != uid }
        }
    }

    private fun releaseFor(g: GitHub, pkg: BlPackage, dry: Boolean) {
        var release = g.call("GET", "${Repo.API}/releases/tags/${pkg.tag}")?.jsonObject
        if (release != null) {
            val asset = release["assets"]?.jsonArray?.map { it.jsonObject }?.firstOrNull { it.text("name") == pkg.asset }
            if (asset != null) {
                var digest = (asset.text("digest") ?: "").removePrefix("sha256:")
                if (digest.isEmpty()) {
                    digest = BlPackage.hex(MessageDigest.getInstance("SHA-256")
                        .digest(GitHub.download(asset.text("browser_download_url")!!)))
                }
                if (digest != pkg.sha256) {
                    throw Fail(I18n.tr("${pkg.tag} já foi publicada com outro arquivo. " +
                        "Suba a versão no manifest.json em vez de trocar a publicada.", "${pkg.tag} was already published with a different file. " +
                        "Bump the version in manifest.json instead of replacing the published one."))
                }
                log(I18n.tr("  Release ${pkg.tag} já existe com este arquivo: reaproveitada", "  Release ${pkg.tag} already has this file: reused"))
                return
            }
        } else if (dry) {
            log(I18n.tr("  [dry-run] Release ${pkg.tag} com ${pkg.asset} (${pkg.size} bytes)", "  [dry-run] Release ${pkg.tag} with ${pkg.asset} (${pkg.size} bytes)"))
            return
        } else {
            release = g.call("POST", "${Repo.API}/releases", buildJsonObject {
                put("tag_name", pkg.tag)
                put("target_commitish", "main")
                put("name", "${pkg.name} v${pkg.version}")
                put("body", "${pkg.name} for Bunny Loader.\n\n- uid: `${pkg.uid}`\n- sha256: `${pkg.sha256}`\n" +
                    "- license: ${pkg.manifest.text("license")?.takeIf { it.isNotEmpty() } ?: "see license.md"}\n")
                put("make_latest", "false")
            })!!.jsonObject
        }
        if (dry) {
            log(I18n.tr("  [dry-run] ${pkg.asset} subiria para o Release ${pkg.tag}", "  [dry-run] ${pkg.asset} would be uploaded to Release ${pkg.tag}"))
            return
        }
        val id = (release!!["id"] as JsonPrimitive).content
        g.upload("${Repo.UPLOADS}/releases/$id/assets?name=${URLEncoder.encode(pkg.asset, "UTF-8")}",
            pkg.bytes, "application/zip")
        log(I18n.tr("  Release ${pkg.tag} publicada", "  Release ${pkg.tag} published"))
    }

    /**
     * Um commit no main do repositório de mods com as mudanças que `build` monta sobre a ponta
     * atual. Se o branch andou no meio (outra publicação, o PC), refaz em cima
     * da ponta nova em vez de sobrescrever.
     */
    private fun commitIndex(g: GitHub, message: String, dry: Boolean = false,
                            build: (Head) -> Pair<List<JsonObject>, List<JsonObject>>) {
        repeat(3) { attempt ->
            val h = loadHead()
            head = h
            val (changes, mods) = build(h)
            val index = JsonObject(LinkedHashMap(h.index).apply {
                put("format", JsonPrimitive(FORMAT))
                put("mods", JsonArray(mods.sortedWith(compareBy(
                    { (it.obj("manifest").text("name") ?: "").lowercase() }, { it.text("uid") ?: "" }))))
            })
            val text = PyJson.dump(index)
            val all = changes.toMutableList()
            if (text != h.indexText) all += textEntry(INDEX_PATH, text)
            if (all.isEmpty()) {
                log(I18n.tr("  índice sem mudança", "  index unchanged"))
                return
            }
            if (dry) {
                all.forEach { log(I18n.tr("  [dry-run] ${if (it["sha"] == JsonNull) "apagaria" else "gravaria"} ${it.text("path")}", "  [dry-run] would ${if (it["sha"] == JsonNull) "delete" else "write"} ${it.text("path")}")) }
                log(I18n.tr("  [dry-run] commit \"$message\" no ${Repo.BRANCH} (sem enviar)", "  [dry-run] commit \"$message\" on ${Repo.BRANCH} (not pushed)"))
                return
            }
            val tree = g.call("POST", "${Repo.API}/git/trees", buildJsonObject {
                put("base_tree", h.tree)
                put("tree", JsonArray(all))
            })!!.jsonObject.text("sha")!!
            if (tree == h.tree) {
                log(I18n.tr("  índice sem mudança", "  index unchanged"))
                return
            }
            val commit = g.call("POST", "${Repo.API}/git/commits", buildJsonObject {
                put("message", message)
                put("tree", tree)
                put("parents", buildJsonArray { add(JsonPrimitive(h.commit)) })
            })!!.jsonObject.text("sha")!!
            try {
                g.call("PATCH", "${Repo.API}/git/refs/heads/${Repo.BRANCH}", buildJsonObject {
                    put("sha", commit)
                    put("force", false)
                })
                head = null
                log(I18n.tr("  ${Repo.BRANCH} enviado", "  ${Repo.BRANCH} pushed"))
                announce(h, mods, all, commit)
                return
            } catch (e: Fail) {
                if (": 422 " !in (e.message ?: "") || attempt == 2) throw e
                log(I18n.tr("  o ${Repo.BRANCH} mudou no meio do caminho: refazendo em cima dele", "  ${Repo.BRANCH} moved meanwhile: rebuilding on top of it"))
            }
        }
    }

    /** O aviso no Discord do commit que acabou de entrar; uma falha aqui não desfaz a publicação. */
    private fun announce(h: Head, after: List<JsonObject>, changes: List<JsonObject>, commit: String) {
        if (!discord.configured) return
        try {
            fun withManifest(list: List<JsonObject>) = list.map { mod ->
                if (mod["manifest"] is JsonObject) mod
                else JsonObject(mod + ("manifest" to storeManifest(h, mod.text("uid") ?: "")))
            }
            discord.announce(withManifest(mods(h.index)), withManifest(after), h.commit, commit,
                changes.mapNotNull { it.text("path") })
        } catch (e: Exception) {
            log("  Discord notifications could not be prepared. The catalog operation succeeded.")
        }
    }

    private fun blobEntry(g: GitHub, path: String, data: ByteArray, dry: Boolean): JsonObject {
        if (dry) return treeEntry(path) { put("sha", gitSha(data)) }
        val sha = g.call("POST", "${Repo.API}/git/blobs", buildJsonObject {
            put("content", Base64.encodeToString(data, Base64.NO_WRAP))
            put("encoding", "base64")
        })!!.jsonObject.text("sha")!!
        return treeEntry(path) { put("sha", sha) }
    }

    private fun utcNow(): String =
        java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", java.util.Locale.US)
            .apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }
            .format(java.util.Date())

    private fun textEntry(path: String, text: String) = treeEntry(path) { put("content", text) }

    private fun deleteEntry(path: String) = treeEntry(path) { put("sha", JsonNull) }

    private fun treeEntry(path: String, extra: kotlinx.serialization.json.JsonObjectBuilder.() -> Unit) = buildJsonObject {
        put("path", path)
        put("mode", "100644")
        put("type", "blob")
        extra()
    }

    companion object {
        private const val FORMAT = 1
        private const val INDEX_PATH = Repo.STORE + "index.json"
        private const val STATS = "stats.json"
        private const val FEATURED = "featured.json"
        private val ICONS = listOf("icon.gif", "icon.png")
        private val UID = Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")

        private fun mods(index: JsonObject): List<JsonObject> =
            (index["mods"] as? JsonArray)?.map { it.jsonObject } ?: emptyList()

        private fun JsonObject.text(key: String): String? = (this[key] as? JsonPrimitive)?.contentOrNull

        private fun JsonObject.obj(key: String): JsonObject = this[key] as? JsonObject ?: JsonObject(emptyMap())

        private fun gitSha(data: ByteArray): String {
            val md = MessageDigest.getInstance("SHA-1")
            md.update("blob ${data.size}\u0000".toByteArray())
            md.update(data)
            return BlPackage.hex(md.digest())
        }

        fun authorLine(manifest: JsonObject): String {
            val names = (manifest["authors"] as? JsonArray)?.mapNotNull {
                when (it) {
                    is JsonPrimitive -> it.contentOrNull
                    is JsonObject -> it.text("name")
                    else -> null
                }
            }?.filter { it.isNotEmpty() }.orEmpty()
            return names.ifEmpty { listOfNotNull(manifest.text("author")?.takeIf { it.isNotEmpty() }) }.joinToString(", ")
        }

        /** O version_key do manager.py: números por pedaço, o resto vale 0. */
        fun compareVersions(a: String, b: String): Int {
            fun key(v: String) = v.removePrefix("v").split('.', '-', '+').map { it.toIntOrNull() ?: 0 }
            val ka = key(a)
            val kb = key(b)
            for (i in 0 until maxOf(ka.size, kb.size)) {
                val c = ka.getOrElse(i) { 0 }.compareTo(kb.getOrElse(i) { 0 })
                if (c != 0) return c
            }
            return 0
        }
    }
}
