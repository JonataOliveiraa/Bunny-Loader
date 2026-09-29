package dev.bunnyloader.game

import android.content.Context

/**
 * O Terraria está instalado neste aparelho?
 *
 * Só detecção: não confere origem (Play) nem assinatura, e nunca bloqueia o
 * jogo: o launcher avisa uma vez (LauncherActivity) e o GameActivity só
 * registra no log. A verificação de verdade fica para depois.
 */
object Eligibility {
    data class Result(val found: Boolean, val detail: String)

    fun check(ctx: Context): Result {
        val found = runCatching {
            ctx.packageManager.getPackageInfo(GameInstall.PACKAGE, 0)
        }.isSuccess
        return if (found) Result(true, "Terraria encontrado.")
        else Result(false, "O Terraria não está instalado neste aparelho.")
    }
}
