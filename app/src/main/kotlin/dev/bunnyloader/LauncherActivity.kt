package dev.bunnyloader

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.bunnyloader.game.BootLog
import dev.bunnyloader.game.BundledRuntime
import dev.bunnyloader.game.Eligibility

/**
 * Launcher do Bunny Loader.
 *
 * Navegação por um rodapé de três abas (config / jogar / mods). Só a de jogar
 * faz alguma coisa por enquanto; as outras são casca, para a forma do app
 * ficar decidida antes do conteúdo.
 */
class LauncherActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme { Surface(Modifier.fillMaxSize()) { LauncherScreen() } }
        }
    }
}

/** Fonte pixelada de _icons/font.TTF — a mesma identidade do título. */
private val PixelFont = FontFamily(Font(R.font.bunny))

private val Ink = Color(0xFFE8E4F0)
private val Panel = Color(0xFF1B1726)
private val Accent = Color(0xFF7BC86C)

private enum class Tab(val icon: Int, val label: String) {
    CONFIG(R.drawable.ic_nav_config, "Config"),
    PLAY(R.drawable.ic_nav_play, "Jogar"),
    MODS(R.drawable.ic_nav_mods, "Mods"),
}

@Composable
private fun LauncherScreen() {
    var tab by remember { mutableStateOf(Tab.PLAY) }

    Column(Modifier.fillMaxSize().background(Color(0xFF12101A))) {
        Box(Modifier.weight(1f).fillMaxWidth()) {
            when (tab) {
                Tab.PLAY -> PlayTab()
                Tab.CONFIG -> ConfigTab()
                Tab.MODS -> Placeholder("Mods", "Lista de mods instalados e o que está ligado.")
            }
        }
        NavBar(tab) { tab = it }
    }
}

// --- rodapé ------------------------------------------------------------------

@Composable
private fun NavBar(current: Tab, onSelect: (Tab) -> Unit) {
    Row(
        Modifier.fillMaxWidth().background(Panel).padding(vertical = 10.dp),
        horizontalArrangement = Arrangement.SpaceEvenly,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        for (t in Tab.entries) {
            val selected = t == current
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                modifier = Modifier
                    .clip(RoundedCornerShape(10.dp))
                    .clickable { onSelect(t) }
                    .background(if (selected) Color(0x22FFFFFF) else Color.Transparent)
                    .padding(horizontal = 20.dp, vertical = 6.dp),
            ) {
                PixelIcon(t.icon, if (selected) 40.dp else 32.dp)
                Text(
                    t.label,
                    fontFamily = PixelFont,
                    fontSize = 13.sp,
                    color = if (selected) Accent else Ink.copy(alpha = 0.55f),
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
        }
    }
}

/**
 * Arte de pixel escala com vizinho-mais-próximo.
 *
 * O padrão do Compose é interpolação linear, que num sprite de 60x58 ampliado
 * vira borrão. FilterQuality.None mantém a borda dura.
 */
@Composable
private fun PixelIcon(res: Int, size: androidx.compose.ui.unit.Dp) {
    Image(
        bitmap = ImageBitmap.imageResource(res),
        contentDescription = null,
        filterQuality = FilterQuality.None,
        modifier = Modifier.size(size),
    )
}

// --- abas ---------------------------------------------------------------------

@Composable
private fun PlayTab() {
    val ctx = LocalContext.current
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Image(
            bitmap = ImageBitmap.imageResource(R.drawable.img_title),
            contentDescription = "Bunny Loader",
            filterQuality = FilterQuality.None,
            // Fit + teto de altura: em tela larga o FillWidth esticava o título
            // até empurrar o botão e a versão para fora.
            contentScale = ContentScale.Fit,
            modifier = Modifier.fillMaxWidth(0.7f).heightIn(max = 160.dp),
        )

        Box(Modifier.height(32.dp))

        // O botão no meio da tela, como pedido: é a única ação do app.
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier
                .clip(RoundedCornerShape(16.dp))
                .clickable { ctx.startActivity(Intent(ctx, GameActivity::class.java)) }
                .background(Panel)
                .padding(horizontal = 40.dp, vertical = 20.dp),
        ) {
            PixelIcon(R.drawable.ic_nav_play, 72.dp)
            Text(
                "JOGAR",
                fontFamily = PixelFont,
                fontSize = 24.sp,
                color = Accent,
                modifier = Modifier.padding(top = 10.dp),
            )
        }

        Text(
            "Terraria ${BundledRuntime.VERSION_NAME}",
            fontFamily = PixelFont,
            fontSize = 13.sp,
            color = Ink.copy(alpha = 0.5f),
            modifier = Modifier.padding(top = 20.dp),
        )
    }
}

/**
 * Casca — mais o visor de log.
 *
 * O log fica aqui de propósito, apesar de esta aba ser "só visual" por ora: é o
 * único caminho para reportar uma falha do processo :game, que morre sem deixar
 * nada na tela. Tirá-lo junto com os botões antigos deixaria o app sem
 * diagnóstico.
 */
@Composable
private fun ConfigTab() {
    val ctx = LocalContext.current
    val clipboard = LocalClipboardManager.current
    var log by remember { mutableStateOf("") }

    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Header("Config")
        Text(
            remember { Eligibility.check(ctx).detail },
            fontFamily = PixelFont,
            fontSize = 12.sp,
            color = Ink.copy(alpha = 0.7f),
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(bottom = 20.dp),
        )

        Button(onClick = { log = BootLog.read(ctx) }) { Text("Ver log do último boot") }
        if (log.isNotEmpty()) {
            Button(
                onClick = { clipboard.setText(AnnotatedString(log)) },
                modifier = Modifier.padding(top = 8.dp),
            ) { Text("Copiar log") }
            Text(
                log,
                fontSize = 11.sp,
                color = Ink.copy(alpha = 0.8f),
                modifier = Modifier.padding(top = 8.dp),
            )
        }
    }
}

@Composable
private fun Placeholder(title: String, subtitle: String) {
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Header(title)
        Text(
            subtitle,
            fontFamily = PixelFont,
            fontSize = 13.sp,
            color = Ink.copy(alpha = 0.5f),
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun Header(text: String) {
    Text(
        text,
        fontFamily = PixelFont,
        fontSize = 26.sp,
        color = Ink,
        modifier = Modifier.padding(bottom = 12.dp),
    )
}
