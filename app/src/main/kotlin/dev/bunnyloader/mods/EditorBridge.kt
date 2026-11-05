package dev.bunnyloader.mods

import android.content.Context
import android.util.Log
import java.io.File
import java.util.UUID

/**
 * O "empacotar e instalar" que o BL Editor pede: o mesmo da tela Mod Sources,
 * sempre ligando o mod. Uma instalação por vez — o editor salva seguido, e
 * duas trocando a mesma pasta de pacote ao mesmo tempo se atropelariam.
 */
object EditorBridge {
    private const val TAG = "BunnyLoader"
    private val lock = Any()

    fun install(context: Context, project: String): ModManifest = synchronized(lock) {
        val sources = ModSources(File(context.getExternalFilesDir(null) ?: context.filesDir, "ModSources"))
        val builds = File(context.cacheDir, "ModBuilds").apply { mkdirs() }
        val output = File(builds, "editor-${UUID.randomUUID()}.bl")
        try {
            val manifest = sources.build(project, output)
            val repo = ModRepository(context)
            repo.import(output).getOrThrow()
            repo.setEnabled(manifest.uid, true)
            Log.i(TAG, "Editor: installed ${manifest.id} ${manifest.version}")
            manifest
        } finally {
            output.delete()
        }
    }
}
