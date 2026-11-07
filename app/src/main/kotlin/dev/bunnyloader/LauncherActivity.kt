package dev.bunnyloader

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.saveable.rememberSaveableStateHolder
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
import androidx.compose.foundation.layout.safeDrawingPadding
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
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import dev.bunnyloader.game.Eligibility
import dev.bunnyloader.ui.AppSplash
import dev.bunnyloader.ui.Bl
import dev.bunnyloader.ui.ExplorarTab
import dev.bunnyloader.ui.InicioTab
import dev.bunnyloader.ui.ModBackdrop
import dev.bunnyloader.ui.ModDetail
import dev.bunnyloader.ui.ModUpdatesDialog
import dev.bunnyloader.ui.GameBlockedDialog
import dev.bunnyloader.ui.LanguageConfirmDialog
import dev.bunnyloader.ui.PacotesTab
import dev.bunnyloader.ui.ConfigTab
import dev.bunnyloader.ui.PixelFont
import dev.bunnyloader.ui.PixelIcon
import dev.bunnyloader.ui.PixelCorner
import dev.bunnyloader.ui.pixelPanel
import dev.bunnyloader.ui.UiLocalization
import dev.bunnyloader.ui.tr
import dev.bunnyloader.ui.PixelText
import dev.bunnyloader.ui.mix
import dev.bunnyloader.ui.Shell
import dev.bunnyloader.ui.Ts
import dev.bunnyloader.ui.Prefs
import dev.bunnyloader.ui.LocalRemoteFiles
import dev.bunnyloader.ui.LocalRemoteMedia

/**
 * Launcher do Bunny Loader.
 *
 * Quatro abas num rodapé de terra, com o botão de jogar erguido no meio: a
 * navegação fica nas bordas e o centro é a única ação que importa.
 */
class LauncherActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        UiLocalization.initialize(this)
        val model = ViewModelProvider(this, object : ViewModelProvider.Factory {
            override fun <T : ViewModel> create(modelClass: Class<T>): T {
                require(modelClass == LauncherState::class.java)
                return modelClass.cast(LauncherState(Shell(applicationContext)))
            }
        })[LauncherState::class.java]
        window.setBackgroundDrawableResource(R.color.splash_background)
        setContent {
            var splash by rememberSaveable { mutableStateOf(true) }
            Box(Modifier.fillMaxSize().safeDrawingPadding()) {
                LauncherScreen(model.shell, showDialogs = !splash)
                if (splash) AppSplash({ !model.shell.refreshingPackages }) { splash = false }
            }
        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) hideNavigationBar()
    }

    private fun hideNavigationBar() {
        WindowCompat.getInsetsController(window, window.decorView).apply {
            systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
            hide(WindowInsetsCompat.Type.navigationBars())
        }
    }
}

/** Mantém downloads, importações e a mesma trava durante recriações da Activity. */
private class LauncherState(val shell: Shell) : ViewModel() {
    override fun onCleared() { shell.close() }
}

private enum class Tab(val icon: Int, val label: String) {
    INICIO(R.drawable.ic_tab_inicio, "Home"),
    EXPLORAR(R.drawable.ic_tab_explorar, "Explore"),
    PACOTES(R.drawable.ic_tab_pacotes, "Packages"),
    CONFIG(R.drawable.ic_config, "Settings"),
}

@Composable
private fun LauncherScreen(shell: Shell, showDialogs: Boolean) {
    val ctx = LocalContext.current
    val prefs = remember { Prefs(ctx) }
    // Saveable: o seletor de arquivos (mundos) pode fazer o
    // sistema recriar a Activity, e a resposta dele só chega à aba que pediu
    // se ela for a mesma na volta.
    var tab by rememberSaveable { mutableStateOf(Tab.INICIO) }
    var blocked by remember { mutableStateOf<Eligibility.Status?>(null) }
    var askLanguage by remember { mutableStateOf(!prefs.launchedBefore) }
    var openMod by rememberSaveable { mutableStateOf<String?>(null) }
    val tabState = rememberSaveableStateHolder()

    BackHandler(enabled = openMod != null) { openMod = null }

    // Voltou do jogo ou do gerenciador de arquivos: relê bunny_packs, que pode
    // ter pacote novo ou editado à mão.
    val lifecycle = androidx.compose.ui.platform.LocalLifecycleOwner.current.lifecycle
    androidx.compose.runtime.DisposableEffect(lifecycle) {
        val observer = androidx.lifecycle.LifecycleEventObserver { _, event ->
            if (event == androidx.lifecycle.Lifecycle.Event.ON_RESUME) {
                shell.rescan()
                // Sem forçar: voltar do jogo dentro do intervalo não pede a
                // lista de novo (e, passado o intervalo, o ETag evita baixá-la).
                shell.refreshRemote()
            }
        }
        lifecycle.addObserver(observer)
        onDispose { lifecycle.removeObserver(observer) }
    }

    // Guardada: `shell::requestRemoteFiles` é um objeto novo a cada recomposição,
    // e um valor novo num staticCompositionLocal recompõe a tela inteira.
    val requestFiles = remember(shell) { shell::requestRemoteFiles }
    val mediaEntry = remember(shell) { shell::mediaEntry }
    androidx.compose.runtime.CompositionLocalProvider(
        LocalRemoteFiles provides requestFiles,
        LocalRemoteMedia provides mediaEntry,
    ) {
    Box(Modifier.fillMaxSize().background(Brush.verticalGradient(listOf(Bl.BackdropTop, Bl.BackdropBottom)))) {
        // A ficha aberta pode trazer o fundo do mod (theme.background).
        openMod?.let { shell.entry(it) }?.let { entry ->
            androidx.compose.runtime.key(entry.uid) { ModBackdrop(entry, shell.catalog, Modifier.fillMaxSize()) }
        }
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
                        tabState.SaveableStateProvider(tab.name) {
                            when (tab) {
                                Tab.INICIO -> InicioTab(shell) { openMod = it }
                                Tab.EXPLORAR -> ExplorarTab(shell) { openMod = it }
                                Tab.PACOTES -> PacotesTab(shell) { openMod = it }
                                Tab.CONFIG -> ConfigTab(shell)
                            }
                        }
                    }
                }
            }
            NavBar(
                current = tab,
                onSelect = { tab = it; openMod = null },
                onStart = {
                    val status = Eligibility.check(ctx)
                    if (status == Eligibility.Status.OK) ctx.startActivity(dev.bunnyloader.compile.CompileActivity.gameIntent(ctx))
                    else blocked = status
                },
            )
        }
        if (showDialogs) ModUpdatesDialog(shell)
        blocked?.let { GameBlockedDialog(it) { blocked = null } }
        if (showDialogs && askLanguage) LanguageConfirmDialog {
            prefs.launchedBefore = true
            askLanguage = false
        }
    }
    }
}

