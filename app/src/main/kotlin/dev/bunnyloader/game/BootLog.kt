package dev.bunnyloader.game

import android.content.Context
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
    fun reset(ctx: Context) = runCatching { file(ctx).writeText("") }.let {}

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

    fun read(ctx: Context): String =
        (runCatching { file(ctx).readText() }.getOrDefault("").ifBlank { "(sem registro ainda)" }) +
            "\n--- por que o processo morreu ---\n" + exitReasons(ctx)

    /**
     * Motivo das últimas mortes de processo do app. Crash NATIVO não passa por
     * try/catch nem aparece na trilha acima; esta API pública (Android 11+)
     * entrega o motivo e a descrição, que é o que falta para diagnosticar sem adb.
     */
    private fun exitReasons(ctx: Context): String = runCatching {
        if (android.os.Build.VERSION.SDK_INT < 30) return@runCatching "(precisa Android 11+)"
        val am = ctx.getSystemService(android.app.ActivityManager::class.java)
        val list = am.getHistoricalProcessExitReasons(ctx.packageName, 0, 5)
        if (list.isEmpty()) return@runCatching "(nenhum registro)"
        list.joinToString("\n") { i ->
            "${i.processName}: ${reasonName(i.reason)} status=${i.status}" +
                (i.description?.let { "\n   $it" } ?: "")
        }
    }.getOrElse { "(erro lendo: ${it.javaClass.simpleName})" }

    private fun reasonName(r: Int): String = when (r) {
        android.app.ApplicationExitInfo.REASON_CRASH -> "CRASH (Java)"
        android.app.ApplicationExitInfo.REASON_CRASH_NATIVE -> "CRASH NATIVO"
        android.app.ApplicationExitInfo.REASON_ANR -> "ANR"
        android.app.ApplicationExitInfo.REASON_LOW_MEMORY -> "SEM MEMORIA"
        android.app.ApplicationExitInfo.REASON_SIGNALED -> "SINAL"
        android.app.ApplicationExitInfo.REASON_EXIT_SELF -> "saiu sozinho"
        android.app.ApplicationExitInfo.REASON_USER_REQUESTED -> "usuario"
        android.app.ApplicationExitInfo.REASON_OTHER -> "outro"
        else -> "codigo $r"
    }
}
