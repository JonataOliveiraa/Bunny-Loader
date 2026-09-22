package dev.bunnyloader

import android.app.Activity
import android.content.ComponentCallbacks2
import android.content.Intent
import android.content.pm.ApplicationInfo
import android.content.res.AssetManager
import android.content.res.Configuration
import android.content.res.Resources
import android.os.Bundle
import android.os.Process
import android.util.Log
import android.view.KeyEvent
import android.view.MotionEvent
import android.view.SurfaceView
import android.view.Window
import android.widget.Toast
import dev.bunnyloader.game.BootLog
import dev.bunnyloader.game.GameEnvironment
import dev.bunnyloader.game.GameFiles
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
 */
class GameActivity : Activity() {

    private companion object {
        /** Núcleo de mods: desligado até o hosting puro estar validado. */
        const val ENABLE_NATIVE_CORE = false
    }

    private var env: GameEnvironment? = null
    private var gameAppInfo: ApplicationInfo? = null
    private var host: UnityHost? = null

    // --- Context do jogo -----------------------------------------------------
    // Enquanto env for null (durante super.onCreate), cai no comportamento
    // normal do launcher. O tema da Activity vem do framework.

    override fun getResources(): Resources = env?.resources ?: super.getResources()

    override fun getAssets(): AssetManager = env?.assets ?: super.getAssets()

    override fun getClassLoader(): ClassLoader = env?.classLoader ?: super.getClassLoader()

    // A Unity resolve recursos por nome de pacote (getIdentifier). Sem isto ela
    // procura recursos do jogo dentro do pacote do launcher, recebe id 0 e
    // estoura em Resources.getString(0) — visto em GetGlViewContentDescription.
    override fun getPackageName(): String = env?.install?.packageName ?: super.getPackageName()

    override fun getApplicationInfo(): ApplicationInfo {
        val environment = env ?: return super.getApplicationInfo()
        val libs = localLibs ?: return super.getApplicationInfo()
        return gameAppInfo ?: environment.applicationInfo(super.getApplicationInfo(), libs)
            .also { gameAppInfo = it }
    }

    /** Nossa cópia das .so do jogo — é daqui que a Unity carrega a libunity.so. */
    private var localLibs: File? = null

    // O VMRunner do PairIP abre o APK por estes caminhos para ler os programas
    // da VM; precisam apontar para o do jogo, nao para o nosso.
    override fun getPackageCodePath(): String =
        env?.install?.apkPath ?: super.getPackageCodePath()

    override fun getPackageResourcePath(): String =
        env?.install?.apkPath ?: super.getPackageResourcePath()

    // --- boot ----------------------------------------------------------------

    override fun onCreate(savedInstanceState: Bundle?) {
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        super.onCreate(savedInstanceState)
        BootLog.reset(this)
        BootLog.installCrashHandler(this)
        BootLog.captureLogcat(this)
        BootLog.add(this, "=== GameActivity.onCreate ===")

        val install = GameInstall.locate(this)
        if (install == null) {
            fail("Terraria nao encontrado", null)
            return
        }
        BootLog.add(this, "jogo ${install.packageName} v${install.versionCode} abi=${install.abi}")
        Log.i(TAG, "  apk=${install.apkPath}")
        Log.i(TAG, "  libs=${install.nativeLibDir}")
        dumpNativeLibs(install)

        // ISOLAMENTO (temporário): o núcleo nativo fica DESLIGADO enquanto
        // validamos o hosting puro. Em ARM real o hook pendente de il2cpp_init
        // realmente instala (no emulador o houdini o rejeita), então ele dispara
        // no meio do boot da Unity e é suspeito nº 1 do "tela preta e volta".
        // Religar depois de resolver os símbolos via /proc/self/maps.
        if (ENABLE_NATIVE_CORE) startNativeCore(install) else
            BootLog.add(this, "nucleo nativo DESLIGADO (isolando o hosting)")

        val environment = try {
            GameEnvironment.create(this, install, codeCacheDir)
        } catch (t: Throwable) {
            fail("Falha ao abrir os recursos do jogo", t)
            return
        }
        env = environment
        BootLog.add(this, "GameEnvironment pronto")

        // Nossa cópia da pilha nativa do jogo (ver GameFiles): é dela que a
        // UnityPlayer vai carregar libmain.so. Copiar é obrigatório — o linker
        // não deixa carregar .so do diretório de outro app — e é o que dá o
        // controle de versão. libpairipcore.so fica de fora: ninguém depende
        // dela, e é o PairIP que derrubou as tentativas anteriores.
        //
        // NÃO bootamos a Application do jogo e NÃO carregamos o dex dele. Sem
        // código Java do Terraria, o PairIP simplesmente não entra em cena.
        try {
            val libs = GameFiles.prepare(this, install) { BootLog.add(this, it) }
            localLibs = libs
            gameAppInfo = null   // recalcula com o nativeLibraryDir certo
            BootLog.add(this, "conteudo: " + GameFiles.describe(libs))
            // A libpairipcore procura as classes Java dela no JNI_OnLoad; sem
            // elas a ART aborta em RegisterNatives(NULL). Só torna resolvível.
            BootLog.add(this, GameFiles.addGameDex(this, classLoader, install.allApks()))
            var ok = GameFiles.addLibraryPath(classLoader, libs)
            BootLog.add(this, "libs copiadas (path estendido=$ok)")

            // Testa o dlopen ANTES da Unity: assim um bloqueio do SELinux vira
            // mensagem em vez de SIGABRT mudo lá dentro do engine.
            var err = GameFiles.preload(libs)
            if (err != null) {
                BootLog.add(this, "dlopen da NOSSA copia falhou -> $err")
                // Plano B: a pasta original do jogo é executável (/data/app),
                // ao contrário do nosso diretório de dados. Perde-se o controle
                // de versão, mas responde se a hipótese do SELinux procede.
                val orig = java.io.File(install.nativeLibDir)
                ok = GameFiles.addLibraryPath(classLoader, orig)
                err = GameFiles.preload(orig)
                BootLog.add(this, "fallback pasta do jogo: " + (err ?: "OK") + " (path=$ok)")
                if (err != null) { fail("Não consegui carregar as libs do jogo", null); return }
            } else {
                BootLog.add(this, "dlopen da nossa copia OK")
            }
        } catch (t: Throwable) {
            fail("Falha ao preparar os binários do jogo", t)
            return
        }

        val unity = UnityHost(this, environment.classLoader)
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

    /** Confirma que as .so que a Unity vai carregar estao onde dizemos que estao. */
    private fun dumpNativeLibs(install: GameInstall) {
        val dir = File(install.nativeLibDir)
        Log.i(TAG, "libs existe=${dir.isDirectory} legivel=${dir.canRead()}")
        val names = dir.list()
        if (names == null) {
            Log.w(TAG, "  nao foi possivel listar ${dir.absolutePath}")
            return
        }
        for (name in names.sorted()) {
            val f = File(dir, name)
            Log.i(TAG, "  $name (${f.length()} bytes, legivel=${f.canRead()})")
        }
    }

    /**
     * Fase 2+. Precisa rodar ANTES da Unity carregar a libil2cpp.so, para o
     * watcher pendente de il2cpp_init ja estar registrado. Falha aqui nao e
     * fatal: o jogo sobe sem mods.
     */
    private fun startNativeCore(install: GameInstall) {
        if (!NativeBridge.ensureLoaded()) {
            Log.i(TAG, "nucleo nativo ausente (bl.nativeBuild=false) - seguindo sem mods")
            return
        }
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