// ------------------------------- rodapé -------------------------------

/**
 * A barra de baixo: cada aba é um slot com canto em degrau, a aba atual é o slot
 * dourado e sobe um pouco. O botão de jogar é um disco em pixel de arte erguido
 * no meio.
 */
@Composable
private fun NavBar(current: Tab, onSelect: (Tab) -> Unit, onStart: () -> Unit) {
    Box(Modifier.fillMaxWidth().height(112.dp)) {
        Row(
            Modifier.align(Alignment.BottomCenter)
                .fillMaxWidth()
                .height(88.dp)
                .drawBehind {
                    val u = 2.dp.toPx()
                    val bar = Bl.GamePanel
                    // Contorno, uma linha de luz e o corpo; embaixo, um tom mais
                    // fundo dá peso à barra sem sombra projetada.
                    drawRect(Bl.Outline, size = Size(size.width, u * 2))
                    drawRect(bar, Offset(0f, u * 2), Size(size.width, size.height))
                    drawRect(bar.mix(Color.White, 0.18f), Offset(0f, u * 2), Size(size.width, u))
                    drawRect(bar.mix(Bl.Night, 0.22f), Offset(0f, size.height * 0.62f),
                        Size(size.width, size.height * 0.38f))
                    drawRect(bar.mix(Bl.Night, 0.12f), Offset(0f, size.height * 0.62f - u),
                        Size(size.width, u))
                }
                .navigationBarsPadding(),
            horizontalArrangement = Arrangement.SpaceEvenly,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            TabButton(Tab.INICIO, current, onSelect)
            TabButton(Tab.EXPLORAR, current, onSelect)
            Spacer(Modifier.width(92.dp))
            TabButton(Tab.PACOTES, current, onSelect)
            TabButton(Tab.CONFIG, current, onSelect)
        }
        PlayButton(onStart, Modifier.align(Alignment.TopCenter))
    }
}

/**
 * O disco de jogar, desenhado pixel a pixel (43 de lado, 2 dp cada): contorno,
 * aro com luz em cima e sombra embaixo, miolo na cor de título e um
 * brilho curto no alto. No toque, o contorno fica amarelo e o miolo clareia.
 */
@Composable
private fun PlayButton(onStart: () -> Unit, modifier: Modifier = Modifier) {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    val fill = if (pressed) Bl.ButtonPressed else Bl.TitleFill
    val disk = remember(fill, pressed) {
        dev.bunnyloader.ui.pixelDisk(
            43, outline = if (pressed) Bl.PressedBorder else Bl.Outline, fill = fill,
            light = fill.mix(Color.White, 0.3f), dark = fill.mix(Bl.Night, 0.45f),
        )
    }
    Box(
        modifier.size(86.dp)
            .clip(CircleShape)
            .clickable(interaction, indication = null, onClick = onStart),
        contentAlignment = Alignment.Center,
    ) {
        androidx.compose.foundation.Image(disk, tr("Play"), Modifier.size(86.dp),
            filterQuality = androidx.compose.ui.graphics.FilterQuality.None)
        // Centro ótico, não geométrico: o triângulo tem a massa na base
        // (à esquerda) e a arte traz a sombra embaixo à direita.
        PixelIcon(R.drawable.ic_start, 44.dp, Modifier.offset(x = 3.dp, y = if (pressed) 3.dp else 1.dp))
    }
}

@Composable
private fun TabButton(tab: Tab, current: Tab, onSelect: (Tab) -> Unit) {
    val selected = tab == current
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    val lift by animateFloatAsState(if (selected) -5f else 0f, tween(180), label = "slot")
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier.clickable(interaction, indication = null) { onSelect(tab) }
            .padding(horizontal = 2.dp),
    ) {
        Box(
            Modifier.offset(y = lift.dp).size(54.dp, 46.dp)
                .pixelPanel(
                    fill = if (selected) Bl.SlotGold else Bl.Select,
                    outline = when {
                        pressed -> Bl.PressedBorder
                        selected -> Bl.SlotGoldOutline
                        else -> Bl.Outline
                    },
                    corner = PixelCorner.LARGE,
                ),
            contentAlignment = Alignment.Center,
        ) {
            PixelIcon(tab.icon, if (selected) 32.dp else 28.dp, alpha = if (selected || pressed) 1f else 0.85f)
        }
        PixelText(
            tr(tab.label),
            size = Ts.Small,
            color = if (selected || pressed) Bl.PressedText else Bl.TextDim,
            modifier = Modifier.padding(top = 3.dp),
        )
    }
}
