package dev.bunnyloader.ui

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
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
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import dev.bunnyloader.R
import dev.bunnyloader.mods.Catalog

@Composable
fun StoreLoadingIcon(modifier: Modifier = Modifier) {
    val animation = rememberInfiniteTransition(label = "carregamento da loja")
    val angle by animation.animateFloat(0f, 360f,
        infiniteRepeatable(tween(1000, easing = LinearEasing), RepeatMode.Restart),
        label = "giro")
    PixelIcon(R.drawable.ic_refresh, 24.dp,
        modifier.graphicsLayer { rotationZ = angle }.semantics { contentDescription = tr("LoadingMods") })
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

    val pending = notices.filter { !shell.isUpdated(it) && it.uid !in shell.downloads }
    val busy = notices.any { it.uid in shell.downloads }
    val allDone = notices.all(shell::isUpdated)
    Dialog(onDismissRequest = shell::dismissUpdateNotices) {
        Column(Modifier.widthIn(max = 480.dp).fillMaxWidth().heightIn(max = 600.dp).pixelPanel(corner = PixelCorner.LARGE)
            .padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            PixelText(tr("ModUpdates"), size = Ts.Head)
            PixelText(if (allDone) tr("ModUpdatesAllDone")
                      else tr("ModUpdatesAvailable"),
                size = Ts.Body, color = Bl.TextDim)
            LazyColumn(Modifier.fillMaxWidth().weight(1f, fill = false).heightIn(max = 300.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(notices, key = { it.uid }) { entry -> UpdateNoticeRow(entry, shell) }
            }
            if (pending.size > 1) {
                PixelButton(tr("UpdateAllCount", pending.size), shell::updateAllNotices,
                    Modifier.fillMaxWidth(), fill = Bl.Good, fontSize = Ts.Body)
            }
            PixelButton(
                when {
                    allDone -> tr("Done")
                    busy -> tr("ContinueInBackground")
                    else -> tr("NotNow")
                },
                shell::dismissUpdateNotices, Modifier.fillMaxWidth(), fontSize = Ts.Body,
            )
        }
    }
}

/**
 * Uma linha da janela. O retorno é o próprio botão: vira a barra do download
 * e depois a marca de atualizado, sem sumir da lista.
 */
@Composable
private fun UpdateNoticeRow(entry: Catalog.Entry, shell: Shell) {
    val done = shell.isUpdated(entry)
    Column(Modifier.fillMaxWidth().pixelPanel(fill = if (done) Bl.EnabledPanel else Bl.Select, raised = false)
        .padding(10.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        PixelText(entry.manifest.nameFor(UiLocalization.language), size = Ts.Item)
        val progress = shell.downloads[entry.uid]
        when {
            done -> Row(verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                PixelTag(tr("Updated"), Bl.Good)
                PixelText("v${entry.manifest.version}", size = Ts.Small, color = Bl.TextDim)
            }
            progress != null -> {
                PixelText(tr("VersionChange", shell.installedVersion(entry.uid), entry.manifest.version),
                    size = Ts.Small, color = Bl.TextDim)
                Row(verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Box(Modifier.weight(1f).height(18.dp).pixelPanel(Bl.FrameFill, raised = false)) {
                        Box(Modifier.fillMaxWidth(progress.coerceIn(0f, 1f)).height(18.dp).padding(3.dp)
                            .background(Bl.Good))
                    }
                    PixelText(if (progress >= 1f) tr("Installing") else "${(progress * 100).toInt()}%",
                        size = Ts.Small)
                }
            }
            else -> {
                PixelText(tr("InstalledAndNewVersion", shell.installedVersion(entry.uid), entry.manifest.version),
                    size = Ts.Small, color = Bl.TextDim)
                PixelButton(if (entry.uid in shell.downloadErrors) tr("TryAgain") else tr("Update"),
                    { shell.installRemote(entry) }, Modifier.fillMaxWidth(), fontSize = Ts.Body)
            }
        }
        if (!done && progress == null) shell.downloadErrors[entry.uid]?.let {
            PixelText(tr("Failed", it), size = Ts.Small, color = Bl.Bad)
        }
    }
}
