package dev.bunnyloader.game

import android.content.Context
import android.util.Log
import dev.bunnyloader.TAG
import dev.bunnyloader.mods.ModRepository
import java.io.File

/**
 * Caminho B (lançar + injetar).
 *
 * Em vez de hospedar a UnityPlayer no nosso processo (o que esbarrou no PairIP,
 * ver docs/DECISAO-ARQUITETURA.md), o launcher inicia o Terraria no processo
 * DELE, com a libbunny.so pre-carregada via a propriedade wrap.<pacote>. Assim
 * o Application do jogo roda normal, o PairIP inicializa, as strings sao
 * decifradas, e a nossa lib ja esta la dentro para hookar o il2cpp_init.
 *
 * Tudo isto exige root. Alvo de desenvolvimento: MuMu (tem root, SELinux
 * permissive). Distribuicao sem root fica para depois.
 *
 * Passos:
 *   1. copiar libbunny.so para um caminho legivel pelo processo do jogo
 *   2. gravar o arquivo de config que a lib le no load
 *   3. setprop wrap.<pkg> = "LD_PRELOAD=<lib>"
 *   4. am start da Activity do jogo
 *   5. (limpeza) o wrap fica setado ate ser limpo; o caller decide quando
 */
object GameLauncher {

    private const val WORK_DIR = "/data/local/tmp/bunny"
    private const val CONFIG_PATH = "$WORK_DIR/config"
    private const val LIB_DEST = "$WORK_DIR/libbunny.so"
    private const val LOG_PATH = "$WORK_DIR/bunny.log"
    private const val GAME_ACTIVITY = "com.unity3d.player.UnityPlayerActivity"

    data class Result(val ok: Boolean, val detail: String)

    /**
     * @param context o launcher (processo normal, sem root para a UI).
     * @param install o Terraria localizado.
     * @param withMods false = inicia o jogo limpo (sem wrap), util para comparar.
     */
    fun launch(context: Context, install: GameInstall, withMods: Boolean = true): Result {
        val libSrc = File(context.applicationInfo.nativeLibraryDir, "libbunny.so")
        if (withMods && !libSrc.exists()) {
            return Result(false, "libbunny.so nao encontrada (compile com bl.nativeBuild=true)")
        }

        val repo = ModRepository(context)
        val enabled = repo.enabledIds()

        val script = buildString {
            appendLine("set -e")
            appendLine("mkdir -p $WORK_DIR")
            if (withMods) {
                // Copia a lib para um caminho que o uid do jogo consiga ler.
                appendLine("cp '${libSrc.absolutePath}' $LIB_DEST")
                appendLine("chmod 755 $WORK_DIR $LIB_DEST")

                // Config lida pela lib no constructor (ver Entry.cpp).
                appendLine("cat > $CONFIG_PATH <<'BLCFG'")
                appendLine("gameLibDir=${install.nativeLibDir}")
                appendLine("modsDir=${repo.modsDir.absolutePath}")
                appendLine("enabledMods=${enabled.joinToString(",")}")
                appendLine("logPath=$LOG_PATH")
                appendLine("gameVersion=${install.versionCode}")
                appendLine("BLCFG")
                appendLine("chmod 644 $CONFIG_PATH")

                // Faz o jogo subir com a nossa lib pre-carregada.
                appendLine("setprop wrap.${install.packageName} 'LD_PRELOAD=$LIB_DEST'")
            } else {
                // Garante que nao ha wrap pendente de uma execucao anterior.
                appendLine("setprop wrap.${install.packageName} ''")
                appendLine("rm -f $CONFIG_PATH")
            }
            // Fecha o jogo se estiver aberto, para o wrap valer no proximo start.
            appendLine("am force-stop ${install.packageName}")
            appendLine("am start -n ${install.packageName}/$GAME_ACTIVITY")
        }

        Log.i(TAG, "GameLauncher: iniciando ${install.packageName} (withMods=$withMods)")
        val res = Root.run(script)
        if (!res.ok) {
            Log.e(TAG, "GameLauncher falhou:\n${res.output}")
            return Result(false, res.output.take(200))
        }
        return Result(true, if (withMods) "jogo iniciado com mods" else "jogo iniciado limpo")
    }

    /** Remove o wrap para o jogo voltar a abrir normal fora do launcher. */
    fun clearWrap(install: GameInstall) {
        Root.run("setprop wrap.${install.packageName} ''")
    }
}
