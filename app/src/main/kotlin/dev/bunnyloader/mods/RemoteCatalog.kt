package dev.bunnyloader.mods

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.io.File
import java.io.IOException
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import java.util.concurrent.atomic.AtomicInteger

/**
 * Os mods publicados online: o repositório Bunny-Loader-Mods, separado do
 * código do app (um pedido de remoção contra um mod não derruba o app), com o
 * índice e a vitrine de cada mod no `main` e os pacotes nas Releases.
 *
 *     main/index.json                  a lista: uid, versão, pacote, arquivos e o
 *                                      manifesto de cada mod (`manifest`), e
 *                                      os mods em destaque no Início (`featured`)
 *     main/stats.json                  downloads de cada mod, somados das
 *                                      Releases por uma Action a cada 6 h
 *     main/mods/<uid>/...              icon.png (ou .gif), banner.png (ou .gif...), description.md,
 *                                      authors/... — o que a ficha mostra, sem
 *                                      baixar o mod inteiro
 *     releases/mod-<id>-v<versão>      o .bl, que só desce ao instalar
 *
 * O custo de rede cresce devagar com o número de mods:
 *  - a lista sai do índice sozinho (uma requisição), porque ele traz o
 *    manifesto de cada mod; e o índice vai com o ETag da última vez, então
 *    sem mudança o servidor responde 304, sem corpo;
 *  - o ícone de um mod desce quando o cartão dele aparece na tela
 *    ([ensureFiles] com icon.png), e o resto da vitrine quando a ficha abre;
 *  - um arquivo baixado vale enquanto o sha256 do pacote não muda.
 * Índice antigo, sem `manifest`, ainda funciona: o manifesto de cada mod é
 * baixado na atualização; o ícone continua sendo pedido só quando aparece.
 *
 * Tudo é leitura pública e anônima: o app não carrega token nenhum. Publicar é
 * coisa do tools/mods/publish.py, no PC de quem cuida do repositório.
 *
 * A vitrine baixada vira uma pasta comum em `cache/store/<uid>/`, e daí uma
 * Catalog.Entry de disco como qualquer pacote importado: a ficha, os autores e
 * o Markdown funcionam sem caminho novo.
 */
class RemoteCatalog(private val context: Context, private val catalog: Catalog) {

    private val json = Json { ignoreUnknownKeys = true }
    private val storeDir get() = File(context.cacheDir, "store")
    private val indexFile get() = File(storeDir, INDEX)
    private val etagFile get() = File(storeDir, ETAG)
    private val statsFile get() = File(storeDir, STATS)
    private val statsEtagFile get() = File(storeDir, STATS_ETAG)
    private val locks = java.util.concurrent.ConcurrentHashMap<String, Any>()

    /** A última lista baixada, sem rede. Vazia na primeira abertura. */
    fun cached(): CatalogSnapshot = runCatching {
        snapshotOf(json.decodeFromString<RemoteIndex>(indexFile.readText()), cachedDownloads())
    }.getOrDefault(CatalogSnapshot())

