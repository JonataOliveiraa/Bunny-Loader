package dev.bunnyloader.mods

import android.content.Context
import android.net.Uri
import kotlinx.serialization.json.Json
import java.io.File
import java.io.InputStream
import java.util.zip.ZipInputStream

/**
 * Os mods instalados, em `filesDir/mods/<id>/` — a mesma pasta de onde o núcleo
 * nativo carrega. Importar é desempacotar ali; não há segundo lugar nem índice
 * paralelo que possa discordar do disco.
 *
 * Um pacote é um `.bmod` (zip). O conteúdo esperado está em Catalog.Companion.
 */
class ModRepository(private val context: Context) {
    val modsDir: File = File(context.filesDir, "mods").apply { mkdirs() }
    private val prefs = context.getSharedPreferences("mods", Context.MODE_PRIVATE)
    private val json = Json { ignoreUnknownKeys = true }

    /**
     * Ordenada pelo uid, nao pela ordem do sistema de arquivos.
     *
     * A ordem de carga vira a ordem da CADEIA de hooks: quando dois mods
     * hookam o mesmo metodo, o primeiro carregado roda por fora e decide se o
     * segundo chega a rodar. Deixar isso a cargo do `listFiles()` faria dois
     * aparelhos se comportarem diferente com os mesmos mods.
     */
    fun list(): List<ModManifest> = modsDir.listFiles().orEmpty()
        .filter { it.isDirectory }
        .mapNotNull { dir -> readManifest(dir) }
        .sortedBy { it.key }

    /**
     * Instala um `.bmod` escolhido pelo seletor de arquivos.
     *
     * Desempacota num diretório temporário primeiro: se o zip não tiver
     * manifesto, ou pedir uma versão do loader que não existe, nada chega em
     * `mods/` — em vez de deixar meio mod instalado e o jogo carregar pela
     * metade no próximo boot.
     */
    fun import(uri: Uri): Result<ModManifest> = runCatching {
        val temp = File(context.cacheDir, "import").apply { deleteRecursively(); mkdirs() }
        context.contentResolver.openInputStream(uri)
            ?.use { unzip(it, temp) }
            ?: error("não consegui abrir o arquivo")

        // Alguns compactadores põem tudo dentro de uma pasta com o nome do
        // pacote. Se a raiz só tem um diretório e o manifesto está lá, sobe um
        // nível — senão o mod instalaria com um andar a mais e não carregaria.
        val root = temp.listFiles().orEmpty()
            .singleOrNull { it.isDirectory && readManifest(it) != null } ?: temp

        val manifest = readManifest(root)
            ?: error("pacote sem ${Catalog.MANIFESTS.first()}")
        require(manifest.id.isNotBlank()) { "manifesto sem id" }
        require(manifest.blVersion <= BL_VERSION) {
            "o pacote pede o Bunny Loader ${manifest.blVersion}; este é o $BL_VERSION"
        }
        require(entryOf(root) != null) { "pacote sem ${Catalog.CONTENT}/${manifest.entry}" }

        val target = File(modsDir, manifest.key)
        target.deleteRecursively()
        target.parentFile?.mkdirs()
        if (!root.renameTo(target)) root.copyRecursively(target, overwrite = true)
        temp.deleteRecursively()
        manifest
    }

    fun setEnabled(id: String, enabled: Boolean) = prefs.edit().putBoolean(id, enabled).apply()
    /**
     * Um mod sem escolha registrada segue o padrao das Configuracoes. E o que
     * faz "Ligar ao instalar" valer para o proximo pacote sem precisar varrer
     * a lista inteira gravando preferencia para cada um.
     */
    fun isEnabled(id: String): Boolean = prefs.getBoolean(id, defaultEnabled)

    private val defaultEnabled: Boolean
        get() = context.getSharedPreferences("settings", Context.MODE_PRIVATE)
            .getBoolean("enableOnInstall", true)
    fun enabledIds(): List<String> = list().filter { isEnabled(it.key) }.map { it.key }

    /** O arquivo de entrada, em `content/` ou na raiz (formato antigo). */
    private fun entryOf(dir: File): File? {
        val m = readManifest(dir) ?: return null
        return listOf(File(dir, "${Catalog.CONTENT}/${m.entry}"), File(dir, m.entry))
            .firstOrNull { it.isFile }
    }

    private fun readManifest(dir: File): ModManifest? = Catalog.MANIFESTS
        .map { File(dir, it) }
        .firstOrNull { it.isFile }
        ?.let { f -> runCatching { json.decodeFromString<ModManifest>(f.readText()) }.getOrNull() }

    /** Protege contra zip slip: nenhuma entrada pode escrever fora do alvo. */
    private fun unzip(input: InputStream, target: File) {
        ZipInputStream(input).use { zip ->
            generateSequence { zip.nextEntry }.forEach { entry ->
                val out = File(target, entry.name).canonicalFile
                require(out.path.startsWith(target.canonicalPath)) { "caminho inválido no zip" }
                if (entry.isDirectory) out.mkdirs()
                else { out.parentFile?.mkdirs(); out.outputStream().use { zip.copyTo(it) } }
            }
        }
    }

    companion object {
        const val BL_VERSION = 1
    }
}
