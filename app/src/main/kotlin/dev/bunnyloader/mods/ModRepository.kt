package dev.bunnyloader.mods

import android.content.Context
import android.net.Uri
import android.provider.OpenableColumns
import kotlinx.serialization.json.Json
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import java.io.BufferedInputStream
import java.io.File
import java.io.FilterInputStream
import java.io.InputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipInputStream
import java.util.zip.ZipOutputStream

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
 * Um pacote é um zip (`.bl`, `.bmod` ou `.zip`). O conteúdo esperado está em
 * Catalog.Companion.
 */
class ModRepository(private val context: Context) {
    val modsDir: File = packsDir(context).also { migrateFrom(File(context.filesDir, "mods"), it) }
    private val prefs = context.getSharedPreferences("mods", Context.MODE_PRIVATE)
    private val orderPrefs = context.getSharedPreferences("mod_order", Context.MODE_PRIVATE)
    private val json = Json { ignoreUnknownKeys = true }

    /**
     * Ordenada pela escolha do usuario; sem escolha, pelo uid.
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
    fun list(): List<ModManifest> {
        val found = modsDir.listFiles().orEmpty()
        .filter { it.isDirectory && !it.name.startsWith('.') }
        .mapNotNull { dir -> readManifest(dir)?.let { dir to it } }
        .filter { (_, m) -> m.hasValidUid }
        .mapNotNull { (dir, m) -> m.takeIf { settle(dir, it) } }
        .distinctBy { it.uid }
        val saved = savedOrder()
        val byUid = found.associateBy { it.uid }
        val enabled = enabledUids(found)
        return ModOrder.resolve(found.map { it.uid }, saved, enabled).map { byUid.getValue(it) }
    }

    fun savedOrder(): List<String> = runCatching {
        json.decodeFromString<List<String>>(orderPrefs.getString("uids", "[]") ?: "[]")
    }.getOrDefault(emptyList())

    fun saveOrder(order: List<String>) {
        orderPrefs.edit().putString("uids", json.encodeToString(order)).apply()
    }

    fun move(uid: String, direction: Int) {
        val packages = list()
        val current = packages.map { it.uid }
        val changed = ModOrder.move(current, uid, direction, enabledUids(packages))
        if (changed != current) {
            orderPrefs.edit().putString("uids", json.encodeToString(changed)).apply()
        }
    }

    fun moveTo(uid: String, targetUid: String) {
        val packages = list()
        val current = packages.map { it.uid }
        val changed = ModOrder.moveTo(current, uid, targetUid, enabledUids(packages))
        if (changed != current) {
            orderPrefs.edit().putString("uids", json.encodeToString(changed)).apply()
        }
    }

    private fun enabledUids(packages: List<ModManifest>): Set<String> =
        packages.filter { !it.isOutdated && isEnabled(it.uid) }.map { it.uid }.toSet()

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
     * Um pacote desempacotado e conferido, ainda fora da lista: `installed` é o
     * manifesto do mesmo uid já instalado (null se é novo). `commit` o põe no
     * lugar; `discard` joga fora.
     */
    class StagedPackage internal constructor(
        val manifest: ModManifest,
        val installed: ModManifest?,
        internal val root: File,
        internal val temp: File,
    )

    /** Instala um pacote escolhido pelo seletor de arquivos (sem perguntar nada). */
    fun import(uri: Uri): Result<ModManifest> = stage(uri).mapCatching { commit(it).getOrThrow() }

    /** O mesmo import, para um pacote já no disco (o que o RemoteCatalog baixou). */
    fun import(file: File, validate: (ModManifest) -> Unit = {}): Result<ModManifest> =
        stage(file, validate).mapCatching { commit(it).getOrThrow() }

    /** Desempacota e confere; onProgress vai de 0 a 1 (o tamanho vem do seletor). */
    fun stage(uri: Uri, onProgress: (Float) -> Unit = {}): Result<StagedPackage> =
        stageFrom(sizeOf(uri), onProgress = onProgress) { context.contentResolver.openInputStream(uri) }

    fun stage(file: File, validate: (ModManifest) -> Unit = {}, onProgress: (Float) -> Unit = {}): Result<StagedPackage> =
        stageFrom(file.length(), validate, onProgress) { file.inputStream() }

    private fun sizeOf(uri: Uri): Long = runCatching {
        context.contentResolver.query(uri, arrayOf(OpenableColumns.SIZE), null, null, null)?.use { c ->
            if (c.moveToFirst() && !c.isNull(0)) c.getLong(0) else -1L
        } ?: -1L
    }.getOrDefault(-1L)

