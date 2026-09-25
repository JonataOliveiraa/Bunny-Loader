package dev.bunnyloader.mods

import android.content.Context
import android.graphics.BitmapFactory
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import kotlinx.serialization.json.Json
import java.io.File

/**
 * Catálogo dos mods que vêm dentro do app.
 *
 * Eles moram em `assets/mods/<pasta>/`, copiados de `samples/` pelo Gradle —
 * a mesma pasta que o CMake lê. Isso é de propósito: o que a vitrine lista é
 * literalmente o arquivo que o motor vai avaliar, então tamanho, versão e data
 * são o arquivo de verdade e não uma tabela que envelhece sozinha.
 *
 * Instalar é copiar para `bunny_packs/<uid>/`, que já é de onde o núcleo
 * nativo carrega (`ModLoader::loadAll`). Nenhum caminho novo: a vitrine só põe
 * arquivo onde o motor já procurava.
 */
class Catalog(private val context: Context) {

    /**
     * Um mod para a vitrine. Os do catálogo apontam para `assets/` (caminho
     * relativo); os que só existem no disco — importados ou colados à mão em
     * bunny_packs — apontam para arquivos (caminho absoluto). `loadBitmap`
     * entende os dois.
     */
    data class Entry(
        val manifest: ModManifest,
        val assetDir: String,
        val sizeBytes: Long,
        val previews: List<String>,
        val iconAsset: String?,
        val bannerAsset: String?,
        val onDisk: Boolean = false,
    ) {
        /** Identidade do pacote. Ver ModManifest.uid. */
        val uid get() = manifest.uid
    }

    private val json = Json { ignoreUnknownKeys = true }
    private val repo = ModRepository(context)

    val entries: List<Entry> by lazy { scan() }

    private fun scan(): List<Entry> {
        val dirs = runCatching { context.assets.list(ROOT)?.toList() }.getOrNull().orEmpty()
        return dirs.mapNotNull { dir ->
            val base = "$ROOT/$dir"
            val files = runCatching { context.assets.list(base)?.toList() }.getOrNull().orEmpty()
            val manifestName = MANIFESTS.firstOrNull { it in files } ?: return@mapNotNull null
            val manifest = runCatching {
                json.decodeFromString<ModManifest>(read(base, manifestName).decodeToString())
            }.getOrNull() ?: return@mapNotNull null
            // A mesma regra do import vale para os nossos: um mod embutido sem
            // uid valido nao aparece na vitrine. Se um dia um sample for
            // empacotado errado, ele some da lista em vez de instalar numa
            // pasta que o `list()` do repositorio depois ignora.
            if (!manifest.hasValidUid) return@mapNotNull null

            val shots = THUMBS.firstOrNull { it in files }
            val previews = shots?.let {
                runCatching { context.assets.list("$base/$it")?.toList() }.getOrNull().orEmpty()
                    .filter { f -> f.endsWith(".png") || f.endsWith(".jpg") }
                    .sorted()
                    .map { f -> "$base/$it/$f" }
            }.orEmpty()

            Entry(
                manifest = manifest,
                assetDir = base,
                sizeBytes = treeSize(base),
                previews = previews,
                iconAsset = if (ICON in files) "$base/$ICON" else null,
                bannerAsset = if (BANNER in files) "$base/$BANNER" else null,
            )
        }.sortedBy { it.manifest.name }
    }

    /** Bytes de tudo no pacote — é o número que a ficha do mod mostra. */
    private fun treeSize(dir: String): Long {
        val children = runCatching { context.assets.list(dir)?.toList() }.getOrNull().orEmpty()
        if (children.isEmpty()) return 0
        return children.sumOf { child ->
            val path = "$dir/$child"
            val sub = runCatching { context.assets.list(path)?.toList() }.getOrNull().orEmpty()
            if (sub.isEmpty()) runCatching { read(dir, child).size.toLong() }.getOrDefault(0L)
            else treeSize(path)
        }
    }

    fun isInstalled(uid: String): Boolean =
        File(repo.modsDir, "$uid/content/main.js").isFile ||
            File(repo.modsDir, "$uid/main.js").isFile

    /** Copia o pacote para onde o motor procura. Substitui se já existir. */
    fun install(entry: Entry) {
        val target = File(repo.modsDir, entry.uid)
        target.deleteRecursively()
        target.mkdirs()
        copyTree(entry.assetDir, target)
    }

    fun uninstall(uid: String) {
        File(repo.modsDir, uid).deleteRecursively()
    }

    /**
     * Na primeira vez, deixa um mod já instalado.
     *
     * Sem isto o app abre com a pasta de mods vazia e o núcleo cai nos
     * embutidos da libbunny — o jogo funcionaria, mas a aba de Pacotes estaria
     * vazia contradizendo a tela. Uma marca em prefs garante que o usuário
     * possa desinstalar sem o app reinstalar por cima na próxima abertura.
     */
    fun seedOnFirstRun() {
        val prefs = context.getSharedPreferences("catalog", Context.MODE_PRIVATE)
        if (prefs.getBoolean(SEEDED, false)) return
        entries.firstOrNull { it.manifest.featured }?.let(::install)
        prefs.edit().putBoolean(SEEDED, true).apply()
    }

