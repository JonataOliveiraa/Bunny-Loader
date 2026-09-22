package dev.bunnyloader

import android.content.Context
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
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp

/**
 * Launcher do Bunny Loader — app proprio (com.bunnyloader), separado do jogo.
 *
 * O botao inicia o Terraria MODIFICADO, que e instalado ao lado do original com
 * pacote renomeado (com.bunnyloader.terraria.paid, ver tools/repack.py --rename)
 * — a libbunny ja vem embutida nele. Sem root: o launcher so dispara o Intent de
 * abertura; a injecao aconteceu no repackage.
 */
class LauncherActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme { Surface(Modifier.fillMaxSize()) { LauncherScreen() } }
        }
    }

    companion object {
        // Pacote do Terraria modificado (coexiste com o original).
        const val MODDED_GAME = "com.bunnyloader.terraria.paid"
    }
}

private fun isInstalled(ctx: Context, pkg: String): Boolean =
    runCatching { ctx.packageManager.getPackageInfo(pkg, 0) }.isSuccess

private fun launchGame(ctx: Context, pkg: String) {
    val intent = ctx.packageManager.getLaunchIntentForPackage(pkg)
    if (intent == null) {
        Toast.makeText(ctx, "Não consegui abrir o jogo.", Toast.LENGTH_LONG).show()
        return
    }
    ctx.startActivity(intent)
}

@Composable
private fun LauncherScreen() {
    val ctx = LocalContext.current
    var installed by remember { mutableStateOf(isInstalled(ctx, LauncherActivity.MODDED_GAME)) }

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
        Text(
            "Bunny Loader",
            style = MaterialTheme.typography.headlineMedium,
            modifier = Modifier.padding(top = 12.dp),
        )
        Text(
            if (installed) "Terraria (Bunny) instalado" else "Terraria (Bunny) não instalado",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(top = 8.dp, bottom = 24.dp),
        )

        Button(
            onClick = { launchGame(ctx, LauncherActivity.MODDED_GAME) },
            enabled = installed,
        ) { Text("Iniciar Terraria com mods") }

        if (!installed) {
            Text(
                "Instale primeiro o terraria-bunny-coexist.apk (o Terraria com mods). " +
                    "Ele fica ao lado do Terraria original, sem substituí-lo.",
                style = MaterialTheme.typography.bodySmall,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(top = 16.dp),
            )
            Button(
                onClick = { installed = isInstalled(ctx, LauncherActivity.MODDED_GAME) },
                modifier = Modifier.padding(top = 12.dp),
            ) { Text("Verificar de novo") }
        }
    }
}
