package dev.bunnyloader

import android.app.Activity
import android.content.res.Configuration
import android.os.Bundle
import android.os.Process
import android.view.Window
import android.widget.Toast
import dalvik.system.DexClassLoader
import dev.bunnyloader.game.GameContext
import dev.bunnyloader.game.GameInstall
import dev.bunnyloader.game.UnityHost
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.nativebridge.NativeBridge
import dev.bunnyloader.nativebridge.NativeConfig
import java.io.File

/**
 * Sobe o Terraria dentro do processo :game. Sequência (ver docs/ARCHITECTURE §1):
 * localizar jogo → iniciar núcleo nativo → contexto do jogo → DexClassLoader →
 * criar UnityPlayer.
 */
class GameActivity : Activity() {
    private lateinit var host: UnityHost

    override fun onCreate(savedInstanceState: Bundle?) {
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        super.onCreate(savedInstanceState)

        val install = GameInstall.locate(this)
            ?: return finishWithError("Terraria não encontrado")

        val repo = ModRepository(this)

        // Fase 2+: só instala o núcleo se a lib nativa carregar.
        if (NativeBridge.ensureLoaded()) {
            val logDir = File(filesDir, "logs").apply { mkdirs() }
            val ok = runCatching {
                NativeBridge.init(
                    NativeConfig(
                        gameLibDir = install.nativeLibDir,
                        modsDir = repo.modsDir.absolutePath,
                        enabledMods = repo.enabledIds().toTypedArray(),
                        logPath = File(logDir, "bunny.log").absolutePath,
                        gameVersion = install.versionCode,
                    ),
                )
            }.getOrDefault(false)
            if (!ok) {
                // Não aborta na Fase 1: o jogo ainda deve subir sem mods.
                Toast.makeText(this, "Núcleo não iniciado (rodando sem mods)", Toast.LENGTH_SHORT).show()
            }
        }

        val gameContext = GameContext.create(this, install)
        val loader = DexClassLoader(
            install.apkPath,
            codeCacheDir.absolutePath,
            install.nativeLibDir,
            classLoader,
        )

        host = UnityHost(this, gameContext, loader)
        val view = host.create()
        setContentView(view)
        view.requestFocus()
    }

    override fun onResume() { super.onResume(); if (::host.isInitialized) host.resume() }
    override fun onPause() { super.onPause(); if (::host.isInitialized) host.pause() }
    override fun onDestroy() {
        if (::host.isInitialized) host.destroy()
        super.onDestroy()
        Process.killProcess(Process.myPid())
    }
    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (::host.isInitialized) host.windowFocusChanged(hasFocus)
    }
    override fun onConfigurationChanged(newConfig: Configuration) {
        super.onConfigurationChanged(newConfig)
        if (::host.isInitialized) host.configurationChanged(newConfig)
    }
    override fun onLowMemory() { super.onLowMemory(); if (::host.isInitialized) host.lowMemory() }

    private fun finishWithError(message: String) {
        Toast.makeText(this, message, Toast.LENGTH_LONG).show()
        finish()
    }
}
