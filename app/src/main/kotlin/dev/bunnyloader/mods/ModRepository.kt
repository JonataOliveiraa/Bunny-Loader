package dev.bunnyloader.mods

import android.content.Context
import android.net.Uri
import kotlinx.serialization.json.Json
import java.io.File
import java.io.InputStream
import java.util.zip.ZipInputStream

/**
 * Os mods instalados, em `Android/data/com.bunnyloader/bunny_packs/<uid>/` — a
 * mesma pasta de onde o núcleo nativo carrega. Importar é desempacotar ali; não
 * há segundo lugar nem índice paralelo que possa discordar do disco.
 *
 * Fica no armazenamento externo do app, ao lado de `Players/` e `Worlds/`, de
 * propósito: dá para abrir num gerenciador de arquivos (ou `adb push`), mexer
 * no `main.js` ou numa textura e só reabrir o jogo. Pasta colada ali à mão
 * aparece na lista como qualquer pacote importado.
 *
 * Um pacote é um zip (`.bmod` ou `.zip`). O conteúdo esperado está em
 * Catalog.Companion.
 */
class ModRepository(private val context: Context) {
    val modsDir: File = packsDir(context).also { migrateFrom(File(context.filesDir, "mods"), it) }
    private val prefs = context.getSharedPreferences("mods", Context.MODE_PRIVATE)
    private val json = Json { ignoreUnknownKeys = true }

    /**
     * Ordenada pelo uid, nao pela ordem do sistema de arquivos.
     *
     * A ordem de carga vira a ordem da CADEIA de hooks: quando dois mods
     * hookam o mesmo metodo, o primeiro carregado roda por fora e decide se o
     * segundo chega a rodar. Deixar isso a cargo do `listFiles()` faria dois
     * aparelhos se comportarem diferente com os mesmos mods.
     *
     * Pasta sem uid valido e ignorada aqui, e nao so recusada no import: o
     * disco pode ter sobra de uma versao anterior, e o que esta funcao devolve
     * e exatamente a lista que vai para o nucleo nativo carregar.
     */
    fun list(): List<ModManifest> = modsDir.listFiles().orEmpty()
        .filter { it.isDirectory }
        .mapNotNull { dir -> readManifest(dir)?.let { dir to it } }
        .filter { (_, m) -> m.hasValidUid }
        .mapNotNull { (dir, m) -> m.takeIf { settle(dir, it) } }
        .distinctBy { it.uid }
        .sortedBy { it.uid }

    /**
     * Pasta colada à mão com outro nome (`bunny_packs/MeuMod/`) vira
     * `bunny_packs/<uid>/`: é por esse nome que o núcleo carrega e a lista
     * acha o pacote. Antes ela entrava na contagem de ligados, mas não
     * aparecia na lista nem carregava. Se já existe a pasta do uid, a cópia
     * fica de fora (a do uid vale).
     */
    private fun settle(dir: File, manifest: ModManifest): Boolean {
        if (dir.name == manifest.uid) return true
        val target = File(modsDir, manifest.uid)
        if (target.exists()) return false
        return dir.renameTo(target)
    }

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
        val entries = context.contentResolver.openInputStream(uri)
            ?.use { unzip(it, temp) }
            ?: error("não consegui abrir o arquivo")
        require(entries > 0) {
            "isto não é um pacote: o arquivo precisa ser um zip (.bmod ou .zip) com " +
                "manifest.json, icon.png e a pasta content/"
        }

        // Alguns compactadores põem tudo dentro de uma pasta com o nome do
        // pacote. Se a raiz só tem um diretório e o manifesto está lá, sobe um
        // nível — senão o mod instalaria com um andar a mais e não carregaria.
        val root = temp.listFiles().orEmpty()
            .singleOrNull { it.isDirectory && readManifest(it) != null } ?: temp

        val manifest = readManifest(root)
            ?: error("pacote sem ${Catalog.MANIFESTS.first()}")
        require(manifest.hasValidUid) {
            if (manifest.uid.isBlank()) {
                "pacote sem uid. Todo mod precisa de um uid emitido pelo site " +
                    "do Bunny Loader — sem ele não dá para saber se isto é uma " +
                    "atualização do seu mod ou o mod de outra pessoa."
            } else {
                "uid inválido: \"${manifest.uid}\". O formato é um UUID " +
                    "minúsculo, como dfac5a5e-dd9a-4e57-a306-4147d34693cd."
            }
        }
        require(manifest.id.isNotBlank()) { "manifesto sem id" }
        require(manifest.blVersion <= BL_VERSION) {
            "o pacote pede o Bunny Loader ${manifest.blVersion}; este é o $BL_VERSION"
        }
        require(entryOf(root) != null) { "pacote sem ${Catalog.CONTENT}/${manifest.entry}" }

        val target = File(modsDir, manifest.uid)
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
    /**
     * O que vai para o núcleo nativo: `uid=entry`, um por mod habilitado.
     *
     * Os dois juntos na mesma entrada, e não em duas listas alinhadas por
     * índice, que é uma dessincronização esperando acontecer. O núcleo parte
     * no primeiro `=`.
     */
    fun enabledSpecs(): List<String> =
        list().filter { isEnabled(it.uid) }.map { "${it.uid}=${it.entry}" }

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

    /**
     * Protege contra zip slip: nenhuma entrada pode escrever fora do alvo.
     * Devolve quantas entradas leu — zero é "não era zip", que o
     * ZipInputStream não acusa sozinho.
     */
    private fun unzip(input: InputStream, target: File): Int {
        var count = 0
        ZipInputStream(input).use { zip ->
            generateSequence { zip.nextEntry }.forEach { entry ->
                count++
                val out = File(target, entry.name).canonicalFile
                require(out.path.startsWith(target.canonicalPath)) { "caminho inválido no zip" }
                if (entry.isDirectory) out.mkdirs()
                else { out.parentFile?.mkdirs(); out.outputStream().use { zip.copyTo(it) } }
            }
        }
        return count
    }

    /** A pasta de um mod instalado, se ele está no disco. */
    fun dirOf(uid: String): File? = File(modsDir, uid).takeIf { it.isDirectory }

    companion object {
        const val BL_VERSION = 1
        const val PACKS_DIR = "bunny_packs"

        /**
         * `Android/data/<pacote>/bunny_packs`. getExternalFilesDir aponta para
         * `.../<pacote>/files`; o pai é a pasta do app, a mesma dos saves. Sem
         * armazenamento externo (raro), cai na pasta interna.
         */
        fun packsDir(context: Context): File {
            val base = context.getExternalFilesDir(null)?.parentFile ?: context.filesDir
            return File(base, PACKS_DIR).apply { mkdirs() }
        }

        /**
         * Até 2026-09-24 os mods moravam em `filesDir/mods`, invisível para o
         * usuário. Muda uma vez: o que já existe no destino ganha (é mais novo).
         */
        private fun migrateFrom(old: File, target: File) {
            val children = old.listFiles() ?: return
            for (dir in children) {
                val dest = File(target, dir.name)
                if (!dest.exists() && !dir.renameTo(dest)) {
                    dir.copyRecursively(dest, overwrite = true)
                }
            }
            old.deleteRecursively()
        }
    }
}
