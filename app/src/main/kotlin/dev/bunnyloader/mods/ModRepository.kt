package dev.bunnyloader.mods

import android.content.Context
import android.net.Uri
import kotlinx.serialization.json.Json
import java.io.File
import java.io.InputStream
import java.util.zip.ZipInputStream

/**
 * Gerencia os mods instalados. Um mod é um .bmod (zip) com mod.json + main.js.
 * TODO(Fase 5): importação via ActivityResultContracts.OpenDocument na UI.
 */
class ModRepository(private val context: Context) {
    val modsDir: File = File(context.filesDir, "mods").apply { mkdirs() }
    private val prefs = context.getSharedPreferences("mods", Context.MODE_PRIVATE)
    private val json = Json { ignoreUnknownKeys = true }

    fun list(): List<ModManifest> = modsDir.listFiles().orEmpty()
        .filter { it.isDirectory }
        .mapNotNull { dir -> readManifest(File(dir, "mod.json")) }

    fun import(uri: Uri): Result<ModManifest> = runCatching {
        val temp = File(context.cacheDir, "import").apply { deleteRecursively(); mkdirs() }
        context.contentResolver.openInputStream(uri)!!.use { unzip(it, temp) }
        val manifest = readManifest(File(temp, "mod.json")) ?: error("mod.json inválido")
        require(manifest.blVersion <= BL_VERSION) { "Mod requer Bunny Loader mais novo" }
        val target = File(modsDir, manifest.id)
        target.deleteRecursively()
        temp.renameTo(target)
        manifest
    }

    fun setEnabled(id: String, enabled: Boolean) = prefs.edit().putBoolean(id, enabled).apply()
    fun isEnabled(id: String): Boolean = prefs.getBoolean(id, true)
    fun enabledIds(): List<String> = list().filter { isEnabled(it.id) }.map { it.id }

    private fun readManifest(file: File): ModManifest? =
        runCatching { json.decodeFromString<ModManifest>(file.readText()) }.getOrNull()

    /** Protege contra zip slip: nenhuma entrada pode escrever fora do alvo. */
    private fun unzip(input: InputStream, target: File) {
        ZipInputStream(input).use { zip ->
            generateSequence { zip.nextEntry }.forEach { entry ->
                val out = File(target, entry.name).canonicalFile
                require(out.path.startsWith(target.canonicalPath)) { "Caminho inválido no zip" }
                if (entry.isDirectory) out.mkdirs()
                else { out.parentFile?.mkdirs(); out.outputStream().use { zip.copyTo(it) } }
            }
        }
    }

    companion object {
        const val BL_VERSION = 1
    }
}
