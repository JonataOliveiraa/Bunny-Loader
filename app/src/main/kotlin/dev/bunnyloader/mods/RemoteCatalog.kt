package dev.bunnyloader.mods

import android.content.Context
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.io.File
import java.io.IOException
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest

/**
 * Os mods publicados online: o branch `mods-index` do repositório do Bunny
 * Loader (o índice e a vitrine de cada mod) e as Releases (os pacotes).
 *
 *     mods-index/index.json            a lista: uid, versão, pacote, arquivos
 *     mods-index/mods/<uid>/...        manifest.json, icon.png, banner.png,
 *                                      description.md, authors/... — o que a
 *                                      ficha mostra, sem baixar o mod inteiro
 *     releases/mod-<id>-v<versão>      o .bl, que só desce ao instalar
 *
 * Tudo é leitura pública e anônima: o app não carrega token nenhum. Publicar é
 * coisa do tools/mods/publish.py, no PC de quem cuida do repositório.
 *
 * A vitrine baixada vira uma pasta comum em `cache/store/<uid>/`, e daí uma
 * Catalog.Entry de disco como qualquer pacote importado: a ficha, os autores e
 * o Markdown funcionam sem caminho novo. Ela só é baixada de novo quando o
 * sha256 do pacote muda, e a última lista boa fica para quando não há rede.
 */
class RemoteCatalog(private val context: Context, private val catalog: Catalog) {

    private val json = Json { ignoreUnknownKeys = true }
    private val storeDir get() = File(context.cacheDir, "store")
    private val indexFile get() = File(storeDir, INDEX)

    /** A última lista baixada, sem rede. Vazia na primeira abertura. */
    fun cached(): List<Catalog.Entry> = runCatching {
        entriesOf(json.decodeFromString<RemoteIndex>(indexFile.readText()))
    }.getOrDefault(emptyList())

    /**
     * Baixa o índice e a vitrine que mudou. Uma vitrine que falha fica de fora
     * desta lista e as outras seguem: um arquivo quebrado de um mod não pode
     * esconder o catálogo inteiro.
     */
    fun refresh(): List<Catalog.Entry> {
        val raw = fetch(INDEX_URL, MAX_INDEX_BYTES).decodeToString()
        val index = json.decodeFromString<RemoteIndex>(raw)
        require(index.format <= FORMAT) {
            "o catálogo online é de um Bunny Loader mais novo: atualize o app"
        }
        storeDir.mkdirs()
        val wanted = index.mods.filter(::isSane)
        for (mod in wanted) {
            runCatching { syncStore(mod) }
        }
        // Mod que saiu do índice sai do cache também.
        val keep = wanted.map { it.uid }.toSet()
        storeDir.listFiles().orEmpty()
            .filter { it.isDirectory && it.name !in keep }
            .forEach { it.deleteRecursively() }
        indexFile.writeText(raw)
        return entriesOf(index)
    }

    /**
     * Baixa o pacote para o cache e confere o sha256 enquanto lê. Devolve o
     * arquivo só se o hash bater: um .bl trocado ou cortado no meio do caminho
     * nunca chega no import.
     */
    fun download(mod: RemoteMod, onProgress: (Float) -> Unit): File {
        val target = File(context.cacheDir, "download/${mod.uid}.bl")
        target.parentFile?.mkdirs()
        val digest = MessageDigest.getInstance("SHA-256")
        val expected = mod.download.size
        try {
            open(mod.download.url).use { input ->
                target.outputStream().use { out ->
                    val buffer = ByteArray(64 * 1024)
                    var total = 0L
                    while (true) {
                        val n = input.read(buffer)
                        if (n < 0) break
                        total += n
                        if (total > MAX_PACKAGE_BYTES || (expected > 0 && total > expected)) {
                            throw IOException("o pacote é maior do que o catálogo diz")
                        }
                        digest.update(buffer, 0, n)
                        out.write(buffer, 0, n)
                        if (expected > 0) onProgress(total.toFloat() / expected)
                    }
                }
            }
            val got = digest.digest().joinToString("") { "%02x".format(it) }
            if (got != mod.download.sha256.lowercase()) {
                throw IOException("o arquivo baixado não confere (sha256 diferente do catálogo)")
            }
            return target
        } catch (e: Exception) {
            target.delete()
            throw e
        }
    }

    private fun entriesOf(index: RemoteIndex): List<Catalog.Entry> =
        index.mods.filter(::isSane).mapNotNull { mod ->
            val dir = File(storeDir, mod.uid)
            if (stampOf(dir) != mod.download.sha256.lowercase()) return@mapNotNull null
            val manifest = runCatching {
                json.decodeFromString<ModManifest>(File(dir, Catalog.MANIFESTS.first()).readText())
            }.getOrNull() ?: return@mapNotNull null
            // A vitrine diz quem ela é; se não for o mod do índice, não entra.
            if (manifest.uid != mod.uid) return@mapNotNull null
            catalog.fromDisk(manifest, dir).copy(sizeBytes = mod.download.size, remote = mod)
        }

