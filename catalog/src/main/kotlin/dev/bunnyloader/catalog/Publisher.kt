package dev.bunnyloader.catalog

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
class Publisher(token: String, private val uploads: File) {
    private val gh = token.takeIf { it.isNotEmpty() }?.let(::GitHub)
    private var head: Head? = null
    private var account: JsonObject? = null
    private val storeManifests = HashMap<String, JsonObject>()
    var log: (String) -> Unit = {}

    private class Head(
        val commit: String,
        val tree: String,
        val files: Map<String, String>,
        val index: JsonObject,
        val indexText: String,
    )

    fun state(): JsonObject {
        val h = head ?: loadHead().also { head = it }
        return buildJsonObject {
            put("token", account())
            put("worktree", true)
            put("catalog", JsonArray(mods(h.index).map { pageEntry(h, it) }))
            put("samples", JsonNull)
        }
    }

    fun sync() {
        account = null
        head = loadHead()
        log("Catálogo sincronizado com o GitHub.")
    }

    // ------------------------------------------------------------ conta

    private fun account(): JsonObject {
        account?.let { return it }
        val g = gh ?: return buildJsonObject {
            put("set", false)
            put("hint", "o APK foi gerado sem _token.txt")
        }
        return try {
            val user = g.call("GET", "https://api.github.com/user")?.jsonObject
            buildJsonObject {
                put("set", true)
                put("source", "embedded")
                put("login", user?.text("login"))
                put("avatar", user?.text("avatar_url"))
            }.also { account = it }
        } catch (e: Fail) {
            if (": 401 " in (e.message ?: "")) buildJsonObject {
                put("set", true)
                put("rejected", true)
                put("error", "o GitHub recusou o token embutido (vencido ou revogado?): gere o APK de novo")
            } else buildJsonObject {
                put("set", true)
                put("source", "embedded")
                put("error", e.message)
            }
        } catch (e: IOException) {
            buildJsonObject {
                put("set", true)
                put("source", "embedded")
                put("offline", true)
            }
        }
    }

    // ------------------------------------------------------------ leitura

    private fun loadHead(): Head {
        val g = gh ?: run {
            val text = String(GitHub.download("${Repo.RAW}/${Repo.BRANCH}/index.json"), Charsets.UTF_8)
            return Head(Repo.BRANCH, "", emptyMap(), Json.parseToJsonElement(text).jsonObject, text)
        }
        val ref = g.call("GET", "${Repo.API}/git/ref/heads/${Repo.BRANCH}")?.jsonObject
            ?: throw Fail("o branch ${Repo.BRANCH} não existe no GitHub")
        val commit = ref.obj("object").text("sha")!!
        val tree = g.call("GET", "${Repo.API}/git/commits/$commit")!!.jsonObject.obj("tree").text("sha")!!
        val listing = g.call("GET", "${Repo.API}/git/trees/$tree?recursive=1")!!.jsonObject
        if ((listing["truncated"] as? JsonPrimitive)?.booleanOrNull == true) {
            throw Fail("a árvore do ${Repo.BRANCH} é grande demais para a API")
        }
        val files = listing["tree"]!!.jsonArray.map { it.jsonObject }
            .filter { it.text("type") == "blob" }
            .associate { it.text("path")!! to it.text("sha")!! }
        val indexText = files["index.json"]?.let { sha ->
            val blob = g.call("GET", "${Repo.API}/git/blobs/$sha")!!.jsonObject
            String(Base64.decode(blob.text("content"), Base64.DEFAULT), Charsets.UTF_8)
        } ?: ""
        val index = if (indexText.isBlank()) buildJsonObject {
            put("format", FORMAT)
            put("mods", JsonArray(emptyList()))
        } else Json.parseToJsonElement(indexText).jsonObject
        return Head(commit, tree, files, index, indexText)
    }

