package dev.bunnyloader.ui

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R

/** O convite do servidor do Bunny Loader no Discord. */
const val DISCORD_URL = "https://discord.gg/uPFkuJXMJz"

/**
 * O cartão do Discord, no Início e nas Configurações. O cartão inteiro é o
 * botão; o "Entrar" à direita só diz o que o toque faz.
 *
 * O ícone (26x18, desenhado em 2x: pixel da arte inteiro na tela) sobe e
 * desce devagar, só na camada, e para quando os efeitos do tema param.
 */
@Composable
fun DiscordCard(modifier: Modifier = Modifier) {
    val uriHandler = LocalUriHandler.current
    val fx = LocalThemeFx.current
    val bob = rememberInfiniteTransition(label = "discord")
    val lift by bob.animateFloat(0f, 1f,
        infiniteRepeatable(tween(900), RepeatMode.Reverse), label = "discord sobe")
    val step = with(LocalDensity.current) { 1.dp.toPx() }
    PixelCard(modifier.fillMaxWidth(), fill = Bl.Discord, onClick = {
        // Sem navegador nem o app do Discord o openUri lança; o toque só não faz nada.
        runCatching { uriHandler.openUri(DISCORD_URL) }
    }) {
        Row(Modifier.fillMaxWidth().padding(10.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(
                Modifier.size(68.dp, 52.dp).pixelPanel(fill = Bl.DiscordDeep, raised = false),
                contentAlignment = Alignment.Center,
            ) {
                Image(
                    bitmap = ImageBitmap.imageResource(R.drawable.ic_discord),
                    contentDescription = "Discord",
                    filterQuality = FilterQuality.None,
                    modifier = Modifier.size(52.dp, 36.dp).graphicsLayer {
                        translationY = if (fx.paused()) 0f else -kotlin.math.round(lift * 2f) * step
                    },
                )
            }
            Column(Modifier.weight(1f).padding(horizontal = 12.dp)) {
                PixelText(tr("Entre no nosso Discord"), size = Ts.Item, color = Bl.Text)
                PixelText(tr("Novidades, ajuda com mods e a comunidade do Bunny Loader."),
                    size = Ts.Small, color = Bl.DiscordText)
            }
            Row(
                Modifier.pixelPanel(fill = Bl.DiscordDeep)
                    .padding(start = 10.dp, end = 6.dp, top = 6.dp, bottom = 6.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                PixelText(tr("Entrar"), size = Ts.Small, color = Bl.PressedText)
                PixelIcon(R.drawable.ic_seta_esq, 14.dp, Modifier.padding(start = 4.dp).rotate(180f))
            }
        }
    }
}
