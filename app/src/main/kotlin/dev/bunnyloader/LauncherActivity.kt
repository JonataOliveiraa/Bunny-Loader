package dev.bunnyloader

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.bunnyloader.ui.Bl
import dev.bunnyloader.ui.ExplorarTab
import dev.bunnyloader.ui.InicioTab
import dev.bunnyloader.ui.ModDetail
import dev.bunnyloader.ui.PacotesTab
import dev.bunnyloader.ui.PerfilTab
import dev.bunnyloader.ui.PixelFont
import dev.bunnyloader.ui.PixelIcon
import dev.bunnyloader.ui.Shell
import dev.bunnyloader.ui.Ts
import dev.bunnyloader.ui.drawTileGround
import dev.bunnyloader.ui.drawTileWall
import dev.bunnyloader.ui.pixelShadow

/**
 * Launcher do Bunny Loader.
 *
 * Quatro abas num rodapé de terra, com o botão de jogar erguido no meio: a
 * navegação fica nas bordas e o centro é a única ação que importa.
 */
class LauncherActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { LauncherScreen() }
    }
}

private enum class Tab(val icon: Int, val label: String) {
    INICIO(R.drawable.ic_tab_inicio, "Início"),
    EXPLORAR(R.drawable.ic_tab_explorar, "Explorar"),
    PACOTES(R.drawable.ic_tab_pacotes, "Pacotes"),
    // User.png e azul-escuro: sobre a terra do rodape ele some. O coelho e
    // claro, e de todo jeito e o mascote do app.
    PERFIL(R.drawable.ic_bunny_head, "Perfil"),
}

@Composable
private fun LauncherScreen() {
    val ctx = LocalContext.current
    val shell = remember { Shell(ctx) }
    var tab by remember { mutableStateOf(Tab.INICIO) }
    var openMod by remember { mutableStateOf<String?>(null) }

    BackHandler(enabled = openMod != null) { openMod = null }

    val stone = ImageBitmap.imageResource(R.drawable.tile_stone)

    Column(
        Modifier.fillMaxSize()
            .drawBehind {
                // Parede de pedra, bem escurecida. Um fundo liso deixava os
                // cartões boiando: sem textura atrás, sombra não tem em que
                // cair e a tela inteira parece um esboço.
                drawTileWall(stone, 20.dp.toPx(), Bl.Night.copy(alpha = 0.78f))
            }
            .statusBarsPadding(),
    ) {
        // Teto de largura: o layout foi pensado para um celular em pé. Solto num
        // tablet ou no emulador deitado, uma linha de mod com 1600px vira uma
        // faixa vazia com um ícone perdido na esquerda.
        Box(Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.TopCenter) {
            Box(Modifier.widthIn(max = 560.dp).fillMaxSize()) {
                val detail = openMod?.let { shell.entry(it) }
                if (detail != null) {
                    ModDetail(detail, shell) { openMod = null }
                } else {
                    when (tab) {
                        Tab.INICIO -> InicioTab(shell) { openMod = it }
                        Tab.EXPLORAR -> ExplorarTab(shell) { openMod = it }
                        Tab.PACOTES -> PacotesTab(shell) { openMod = it }
                        Tab.PERFIL -> PerfilTab(shell)
                    }
                }
            }
        }
        NavBar(
            current = tab,
            onSelect = { tab = it; openMod = null },
            onStart = { ctx.startActivity(Intent(ctx, GameActivity::class.java)) },
        )
    }
}

// ------------------------------- rodapé -------------------------------

/**
 * O rodapé é o CHÃO: terra repetida com uma linha de grama em cima. O botão de
 * jogar fica plantado nele, meio para fora — é o que dá a hierarquia da tela
 * sem precisar de nenhum rótulo dizendo qual é o botão principal.
 */
@Composable
private fun NavBar(current: Tab, onSelect: (Tab) -> Unit, onStart: () -> Unit) {
    val grass = ImageBitmap.imageResource(R.drawable.tile_grass)
    Box(Modifier.fillMaxWidth().height(96.dp)) {
        Row(
            Modifier.align(Alignment.BottomCenter)
                .fillMaxWidth()
                .height(76.dp)
                .drawBehind {
                    // O rodapé é chão de verdade: os tiles de grama do próprio
                    // jogo, escurecidos o bastante para o texto ficar legível.
                    drawTileGround(grass, 0f, 20.dp.toPx())
                    drawRect(Bl.Night.copy(alpha = 0.52f), Offset(0f, 20.dp.toPx()),
                        Size(size.width, size.height - 20.dp.toPx()))
                }
                .navigationBarsPadding(),
            horizontalArrangement = Arrangement.SpaceEvenly,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            TabButton(Tab.INICIO, current, onSelect)
            TabButton(Tab.EXPLORAR, current, onSelect)
            Spacer(Modifier.width(72.dp))
            TabButton(Tab.PACOTES, current, onSelect)
            TabButton(Tab.PERFIL, current, onSelect)
        }

        // Botão de jogar, erguido acima da linha da grama.
        Box(
            Modifier.align(Alignment.TopCenter)
                .padding(bottom = 8.dp)
                .size(76.dp)
                .drawBehind {
                    // Sombra redonda antes do botao, para ele pousar no chao
                    // em vez de flutuar sobre a grama.
                    drawCircle(Bl.Shadow, center = center.copy(
                        x = center.x + 3.dp.toPx(), y = center.y + 5.dp.toPx()))
                    drawCircle(Bl.Outline)
                    drawCircle(
                        Brush.verticalGradient(listOf(Bl.Grass4, Bl.Grass1)),
                        radius = size.minDimension / 2 - 3.dp.toPx(),
                    )
                }
                .clip(CircleShape)
                .clickable(onClick = onStart),
            contentAlignment = Alignment.Center,
        ) {
            PixelIcon(R.drawable.ic_start, 38.dp)
        }
    }
}

@Composable
private fun TabButton(tab: Tab, current: Tab, onSelect: (Tab) -> Unit) {
    val selected = tab == current
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier.clickable { onSelect(tab) }.padding(horizontal = 6.dp),
    ) {
        PixelIcon(tab.icon, if (selected) 30.dp else 26.dp, alpha = if (selected) 1f else 0.8f)
        Text(
            tab.label,
            fontFamily = PixelFont,
            fontSize = Ts.Small.sp,
            color = if (selected) Bl.Grass4 else Bl.Stone4.copy(alpha = 0.85f),
            modifier = Modifier.padding(top = 3.dp),
        )
    }
}
