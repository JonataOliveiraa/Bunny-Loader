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
     * Híbrido: identidade e APK do JOGO (a Unity consulta packageName e os
     * caminhos do APK), dados NOSSOS (não podemos escrever no dataDir do jogo).
     *
     * `nativeLibraryDir` aponta para a NOSSA cópia das .so, não para a pasta do
     * jogo. A Unity monta o caminho da libunity.so a partir deste campo, e do
     * diretório do outro app o dlopen falha por namespace de linker:
     *
     *   JNI FatalError: Unable to load library: /data/app/...TerrariaPaid.../
     *     lib/arm64/libunity.so [dlopen failed: library "libunity.so" not found]
     */
    fun applicationInfo(base: ApplicationInfo, localLibDir: File): ApplicationInfo =
        ApplicationInfo(base).apply {
            val game = gameContext.applicationInfo
            packageName = game.packageName
            nativeLibraryDir = localLibDir.absolutePath
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
            // SEM CONTEXT_INCLUDE_CODE — de propósito.
            //
            // Queremos os recursos/assets do jogo, NUNCA o dex dele. O PairIP
            // protege a camada Java: carregar as classes do jogo traz junto as
            // strings cifradas (que derrubaram a Fase 1) e o license check (que
            // derrubou o Estágio 2). A pilha nativa não tem nada disso.
            //
            // O ClassLoader é o NOSSO: as classes com.unity3d.player.* limpas
            // vêm no nosso APK, e as .so saem da nossa cópia (ver GameFiles).
            // É o mesmo desenho do TL Pro.
            val gameContext = base.createPackageContext(
                install.packageName,
                Context.CONTEXT_IGNORE_SECURITY,
            )
            return GameEnvironment(install, gameContext, base.classLoader)
        }
    }
}
