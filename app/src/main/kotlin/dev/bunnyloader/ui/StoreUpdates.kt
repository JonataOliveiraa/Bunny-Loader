package dev.bunnyloader.ui

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import dev.bunnyloader.R

@Composable
fun StoreLoadingIcon(modifier: Modifier = Modifier) {
    val animation = rememberInfiniteTransition(label = "carregamento da loja")
    val angle by animation.animateFloat(0f, 360f,
        infiniteRepeatable(tween(1000, easing = LinearEasing), RepeatMode.Restart),
        label = "giro")
    PixelIcon(R.drawable.ic_refresh, 24.dp,
        modifier.rotate(angle).semantics { contentDescription = "Carregando mods" })
}

@Composable
fun ModUpdatesDialog(shell: Shell) {
    val notices = shell.updateNotices
    if (notices.isEmpty()) return

    val lifecycle = LocalLifecycleOwner.current.lifecycle
    DisposableEffect(notices, lifecycle) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) shell.markUpdateNoticesShown()
        }
        lifecycle.addObserver(observer)
        if (lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) shell.markUpdateNoticesShown()
        onDispose { lifecycle.removeObserver(observer) }
    }

    Dialog(onDismissRequest = shell::dismissUpdateNotices) {
        Column(Modifier.widthIn(max = 480.dp).fillMaxWidth().heightIn(max = 600.dp).pixelPanel()
            .padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            PixelText("Atualizações de mods", size = Ts.Head)
            PixelText("Há versões novas dos seus mods instalados.", size = Ts.Body, color = Bl.TextDim)
            LazyColumn(Modifier.fillMaxWidth().weight(1f, fill = false).heightIn(max = 300.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(notices, key = { it.uid }) { entry ->
                    Column(Modifier.fillMaxWidth().pixelPanel(fill = Bl.Select, raised = false)
                        .padding(10.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        PixelText(entry.manifest.name, size = Ts.Item)
                        PixelText("Instalado: v${shell.installedVersion(entry.uid)} · Novo: v${entry.manifest.version}",
                            size = Ts.Small, color = Bl.TextDim)
                        val progress = shell.downloads[entry.uid]
                        if (progress != null) {
                            Row(verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                StoreLoadingIcon()
                                PixelText("Baixando ${(progress * 100).toInt()}%", size = Ts.Small)
                            }
                        } else {
                            PixelButton("Atualizar", { shell.installRemote(entry) },
                                Modifier.fillMaxWidth(), fontSize = Ts.Body, shadow = false)
                        }
                        shell.downloadErrors[entry.uid]?.let {
                            PixelText("Não deu: $it", size = Ts.Small, color = Bl.Bad)
                        }
                    }
                }
            }
            if (notices.size > 1 && notices.any { it.uid !in shell.downloads }) {
                PixelButton("Atualizar todos", shell::updateAllNotices,
                    Modifier.fillMaxWidth(), fontSize = Ts.Body, shadow = false)
            }
            PixelButton(if (notices.any { it.uid in shell.downloads }) "Continuar em segundo plano" else "Agora não",
                shell::dismissUpdateNotices, Modifier.fillMaxWidth(), fontSize = Ts.Body, shadow = false)
        }
    }
}
