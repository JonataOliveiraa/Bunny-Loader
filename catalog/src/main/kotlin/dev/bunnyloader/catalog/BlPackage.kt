package dev.bunnyloader.catalog

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonObject
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.File
import java.security.MessageDigest
import java.util.zip.ZipFile
import java.util.zip.ZipInputStream

class Fail(message: String) : Exception(message)

/** Um .bl aberto e conferido com as regras do publish.py (que são as do import do app). */
class BlPackage(val file: File) {
    val bytes: ByteArray = file.readBytes()
    val sha256: String = hex(MessageDigest.getInstance("SHA-256").digest(bytes))
    val size: Int = bytes.size
    val warnings = mutableListOf<String>()
    val manifest: JsonObject
    val manifestText: String
    val uid: String
    val id: String
    val version: String
    val name: String
    private val zip: ZipFile = open(file, bytes)
    val names: Set<String> = zip.entries().asSequence().filter { !it.isDirectory }.map { it.name }.toSet()

    init {
        val label = file.name
        for (n in names) {
            if (!PACKAGE_PATH.matches(n) || n.split('/').any { it == "." || it == ".." }) {
                throw Fail("$label: caminho que o app recusa: '$n'")
            }
        }
        if ("manifest.json" !in names) throw Fail("$label: sem manifest.json na raiz")
        manifestText = String(read("manifest.json"), Charsets.UTF_8).removePrefix("\uFEFF")
        manifest = try {
            Json.parseToJsonElement(manifestText).jsonObject
        } catch (e: Exception) {
            throw Fail("$label: manifest.json inválido (${e.message})")
        }
        uid = text("uid") ?: ""
        id = text("id") ?: ""
        version = text("version") ?: ""
        name = text("name") ?: id
        if (!UID.matches(uid)) throw Fail("$label: uid ausente ou fora do formato UUID minúsculo")
        if (!ID.matches(id)) throw Fail("$label: id '$id' precisa ser minúsculo, sem espaço")
        if (!VERSION.matches(version)) throw Fail("$label: versão '$version' não é do tipo 1.2.0")
        val declared = manifest["blVersion"]
        val bl = if (declared == null) 1 else (declared as? JsonPrimitive)?.takeIf { !it.isString }?.intOrNull
        if (bl != BL_VERSION) throw Fail("$label: blVersion ${declared ?: 1}; o app carrega $BL_VERSION")
        val kind = (text("type") ?: "").trim().lowercase()
        if (kind in TEXTURE_KINDS) {
            if (names.none { (it.startsWith("content/Images/") || it.startsWith("Content/Images/")) && it.lowercase().endsWith(".png") }) {
                throw Fail("$label: pacote de textura sem PNGs em content/Images")
            }
        } else {
            val entry = "content/${text("entry") ?: "main.js"}"
            if (entry !in names) throw Fail("$label: sem $entry")
        }
        if ("icon.png" !in names && "icon.gif" !in names) {
            val other = names.firstOrNull { it.lowercase() == "icon.png" || it.lowercase() == "icon.gif" }
            warnings += "$label sem icon.png" + (other?.let { " (achei '$it': o nome é minúsculo)" } ?: "") +
                "; o app usa o ícone da categoria"
        } else if ("icon.png" !in names) {
            warnings += "$label tem icon.gif mas não icon.png; no jogo o mod fica sem ícone"
        }
    }

    val tag get() = "mod-$id-v$version"
    val asset get() = "$id-$version.bl"
    val url get() = "${Repo.DOWNLOAD}/$tag/$asset"

    fun read(name: String): ByteArray = zip.getInputStream(zip.getEntry(name)).use { input ->
        val out = ByteArrayOutputStream()
        input.copyTo(out)
        out.toByteArray()
    }

    private fun text(key: String): String? = (manifest[key] as? JsonPrimitive)?.contentOrNull

    /** O que a ficha do mod precisa antes de baixar o pacote inteiro. */
    fun storeFiles(): List<String> {
        val files = STORE_ROOT_FILES.filter { it in names }.toMutableList()
        files += names.filter { LOCALIZED_PAGE.matches(it) }.sorted()
        files += names.filter { n -> STORE_DIRS.any { n.startsWith(it) } }.sorted()
        (manifest["pages"] as? JsonArray)?.forEach { page ->
            val f = ((page as? JsonObject)?.get("file") as? JsonPrimitive)?.contentOrNull ?: ""
            if (f in names) files += f
        }
        // O fundo da ficha (theme.background), quando é imagem e não cor.
        val background = (((manifest["theme"] as? JsonObject)?.get("background")) as? JsonPrimitive)
            ?.contentOrNull?.trim().orEmpty()
        if (background.isNotEmpty() && !HEX_COLOR.matches(background)) {
            if (background in names) files += background
            else warnings += "${file.name}: theme.background '$background' não está no pacote"
        }
        for (md in files.filter { it.endsWith(".md") }) {
            val text = String(read(md), Charsets.UTF_8)
            for (m in MD_IMAGE.findAll(text)) {
                val ref = percentDecode(m.groupValues[1].substringBefore('#'))
                if (ref in names) files += ref
            }
        }
        val out = files.distinct()
        for (f in out) {
            if (!SAFE_PATH.matches(f) || f.split('/').any { it == "." || it == ".." }) {
                throw Fail("${file.name}: caminho que a vitrine recusa: '$f'")
            }
            if (zip.getEntry(f).size > MAX_STORE_FILE) throw Fail("${file.name}: $f passa de 4 MB, o limite da vitrine")
        }
        return out
    }

