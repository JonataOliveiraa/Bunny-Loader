package dev.bunnyloader.catalog

import android.content.Context
import java.io.File

/**
 * O compilador dos mods C# com fontes, o mesmo BunnyLoader.Compiler do app:
 * Mono + Roslyn + as referências da API em `assets/dotnet/` (o build copia a
 * pasta do app), extraídos para `filesDir/dotnet` na primeira vez e a cada APK
 * novo. O Mono sobe no primeiro build e fica.
 */
class CompilerRuntime(private val context: Context) {
    class Report(
        val status: String,
        val reason: String,
        val milliseconds: Long,
        val files: Int,
        val lines: Long,
        val errors: List<String>,
        val warnings: List<String>,
    ) {
        val success get() = status != "failed"
        val compiled get() = status == "compiled"
    }

    private var dir: String? = null

    /** Compila o projeto do `entry` (content/MeuMod.cs de um pacote extraído), a não ser que o bin/ esteja em dia. */
    fun build(entry: File, force: Boolean = false): Report {
        val runtime = dir ?: prepare().also { dir = it }
        val text = String(nativeBuild(runtime, context.applicationInfo.nativeLibraryDir, entry.absolutePath, force), Charsets.UTF_8)
        return parse(text)
    }

    private fun prepare(): String {
        val names = context.assets.list(FOLDER).orEmpty()
        if (names.isEmpty()) throw Fail(I18n.tr("este APK saiu sem o compilador de C#", "this APK was built without the C# compiler"))
        val target = File(context.filesDir, FOLDER)
        val stamp = context.packageManager.getPackageInfo(context.packageName, 0).lastUpdateTime.toString()
        val stampFile = File(target, STAMP)
        if (!stampFile.isFile || stampFile.readText() != stamp) {
            target.deleteRecursively()
            target.mkdirs()
            for (name in names) {
                context.assets.open("$FOLDER/$name").use { input -> File(target, name).outputStream().use { input.copyTo(it) } }
            }
            stampFile.writeText(stamp)
        }
        loadLibraries()
        return target.absolutePath
    }

    companion object {
        private const val FOLDER = "dotnet"
        private const val STAMP = ".stamp"
        private var loaded = false

        /**
         * A criptografia do .NET (o Roslyn calcula SHA-1 e SHA-256) precisa do
         * JNI_OnLoad dela, que só o System.loadLibrary roda; o Mono vem antes
         * do host para o dlopen dele achar a lib no namespace do app.
         */
        @Synchronized
        private fun loadLibraries() {
            if (loaded) return
            System.loadLibrary("System.Security.Cryptography.Native.Android")
            System.loadLibrary("monosgen-2.0")
            System.loadLibrary("blcompiler")
            loaded = true
        }

        @JvmStatic
        private external fun nativeBuild(runtimeDir: String, nativeDir: String, entry: String, force: Boolean): ByteArray

        /** O BuildResult.ToReport() do C#. */
        fun parse(text: String): Report {
            var status = "failed"
            var reason = ""
            var ms = 0L
            var files = 0
            var lines = 0L
            val errors = mutableListOf<String>()
            val warnings = mutableListOf<String>()
            for (line in text.lineSequence()) {
                val key = line.substringBefore(' ')
                val value = line.substringAfter(' ', "")
                when (key) {
                    "status" -> status = value
                    "reason" -> reason = value
                    "stats" -> value.split(' ').let {
                        ms = it.getOrNull(0)?.toLongOrNull() ?: 0
                        files = it.getOrNull(1)?.toIntOrNull() ?: 0
                        lines = it.getOrNull(2)?.toLongOrNull() ?: 0
                    }
                    "error" -> errors += value
                    "warning" -> warnings += value
                }
            }
            return Report(status, reason, ms, files, lines, errors, warnings)
        }
    }
}
