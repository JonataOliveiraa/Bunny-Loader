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
    /** Recursos da cópia congelada, ou null quando rodamos o jogo instalado. */
    private val pinnedResources: Resources?,
    /** APK que a Unity deve enxergar como "o app": o congelado, ou o instalado. */
    val codePath: String,
    /** Splits da cópia congelada, ou null. */
    private val pinnedSplits: Array<String>?,
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
                // Os splits da cópia congelada, não os do install: apontar para
                // o instalado faria a Unity ler assets de outra versão.
                splitSourceDirs = pinnedSplits
                splitPublicSourceDirs = pinnedSplits
            } else {
                splitSourceDirs = game.splitSourceDirs
                splitPublicSourceDirs = game.splitPublicSourceDirs
            }
        }

    companion object {
        /**
         * @param base a GameActivity. Nada aqui toca em getResources() dela,
         *   para não recursar antes do ambiente estar pronto.
         * @param pinned APKs congelados (base primeiro), ou vazio.
         */
        fun create(base: Context, install: GameInstall, pinned: List<File>): GameEnvironment {
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
            val res = if (pinned.isEmpty()) null else openResources(base, pinned)
            return GameEnvironment(
                install, gameContext, base.classLoader, res,
                if (res != null) pinned.first().absolutePath else install.apkPath,
                if (res != null) pinned.drop(1).map { it.absolutePath }.toTypedArray()
                    .takeIf { it.isNotEmpty() } else null,
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
        private fun openResources(base: Context, apks: List<File>): Resources {
            val pm = base.packageManager
            val baseApk = apks.first()
            val info = pm.getPackageArchiveInfo(baseApk.absolutePath, 0)
                ?: error("APK congelado ilegível: ${baseApk.name}")
            val app = info.applicationInfo ?: error("APK congelado sem ApplicationInfo")
            app.sourceDir = baseApk.absolutePath
            app.publicSourceDir = baseApk.absolutePath
            // Os splits entram junto: num install da Play os assets estão no
            // base, mas recursos do jogo podem estar em qualquer um deles.
            val splits = apks.drop(1).map { it.absolutePath }.toTypedArray()
            app.splitSourceDirs = splits.takeIf { it.isNotEmpty() }
            app.splitPublicSourceDirs = splits.takeIf { it.isNotEmpty() }
            return runCatching { pm.getResourcesForApplication(app) }.getOrElse {
                error("não consegui abrir os assets da cópia congelada: ${it.javaClass.simpleName}: ${it.message}")
            }
        }
    }
}
