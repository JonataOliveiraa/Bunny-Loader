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
 * Instalar é copiar para `filesDir/mods/<id>/`, que já é de onde o núcleo
 * nativo carrega (`ModLoader::loadAll`). Nenhum caminho novo: a vitrine só põe
 * arquivo onde o motor já procurava.
 */
class Catalog(private val context: Context) {

    data class Entry(
        val manifest: ModManifest,
        val assetDir: String,
        val sizeBytes: Long,
        val previews: List<String>,
        val iconAsset: String?,
    ) {
        val id get() = manifest.id
    }

    private val json = Json { ignoreUnknownKeys = true }
    private val repo = ModRepository(context)

    val entries: List<Entry> by lazy { scan() }

    private fun scan(): List<Entry> {
        val dirs = runCatching { context.assets.list(ROOT)?.toList() }.getOrNull().orEmpty()
        return dirs.mapNotNull { dir ->
            val base = "$ROOT/$dir"
            val files = runCatching { context.assets.list(base)?.toList() }.getOrNull().orEmpty()
            if ("mod.json" !in files) return@mapNotNull null
            val manifest = runCatching {
                json.decodeFromString<ModManifest>(read(base, "mod.json").decodeToString())
            }.getOrNull() ?: return@mapNotNull null

            val previews = runCatching { context.assets.list("$base/preview")?.toList() }
                .getOrNull().orEmpty()
                .filter { it.endsWith(".png") || it.endsWith(".jpg") }
                .sorted()
                .map { "$base/preview/$it" }

            Entry(
                manifest = manifest,
                assetDir = base,
                sizeBytes = files.sumOf { read(base, it).size.toLong() },
                previews = previews,
                iconAsset = if ("icon.png" in files) "$base/icon.png" else null,
            )
        }.sortedBy { it.manifest.name }
    }

    fun isInstalled(id: String): Boolean = File(repo.modsDir, "$id/main.js").isFile

    /** Copia o pacote para onde o motor procura. Substitui se já existir. */
    fun install(entry: Entry) {
        val target = File(repo.modsDir, entry.id)
        target.deleteRecursively()
        target.mkdirs()
        copyTree(entry.assetDir, target)
    }

    fun uninstall(id: String) {
        File(repo.modsDir, id).deleteRecursively()
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

    fun loadBitmap(assetPath: String): ImageBitmap? = runCatching {
        context.assets.open(assetPath).use { BitmapFactory.decodeStream(it) }?.asImageBitmap()
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

    private companion object {
        const val ROOT = "mods"
        const val SEEDED = "seeded"
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
