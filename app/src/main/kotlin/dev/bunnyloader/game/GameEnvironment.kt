package dev.bunnyloader.game

import android.content.Context
import android.content.pm.ApplicationInfo
import android.content.res.AssetManager
import android.content.res.Resources
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
 *
 * ## Uma versão só, do começo ao fim
 *
 * Quando há APK fixado, TUDO vem dele: libs, assets, resources e os caminhos
 * que a Unity consulta. Misturar não é opção — o `global-metadata.dat` precisa
 * casar exatamente com a `libil2cpp.so`, e servir libs de uma versão com assets
 * de outra é crash na melhor das hipóteses.
 */
class GameEnvironment private constructor(
    val install: GameInstall,
    /** ContextImpl do pacote do jogo — guarda a LoadedApk real (ver AppBoot). */
    val gameContext: Context,
    val classLoader: ClassLoader,
    /** Recursos do APK fixado, ou null quando rodamos o jogo instalado. */
    private val pinnedResources: Resources?,
    /** APK que a Unity deve enxergar como "o app": o fixado, ou o instalado. */
    val codePath: String,
) {
    val resources: Resources get() = pinnedResources ?: gameContext.resources
    val assets: AssetManager get() = resources.assets

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
     *
     * `sourceDir` segue o [codePath]: com APK fixado, a Unity tem de ler os
     * assets DELE, e não os do jogo instalado.
     */
    fun applicationInfo(base: ApplicationInfo, localLibDir: File): ApplicationInfo =
        ApplicationInfo(base).apply {
            val game = gameContext.applicationInfo
            packageName = game.packageName
            nativeLibraryDir = localLibDir.absolutePath
            sourceDir = codePath
            publicSourceDir = codePath
            if (pinnedResources != null) {
                // O fixado é um APK único: anunciar splits do install faria a
                // Unity procurar assets numa versão que não é a que vai rodar.
                splitSourceDirs = null
                splitPublicSourceDirs = null
            } else {
                splitSourceDirs = game.splitSourceDirs
                splitPublicSourceDirs = game.splitPublicSourceDirs
            }
        }

    companion object {
        /**
         * @param base a GameActivity. Nada aqui toca em getResources() dela,
         *   para não recursar antes do ambiente estar pronto.
         * @param pinned APK fixado pelo usuário, se houver.
         */
        fun create(base: Context, install: GameInstall, pinned: File?): GameEnvironment {
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
            val res = pinned?.let { openResources(base, it) }
            return GameEnvironment(
                install, gameContext, base.classLoader, res,
                if (res != null) pinned.absolutePath else install.apkPath,
            )
        }

        /**
         * Resources/AssetManager de um APK avulso, por API pública.
         *
         * `getResourcesForApplication(ApplicationInfo)` monta o AssetManager a
         * partir de `sourceDir`, e o ApplicationInfo pode vir de um arquivo via
         * `getPackageArchiveInfo` — que devolve os caminhos vazios, então
         * preenchemos. Isso evita o `AssetManager.addAssetPath` por reflection,
         * que é API oculta e barrada conforme o targetSdk.
         *
         * Lança com mensagem própria: rodar com os assets da versão errada é
         * pior que não rodar.
         */
        private fun openResources(base: Context, apk: File): Resources {
            val pm = base.packageManager
            val info = pm.getPackageArchiveInfo(apk.absolutePath, 0)
                ?: error("APK fixado ilegível: ${apk.name}")
            val app = info.applicationInfo ?: error("APK fixado sem ApplicationInfo")
            app.sourceDir = apk.absolutePath
            app.publicSourceDir = apk.absolutePath
            app.splitSourceDirs = null
            app.splitPublicSourceDirs = null
            return runCatching { pm.getResourcesForApplication(app) }.getOrElse {
                error("não consegui abrir os assets do APK fixado: ${it.javaClass.simpleName}: ${it.message}")
            }
        }
    }
}