    /**
     * Quando o app é atualizado, recopia os mods do catálogo que já estão
     * instalados. Sem isto a cópia em bunny_packs ficava a da primeira
     * instalação: o APK novo trazia o Example Mod com tiles e o aparelho seguia
     * com o antigo. Ligado/desligado fica em prefs pelo uid e não se perde.
     */
    fun refreshInstalledOnUpdate() {
        val prefs = context.getSharedPreferences("catalog", Context.MODE_PRIVATE)
        val stamp = runCatching {
            context.packageManager.getPackageInfo(context.packageName, 0).lastUpdateTime
        }.getOrDefault(0L)
        if (stamp == 0L || prefs.getLong(APP_STAMP, 0L) == stamp) return
        for (entry in entries) {
            if (isInstalled(entry.uid)) runCatching { install(entry) }
        }
        prefs.edit().putLong(APP_STAMP, stamp).apply()
    }

    /** A entrada de um mod que está só no disco (a pasta dele em bunny_packs). */
    fun fromDisk(manifest: ModManifest, dir: File): Entry {
        val shots = THUMBS.map { File(dir, it) }.firstOrNull { it.isDirectory }
        return Entry(
            manifest = manifest,
            assetDir = dir.path,
            sizeBytes = dir.walkTopDown().filter { it.isFile }.sumOf { it.length() },
            previews = shots?.listFiles().orEmpty()
                .filter { it.name.endsWith(".png") || it.name.endsWith(".jpg") }
                .sortedBy { it.name }
                .map { it.path },
            iconAsset = File(dir, ICON).takeIf { it.isFile }?.path,
            bannerAsset = File(dir, BANNER).takeIf { it.isFile }?.path,
            onDisk = true,
        )
    }

    /** Caminho absoluto = arquivo no disco; relativo = dentro de `assets/`. */
    fun loadBitmap(path: String): ImageBitmap? = runCatching {
        if (path.startsWith("/")) BitmapFactory.decodeFile(path)?.asImageBitmap()
        else context.assets.open(path).use { BitmapFactory.decodeStream(it) }?.asImageBitmap()
    }.getOrNull()

    private fun read(dir: String, name: String): ByteArray =
        context.assets.open("$dir/$name").use { it.readBytes() }

    private fun copyTree(assetDir: String, target: File) {
        val children = runCatching { context.assets.list(assetDir)?.toList() }.getOrNull().orEmpty()
        if (children.isEmpty()) return
        for (child in children) {
            val path = "$assetDir/$child"
            val grandChildren = runCatching { context.assets.list(path)?.toList() }
                .getOrNull().orEmpty()
            if (grandChildren.isEmpty()) {
                context.assets.open(path).use { input ->
                    File(target, child).outputStream().use(input::copyTo)
                }
            } else {
                val sub = File(target, child).apply { mkdirs() }
                copyTree(path, sub)
            }
        }
    }

    companion object {
        const val ROOT = "mods"
        private const val SEEDED = "seeded"
        private const val APP_STAMP = "appUpdateTime"

        /**
         * O formato de pacote (.bmod é um zip com isto dentro):
         *
         *     manifest.json    uid, id, nome, autor, categoria, descrição...
         *     icon.png         ícone do mod, quadrado (opcional; sem ele, o da categoria)
         *     banner.png       capa da vitrine, ~3,4:1 (opcional)
         *     thumbnails/      imagens da vitrine (opcional)
         *     content/         o mod em si — main.js e o que mais ele precisar
         *
         * O `uid` é obrigatório e é emitido pelo site do Bunny Loader, não pelo
         * autor: é o que garante que dois mods de pessoas diferentes nunca
         * ocupem a mesma pasta. Pacote sem uid não instala e não carrega.
         *
         * Os nomes antigos continuam aceitos: quem já tem pacote com `mod.json`
         * e `preview/` não precisa reempacotar, e o custo disso é uma lista de
         * dois nomes em cada lugar.
         */
        val MANIFESTS = listOf("manifest.json", "mod.json")
        val THUMBS = listOf("thumbnails", "preview")
        const val ICON = "icon.png"
        const val BANNER = "banner.png"
        const val CONTENT = "content"
    }
}

/** "24 KB", "1.2 MB" — o único número que a ficha do mod mostra. */
fun formatSize(bytes: Long): String = when {
    bytes >= 1024 * 1024 -> String.format("%.1f MB", bytes / 1024.0 / 1024.0)
    bytes >= 1024 -> "${bytes / 1024} KB"
    else -> "$bytes B"
}

/** "2026-09-22" -> "22 de set. de 2026". */
fun formatDate(iso: String): String {
    val parts = iso.split("-")
    if (parts.size != 3) return iso
    val months = listOf(
        "jan.", "fev.", "mar.", "abr.", "mai.", "jun.",
        "jul.", "ago.", "set.", "out.", "nov.", "dez.",
    )
    val m = parts[1].toIntOrNull()?.minus(1)?.coerceIn(0, 11) ?: return iso
    return "${parts[2].trimStart('0')} de ${months[m]} de ${parts[0]}"
}
