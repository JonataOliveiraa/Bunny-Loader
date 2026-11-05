package dev.bunnyloader

import android.app.Activity
import android.app.ActivityManager
import android.content.Intent
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.Process
import android.provider.DocumentsContract
import android.util.Log
import dev.bunnyloader.game.Eligibility
import dev.bunnyloader.mods.EditorBridge
import dev.bunnyloader.mods.ModSourcesDocumentsProvider

/**
 * A porta de entrada do BL Editor (com.bunnyloader.editor), um APK à parte.
 *
 * Só apps assinados com a mesma chave chegam aqui (permissão de assinatura
 * no manifesto). Duas ações:
 *  - [ACTION_CONNECT] devolve a árvore da pasta ModSources com leitura e
 *    escrita persistentes: o editor edita os projetos no lugar, sem root e
 *    sem passar pelo seletor de pastas do sistema;
 *  - [ACTION_TEST] empacota o projeto [EXTRA_PROJECT], instala, liga e abre o
 *    jogo (reiniciando, se ele já estiver aberto).
 *
 * Tela transparente: enquanto empacota, quem aparece é o próprio editor.
 * Erro volta como [RESULT_ERROR] com o texto em [EXTRA_ERROR].
 */
class EditorBridgeActivity : Activity() {

    private val handler = Handler(Looper.getMainLooper())

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        if (savedInstanceState != null) return
        when (intent.action) {
            ACTION_CONNECT -> connect()
            ACTION_TEST -> test(intent.getStringExtra(EXTRA_PROJECT).orEmpty())
            else -> fail("Unknown action: ${intent.action}")
        }
    }

    private fun connect() {
        val tree = DocumentsContract.buildTreeDocumentUri(
            ModSourcesDocumentsProvider.AUTHORITY, ModSourcesDocumentsProvider.ROOT_ID)
        setResult(RESULT_OK, Intent().setData(tree).addFlags(
            Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION or
                Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION or Intent.FLAG_GRANT_PREFIX_URI_PERMISSION))
        finish()
    }

    /** Sem o Terraria o mod ainda é instalado; só o jogo não abre. */
    private fun test(project: String) {
        val context = applicationContext
        Thread {
            val error = runCatching { EditorBridge.install(context, project) }.exceptionOrNull()
            handler.post {
                if (error != null) {
                    Log.w(TAG, "Editor test of '$project' failed", error)
                    fail(error.message ?: error.javaClass.simpleName)
                } else if (Eligibility.check(this) != Eligibility.Status.OK) {
                    fail("Terraria is not installed", ERROR_NO_GAME)
                } else {
                    val compile = dev.bunnyloader.compile.CompileActivity.compileIntent(this)
                    if (compile != null) {
                        startActivityForResult(
                            compile.putExtra(dev.bunnyloader.compile.CompileActivity.EXTRA_RETURN_RESULT, true),
                            REQUEST_COMPILE,
                        )
                    } else {
                        openGame()
                    }
                }
            }
        }.start()
    }

    private fun openGame() {
        launchGame()
        setResult(RESULT_OK)
        finish()
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode != REQUEST_COMPILE) return
        if (resultCode == dev.bunnyloader.compile.CompileActivity.RESULT_FAILED) {
            fail(data?.getStringExtra(dev.bunnyloader.compile.CompileActivity.EXTRA_ERRORS).orEmpty())
        } else {
            openGame()
        }
    }

    /** Jogo aberto: fecha e abre de novo pelo RestartActivity, como o "Reiniciar" do Mod Menu. */
    private fun launchGame() {
        val game = (getSystemService(ACTIVITY_SERVICE) as ActivityManager).runningAppProcesses
            ?.firstOrNull { it.processName == "$packageName:game" }
        if (game != null) {
            startActivity(Intent(this, RestartActivity::class.java)
                .putExtra(RestartActivity.EXTRA_PID, game.pid)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
            Process.killProcess(game.pid)
        } else {
            startActivity(Intent(this, GameActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        }
    }

    private fun fail(message: String, code: String = ERROR_BUILD) {
        setResult(RESULT_ERROR, Intent().putExtra(EXTRA_ERROR, message).putExtra(EXTRA_ERROR_CODE, code))
        finish()
    }

    companion object {
        private const val TAG = "BunnyLoader"
        private const val REQUEST_COMPILE = 1
        const val ACTION_CONNECT = "com.bunnyloader.action.CONNECT_EDITOR"
        const val ACTION_TEST = "com.bunnyloader.action.TEST_SOURCE"
        /** O nome da pasta do projeto dentro de ModSources. */
        const val EXTRA_PROJECT = "project"
        const val EXTRA_ERROR = "error"
        const val EXTRA_ERROR_CODE = "errorCode"
        const val ERROR_NO_GAME = "noGame"
        const val ERROR_BUILD = "build"
        const val RESULT_ERROR = RESULT_FIRST_USER
    }
}
