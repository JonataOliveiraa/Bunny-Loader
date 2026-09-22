package dev.bunnyloader.game

import android.content.Context
import android.util.Log

/**
 * ESTÁGIO 0 — descobrir o que faz a libpairipcore popular as strings do app.
 *
 * O PairIP criptografa as constantes de string; elas vivem em classes-depósito
 * sem métodos e são preenchidas em runtime. O `VMRunner.invoke` gravado no dex é
 * um stub (`return null` na 1ª instrução); a libpairipcore reescreve o método em
 * memória quando o app boota pelo caminho legítimo. Sem isso a Unity estoura.
 *
 * A Fase 1 falhou usando um DexClassLoader CASEIRO. Aqui testamos, em degraus,
 * usando o ClassLoader do SISTEMA (o da LoadedApk real do jogo, via
 * createPackageContext), e medimos a mesma sonda a cada passo:
 *
 *   uk.co.drstudios.lvl.yh.sHQPfQqKk.pPb  deve virar "window"
 */
object PairipSpike {
    private const val TAG = "BunnyLoader"
    private const val TERRARIA = "com.and.games505.TerrariaPaid"
    private const val PROBE_CLASS = "uk.co.drstudios.lvl.yh.sHQPfQqKk"
    private const val PROBE_FIELD = "pPb"
    private const val EXPECTED = "window"

    /** Roda os degraus e devolve um resumo curto (também vai pro logcat). */
    fun run(host: Context): String {
        val out = StringBuilder()
        fun log(s: String) { Log.i(TAG, "spike: $s"); out.appendLine(s) }
        fun step(n: String, body: () -> Unit) {
            runCatching(body)
                .onSuccess { log("$n OK  -> probe=${probe()}") }
                .onFailure { log("$n FALHOU (${it.javaClass.simpleName}: ${it.message})") }
        }

        var cl: ClassLoader? = null
        var gameCtx: Context? = null

        step("1) createPackageContext(INCLUDE_CODE|IGNORE_SECURITY)") {
            val c = host.createPackageContext(
                TERRARIA, Context.CONTEXT_INCLUDE_CODE or Context.CONTEXT_IGNORE_SECURITY)
            gameCtx = c
            cl = c.classLoader
            loader = c.classLoader
            Log.i(TAG, "spike:    classLoader=${c.classLoader}")
            Log.i(TAG, "spike:    nativeLibraryDir=${c.applicationInfo.nativeLibraryDir}")
        }
        val loaderRef = cl ?: run { log("abortado: sem ClassLoader"); return out.toString() }
        val ctxRef = gameCtx!!

        // 2) força o <clinit> do VMRunner -> System.loadLibrary("pairipcore")
        step("2) loadClass(com.pairip.VMRunner)") { loaderRef.loadClass("com.pairip.VMRunner") }

        // 3) VMRunner.setContext(contexto do JOGO)
        step("3) VMRunner.setContext(gameCtx)") {
            loaderRef.loadClass("com.pairip.VMRunner")
                .getMethod("setContext", Context::class.java).invoke(null, ctxRef)
        }

        // 4) StartupLauncher.launch() — o que a Fase 1 tentou e não bastou
        step("4) StartupLauncher.launch()") {
            loaderRef.loadClass("com.pairip.StartupLauncher").getMethod("launch").invoke(null)
        }

        // 5) O PULO DO GATO: criar a Application do jogo pela máquina REAL do
        //    Android (LoadedApk.makeApplication), em vez de chamar o PairIP na
        //    mão. É isso que dispara attachBaseContext + <clinit> de verdade.
        step("5) LoadedApk.makeApplication()") { makeApplication(ctxRef) }

        val v = probe()
        log(if (v == EXPECTED) "RESULTADO: SUCESSO — strings populadas ($v)"
            else "RESULTADO: falhou — probe=$v (esperado \"$EXPECTED\")")
        return out.toString()
    }

    private var loader: ClassLoader? = null

    private fun probe(): String? = runCatching {
        loader?.loadClass(PROBE_CLASS)?.getDeclaredField(PROBE_FIELD)?.apply { isAccessible = true }
            ?.get(null) as? String
    }.getOrElse { "<erro ${it.javaClass.simpleName}>" }

    /**
     * Cria a Application do jogo pelo caminho do sistema. O ContextImpl devolvido
     * pelo createPackageContext guarda a LoadedApk real do jogo em `mPackageInfo`;
     * `makeApplication` nela roda attachBaseContext + onCreate como o Android faz.
     * Usa APIs ocultas — pode ser bloqueado (o próprio bloqueio é informação).
     */
    private fun makeApplication(gameCtx: Context) {
        val fPackageInfo = gameCtx.javaClass.getDeclaredField("mPackageInfo").apply { isAccessible = true }
        val loadedApk = fPackageInfo.get(gameCtx) ?: error("mPackageInfo nulo")
        Log.i(TAG, "spike:    LoadedApk=${loadedApk.javaClass.name}")

        val at = Class.forName("android.app.ActivityThread")
            .getMethod("currentActivityThread").invoke(null)
        val instrumentation = at?.javaClass?.getDeclaredField("mInstrumentation")
            ?.apply { isAccessible = true }?.get(at)

        // Android mudou o nome/assinatura ao longo das versões.
        val m = loadedApk.javaClass.declaredMethods.firstOrNull {
            (it.name == "makeApplication" || it.name == "makeApplicationInner") &&
                it.parameterTypes.size == 2
        } ?: error("makeApplication não encontrado")
        m.isAccessible = true
        val app = m.invoke(loadedApk, false, instrumentation)
        Log.i(TAG, "spike:    Application criada = ${app?.javaClass?.name}")
    }
}
