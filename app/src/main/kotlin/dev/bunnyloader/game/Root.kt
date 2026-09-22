package dev.bunnyloader.game

import java.io.BufferedReader

/**
 * Executa um script de shell como root via `su -c`. Usado no caminho B para
 * preparar e lançar o jogo (ver GameLauncher). Alvo de desenvolvimento com root
 * (MuMu); num aparelho sem root isto simplesmente falha e o caller reporta.
 */
object Root {

    data class Result(val ok: Boolean, val output: String)

    fun available(): Boolean = runCatching {
        val p = ProcessBuilder("su", "-c", "id").redirectErrorStream(true).start()
        val out = p.inputStream.bufferedReader().use(BufferedReader::readText)
        p.waitFor()
        p.exitValue() == 0 && out.contains("uid=0")
    }.getOrDefault(false)

    fun run(script: String): Result = runCatching {
        val p = ProcessBuilder("su", "-c", "sh").redirectErrorStream(true).start()
        p.outputStream.bufferedWriter().use { it.write(script) }
        val out = p.inputStream.bufferedReader().use(BufferedReader::readText)
        p.waitFor()
        Result(p.exitValue() == 0, out)
    }.getOrElse { Result(false, "su indisponivel: ${it.message}") }
}
