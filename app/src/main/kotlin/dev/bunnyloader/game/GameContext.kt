package dev.bunnyloader.game

import android.content.Context
import android.content.ContextWrapper
import android.content.pm.ApplicationInfo
import android.content.res.AssetManager
import android.content.res.Resources

/**
 * Entrega os recursos do jogo (assets, global-metadata.dat) para a UnityPlayer,
 * mas mantém a identidade do Bunny Loader (package, filesDir), então os saves
 * ficam separados dos do jogo original.
 */
class GameContext(
    base: Context,
    private val game: Context,
) : ContextWrapper(base) {

    override fun getResources(): Resources = game.resources
    override fun getAssets(): AssetManager = game.assets

    override fun getApplicationInfo(): ApplicationInfo {
        val info = ApplicationInfo(super.getApplicationInfo())
        info.nativeLibraryDir = game.applicationInfo.nativeLibraryDir
        info.sourceDir = game.applicationInfo.sourceDir
        return info
    }

    companion object {
        fun create(base: Context, install: GameInstall): GameContext {
            val game = base.createPackageContext(
                install.packageName,
                Context.CONTEXT_INCLUDE_CODE or Context.CONTEXT_IGNORE_SECURITY,
            )
            return GameContext(base, game)
        }
    }
}
