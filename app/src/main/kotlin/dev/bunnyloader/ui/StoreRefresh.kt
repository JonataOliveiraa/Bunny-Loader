package dev.bunnyloader.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.pulltorefresh.pullToRefresh
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.semantics.CustomAccessibilityAction
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.customActions
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R

/** O arraste só começa depois que o conteúdo chega ao topo da rolagem. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun StorePullToRefresh(
    shell: Shell,
    modifier: Modifier = Modifier,
    content: @Composable BoxScope.() -> Unit,
) {
    val state = rememberPullToRefreshState()
    val loading = shell.remoteStatus == RemoteStatus.Loading
    val refresh = { shell.refreshRemote(force = true) }

    Box(
        modifier.clipToBounds()
            .pullToRefresh(
                state = state,
                isRefreshing = loading,
                enabled = !loading,
                onRefresh = refresh,
            )
            .semantics {
                customActions = if (loading) emptyList() else listOf(
                    CustomAccessibilityAction("Atualizar loja") {
                        refresh()
                        true
                    },
                )
            },
    ) {
        content()

        val distance = state.distanceFraction

        if (distance > 0f || loading) {
            Row(
                Modifier.align(Alignment.TopCenter).padding(top = 8.dp)
                    .graphicsLayer {
                        alpha = distance.coerceIn(0f, 1f)
                        translationY = (distance.coerceIn(0f, 1f) - 1f) * size.height
                    }
                    .pixelShadow(2.dp, 3.dp).pixelPanel(fill = Bl.Select)
                    .padding(horizontal = 12.dp, vertical = 8.dp)
                    .semantics { liveRegion = LiveRegionMode.Polite },
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                if (loading) {
                    StoreLoadingIcon()
                } else {
                    PixelIcon(R.drawable.ic_refresh, 24.dp,
                        Modifier.rotate(distance.coerceIn(0f, 1f) * 180f))
                }

                val progress = shell.remoteProgress

                PixelText(
                    when {
                        loading && progress != null && progress.total > 0 ->
                            "Atualizando loja: ${progress.completed}/${progress.total}"
                        loading -> "Atualizando loja..."
                        distance >= 1f -> "Solte para atualizar"
                        else -> "Puxe para atualizar"
                    },
                    size = Ts.Small, color = Bl.Text,
                )
            }
        }
    }
}