    private fun pageEntry(h: Head, mod: JsonObject): JsonObject {
        val manifest = mod["manifest"] as? JsonObject ?: storeManifest(h, mod.text("uid") ?: "")
        val files = (mod["files"] as? JsonArray)?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull } ?: emptyList()
        val tag = (mod.obj("download").text("url") ?: "").substringAfter("/releases/download/").substringBefore('/')
        val icon = ICONS.firstOrNull { it in files }?.let { "${Repo.RAW}/${h.commit}/mods/${mod.text("uid")}/$it" }
        return JsonObject(mod + mapOf(
            "authors" to JsonPrimitive(authorLine(manifest)),
            "category" to JsonPrimitive(manifest.text("category") ?: ""),
            "summary" to JsonPrimitive(manifest.text("summary") ?: ""),
            "icon" to (icon?.let(::JsonPrimitive) ?: JsonNull),
            "release" to JsonPrimitive("https://github.com/${Repo.OWNER}/${Repo.NAME}/releases/tag/$tag"),
        ))
    }

    /** Entradas antigas do índice não levam o manifesto: lê o da vitrine, como o manager.py. */
    private fun storeManifest(h: Head, uid: String): JsonObject {
        val g = gh ?: return JsonObject(emptyMap())
        val sha = h.files["mods/$uid/manifest.json"] ?: return JsonObject(emptyMap())
        return storeManifests.getOrPut(sha) {
            runCatching {
                val blob = g.call("GET", "${Repo.API}/git/blobs/$sha")!!.jsonObject
                val text = String(Base64.decode(blob.text("content"), Base64.DEFAULT), Charsets.UTF_8)
                Json.parseToJsonElement(text.removePrefix("\uFEFF")).jsonObject
            }.getOrDefault(JsonObject(emptyMap()))
        }
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
            throw Fail("o arquivo precisa ser .bl, .bmod ou .zip")
        }
        return File(uploads, clean)
    }

    fun check(file: File): JsonObject {
        val pkg = BlPackage(file)
        pkg.warnings.forEach { log("aviso: $it") }
        val h = head ?: loadHead().also { head = it }
        val (code, text) = status(h, pkg.uid, pkg.version, pkg.sha256)
        val icon = ICONS.firstOrNull { it in pkg.names }?.let {
            "data:image/${it.substringAfter('.')};base64," + Base64.encodeToString(pkg.read(it), Base64.NO_WRAP)
        }
        return buildJsonObject {
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
            put("store", buildJsonArray { pkg.storeFiles().forEach { add(JsonPrimitive(it)) } })
            put("icon", icon)
            put("status", code)
            put("statusText", text)
        }
    }

    private fun status(h: Head, uid: String, version: String, sha: String): Pair<String, String> {
        val pub = mods(h.index).firstOrNull { it.text("uid") == uid } ?: return "new" to "Ainda não publicado"
        if (pub.obj("download").text("sha256") == sha) return "same" to "Em dia"
        val published = pub.text("version") ?: ""
        val c = compareVersions(version, published)
        return when {
            c > 0 -> "newer" to "Versão nova pronta (publicado: v$published)"
            c == 0 -> "changed" to "Mudou, mas a versão é a mesma: suba a versão"
            else -> "older" to "Local é mais velho que o publicado (v$published)"
        }
    }

    // ------------------------------------------------------------ publicar e tirar

    /** `dry`: confere e monta tudo, como o --dry-run do publish.py, sem gravar nada no GitHub. */
    fun publish(file: File, dry: Boolean = false) {
        val g = gh ?: throw Fail("sem token: gere o APK com o _token.txt")
        if (!file.isFile) throw Fail("o arquivo enviado não está mais aqui; envie de novo")
        val pkg = BlPackage(file)
        log("${pkg.name} v${pkg.version}  (${pkg.uid})")
        log("  ${pkg.size} bytes, sha256 ${pkg.sha256}")
        pkg.warnings.forEach { log("  aviso: $it") }
        val files = pkg.storeFiles()
        releaseFor(g, pkg, dry)
        commitIndex(g, "Publicar ${pkg.name} v${pkg.version}", dry) { h ->
            val old = mods(h.index).firstOrNull { it.text("uid") == pkg.uid }
            if (old != null && old.text("id") != pkg.id) {
                log("  aviso: o id mudou de '${old.text("id")}' para '${pkg.id}' (o uid manda)")
            }
            val prefix = "mods/${pkg.uid}/"
            val changes = mutableListOf<JsonObject>()
            for (f in files) {
                val data = pkg.read(f)
                if (h.files[prefix + f] != gitSha(data)) changes += blobEntry(g, prefix + f, data, dry)
            }
            h.files.keys.filter { it.startsWith(prefix) && it.removePrefix(prefix) !in files }
                .forEach { changes += deleteEntry(it) }
            val entry = buildJsonObject {
                put("uid", pkg.uid)
                put("id", pkg.id)
                put("name", pkg.name)
                put("version", pkg.version)
                put("download", buildJsonObject {
                    put("url", pkg.url)
                    put("sha256", pkg.sha256)
                    put("size", pkg.size)
                })
                put("files", buildJsonArray { files.forEach { add(JsonPrimitive(it)) } })
                put("manifest", Json.parseToJsonElement(pkg.manifestText))
            }
            log("  vitrine: ${files.joinToString(", ")}")
            changes to (mods(h.index).filter { it.text("uid") != pkg.uid } + entry)
        }
        if (!dry) file.delete()
    }

    fun remove(uid: String) {
        val g = gh ?: throw Fail("sem token: gere o APK com o _token.txt")
        if (!UID.matches(uid)) throw Fail("uid inválido")
        commitIndex(g, "Tirar $uid do catálogo") { h ->
            val mods = mods(h.index)
            if (mods.none { it.text("uid") == uid }) throw Fail("$uid não está no catálogo")
            h.files.keys.filter { it.startsWith("mods/$uid/") }.map(::deleteEntry) to
                mods.filter { it.text("uid") != uid }
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
                    throw Fail("${pkg.tag} já foi publicada com outro arquivo. " +
                        "Suba a versão no manifest.json em vez de trocar a publicada.")
                }
                log("  Release ${pkg.tag} já existe com este arquivo: reaproveitada")
                return
            }
        } else if (dry) {
            log("  [dry-run] Release ${pkg.tag} com ${pkg.asset} (${pkg.size} bytes)")
            return
        } else {
            release = g.call("POST", "${Repo.API}/releases", buildJsonObject {
                put("tag_name", pkg.tag)
                put("target_commitish", "main")
                put("name", "${pkg.name} v${pkg.version}")
                put("body", "Mod para o Bunny Loader.\n\n- uid: `${pkg.uid}`\n- sha256: `${pkg.sha256}`")
                put("make_latest", "false")
            })!!.jsonObject
        }
        if (dry) {
            log("  [dry-run] ${pkg.asset} subiria para o Release ${pkg.tag}")
            return
        }
        val id = (release!!["id"] as JsonPrimitive).content
        g.upload("${Repo.UPLOADS}/releases/$id/assets?name=${URLEncoder.encode(pkg.asset, "UTF-8")}",
            pkg.bytes, "application/zip")
        log("  Release ${pkg.tag} publicada")
    }

    /**
     * Um commit no mods-index com as mudanças que `build` monta sobre a ponta
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
                put("mods", JsonArray(mods.sortedBy { (it.text("name") ?: it.text("uid") ?: "").lowercase() }))
            })
            val text = PyJson.dump(index)
            val all = changes.toMutableList()
            if (text != h.indexText) all += textEntry("index.json", text)
            if (h.files["README.md"] != gitSha(README.toByteArray())) all += textEntry("README.md", README)
            if (all.isEmpty()) {
                log("  índice sem mudança")
                return
            }
            if (dry) {
                all.forEach { log("  [dry-run] ${if (it["sha"] == JsonNull) "apagaria" else "gravaria"} ${it.text("path")}") }
                log("  [dry-run] commit \"$message\" no ${Repo.BRANCH} (sem enviar)")
                return
            }
            val tree = g.call("POST", "${Repo.API}/git/trees", buildJsonObject {
                put("base_tree", h.tree)
                put("tree", JsonArray(all))
            })!!.jsonObject.text("sha")!!
            if (tree == h.tree) {
                log("  índice sem mudança")
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
                log("  ${Repo.BRANCH} enviado")
                return
            } catch (e: Fail) {
                if (": 422 " !in (e.message ?: "") || attempt == 2) throw e
                log("  o ${Repo.BRANCH} mudou no meio do caminho: refazendo em cima dele")
            }
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
        private val ICONS = listOf("icon.gif", "icon.png")
        private val UID = Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
        private const val README = "# Catálogo de mods do Bunny Loader\n\n" +
            "Este branch é gerado por `tools/mods/publish.py` (no `main`). Não edite à mão.\n\n" +
            "- `index.json`: a lista que o app lê.\n" +
            "- `mods/<uid>/`: a vitrine de cada mod (manifesto, ícone, capa, descrição).\n" +
            "- Os pacotes `.bl` estão nas Releases `mod-<id>-v<versão>`.\n"

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

        private fun authorLine(manifest: JsonObject): String {
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
