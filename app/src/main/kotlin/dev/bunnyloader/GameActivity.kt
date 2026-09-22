package dev.bunnyloader

import android.app.Activity
import android.content.Intent
import android.content.pm.ApplicationInfo
import android.content.res.AssetManager
import android.content.res.Configuration
import android.content.res.Resources
import android.os.Bundle
import android.os.Process
import android.view.KeyEvent
import android.view.MotionEvent
import android.view.Window
import android.widget.Toast
import dev.bunnyloader.game.GameEnvironment
import dev.bunnyloader.game.GameInstall
import dev.bunnyloader.game.UnityHost
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.nativebridge.NativeBridge
import dev.bunnyloader.nativebridge.NativeConfig
import java.io.File

/**
 * Sobe o Terraria dentro do processo :game.
 *
 * Esta Activity **é** o Context que a Unity recebe: ela devolve os recursos,
 * assets, ApplicationInfo e ClassLoader do jogo nos próprios getters. Isso é
 * obrigatório — a UnityPlayer faz `instanceof Activity` direto no Context, sem
 * desembrulhar ContextWrapper (ver GameEnvironment).
 *
 * O ciclo de vida e o encaminhamento de input reproduzem a
 * com.unity3d.player.UnityPlayerActivity de fábrica, conferida no dex do jogo.
 */
class GameActivity : Activity() {

    private var env: GameEnvironment? = null
    private var gameAppInfo: ApplicationInfo? = null
    private var host: UnityHost? = null

    // --- Context do jogo -----------------------------------------------------
    // Enquanto env for null (durante super.onCreate), cai no comportamento
    // normal do launcher. O tema da Activity é de framework, então não depende
    // dos nossos recursos.

    override fun getResources(): Resources = env?.resources ?: super.getResources()

    override fun getAssets(): AssetManager = env?.assets ?: super.getAssets()

    override fun getClassLoader(): ClassLoader = env?.classLoader ?: super.getClassLoader()

    override fun getApplicationInfo(): ApplicationInfo {
        val environment = env ?: return super.getApplicationInfo()
        return gameAppInfo ?: environment.applicationInfo(super.getApplicationInfo())
            .also { gameAppInfo = it }
    }

    // --- boot ----------------------------------------------------------------

    override fun onCreate(savedInstanceState: Bundle?) {
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        super.onCreate(savedInstanceState)

        val install = GameInstall.locate(this)
            ?: return finishWithError("Terraria não encontrado")

        startNativeCore(install)

        val environment = runCatching {
            GameEnvironment.create(this, install, codeCacheDir)
        }.getOrElse { return finishWithError("Falha ao abrir os recursos do jogo: ${it.message}") }
        env = environment

        val unity = UnityHost(this, environment.classLoader)
        unity.onQuit = { finish() }

        val view = runCatching { unity.create(this) }
            .getOrElse { return finishWithError("Falha ao criar a UnityPlayer: ${it.message}") }

        host = unity
        setContentView(view)
        view.requestFocus()
    }

    /**
     * Fase 2+. Precisa rodar ANTES da Unity carregar a libil2cpp.so, para o
     * watcher pendente de il2cpp_init já estar registrado. Falha aqui não é
     * fatal: o jogo sobe sem mods.
     */
    private fun startNativeCore(install: GameInstall) {
        if (!NativeBridge.ensureLoaded()) return

        val repo = ModRepository(this)
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
            Toast.makeText(this, "Núcleo não iniciado — rodando sem mods", Toast.LENGTH_SHORT).show()
        }
    }

    // --- ciclo de vida (espelha a UnityPlayerActivity) -----------------------

    override fun onStart() { super.onStart(); host?.start() }
    override fun onResume() { super.onResume(); host?.resume() }
    override fun onPause() { super.onPause(); host?.pause() }
    override fun onStop() { super.onStop(); host?.stop() }

    override fun onDestroy() {
        host?.destroy()
        super.onDestroy()
        // Processo :game dedicado: derruba Unity + IL2CPP + VM de uma vez.
        Process.killProcess(Process.myPid())
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        host?.newIntent(intent)
    }

    override fun onConfigurationChanged(newConfig: Configuration) {
        super.onConfigurationChanged(newConfig)
        host?.configurationChanged(newConfig)
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        host?.windowFocusChanged(hasFocus)
    }

    override fun onLowMemory() { super.onLowMemory(); host?.lowMemory() }

    override fun onTrimMemory(level: Int) {
        super.onTrimMemory(level)
        if (level >= android.content.ComponentCallbacks2.TRIM_MEMORY_RUNNING_CRITICAL) host?.lowMemory()
    }

    // --- input ---------------------------------------------------------------
    // A UnityPlayerActivity encaminha tudo via injectEvent.

    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (event.action == KeyEvent.ACTION_MULTIPLE && host?.injectEvent(event) == true) return true
        return super.dispatchKeyEvent(event)
    }

    override fun onKeyDown(keyCode: Int, event: KeyEvent): Boolean =
        host?.injectEvent(event) ?: super.onKeyDown(keyCode, event)

    override fun onKeyUp(keyCode: Int, event: KeyEvent): Boolean =
        host?.injectEvent(event) ?: super.onKeyUp(keyCode, event)

    override fun onTouchEvent(event: MotionEvent): Boolean =
        host?.injectEvent(event) ?: super.onTouchEvent(event)

    override fun onGenericMotionEvent(event: MotionEvent): Boolean =
        host?.injectEvent(event) ?: super.onGenericMotionEvent(event)

    // -------------------------------------------------------------------------

    private fun finishWithError(message: String) {
        Toast.makeText(this, message, Toast.LENGTH_LONG).show()
        finish()
    }
}