    /**
     * Desempacota num diretório temporário primeiro: se o zip não tiver
     * manifesto, ou pedir uma versão do loader que não existe, nada chega na
     * lista — em vez de deixar meio mod instalado e o jogo carregar pela
     * metade no próximo boot.
     *
     * O temporário fica DENTRO de bunny_packs (pasta oculta, que a lista
     * ignora): o pacote pronto entra no lugar com um rename, sem copiar tudo
     * de novo do armazenamento interno para o externo.
     */
    private fun stageFrom(
        size: Long,
        validate: (ModManifest) -> Unit = {},
        onProgress: (Float) -> Unit = {},
        open: () -> InputStream?,
    ): Result<StagedPackage> = runCatching {
        val temp = stagingDir().apply { deleteRecursively(); mkdirs() }
        try {
            onProgress(0f)
            val entries = open()
                ?.let { raw -> CountingInputStream(raw) { read -> if (size > 0) onProgress(UNZIP_SHARE * read / size) } }
                ?.use { unzip(it, temp) }
                ?: error("não consegui abrir o arquivo")
            require(entries > 0) {
                "isto não é um pacote: o arquivo precisa ser um zip (.bl, .bmod ou .zip) com " +
                    "manifest.json, icon.png e a pasta content/"
            }
            onProgress(UNZIP_SHARE)

            // Alguns compactadores põem tudo dentro de uma pasta com o nome do
            // pacote. Se a raiz só tem um diretório e o manifesto está lá, sobe um
            // nível — senão o mod instalaria com um andar a mais e não carregaria.
            val root = TexturePackFiles.importRoot(temp)
            TexturePackFiles.convertPcPack(root)

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
            require(!manifest.isOutdated) {
                "pacote do formato antigo (blVersion ${manifest.blVersion}). O Bunny Loader $BL_VERSION " +
                    "pede Assets/, Common/, Content/ e a classe Mod no arquivo de entrada " +
                    "(export default class ... extends Mod): peça ao autor uma versão nova."
            }
            if (manifest.packType == PackType.TEXTURE) {
                require(TexturePackFiles.hasImages(root)) { "pacote de textura sem PNGs em content/Images" }
            } else if (manifest.packType == PackType.FONT) {
                require(TexturePackFiles.hasFonts(root)) { "pacote de fonte sem .json em content/Fonts" }
            } else {
                require(entryOf(root) != null) { "pacote sem ${Catalog.CONTENT}/${manifest.entry}" }
            }

            // Admission runs before replacing an existing installation.
            validate(manifest)
            val installed = dirOf(manifest.uid)?.let { readManifest(it) }
            StagedPackage(manifest, installed, root, temp)
        } catch (e: Throwable) {
            temp.deleteRecursively()
            throw e
        }
    }

    /**
     * Põe o pacote no lugar (substituindo o instalado do mesmo uid). Um mod
     * NOVO entra no topo da lista, como quem acabou de ser ligado.
     */
    fun commit(staged: StagedPackage): Result<ModManifest> = runCatching {
        try {
            val manifest = staged.manifest
            val target = File(modsDir, manifest.uid)
            val isNew = !target.exists()
            // O antigo sai do caminho com um rename e só depois é apagado: o
            // novo entra inteiro, mesmo se apagar demorar ou falhar.
            val old = File(modsDir, ".old-${manifest.uid}")
            old.deleteRecursively()
            if (target.exists() && !target.renameTo(old)) target.deleteRecursively()
            target.parentFile?.mkdirs()
            if (!staged.root.renameTo(target)) staged.root.copyRecursively(target, overwrite = true)
            old.deleteRecursively()
            if (isNew) saveOrder(listOf(manifest.uid) + savedOrder().filter { it != manifest.uid })
            manifest
        } finally {
            staged.temp.deleteRecursively()
        }
    }

    fun discard(staged: StagedPackage) {
        staged.temp.deleteRecursively()
    }

    private fun stagingDir(): File {
        val inside = File(modsDir, ".import")
        return if (modsDir.isDirectory && modsDir.canWrite()) inside else File(context.cacheDir, "import")
    }

    /** Conta os bytes lidos do arquivo (o zip comprimido) para o progresso. */
    private class CountingInputStream(input: InputStream, private val onRead: (Long) -> Unit) : FilterInputStream(input) {
        private var count = 0L
        private var reported = 0L

        override fun read(): Int = super.read().also { if (it >= 0) advance(1) }

