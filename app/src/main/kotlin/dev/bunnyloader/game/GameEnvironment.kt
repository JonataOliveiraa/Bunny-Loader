package dev.bunnyloader.game

import android.content.Context
import android.content.pm.ApplicationInfo
import android.content.res.AssetManager
import android.content.res.Resources
import dalvik.system.DexClassLoader
import java.io.File

/**
 * Recursos do jogo, prontos para a [dev.bunnyloader.GameActivity] devolver nos
 * seus próprios getters.
 *
 * Por que não um ContextWrapper: o construtor da UnityPlayer faz
 * `instanceof Activity` direto no Context recebido, SEM desembrulhar
 * getBaseContext(). Um ContextWrapper reprova no teste, `mActivity` fica nulo e
 * a Unity estoura em getRequestedOrientation(). Conferido no bytecode de
 * UnityPlayer.<init>(Context, IUnityPlayerLifecycleEvents), Unity 2021.3.56f2.
 *
 * Então a própria Activity precisa ser o Context, e é ela que sobrescreve
 * getResources()/getAssets()/getApplicationInfo()/getClassLoader().
 */
class GameEnvironment private constructor(
    val install: GameInstall,
    private val gameContext: Context,
    val classLoader: ClassLoader,
) {
    val resources: Resources get() = gameContext.resources
    val assets: AssetManager get() = gameContext.assets

    /** Copia do ApplicationInfo do launcher, apontando para os binários do jogo. */
    fun applicationInfo(base: ApplicationInfo): ApplicationInfo =
        ApplicationInfo(base).apply {
            nativeLibraryDir = gameContext.applicationInfo.nativeLibraryDir
            sourceDir = gameContext.applicationInfo.sourceDir
            publicSourceDir = gameContext.applicationInfo.publicSourceDir
        }

    companion object {
        /**
         * @param base a GameActivity. Nada aqui toca em getResources() dela,
         *   para não recursar antes do ambiente estar pronto.
         */
        fun create(base: Context, install: GameInstall, codeCacheDir: File): GameEnvironment {
            val gameContext = base.createPackageContext(
                install.packageName,
                Context.CONTEXT_INCLUDE_CODE or Context.CONTEXT_IGNORE_SECURITY,
            )
            // ClassLoader próprio: precisa enxergar as classes E as .so do jogo.
            val loader = DexClassLoader(
                install.apkPath,
                codeCacheDir.absolutePath,
                install.nativeLibDir,
                base.classLoader,
            )
            return GameEnvironment(install, gameContext, loader)
        }
    }
}
