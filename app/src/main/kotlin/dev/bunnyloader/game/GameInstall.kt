package dev.bunnyloader.game

import android.content.Context
import android.os.Process
import java.io.File

/** Localiza e valida o Terraria instalado no aparelho. */
data class GameInstall(
    val packageName: String,
    val apkPath: String,
    val nativeLibDir: String,
    val versionCode: Long,
    val abi: String,
) {
    /** Caminho da .so; cobre APKs com extractNativeLibs=false. */
    fun libPath(name: String): String {
        val extracted = File(nativeLibDir, name)
        if (extracted.exists()) return extracted.absolutePath
        return "$apkPath!/lib/$abi/$name"
    }

    companion object {
        const val PACKAGE = "com.and.games505.TerrariaPaid"

        fun locate(context: Context): GameInstall? {
            val pm = context.packageManager
            val info = runCatching { pm.getPackageInfo(PACKAGE, 0) }.getOrNull() ?: return null
            val app = info.applicationInfo ?: return null
            val abi = if (Process.is64Bit()) "arm64-v8a" else "armeabi-v7a"
            @Suppress("DEPRECATION")
            val versionCode = info.longVersionCode
            return GameInstall(PACKAGE, app.sourceDir, app.nativeLibraryDir, versionCode, abi)
        }
    }
}