        override fun read(b: ByteArray, off: Int, len: Int): Int =
            super.read(b, off, len).also { if (it > 0) advance(it.toLong()) }

        private fun advance(n: Long) {
            count += n
            // Um aviso a cada 64 KB basta para a barra andar sem afogar a UI.
            if (count - reported >= 64 * 1024) {
                reported = count
                onRead(count)
            }
        }
    }

    /** Empacota o estado atual da pasta do mod como um ZIP .bl reimportável. */
    fun export(uid: String, uri: Uri): Result<Unit> = runCatching {
        val dir = requireNotNull(dirOf(uid)) { "mod $uid não encontrado" }
        require(readManifest(dir)?.uid == uid) { "manifesto inválido de $uid" }
        val base = dir.canonicalFile
        val files = dir.walkTopDown().filter { it.isFile }.sortedBy { it.path }.toList()
        require(files.isNotEmpty()) { "pasta do mod vazia" }
        files.forEach { file ->
            require(file.canonicalPath.startsWith(base.path + File.separator)) {
                "arquivo fora da pasta do mod: ${file.name}"
            }
        }
        val stream = requireNotNull(context.contentResolver.openOutputStream(uri, "wt")) {
            "não consegui criar o arquivo"
        }
        stream.use { output ->
            ZipOutputStream(output).use { zip ->
                files.forEach { file ->
                    val name = dir.toPath().relativize(file.toPath()).toString()
                        .replace(File.separatorChar, '/')
                    zip.putNextEntry(ZipEntry(name))
                    file.inputStream().use { it.copyTo(zip) }
                    zip.closeEntry()
                }
            }
        }
    }

    fun setEnabled(id: String, enabled: Boolean) {
        prefs.edit().putBoolean(id, enabled).apply()
    }
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
     * O que vai para o núcleo nativo: `uid=entry` para mods, `uid=@texture`
     * para PNGs sem codigo e `uid=@font` para fontes (content/Fonts).
     *
     * Os dois juntos na mesma entrada, e não em duas listas alinhadas por
     * índice, que é uma dessincronização esperando acontecer. O núcleo parte
     * no primeiro `=`.
     */
    fun enabledSpecs(): List<String> =
        list().filter { !it.isOutdated && isEnabled(it.uid) }.map { "${it.uid}=${nativeEntry(it)}" }

    private fun nativeEntry(m: ModManifest): String = when (m.packType) {
        PackType.TEXTURE -> "@texture"
        PackType.FONT -> "@font"
        PackType.MOD -> m.entry
    }

    /** O pacote esta no disco: script de mod, imagens de textura ou fontes no lugar. */
    fun hasEntry(uid: String): Boolean = dirOf(uid)?.let { entryOf(it) != null } ?: false

    private fun entryOf(dir: File): File? {
        val m = readManifest(dir) ?: return null
        if (m.packType == PackType.TEXTURE) return TexturePackFiles.images(dir)?.takeIf { TexturePackFiles.hasImages(dir) }
        if (m.packType == PackType.FONT) return TexturePackFiles.fonts(dir)?.takeIf { TexturePackFiles.hasFonts(dir) }
        val path = resolvePackagePath(dir.path, true, "${Catalog.CONTENT}/${m.entry}") ?: return null
        return File(path).takeIf { it.isFile }
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
        val base = target.canonicalPath + File.separator
        val buffer = ByteArray(COPY_BUFFER)
        ZipInputStream(BufferedInputStream(input, COPY_BUFFER)).use { zip ->
            generateSequence { zip.nextEntry }.forEach { entry ->
                count++
                val out = File(target, entry.name).canonicalFile
                require(out.path.startsWith(base)) { "caminho inválido no zip" }
                if (entry.isDirectory) out.mkdirs()
                else {
                    out.parentFile?.mkdirs()
                    out.outputStream().use { file ->
                        while (true) {
                            val n = zip.read(buffer)
                            if (n < 0) break
                            file.write(buffer, 0, n)
                        }
                    }
                }
            }
        }
        return count
    }

    /** A pasta de um mod instalado, se ele está no disco. */
    fun dirOf(uid: String): File? = File(modsDir, uid).takeIf { it.isDirectory }

    companion object {
        const val BL_VERSION = 2
        /** A extração é quase todo o trabalho do import: o resto (conferir, mover) é o fim da barra. */
        private const val UNZIP_SHARE = 0.95f
        private const val COPY_BUFFER = 64 * 1024
        /** O formato mais antigo que ainda carrega (ModManifest.blVersion). */
        const val MIN_BL_VERSION = 2
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
