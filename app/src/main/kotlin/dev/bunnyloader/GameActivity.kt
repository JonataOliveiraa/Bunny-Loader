package dev.bunnyloader

import android.app.Activity
import android.content.ComponentCallbacks2
import android.content.Intent
import android.content.res.Configuration
import android.os.Bundle
import android.os.Process
import android.util.Log
import android.view.KeyEvent
import android.view.MotionEvent
import android.view.SurfaceView
import android.view.Window
import android.widget.Toast
import dev.bunnyloader.game.BootLog
import dev.bunnyloader.game.BundledRuntime
import dev.bunnyloader.game.Eligibility
import dev.bunnyloader.game.UnityHost
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.nativebridge.NativeBridge
import dev.bunnyloader.nativebridge.NativeConfig
import java.io.File

/**
 * Sobe o jogo dentro do processo :game.
 *
 * Com o runtime INTEGRADO não há Context alheio para forjar: as .so vêm do
 * nosso próprio `nativeLibraryDir` e os assets do nosso próprio AssetManager.
 * Sumiram daqui os overrides de getResources/getAssets/getApplicationInfo/
 * getPackageCodePath — eles existiam apenas para apontar a Unity aos arquivos
 * de outro app.
 *
 * O que continua valendo: esta Activity precisa SER o Context passado à
 * UnityPlayer, porque o construtor dela faz `instanceof Activity` direto, sem
 * desembrulhar ContextWrapper.
 */
class GameActivity : Activity() {

    private companion object {
        /**
         * Núcleo de mods. Religado agora que o hosting está provado (o jogo
         * renderiza dentro do nosso processo). Detalhe importante: a libil2cpp
         * do jogo passa a viver no NOSSO namespace de linker, então o
         * dlopen(RTLD_NOLOAD) da sonda deve enxergá-la — era justamente isso que
         * faltava quando o jogo rodava no processo dele.
         */
        const val ENABLE_NATIVE_CORE = true
    }

    private var host: UnityHost? = null

    // --- boot ----------------------------------------------------------------

    override fun onCreate(savedInstanceState: Bundle?) {
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        super.onCreate(savedInstanceState)
        BootLog.reset(this)
        BootLog.installCrashHandler(this)
        BootLog.captureLogcat(this)
        BootLog.add(this, "=== GameActivity.onCreate ===")

        // Elegibilidade: os binários são nossos, mas o jogo só abre para quem
        // tem o Terraria oficial da Play no aparelho. Ver Eligibility para o
        // que isso prova de fato — e o que não prova.
        val gate = Eligibility.check(this)
        BootLog.add(this, "elegibilidade: ${gate.detail}")
        if (!gate.ok) { fail(gate.detail, null); return }

        // O runtime integrado está nesta build? Um APK sem os arquivos compila
        // e instala igual; sem esta checagem o sintoma seria um SIGSEGV dentro
        // da Unity.
        if (!BundledRuntime.isPresent(this)) {
            fail(
                "Esta build não contém o runtime do jogo (libil2cpp.so ausente " +
                    "em nativeLibraryDir).",
                null,
            )
            return
        }
        BootLog.add(this, "runtime integrado: " + BundledRuntime.describe(this))
        BundledRuntime.pairipCheck(this)?.let { fail(it, null); return }

        // Carrega a pilha nativa POR NOME: as .so estão no nosso próprio
        // nativeLibraryDir, já no caminho de busca do ClassLoader. Toda a
        // ginástica de cópia e de caminho absoluto existia só porque os
        // binários viviam em outro app.
        BundledRuntime.load()?.let {
            fail("Não consegui carregar o runtime do jogo: $it", null)
            return
        }
        BootLog.add(this, "runtime carregado")

        // Depois das libs do jogo carregadas: a sonda precisa achar a libil2cpp.
        if (ENABLE_NATIVE_CORE) startNativeCore() else
            BootLog.add(this, "nucleo nativo DESLIGADO")

        val unity = UnityHost(this, classLoader)
        unity.onQuit = { finish() }

        val view = try {
            unity.create(this)
        } catch (t: Throwable) {
            fail("Falha ao criar a UnityPlayer", t)
            return
        }

        mUnityPlayer = unity.playerInstance()
        BootLog.add(this, "UnityPlayer criada")
        host = unity
        setContentView(view)
        view.requestFocus()
        BootLog.add(this, "=== UnityPlayer no ar ===")
    }

