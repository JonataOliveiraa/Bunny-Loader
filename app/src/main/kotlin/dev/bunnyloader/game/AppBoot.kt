package dev.bunnyloader.game

import android.content.Context
import android.util.Log
import dev.bunnyloader.TAG

/**
 * Faz o Android bootar a Application do JOGO dentro do nosso processo.
 *
 * Esse é o pulo do gato que a Fase 1 não achou. O PairIP cifra as constantes de
 * string do app; quem as preenche é a libpairipcore, e ela só age quando o app
 * boota pelo caminho legítimo. Chamar `StartupLauncher.launch()` na mão NÃO
 * basta (medido: as strings continuam nulas).
 *
 * O que basta: pegar a `LoadedApk` real do jogo — que o `createPackageContext`
 * já criou e guarda no `mPackageInfo` do ContextImpl — e deixar o Android
 * construir a Application por ela. Aí `attachBaseContext` e o `<clinit>` rodam
 * de verdade.
 *
 * Medido no dispositivo (Terraria 1.4.5.6.4): strings 0/47 antes, 47/47 depois.
 *
 * Usa APIs ocultas (`mPackageInfo`, `makeApplication`), confirmadas funcionando
 * de Android 12 a 16. `SignatureCheck` passa porque o APK instalado está intacto.
 */
object AppBoot {

    /** @return a Application do jogo. Lança se o caminho oculto não existir. */
    fun makeApplication(gameCtx: Context): Any {
        val loadedApk = gameCtx.javaClass.getDeclaredField("mPackageInfo")
            .apply { isAccessible = true }.get(gameCtx)
            ?: error("mPackageInfo nulo — o contexto não veio de createPackageContext?")

        val activityThread = Class.forName("android.app.ActivityThread")
            .getMethod("currentActivityThread").invoke(null)
        val instrumentation = activityThread?.javaClass
            ?.getDeclaredField("mInstrumentation")?.apply { isAccessible = true }
            ?.get(activityThread)

        // O nome mudou entre versões do Android.
        val make = loadedApk.javaClass.declaredMethods.firstOrNull {
            (it.name == "makeApplication" || it.name == "makeApplicationInner") &&
                it.parameterTypes.size == 2
        } ?: error("makeApplication não encontrado em ${loadedApk.javaClass.name}")
        make.isAccessible = true

        val app = make.invoke(loadedApk, false, instrumentation)
            ?: error("makeApplication devolveu null")
        Log.i(TAG, "AppBoot: Application do jogo = ${app.javaClass.name}")
        return app
    }
}
