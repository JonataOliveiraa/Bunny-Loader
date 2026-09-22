package dev.bunnyloader.game

import android.app.ActivityManager
import android.app.ApplicationExitInfo
import android.content.Context
import android.os.Build
import android.util.Log
import dev.bunnyloader.TAG
import java.io.File

/**
 * Trilha do boot hospedado, gravada em arquivo.
 *
 * A GameActivity roda no processo `:game`, e quando ela falha só sobra um Toast
 * que some. Sem adb no aparelho do usuário não dá para ler o logcat, então cada
 * passo vai também para um arquivo no filesDir (compartilhado entre os dois
 * processos do app) e o launcher mostra o conteúdo na tela.
 */
object BootLog {
    private const val FILE = "hosted-boot.log"

    private fun file(ctx: Context) = File(ctx.filesDir, FILE)

    /** Começa uma trilha nova (chamar no início do boot). */
    fun reset(ctx: Context) {
        runCatching { file(ctx).writeText("") }
    }

    fun add(ctx: Context, line: String) {
        Log.i(TAG, "boot: $line")
        runCatching { file(ctx).appendText(line + "\n") }
    }

    /** Registra uma falha com a causa raiz desembrulhada (reflection esconde). */
    fun fail(ctx: Context, what: String, t: Throwable?) {
        val root = t?.let { generateSequence(it) { c -> c.cause }.last() }
        val detail = root?.let { "${it.javaClass.simpleName}: ${it.message}" } ?: "sem detalhes"
        add(ctx, "FALHA: $what")
        add(ctx, "  -> $detail")
        root?.stackTrace?.take(6)?.forEach { add(ctx, "     at $it") }
    }

