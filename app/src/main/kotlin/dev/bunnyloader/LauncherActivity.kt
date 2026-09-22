package dev.bunnyloader

import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import dev.bunnyloader.game.GameInstall
import dev.bunnyloader.game.GameLauncher
import dev.bunnyloader.game.Root

/**
 * Tela inicial. Caminho B: o botao lanca o Terraria no processo dele com a
 * libbunny.so pre-carregada (ver GameLauncher). Precisa de root.
 *
 * "Jogar limpo" sobe o jogo sem o wrap — util para confirmar que o launch por
 * root funciona antes de a lib nativa existir, e para comparar comportamento.
 *
 * TODO(Fase 5): lista de mods, importar (.bmod), toggles, visualizador de log.
 */
class LauncherActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val install = GameInstall.locate(this)
        val hasRoot = Root.available()
        setContent {
            MaterialTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    LauncherScreen(
                        install = install,
                        hasRoot = hasRoot,
                        onPlay = { withMods -> play(install, withMods) },
                    )
                }
            }
        }
    }

    private fun play(install: GameInstall?, withMods: Boolean) {
        if (install == null) return
        val r = GameLauncher.launch(this, install, withMods)
        Toast.makeText(this, r.detail, Toast.LENGTH_LONG).show()
    }
}

@Composable
private fun LauncherScreen(
    install: GameInstall?,
    hasRoot: Boolean,
    onPlay: (withMods: Boolean) -> Unit,
) {
    val gameFound = install != null
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text("Bunny Loader", style = MaterialTheme.typography.headlineMedium)
        Text(
            buildString {
                append(if (gameFound) "Terraria ${install!!.versionCode}" else "Terraria NÃO encontrado")
                append(if (hasRoot) " · root OK" else " · SEM root")
            },
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(top = 8.dp, bottom = 24.dp),
        )
        Button(
            onClick = { onPlay(true) },
            enabled = gameFound && hasRoot,
        ) { Text("Jogar com mods") }
        Button(
            onClick = { onPlay(false) },
            enabled = gameFound && hasRoot,
            modifier = Modifier.padding(top = 12.dp),
        ) { Text("Jogar limpo") }
    }
}
