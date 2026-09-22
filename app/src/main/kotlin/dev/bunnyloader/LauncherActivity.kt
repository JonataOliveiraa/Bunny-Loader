package dev.bunnyloader

import android.content.Intent
import android.os.Bundle
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

/**
 * Tela inicial. Fase 1: só um botão "Jogar" que abre a GameActivity.
 * TODO(Fase 5): lista de mods, importar (.bmod), toggles, visualizador de log.
 */
class LauncherActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    LauncherScreen(
                        gameFound = GameInstall.locate(this) != null,
                        onPlay = {
                            startActivity(Intent(this, GameActivity::class.java))
                        },
                    )
                }
            }
        }
    }
}

@Composable
private fun LauncherScreen(gameFound: Boolean, onPlay: () -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text("Bunny Loader", style = MaterialTheme.typography.headlineMedium)
        Text(
            if (gameFound) "Terraria encontrado" else "Terraria NÃO encontrado",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(top = 8.dp, bottom = 24.dp),
        )
        Button(onClick = onPlay, enabled = gameFound) { Text("Jogar") }
    }
}