    /**
     * Fase 2+. Precisa rodar ANTES da Unity carregar a libil2cpp.so, para o
     * watcher pendente de il2cpp_init ja estar registrado. Falha aqui nao e
     * fatal: o jogo sobe sem mods.
     */
    private fun startNativeCore() {
        if (!NativeBridge.ensureLoaded()) {
            Log.i(TAG, "nucleo nativo ausente (bl.nativeBuild=false) - seguindo sem mods")
            return
        }
        val repo = ModRepository(this)
        val prefs = dev.bunnyloader.ui.Prefs(this)
        val logDir = File(filesDir, "logs").apply { mkdirs() }
        val ok = runCatching {
            NativeBridge.init(
                NativeConfig(
                    gameLibDir = BundledRuntime.libDir(this).absolutePath,
                    modsDir = repo.modsDir.absolutePath,
                    enabledMods = repo.enabledIds().toTypedArray(),
                    logPath = File(logDir, "bunny.log").absolutePath,
                    // Canal de dev: adb push <arquivo> para a NOSSA pasta
                    // externa. A do Terraria nao serve — desde o Android 11
                    // um app nao escreve em Android/data de outro. Desligado
                    // nas Configuracoes, vai vazio e o nucleo nem tenta abrir
                    // o arquivo a cada quadro.
                    cmdPath = if (prefs.devChannel) {
                        File(getExternalFilesDir(null), "bunny/cmd")
                            .also { it.parentFile?.mkdirs() }.absolutePath
                    } else "",
                    gameVersion = BundledRuntime.VERSION_CODE,
                ),
            )
        }.onFailure { logFailure("NativeBridge.init", it) }.getOrDefault(false)

        if (!ok) {
            Toast.makeText(this, "Nucleo nao iniciado - rodando sem mods", Toast.LENGTH_SHORT).show()
        }
    }


    // --- glue que o C# do Terraria acessa por JNI NA ACTIVITY --------------
    //
    // O Terraria customizou a com.unity3d.player.UnityPlayerActivity dele com
    // campos e métodos próprios, e o código IL2CPP os procura por nome na
    // Activity corrente. Sem eles o boot morre em:
    //   NoSuchFieldError: no "Ljava/lang/Object;" field "PressedStates"
    // Conferido no dex de 1.4.5.6.4 (dexdump da UnityPlayerActivity).
    // @JvmField é obrigatório: precisam ser CAMPOS Java com estes nomes exatos.

    @JvmField var MouseInside: Boolean = false
    @JvmField var MouseMode: Int = 0
    @JvmField val PressedStates = BooleanArray(330)   // tamanho conferido no dex
    @JvmField var mUnityPlayer: Any? = null

    fun IsKeyPressed(code: Int): Boolean =
        code >= 0 && code < PressedStates.size && PressedStates[code]

    /**
     * Mapeia keycode do Android -> keycode da Unity.
     * TODO: a tabela real está no dex do jogo; por ora identidade, o suficiente
     * para bootar. Teclado/gamepad ficam imprecisos até portarmos a tabela.
     */
    fun GetUnityKeyCode(androidKeyCode: Int): Int = androidKeyCode

    fun SetMouseCursorMode(mode: Int) { MouseMode = mode }

    fun SetSurfaceFrameRate(fps: Float) {
        if (android.os.Build.VERSION.SDK_INT >= 30) {
            runCatching { getSurfaceView()?.holder?.surface?.setFrameRate(fps, 0) }
        }
    }

    fun getSurfaceView(): SurfaceView? = host?.surfaceView()

    private fun setPressed(code: Int, down: Boolean) {
        val u = GetUnityKeyCode(code)
        if (u >= 0 && u < PressedStates.size) PressedStates[u] = down
    }

    // --- ciclo de vida (espelha a UnityPlayerActivity) -----------------------

    override fun onStart() { super.onStart(); host?.start() }
    override fun onResume() { super.onResume(); host?.resume() }
    override fun onPause() { super.onPause(); host?.pause() }
    override fun onStop() { super.onStop(); host?.stop() }

    override fun onDestroy() {
        val hadHost = host != null
        host?.destroy()
        super.onDestroy()
        // So derruba o processo se a Unity chegou a subir; num erro de boot,
        // matar na hora atrapalha a leitura do log.
        if (hadHost) Process.killProcess(Process.myPid())
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
        if (level >= ComponentCallbacks2.TRIM_MEMORY_RUNNING_CRITICAL) host?.lowMemory()
    }

    // --- input ---------------------------------------------------------------

    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (event.action == KeyEvent.ACTION_MULTIPLE && host?.injectEvent(event) == true) return true
        return super.dispatchKeyEvent(event)
    }

    override fun onKeyDown(keyCode: Int, event: KeyEvent): Boolean {
        setPressed(keyCode, true)
        return host?.injectEvent(event) ?: super.onKeyDown(keyCode, event)
    }

    override fun onKeyUp(keyCode: Int, event: KeyEvent): Boolean {
        setPressed(keyCode, false)
        return host?.injectEvent(event) ?: super.onKeyUp(keyCode, event)
    }

    override fun onTouchEvent(event: MotionEvent): Boolean =
        host?.injectEvent(event) ?: super.onTouchEvent(event)

    override fun onGenericMotionEvent(event: MotionEvent): Boolean =
        host?.injectEvent(event) ?: super.onGenericMotionEvent(event)

    // -------------------------------------------------------------------------

    private fun fail(what: String, t: Throwable?) {
        BootLog.fail(this, what, t)
        if (t != null) logFailure(what, t) else Log.e(TAG, "FALHA em $what")
        val detail = t?.describe() ?: "sem detalhes"
        Toast.makeText(this, "$what: $detail", Toast.LENGTH_LONG).show()
        finish()
    }
}
