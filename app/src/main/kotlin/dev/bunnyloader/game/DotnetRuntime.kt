package dev.bunnyloader.game

import android.content.Context
import java.io.File

/**
 * O runtime .NET dos mods C#.
 *
 * Os assemblies (System.Private.CoreLib.dll, BunnyLoader.dll...) vêm em
 * `assets/dotnet/`, e o Mono só os abre por caminho de arquivo: antes de o
 * jogo subir, vão para `filesDir/dotnet`. A cópia se refaz quando o APK muda
 * (o `lastUpdateTime` do pacote); as libs nativas já estão no
 * `nativeLibraryDir`.
 */
object DotnetRuntime {
    private const val FOLDER = "dotnet"
    private const val STAMP = ".stamp"

    /** A pasta pronta, ou "" se este APK saiu sem o runtime (`-Pbl.csharp=false`). */
    fun prepare(ctx: Context): String {
        val names = ctx.assets.list(FOLDER).orEmpty()
        if (names.isEmpty()) return ""
        val dir = File(ctx.filesDir, FOLDER)
        val stamp = ctx.packageManager.getPackageInfo(ctx.packageName, 0).lastUpdateTime.toString()
        val stampFile = File(dir, STAMP)
        if (stampFile.isFile && stampFile.readText() == stamp) return dir.absolutePath
        dir.deleteRecursively()
        dir.mkdirs()
        for (name in names) {
            ctx.assets.open("$FOLDER/$name").use { input ->
                File(dir, name).outputStream().use { input.copyTo(it) }
            }
        }
        stampFile.writeText(stamp)
        return dir.absolutePath
    }
}