    /** Baixa numa pasta ao lado e troca no fim: nunca fica uma vitrine pela metade. */
    private fun syncStore(mod: RemoteMod) {
        val dir = File(storeDir, mod.uid)
        val sha = mod.download.sha256.lowercase()
        if (stampOf(dir) == sha) return
        val part = File(storeDir, "${mod.uid}.part").apply { deleteRecursively(); mkdirs() }
        val base = INDEX_URL.substringBeforeLast('/') + "/mods/${mod.uid}/"
        for (rel in mod.files) {
            val out = File(part, rel)
            out.parentFile?.mkdirs()
            out.writeBytes(fetch(base + rel.split('/').joinToString("/") { encode(it) }, MAX_STORE_FILE_BYTES))
        }
        File(part, STAMP).writeText(sha)
        dir.deleteRecursively()
        if (!part.renameTo(dir)) {
            part.copyRecursively(dir, overwrite = true)
            part.deleteRecursively()
        }
    }

    private fun stampOf(dir: File): String? =
        runCatching { File(dir, STAMP).readText().trim() }.getOrNull()

    /**
     * O índice vem da rede: o uid vira nome de pasta e cada arquivo vira
     * caminho no disco. Nada de `..`, barra no começo ou URL que não seja
     * HTTPS do GitHub.
     */
    private fun isSane(mod: RemoteMod): Boolean =
        ModManifest.isValidUid(mod.uid) &&
            SHA256.matches(mod.download.sha256.lowercase()) &&
            ALLOWED_HOSTS.any { mod.download.url.startsWith(it) } &&
            Catalog.MANIFESTS.first() in mod.files &&
            mod.files.all { SAFE_PATH.matches(it) && it.split('/').none { s -> s == "." || s == ".." } }

    private fun fetch(url: String, limit: Long): ByteArray = open(url).use { input ->
        val bytes = input.readNBytesCompat(limit + 1)
        if (bytes.size > limit) throw IOException("arquivo grande demais: $url")
        bytes
    }

    private fun open(url: String): InputStream {
        val conn = URL(url).openConnection() as HttpURLConnection
        conn.connectTimeout = 15_000
        conn.readTimeout = 30_000
        conn.instanceFollowRedirects = true
        conn.setRequestProperty("User-Agent", "BunnyLoader")
        val code = conn.responseCode
        if (code != HttpURLConnection.HTTP_OK) {
            conn.disconnect()
            throw IOException("o servidor respondeu $code")
        }
        return conn.inputStream
    }

    companion object {
        const val FORMAT = 1
        private const val INDEX = "index.json"
        private const val STAMP = ".sha256"
        const val INDEX_URL =
            "https://raw.githubusercontent.com/JonataOliveiraa/Bunny-Loader/mods-index/index.json"

        /** O pacote só pode vir das Releases do repositório. */
        private val ALLOWED_HOSTS = listOf(
            "https://github.com/JonataOliveiraa/Bunny-Loader/releases/download/",
        )

        private const val MAX_INDEX_BYTES = 1L shl 20
        private const val MAX_STORE_FILE_BYTES = 4L shl 20
        private const val MAX_PACKAGE_BYTES = 200L shl 20

        private val SHA256 = Regex("^[0-9a-f]{64}$")
        private val SAFE_PATH = Regex("^[A-Za-z0-9._ -]+(/[A-Za-z0-9._ -]+)*$")

        private fun encode(segment: String) =
            java.net.URLEncoder.encode(segment, "UTF-8").replace("+", "%20")
    }
}

/** O index.json do branch mods-index. */
@Serializable
data class RemoteIndex(
    val format: Int = 1,
    val mods: List<RemoteMod> = emptyList(),
)

@Serializable
data class RemoteMod(
    val uid: String,
    val version: String = "",
    val download: RemoteDownload,
    /** A vitrine em `mods/<uid>/`, relativa a ela. manifest.json é obrigatório. */
    val files: List<String> = emptyList(),
)

@Serializable
data class RemoteDownload(
    val url: String,
    val sha256: String,
    val size: Long = 0,
)

/**
 * "1.10.0" > "1.9.2": compara número a número, e o que sobra no fim (um
 * "-beta") não decide nada. Positivo quando `a` é mais novo.
 */
fun compareVersions(a: String, b: String): Int {
    val pa = a.removePrefix("v").split('.', '-', '+').map { it.toIntOrNull() ?: 0 }
    val pb = b.removePrefix("v").split('.', '-', '+').map { it.toIntOrNull() ?: 0 }
    for (i in 0 until maxOf(pa.size, pb.size)) {
        val c = pa.getOrElse(i) { 0 }.compareTo(pb.getOrElse(i) { 0 })
        if (c != 0) return c
    }
    return 0
}

/**
 * A frase para a tela. As exceções de rede do Java vêm em inglês e com
 * endereço ("Failed to connect to /140.82.112.3:443"), que não diz nada a
 * quem está sem Wi-Fi.
 */
fun networkMessage(error: Throwable): String = when (error) {
    is java.net.UnknownHostException, is java.net.ConnectException ->
        "sem conexão com a internet"
    is java.net.SocketTimeoutException -> "a conexão demorou demais; tente de novo"
    is javax.net.ssl.SSLException -> "conexão segura recusada (confira a data do celular)"
    is kotlinx.serialization.SerializationException -> "o catálogo online veio num formato que este app não lê"
    else -> error.message ?: error.javaClass.simpleName
}

/** InputStream.readNBytes só existe do Android 13 em diante. */
private fun InputStream.readNBytesCompat(limit: Long): ByteArray {
    val out = java.io.ByteArrayOutputStream()
    val buffer = ByteArray(16 * 1024)
    var total = 0L
    while (total < limit) {
        val n = read(buffer, 0, minOf(buffer.size.toLong(), limit - total).toInt())
        if (n < 0) break
        out.write(buffer, 0, n)
        total += n
    }
    return out.toByteArray()
}