    /**
     * Baixa o índice (ou confirma, pelo ETag, que é o mesmo) e acerta o cache:
     * a vitrine de um mod que mudou de versão sai, para descer de novo quando
     * for vista. Com índice antigo, sem manifesto, baixa somente o manifesto
     * de cada mod que mudou.
     */
    suspend fun refresh(onProgress: (CatalogProgress) -> Unit = {}): CatalogSnapshot {
        storeDir.mkdirs()
        val etag = runCatching { etagFile.readText().trim() }.getOrNull()?.takeIf { indexFile.isFile }
        val response = fetchIfChanged(INDEX_URL, etag, MAX_INDEX_BYTES)
        val raw = response?.body?.decodeToString() ?: indexFile.readText()
        val index = json.decodeFromString<RemoteIndex>(raw)
        require(index.format <= FORMAT) {
            "o catálogo online é de um Bunny Loader mais novo: atualize o app"
        }
        val wanted = index.mods.filter(::isSane).distinctBy { it.uid }
        // Versão nova de um mod: a vitrine velha sai inteira.
        for (mod in wanted) {
            val dir = File(storeDir, mod.uid)
            if (dir.isDirectory && stampOf(dir) != mod.download.sha256.lowercase()) dir.deleteRecursively()
        }
        val legacy = wanted.filter { it.manifest == null }
        onProgress(CatalogProgress(0, legacy.size))
        if (legacy.isNotEmpty()) {
            // Vitrines independentes, com limite para não abrir uma conexão por mod.
            val slots = Semaphore(2)
            val completed = AtomicInteger()
            coroutineScope {
                legacy.map { mod ->
                    async(Dispatchers.IO) {
                        slots.withPermit {
                            runCatching { ensureFiles(mod, listOf(Catalog.MANIFESTS.first())) }
                            onProgress(CatalogProgress(completed.incrementAndGet(), legacy.size))
                        }
                    }
                }.awaitAll()
            }
        }
        // Mod que saiu do índice sai do cache também.
        val keep = wanted.map { it.uid }.toSet()
        storeDir.listFiles().orEmpty()
            .filter { it.isDirectory && it.name !in keep }
            .forEach { it.deleteRecursively() }
        if (response != null) {
            indexFile.writeText(raw)
            if (response.etag != null) etagFile.writeText(response.etag) else etagFile.delete()
        }
        return snapshotOf(index, refreshDownloads())
    }

    /**
     * Os downloads de cada mod. São só um enfeite da lista: sem rede ou com o
     * arquivo quebrado, ficam os da última vez (ou nenhum), e o catálogo segue.
     */
    private fun refreshDownloads(): Map<String, Long> = runCatching {
        val etag = runCatching { statsEtagFile.readText().trim() }.getOrNull()?.takeIf { statsFile.isFile }
        val response = fetchIfChanged(STATS_URL, etag, MAX_STATS_BYTES) ?: return cachedDownloads()
        val raw = response.body.decodeToString()
        val counts = downloadsOf(raw)
        statsFile.writeText(raw)
        if (response.etag != null) statsEtagFile.writeText(response.etag) else statsEtagFile.delete()
        counts
    }.getOrElse { cachedDownloads() }

    private fun cachedDownloads(): Map<String, Long> =
        runCatching { downloadsOf(statsFile.readText()) }.getOrDefault(emptyMap())

    private fun downloadsOf(raw: String): Map<String, Long> =
        json.decodeFromString<RemoteStats>(raw).downloads
            .filter { (uid, count) -> ModManifest.isValidUid(uid) && count >= 0 }

    /**
     * Garante no cache estes arquivos da vitrine de um mod (os que o índice
     * lista; o resto é ignorado). Só baixa o que falta. Chamado fora da thread
     * da UI: pelo ícone que aparece na tela e pela ficha que abre.
     *
     * @return a entrada do mod refeita, com os arquivos novos, ou null se o
     *   manifesto dele não está disponível.
     */
    fun ensureFiles(mod: RemoteMod, rels: List<String>): Catalog.Entry? {
        synchronized(locks.getOrPut(mod.uid) { Any() }) {
            val dir = File(storeDir, mod.uid)
            val sha = mod.download.sha256.lowercase()
            if (stampOf(dir) != sha) {
                dir.deleteRecursively()
                dir.mkdirs()
                File(dir, STAMP).writeText(sha)
            }
            val base = INDEX_URL.substringBeforeLast('/') + "/mods/${mod.uid}/"
            for (rel in rels.distinct()) {
                if (rel !in mod.files) continue
                val out = File(dir, rel)
                if (out.isFile) continue
                out.parentFile?.mkdirs()
                val part = File(out.path + ".part")
                part.writeBytes(fetch(base + rel.split('/').joinToString("/") { encode(it) }, MAX_STORE_FILE_BYTES))
                if (!part.renameTo(out)) {
                    part.copyTo(out, overwrite = true)
                    part.delete()
                }
            }
            return entryOf(mod)
        }
    }

    /** Os arquivos da ficha: tudo o que o índice lista. */
    fun ensureStore(mod: RemoteMod): Catalog.Entry? = ensureFiles(mod, mod.files)

