package dev.bunnyloader.mods

import java.io.File
import java.util.UUID
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive

/** Importa resource packs do PC sem executar codigo do pacote. */
internal object TexturePackFiles {
    private val json = Json { ignoreUnknownKeys = true; prettyPrint = true }

    fun images(dir: File): File? = listOf("content/Images", "Content/Images")
        .map { File(dir, it) }.firstOrNull { it.isDirectory }

    fun hasImages(dir: File): Boolean = images(dir)?.walkTopDown()?.any {
        it.isFile && it.extension.equals("png", ignoreCase = true)
    } == true

    fun importRoot(temp: File): File {
        var root = temp
        repeat(8) {
            if (Catalog.MANIFESTS.any { File(root, it).isFile } || File(root, "pack.json").isFile) return root
            val children = root.listFiles().orEmpty().filter { it.name != "__MACOSX" && !it.name.startsWith('.') }
            root = children.singleOrNull()?.takeIf { it.isDirectory } ?: return root
        }
        return root
    }

    fun convertPcPack(root: File): ModManifest? {
        if (Catalog.MANIFESTS.any { File(root, it).isFile }) return null
        val source = File(root, "pack.json").takeIf { it.isFile } ?: return null
        val pc = json.parseToJsonElement(source.readText().removePrefix("\uFEFF")).jsonObject
        fun text(key: String) = (pc[key] as? JsonPrimitive)?.contentOrNull.orEmpty()
        val name = text("Name")
        val author = text("Author")
        require(name.isNotBlank()) { "pack.json sem Name" }
        require(hasImages(root)) { "resource pack sem PNGs em Content/Images" }
        val version = pc["Version"] as? JsonObject
        val major = (version?.get("major") as? JsonPrimitive)?.intOrNull ?: 1
        val minor = (version?.get("minor") as? JsonPrimitive)?.intOrNull ?: 0
        require(major >= 0 && minor >= 0) { "versão inválida em pack.json" }
        val workshop = File(root, "workshop.json").takeIf { it.isFile }?.let {
            runCatching { json.parseToJsonElement(it.readText()).jsonObject["SteamEntryId"]?.jsonPrimitive?.content }
                .getOrNull()?.takeIf { id -> id.isNotEmpty() && id.all(Char::isDigit) }
        }
        // Workshop preserva a identidade entre atualizacoes. Sem Workshop,
        // nome+autor identifica a importacao local (nao publica no catalogo).
        val identity = workshop?.let { "steam-workshop:$it" } ?: "pc-resource-pack:$author\n$name"
        val uid = UUID.nameUUIDFromBytes(identity.toByteArray(Charsets.UTF_8)).toString()
        val manifest = ModManifest(
            uid = uid, id = "texture-$uid", name = name, version = "$major.$minor.0",
            author = author, type = "texture", category = "Texturas",
            description = text("Description"), blVersion = ModRepository.BL_VERSION,
            links = workshop?.let { listOf(PackLink("Steam Workshop", "https://steamcommunity.com/sharedfiles/filedetails/?id=$it")) }.orEmpty(),
        )
        val oldContent = File(root, "Content")
        val content = File(root, "content")
        if (oldContent.isDirectory && !content.exists()) {
            require(oldContent.renameTo(content)) { "não consegui converter Content/ para content/" }
        }
        File(root, "manifest.json").writeText(json.encodeToString(manifest))
        return manifest
    }
}