    /**
     * Captura exceções de QUALQUER thread do processo.
     *
     * Os try/catch da GameActivity só cobrem o onCreate; a Unity estoura depois,
     * nas threads dela, e aí só sobra "CRASH (Java)" no exit reason — sem stack.
     */
    fun installCrashHandler(ctx: Context) {
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, t ->
            runCatching {
                add(ctx, "CRASH na thread '${thread.name}'")
                var e: Throwable? = t
                var depth = 0
                while (e != null && depth++ < 3) {
                    add(ctx, "  ${e.javaClass.name}: ${e.message}")
                    e.stackTrace.take(24).forEach { add(ctx, "    at $it") }
                    e = e.cause?.also { add(ctx, "  causado por:") }
                }
            }
            previous?.uncaughtException(thread, t)
        }
    }

    private fun logcatFile(ctx: Context) = File(ctx.filesDir, "hosted-logcat.log")

    /**
     * Captura o logcat DO PRÓPRIO processo para arquivo.
     *
     * A Unity imprime o motivo do erro no logcat imediatamente antes de abortar,
     * e um app pode ler os próprios logs sem permissão especial. Isso é bem mais
     * confiável que o tombstone, que desde o Android 12 vem em protobuf e chegou
     * truncado aqui.
     */
    fun captureLogcat(ctx: Context) {
        val out = logcatFile(ctx)
        runCatching { out.writeText("") }
        Thread {
            runCatching {
                val pid = android.os.Process.myPid()
                val p = Runtime.getRuntime().exec(
                    arrayOf("logcat", "-v", "brief", "--pid=$pid"),
                )
                p.inputStream.bufferedReader().forEachLine { line ->
                    val keep = line.contains("Unity") || line.contains("IL2CPP") ||
                        line.contains("DEBUG") || line.contains("libc") ||
                        line.contains("Fatal") || line.contains("FATAL") ||
                        line.contains("Error") || line.contains("error") ||
                        line.contains("JNI") || line.contains("dlopen") ||
                        line.contains("art")
                    // Limite de tamanho: interessa a causa, não o histórico todo.
                    if (keep && out.length() < 60_000) out.appendText(line + "\n")
                }
            }
        }.apply { isDaemon = true }.start()
    }

    fun read(ctx: Context): String =
        (runCatching { file(ctx).readText() }.getOrDefault("").ifBlank { "(sem registro ainda)" }) +
            "\n--- logcat do processo do jogo ---\n" + lastLogcat(ctx) +
            "\n--- por que o processo morreu ---\n" + exitReasons(ctx)

    /**
     * A CAUSA primeiro, depois o resto.
     *
     * Quando a Unity aborta via JNI FatalError, a ART despeja um stack enorme
     * logo depois — e cortar pelo fim justamente esconde a mensagem, que vem
     * antes. Então destacamos as linhas de causa e só depois mostramos a cauda.
     */
    private fun lastLogcat(ctx: Context): String = runCatching {
        val lines = logcatFile(ctx).readLines()
        if (lines.isEmpty()) return@runCatching "(vazio)"
        val causes = lines.filter { l ->
            l.contains("FatalError called") || l.contains("JNI DETECTED") ||
                l.contains("Abort message") || l.contains("UnsatisfiedLink") ||
                l.contains("dlopen") || l.contains("Unable to") ||
                l.contains("Failed to") || l.contains("E/Unity") ||
                l.contains("E Unity") || l.contains("could not")
        }.distinct().take(15)
        buildString {
            if (causes.isNotEmpty()) {
                append(">>> CAUSA:\n")
                causes.forEach { append("  ").append(it.trim()).append("\n") }
                append(">>> resto:\n")
            }
            // Sem os quadros de stack repetidos, que não acrescentam nada.
            lines.filterNot { it.contains("native: #") || it.contains("runtime.cc:") }
                .takeLast(25).forEach { append(it).append("\n") }
        }
    }.getOrElse { "(sem captura)" }

    /**
     * Motivo das últimas mortes de processo do app. Crash NATIVO não passa por
     * try/catch nem aparece na trilha acima; esta API pública (Android 11+)
     * entrega o motivo e, para crash nativo, o tombstone.
     */
    private fun exitReasons(ctx: Context): String = runCatching {
        if (Build.VERSION.SDK_INT < 30) return@runCatching "(precisa Android 11+)"
        val am = ctx.getSystemService(ActivityManager::class.java)
        val list = am.getHistoricalProcessExitReasons(ctx.packageName, 0, 5)
        if (list.isEmpty()) return@runCatching "(nenhum registro)"
        list.joinToString("\n") { info ->
            buildString {
                append(info.processName).append(": ").append(reasonName(info.reason))
                append(" status=").append(info.status)
                info.description?.let { append("\n   ").append(it) }
                append(tombstone(info))
            }
        }
    }.getOrElse { "(erro lendo: ${it.javaClass.simpleName})" }

    /**
     * Para crash NATIVO o sistema guarda o tombstone (mensagem de abort +
     * backtrace) e o entrega por getTraceInputStream(). É a única forma de saber
     * por que o processo abortou sem ter adb no aparelho.
     */
    private fun tombstone(info: ApplicationExitInfo): String {
        if (info.reason != ApplicationExitInfo.REASON_CRASH_NATIVE) return ""
        return runCatching {
            val text = info.traceInputStream?.bufferedReader()?.use { it.readText() }
                ?: return "\n   (sem tombstone)"
            val frame = Regex("""^\s+#\d\d """)
            val useful = text.lineSequence()
                .filter {
                    it.contains("signal") || it.contains("Abort message") ||
                        it.contains("Cause:") || frame.containsMatchIn(it)
                }
                .take(18)
                .joinToString("\n") { "   " + it.trim() }
            if (useful.isBlank()) "\n   (tombstone sem linhas úteis)" else "\n$useful"
        }.getOrElse { "\n   (tombstone ilegível: ${it.javaClass.simpleName})" }
    }

    private fun reasonName(r: Int): String = when (r) {
        ApplicationExitInfo.REASON_CRASH -> "CRASH (Java)"
        ApplicationExitInfo.REASON_CRASH_NATIVE -> "CRASH NATIVO"
        ApplicationExitInfo.REASON_ANR -> "ANR"
        ApplicationExitInfo.REASON_LOW_MEMORY -> "SEM MEMORIA"
        ApplicationExitInfo.REASON_SIGNALED -> "SINAL"
        ApplicationExitInfo.REASON_EXIT_SELF -> "saiu sozinho"
        ApplicationExitInfo.REASON_USER_REQUESTED -> "usuario"
        ApplicationExitInfo.REASON_OTHER -> "outro"
        else -> "codigo $r"
    }
}