    companion object {
        const val BL_VERSION = 2
        private const val MAX_STORE_FILE = 4L shl 20
        private val UID = Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
        private val ID = Regex("^[a-z0-9][a-z0-9._-]*$")
        private val SAFE_PATH = Regex("^[A-Za-z0-9._ -]+(/[A-Za-z0-9._ -]+)*$")
        private val PACKAGE_PATH = Regex("^[A-Za-z0-9._ ()-]+(/[A-Za-z0-9._ ()-]+)*$")
        private val VERSION = Regex("^\\d+(\\.\\d+)*([-+][0-9A-Za-z.]+)?$")
        private val MD_IMAGE = Regex("!\\[[^\\]]*\\]\\(([^)\\s]+)")
        private val TEXTURE_KINDS = setOf("texture", "textures", "textura", "texturas", "resourcepack", "resource-pack")
        // As capas na ordem do Catalog.BANNERS do app: ele usa a primeira que existir.
        private val STORE_ROOT_FILES = listOf("manifest.json", "icon.png", "icon.gif",
            "banner.gif", "banner.png", "banner.webp", "banner.jpg", "banner.jpeg",
            "description.md", "changelog.md", "license.md")
        private val LOCALIZED_PAGE = Regex("^(?:description|changelog)_[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*\\.md$")
        private val HEX_COLOR = Regex("^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")
        private val STORE_DIRS = listOf("authors/", "thumbnails/")

        fun hex(bytes: ByteArray) = bytes.joinToString("") { "%02x".format(it) }

        // Abrir não basta: estragado no caminho, o zip às vezes abre e só falha
        // ao ler uma entrada, e o pacote quebrado iria para a Release.
        private fun open(file: File, bytes: ByteArray): ZipFile {
            val zip = try {
                ZipFile(file)
            } catch (e: Exception) {
                throw Fail("${file.name}: não é zip: ${notZipReason(bytes)}")
            }
            if (!intact(bytes)) {
                zip.close()
                throw Fail("${file.name}: não é zip: ${notZipReason(bytes)}")
            }
            return zip
        }

        /** Todas as entradas lidas até o fim, com o CRC conferido (o ZipInputStream confere). */
        fun intact(bytes: ByteArray): Boolean = try {
            ZipInputStream(ByteArrayInputStream(bytes)).use { zip ->
                var entries = 0
                while (zip.nextEntry != null) {
                    zip.readBytes()
                    entries++
                }
                entries > 0
            }
        } catch (e: Exception) {
            false
        }

        /**
         * Por que estes bytes não abrem como zip, do jeito que quem publica
         * entende (o mesmo texto do publish.py). O .bl costuma chegar estragado
         * no caminho: mandado por app de mensagem, copiado como texto, baixado
         * pela página e não pelo arquivo.
         */
        fun notZipReason(data: ByteArray): String {
            if (data.isEmpty()) return "o arquivo está vazio (0 bytes); exporte de novo"
            val head = String(data, 0, minOf(512, data.size), Charsets.ISO_8859_1).trimStart().lowercase()
            if (head.startsWith("<!doctype") || head.startsWith("<html") || (head.startsWith("<") && "<html" in head)) {
                return "é uma página da web, não o pacote: o link baixou a página. Baixe o arquivo em si"
            }
            if (data.size < 2 || data[0] != 'P'.code.toByte() || data[1] != 'K'.code.toByte()) {
                val first = data.take(4).joinToString(" ") { "%02x".format(it) }
                return "não começa como zip (os primeiros bytes são $first, um .bl começa com 50 4b)"
            }
            if (hasCrlf(data) && intact(withoutCr(data))) {
                return "o arquivo foi tratado como texto no caminho (cada \\n virou \\r\\n). " +
                    "Mande-o como arquivo, ou renomeie para .zip antes de enviar"
            }
            if (!hasEndRecord(data)) return "o zip está cortado: o fim dele não chegou (${data.size} bytes). Copie ou baixe de novo"
            return "o zip está corrompido; exporte de novo pelo Bunny Loader"
        }

        private const val CR: Byte = 13
        private const val LF: Byte = 10

        private fun hasCrlf(data: ByteArray): Boolean =
            (0 until data.size - 1).any { data[it] == CR && data[it + 1] == LF }

        private fun withoutCr(data: ByteArray): ByteArray {
            val out = ByteArrayOutputStream(data.size)
            for (i in data.indices) {
                if (data[i] == CR && i + 1 < data.size && data[i + 1] == LF) continue
                out.write(data[i].toInt())
            }
            return out.toByteArray()
        }

        // A assinatura do fim do diretório (PK 05 06) fica nos últimos 64 KB + 22 bytes.
        private fun hasEndRecord(data: ByteArray): Boolean {
            val from = maxOf(0, data.size - 65_557)
            return (from until data.size - 3).any {
                data[it] == 'P'.code.toByte() && data[it + 1] == 'K'.code.toByte() && data[it + 2] == 5.toByte() && data[it + 3] == 6.toByte()
            }
        }

        private fun percentDecode(s: String): String {
            if ('%' !in s) return s
            val out = ByteArrayOutputStream()
            var i = 0
            while (i < s.length) {
                val c = s[i]
                if (c == '%' && i + 2 < s.length && s.substring(i + 1, i + 3).all { it.isDigit() || it.lowercaseChar() in 'a'..'f' }) {
                    out.write(s.substring(i + 1, i + 3).toInt(16))
                    i += 3
                } else {
                    out.write(c.toString().toByteArray())
                    i++
                }
            }
            return String(out.toByteArray(), Charsets.UTF_8)
        }
    }
}
