package dev.bunnyloader.game

import android.content.Context
import android.util.Log
import dev.bunnyloader.TAG
import dev.bunnyloader.describe
import dev.bunnyloader.rootCause

/**
 * O APK do Terraria e protegido pelo PairIP, que criptografa as constantes de
 * string do app inteiro. Elas ficam em classes-deposito injetadas (por exemplo
 * `uk.co.drstudios.lvl.yh.sHQPfQqKk`, que tem 13 campos `static String` e
 * NENHUM metodo) e sao preenchidas em runtime.
 *
 * Sem isso, codigo aparentemente inocente quebra de forma misteriosa:
 *
 *     UnityPlayer.getNaturalOrientation():
 *         sget-object v1, sHQPfQqKk.pPb          // deveria ser "window"
 *         invoke-virtual Context.getSystemService(v1)   -> null
 *         invoke-interface WindowManager.getDefaultDisplay()  -> NPE
 *
 * Quem preenche as strings e `StartupLauncher.launch()`, chamado do `<clinit>`
 * de `com.pairip.application.Application`. O ponto importante e que a
 * verificacao de assinatura (`SignatureCheck`) NAO esta nesse caminho: ela vive
 * em `Application.attachBaseContext`, que nunca executamos. Entao da para ligar
 * a descriptografia sem passar pela checagem de integridade.
 *
 * `VMRunner.<clinit>` faz `System.loadLibrary("pairipcore")`; a lib esta no
 * nativeLibraryDir do jogo, que ja e o caminho de busca do nosso DexClassLoader.
 */
object PairipBootstrap {

    /** Uma string conhecida, usada so para provar que a descriptografia rodou. */
    private const val PROBE_CLASS = "uk.co.drstudios.lvl.yh.sHQPfQqKk"
    private const val PROBE_FIELD = "pPb"
    private const val PROBE_EXPECTED = "window"

    /**
     * @return true se as strings do jogo foram populadas.
     * @throws Throwable ja desembrulhado se o bootstrap falhar.
     */
    fun run(loader: ClassLoader, context: Context): Boolean {
        Log.i(TAG, "PairIP: antes do bootstrap, $PROBE_CLASS.$PROBE_FIELD = ${probe(loader)}")

        try {
            // Carregar a classe ja dispara o <clinit> -> System.loadLibrary("pairipcore").
            val vmRunner = loader.loadClass("com.pairip.VMRunner")
            Log.i(TAG, "PairIP: VMRunner carregada (libpairipcore ok)")

            vmRunner.getMethod("setContext", Context::class.java).invoke(null, context)
            Log.i(TAG, "PairIP: VMRunner.setContext() ok")

            val launcher = loader.loadClass("com.pairip.StartupLauncher")
            launcher.getMethod("launch").invoke(null)
            Log.i(TAG, "PairIP: StartupLauncher.launch() ok")
        } catch (t: Throwable) {
            throw t.rootCause()
        }

        val value = probe(loader)
        val ok = value == PROBE_EXPECTED
        Log.i(TAG, "PairIP: depois do bootstrap, $PROBE_CLASS.$PROBE_FIELD = $value (esperado \"$PROBE_EXPECTED\")")
        if (!ok) {
            Log.e(TAG, "PairIP: strings NAO foram populadas — a Unity vai falhar adiante")
        }
        return ok
    }

    private fun probe(loader: ClassLoader): String? = runCatching {
        loader.loadClass(PROBE_CLASS).getDeclaredField(PROBE_FIELD).get(null) as? String
    }.getOrElse { "<erro: ${it.describe()}>" }
}
