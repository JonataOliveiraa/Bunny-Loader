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
    /** ContextImpl do pacote do jogo — guarda a LoadedApk real (ver AppBoot). */
    val gameContext: Context,
    val classLoader: ClassLoader,
) {
    val resources: Resources get() = gameContext.resources
    val assets: AssetManager get() = gameContext.assets

    /**
     * Híbrido: identidade e binários do JOGO (a Unity consulta packageName e os
     * caminhos do APK), mas os diretórios de dados continuam sendo os NOSSOS —
     * não temos permissão de escrever no dataDir do jogo (uid diferente).
     */
    fun applicationInfo(base: ApplicationInfo): ApplicationInfo =
        ApplicationInfo(base).apply {
            val game = gameContext.applicationInfo
            packageName = game.packageName
            nativeLibraryDir = game.nativeLibraryDir
            sourceDir = game.sourceDir
            publicSourceDir = game.publicSourceDir
            splitSourceDirs = game.splitSourceDirs
            splitPublicSourceDirs = game.splitPublicSourceDirs
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
            // Usar o ClassLoader DO SISTEMA (o da LoadedApk real do jogo), NÃO um
            // DexClassLoader caseiro. Esse foi o erro central da Fase 1: com o
            // loader caseiro o PairIP nunca inicializa e as strings do app ficam
            // nulas. O do sistema também já traz o caminho de busca das .so —
            // inclusive dos splits (base.apk + split_config.arm64_v8a.apk).
            return GameEnvironment(install, gameContext, gameContext.classLoader)
        }
    }
}
