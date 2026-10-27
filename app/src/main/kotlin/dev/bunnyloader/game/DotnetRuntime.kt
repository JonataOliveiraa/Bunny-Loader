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
        loadCrypto()
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

    /**
     * A criptografia do .NET no Android (o compilador dos mods com fontes usa
     * SHA-1 e SHA-256). O Mono a abre com dlopen, que não chama o JNI_OnLoad
     * dela; carregada daqui, ele roda e acha as classes Java dela pelo class
     * loader do app.
     */
    private fun loadCrypto() {
        try {
            System.loadLibrary("System.Security.Cryptography.Native.Android")
        } catch (_: UnsatisfiedLinkError) {
            // APK sem ela: os hashes do .NET lançam DllNotFoundException.
        }
    }
}
