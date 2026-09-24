package dev.bunnyloader

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
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
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
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
import dev.bunnyloader.ui.ConfigTab
import dev.bunnyloader.ui.PixelFont
import dev.bunnyloader.ui.PixelIcon
import dev.bunnyloader.ui.PixelText
import dev.bunnyloader.ui.mix
import dev.bunnyloader.ui.Shell
import dev.bunnyloader.ui.Ts
import dev.bunnyloader.ui.Biome
import dev.bunnyloader.ui.Prefs
import dev.bunnyloader.ui.Scenery

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
    CONFIG(R.drawable.ic_config, "Config"),
}

@Composable
private fun LauncherScreen() {
    val ctx = LocalContext.current
    val shell = remember { Shell(ctx) }
    val prefs = remember { Prefs(ctx) }
    var tab by remember { mutableStateOf(Tab.INICIO) }
    var openMod by remember { mutableStateOf<String?>(null) }
    var sceneryChoice by remember { mutableStateOf(prefs.scenery) }
    // Saveable: girar a tela recria a Activity, e no automático isso trocaria
    // o cenário no meio do uso.
    val biome = Biome.entries[rememberSaveable(sceneryChoice) {
        prefs.resolveScenery(sceneryChoice).ordinal
    }]

    BackHandler(enabled = openMod != null) { openMod = null }

    // Voltou do jogo ou do gerenciador de arquivos: relê bunny_packs, que pode
    // ter pacote novo ou editado à mão.
    val lifecycle = androidx.compose.ui.platform.LocalLifecycleOwner.current.lifecycle
    androidx.compose.runtime.DisposableEffect(lifecycle) {
        val observer = androidx.lifecycle.LifecycleEventObserver { _, event ->
            if (event == androidx.lifecycle.Lifecycle.Event.ON_RESUME) shell.rescan()
        }
        lifecycle.addObserver(observer)
        onDispose { lifecycle.removeObserver(observer) }
    }

    // A câmera do cenário dá um passo por aba (e meio ao abrir um mod): é o
    // que faz as camadas deslizarem cada uma no seu ritmo.
    val pan by animateFloatAsState(
        tab.ordinal * 240f + (if (openMod != null) 120f else 0f),
        tween(1400, easing = FastOutSlowInEasing), label = "camera",
    )

    Box(Modifier.fillMaxSize()) {
        Scenery(biome, { pan }, Modifier.fillMaxSize())
        Column(Modifier.fillMaxSize().statusBarsPadding()) {
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
                            Tab.CONFIG -> ConfigTab(shell, sceneryChoice) {
                                sceneryChoice = it
                                prefs.scenery = it
                            }
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
}

// ------------------------------- rodapé -------------------------------

/**
 * Barra azul do Terraria, com o botão de jogar erguido no meio.
 *
 * Era chão de terra com um véu escuro por cima, e o véu roubava a cor: de longe
 * virava uma faixa preta translúcida. Agora é o painel do jogo — o mesmo
 * #3f5297 com contorno #131625 do menu de dentro.
 *
 * Reta e de cor chapada, de propósito: o degradê vertical dava um brilho de
 * plástico que destoa do resto, e o canto arredondado deixava dois buracos na
 * quina de baixo da tela, onde a barra encosta.
 */
@Composable
private fun NavBar(current: Tab, onSelect: (Tab) -> Unit, onStart: () -> Unit) {
    val startInteraction = remember { MutableInteractionSource() }
    val startPressed by startInteraction.collectIsPressedAsState()
    Box(Modifier.fillMaxWidth().height(106.dp)) {
        Row(
            Modifier.align(Alignment.BottomCenter)
                .fillMaxWidth()
                .height(84.dp)
                .drawBehind {
                    val b = 3.dp.toPx()
                    drawRect(Bl.Outline)
                    drawRect(
                        Bl.GamePanel,
                        topLeft = Offset(b, b),
                        size = Size(size.width - b * 2, size.height),
                    )
                }
                .navigationBarsPadding(),
            horizontalArrangement = Arrangement.SpaceEvenly,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            TabButton(Tab.INICIO, current, onSelect)
            TabButton(Tab.EXPLORAR, current, onSelect)
            Spacer(Modifier.width(84.dp))
            TabButton(Tab.PACOTES, current, onSelect)
            TabButton(Tab.CONFIG, current, onSelect)
        }

        Box(
            Modifier.align(Alignment.TopCenter)
                .padding(bottom = 8.dp)
                .size(86.dp)
                .drawBehind {
                    // Só o contorno fino. A sombra deslocada que ele tinha
                    // embaixo lia como uma borda grossa de um lado só.
                    // No toque, a borda é a amarela.
                    drawCircle(if (startPressed) Bl.PressedBorder else Bl.Outline)
                    drawCircle(
                        if (startPressed) Bl.ButtonPressed else Bl.TitleFill,
                        radius = size.minDimension / 2 - 2.dp.toPx(),
                    )
                }
                .clip(CircleShape)
                .clickable(startInteraction, indication = null, onClick = onStart),
            contentAlignment = Alignment.Center,
        ) {
            // Centro ótico, não geométrico: o triângulo tem a massa na base
            // (à esquerda) e a arte traz a sombra embaixo à direita. Centrado
            // pela caixa, ele parecia puxado para a esquerda e para cima.
            PixelIcon(R.drawable.ic_start, 44.dp, Modifier.offset(x = 3.dp, y = 1.dp))
        }
    }
}

@Composable
private fun TabButton(tab: Tab, current: Tab, onSelect: (Tab) -> Unit) {
    val selected = tab == current
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    // Botão sem caixa: quem acende no toque (e na aba atual) é o texto.
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier.clickable(interaction, indication = null) { onSelect(tab) }
            .padding(horizontal = 4.dp),
    ) {
        PixelIcon(tab.icon, if (selected) 38.dp else 32.dp, alpha = if (selected) 1f else 0.75f)
        PixelText(
            tab.label,
            size = Ts.Small,
            color = if (selected || pressed) Bl.PressedText else Color.White,
            modifier = Modifier.padding(top = 2.dp),
        )
    }
}
