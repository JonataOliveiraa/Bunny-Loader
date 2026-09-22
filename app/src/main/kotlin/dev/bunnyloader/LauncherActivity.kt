package dev.bunnyloader

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import dev.bunnyloader.patch.ApkPatcher
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

/**
 * Launcher do Bunny Loader — um app só (com.bunnyloader).
 *
 * Modelo TL Pro, sem root: lê o Terraria instalado, cria a versão modificada NO
 * APARELHO (ApkPatcher) e a instala ao lado do original (pacote renomeado). O
 * botão então inicia essa versão. Não redistribui o Terraria — usa a sua cópia.
 */
class LauncherActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme { Surface(Modifier.fillMaxSize()) { LauncherScreen() } }
        }
    }
}

private fun isInstalled(ctx: Context, pkg: String): Boolean =
    runCatching { ctx.packageManager.getPackageInfo(pkg, 0) }.isSuccess

private fun launchGame(ctx: Context) {
    val intent = ctx.packageManager.getLaunchIntentForPackage(ApkPatcher.NEW_PKG)
    if (intent == null) {
        Toast.makeText(ctx, "Não consegui abrir o jogo.", Toast.LENGTH_LONG).show()
        return
    }
    ctx.startActivity(intent)
}

private fun installApk(ctx: Context, apk: File) {
    val uri = FileProvider.getUriForFile(ctx, "${ctx.packageName}.fileprovider", apk)
    val intent = Intent(Intent.ACTION_VIEW).apply {
        setDataAndType(uri, "application/vnd.android.package-archive")
        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
    }
    ctx.startActivity(intent)
}

@Composable
private fun LauncherScreen() {
    val ctx = LocalContext.current
    val scope = rememberCoroutineScope()
    var modded by remember { mutableStateOf(isInstalled(ctx, ApkPatcher.NEW_PKG)) }
    val terraria = remember { isInstalled(ctx, ApkPatcher.TERRARIA) }
    var busy by remember { mutableStateOf(false) }
    var status by remember { mutableStateOf("") }

    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Image(
            painter = painterResource(R.drawable.ic_bunny),
            contentDescription = null,
            modifier = Modifier.size(96.dp),
        )
        Text("Bunny Loader", style = MaterialTheme.typography.headlineMedium,
            modifier = Modifier.padding(top = 12.dp))

        if (!terraria) {
            Text("Instale o Terraria (original) primeiro.",
                style = MaterialTheme.typography.bodyMedium,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(top = 16.dp))
            return@Column
        }

        Text(
            when {
                busy -> status
                modded -> "Terraria com mods pronto."
                else -> "Vamos preparar o Terraria com mods (uma vez)."
            },
            style = MaterialTheme.typography.bodyMedium,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(top = 8.dp, bottom = 24.dp),
        )

        if (modded) {
            Button(onClick = { launchGame(ctx) }, enabled = !busy) {
                Text("Iniciar Terraria com mods")
            }
        } else {
            Button(
                enabled = !busy,
                onClick = {
                    busy = true
                    scope.launch {
                        val result = runCatching {
                            withContext(Dispatchers.IO) {
                                ApkPatcher.build(ctx) { p ->
                                    status = "${p.step}… ${p.pct}%"
                                }
                            }
                        }
                        busy = false
                        result.onSuccess { apk ->
                            status = "Instalando…"
                            installApk(ctx, apk)
                        }.onFailure {
                            android.util.Log.e("BunnyLoader", "patch falhou", it)
                            val msg = "${it.javaClass.simpleName}: ${it.message}"
                            status = "Erro: $msg"
                            Toast.makeText(ctx, "Falhou: $msg", Toast.LENGTH_LONG).show()
                        }
                    }
                },
            ) { Text(if (busy) "Preparando…" else "Criar Terraria com mods") }
        }

        Button(
            onClick = { modded = isInstalled(ctx, ApkPatcher.NEW_PKG) },
            enabled = !busy,
            modifier = Modifier.padding(top = 12.dp),
        ) { Text("Verificar de novo") }

        // ESTÁGIO 0: spike do PairIP (roda o boot do jogo no NOSSO processo).
        Button(
            onClick = { status = dev.bunnyloader.game.PairipSpike.run(ctx) },
            enabled = !busy,
            modifier = Modifier.padding(top = 24.dp),
        ) { Text("Spike PairIP") }
        if (status.isNotEmpty()) {
            Text(
                status,
                style = MaterialTheme.typography.bodySmall,
                modifier = Modifier.padding(top = 8.dp),
            )
        }
    }
}