    /** A vitrine inteira já está no cache? */
    fun hasStore(mod: RemoteMod): Boolean {
        val dir = File(storeDir, mod.uid)
        return stampOf(dir) == mod.download.sha256.lowercase() && mod.files.all { File(dir, it).isFile }
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

    private fun snapshotOf(index: RemoteIndex, downloads: Map<String, Long>): CatalogSnapshot {
        val entries = index.mods.filter(::isSane).distinctBy { it.uid }.mapNotNull(::entryOf)
        return CatalogSnapshot(entries, index.featured.filter(ModManifest::isValidUid).distinct(), downloads)
    }

    /**
     * A entrada de um mod: o manifesto do índice (ou, no índice antigo, o da
     * vitrine baixada) e o que já houver da vitrine no cache.
     */
    private fun entryOf(mod: RemoteMod): Catalog.Entry? {
        val dir = File(storeDir, mod.uid)
        val fresh = stampOf(dir) == mod.download.sha256.lowercase()
        val manifest = mod.manifest ?: (if (!fresh) null else runCatching {
            json.decodeFromString<ModManifest>(File(dir, Catalog.MANIFESTS.first()).readText())
        }.getOrNull()) ?: return null
        // A vitrine diz quem ela é; se não for o mod do índice, não entra.
        if (manifest.uid != mod.uid) return null
        // Vitrine de outra versão não vale: a entrada fica sem ícone até ele descer.
        val base = catalog.fromDisk(manifest, if (fresh) dir else File(storeDir, "${mod.uid}.none"), mod.download.size)
        return base.copy(assetDir = dir.path, sizeBytes = mod.download.size, remote = mod)
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
            (mod.manifest != null || Catalog.MANIFESTS.first() in mod.files) &&
            mod.files.all { SAFE_PATH.matches(it) && it.split('/').none { s -> s == "." || s == ".." } }

    private class CachedResponse(val body: ByteArray, val etag: String?)

    /** O arquivo, ou null quando o servidor confirma (304) que é o mesmo do cache. */
    private fun fetchIfChanged(url: String, etag: String?, limit: Long): CachedResponse? {
        val conn = connect(url)
        if (etag != null) conn.setRequestProperty("If-None-Match", etag)
        val code = conn.responseCode
        if (code == HttpURLConnection.HTTP_NOT_MODIFIED && etag != null) {
            conn.disconnect()
            return null
        }
        if (code != HttpURLConnection.HTTP_OK) {
            conn.disconnect()
            throw IOException("o servidor respondeu $code")
        }
        val body = conn.inputStream.use { it.readNBytesCompat(limit + 1) }
        if (body.size > limit) throw IOException("arquivo grande demais: $url")
        return CachedResponse(body, conn.getHeaderField("ETag"))
    }

    private fun fetch(url: String, limit: Long): ByteArray = open(url).use { input ->
        val bytes = input.readNBytesCompat(limit + 1)
        if (bytes.size > limit) throw IOException("arquivo grande demais: $url")
        bytes
    }

    private fun connect(url: String): HttpURLConnection {
        val conn = URL(url).openConnection() as HttpURLConnection
        conn.connectTimeout = 15_000
        conn.readTimeout = 30_000
        conn.instanceFollowRedirects = true
        conn.setRequestProperty("User-Agent", "BunnyLoader")
        return conn
    }

    private fun open(url: String): InputStream {
        val conn = connect(url)
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
        private const val ETAG = "index.etag"
        private const val STATS = "stats.json"
        private const val STATS_ETAG = "stats.etag"
        private const val STAMP = ".sha256"
        const val INDEX_URL =
            "https://raw.githubusercontent.com/JonataOliveiraa/Bunny-Loader-Mods/main/index.json"
        private val STATS_URL = INDEX_URL.substringBeforeLast('/') + "/stats.json"

        /**
         * O pacote só pode vir das Releases do catálogo. As do repositório do
         * app valem enquanto os pacotes antigos não são copiados para o de
         * mods (tools/mods/migrate_releases.py); depois, esta linha sai.
         */
        private val ALLOWED_HOSTS = listOf(
            "https://github.com/JonataOliveiraa/Bunny-Loader-Mods/releases/download/",
            "https://github.com/JonataOliveiraa/Bunny-Loader/releases/download/",
        )

        /** Com o manifesto de cada mod dentro, o índice cresce ~2 KB por mod. */
        private const val MAX_INDEX_BYTES = 8L shl 20
        /** Um uid e um número por mod: ~60 bytes cada. */
        private const val MAX_STATS_BYTES = 1L shl 20
        private const val MAX_STORE_FILE_BYTES = 4L shl 20
        private const val MAX_PACKAGE_BYTES = 200L shl 20

        private val SHA256 = Regex("^[0-9a-f]{64}$")
        private val SAFE_PATH = Regex("^[A-Za-z0-9._ -]+(/[A-Za-z0-9._ -]+)*$")

        private fun encode(segment: String) =
            java.net.URLEncoder.encode(segment, "UTF-8").replace("+", "%20")
    }
}

/** Por quanto tempo um mod recém-publicado aparece como novo no Explorar. */
const val NEW_MOD_MS = 7L * 24 * 60 * 60 * 1000

data class CatalogProgress(val completed: Int, val total: Int)

/** O que uma leitura do catálogo online traz para a tela. */
data class CatalogSnapshot(
    val entries: List<Catalog.Entry> = emptyList(),
    /** Os uids em destaque no Início, na ordem do índice. */
    val featured: List<String> = emptyList(),
    /** Downloads de cada mod, pelo uid. Vazio enquanto o stats.json não existe. */
    val downloads: Map<String, Long> = emptyMap(),
)

/** O index.json do repositório de mods. */
@Serializable
data class RemoteIndex(
    val format: Int = 1,
    val mods: List<RemoteMod> = emptyList(),
    /**
     * Os mods do "Em destaque" do Início, escolhidos por quem cuida do
     * catálogo (publish.py --feature). Vale mais que o `featured` do
     * manifesto, que qualquer autor pode marcar.
     */
    val featured: List<String> = emptyList(),
)

/**
 * O stats.json do repositório de mods: a Action download-stats soma, a cada
 * 6 h, o download_count de todas as Releases de cada mod (versões antigas
 * inclusive). Cada atualização conta de novo, como no GitHub.
 */
@Serializable
data class RemoteStats(
    val format: Int = 1,
    val updatedAt: String? = null,
    val downloads: Map<String, Long> = emptyMap(),
)

@Serializable
data class RemoteMod(
    val uid: String,
    val version: String = "",
    val download: RemoteDownload,
    /** A vitrine em `mods/<uid>/`, relativa a ela. */
    val files: List<String> = emptyList(),
    /**
     * O manifest.json do mod, dentro do índice (publish.py desde 2026-10-03).
     * Sem ele (índice antigo), o manifesto vem da vitrine e é obrigatório nela.
     */
    val manifest: ModManifest? = null,
    /**
     * Quando o mod entrou no catálogo, em UTC ("2026-10-21T15:04:05Z"): o
     * publish.py grava na primeira publicação e mantém nas versões seguintes.
     * Mod publicado antes do campo vem sem ele e nunca conta como novo.
     */
    val publishedAt: String? = null,
) {
    /** Entrou no catálogo há menos de [NEW_MOD_MS]. */
    fun isNew(now: Long): Boolean {
        val at = publishedMillis ?: return false
        return now - at in 0 until NEW_MOD_MS
    }

    val publishedMillis: Long? get() = publishedAt?.let { text ->
        runCatching {
            java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", java.util.Locale.US)
                .apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }
                .parse(text)?.time
        }.getOrNull()
    }

    /** O ícone que a lista baixa: o animado, se a vitrine tiver; um só dos dois. */
    val iconFile: String get() = Catalog.ICONS.firstOrNull { it in files } ?: Catalog.ICON

    /** A capa que a vitrine tem, na mesma preferência do pacote no disco. */
    val bannerFile: String get() = Catalog.BANNERS.firstOrNull { it in files } ?: Catalog.BANNER
}

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
