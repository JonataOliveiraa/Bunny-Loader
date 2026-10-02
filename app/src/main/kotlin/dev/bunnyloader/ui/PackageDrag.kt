package dev.bunnyloader.ui

import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.foundation.gestures.scrollBy
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import kotlin.math.roundToInt

@Suppress("DEPRECATION")
private fun vibrateDragStart(vibrator: Vibrator?) {
    if (vibrator?.hasVibrator() != true) return
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
        vibrator.vibrate(VibrationEffect.createOneShot(40L, VibrationEffect.DEFAULT_AMPLITUDE))
    } else {
        vibrator.vibrate(40L)
    }
}

private data class PackageDrag(
    val uid: String, val from: Int, val height: Int,
    val top: Float, val to: Int, val uids: List<String>,
    val moved: Boolean = false,
)

private class PackageDragState(private val list: LazyListState) {
    var current by mutableStateOf<PackageDrag?>(null)
        private set

    fun start(y: Float, uids: List<String>): Boolean {
        val info = list.layoutInfo
        val contentY = y + info.viewportStartOffset
        val item = info.visibleItemsInfo.firstOrNull { contentY >= it.offset && contentY < it.offset + it.size }
            ?: return false
        val uid = item.key as? String ?: return false
        if (uids.getOrNull(item.index) != uid) return false
        current = PackageDrag(uid, item.index, item.size, (item.offset - info.viewportStartOffset).toFloat(), item.index, uids)
        return true
    }

    fun move(delta: Float) {
        current = current?.let { it.copy(top = it.top + delta, moved = it.moved || delta != 0f) }
        updateTarget()
    }

    fun updateTarget() {
        val drag = current ?: return
        val info = list.layoutInfo
        val slots = info.visibleItemsInfo.map { PackageDragSlot(it.index, it.offset - info.viewportStartOffset, it.size) }
        current = drag.copy(to = packageDropIndex(drag.from, drag.top + drag.height / 2f, slots))
    }

    fun scrollDelta(edge: Float, speed: Float): Float {
        val drag = current ?: return 0f
        if (!drag.moved) return 0f
        val info = list.layoutInfo
        val top = info.beforeContentPadding
        val bottom = info.viewportEndOffset - info.viewportStartOffset - info.afterContentPadding
        // O cartão pode ocupar as duas bordas; o centro escolhe a direção da rolagem.
        val center = drag.top + drag.height / 2f
        return when {
            center < top + edge && list.canScrollBackward -> -speed * ((top + edge - center) / edge).coerceIn(0f, 1f)
            center > bottom - edge && list.canScrollForward -> speed * ((center - bottom + edge) / edge).coerceIn(0f, 1f)
            else -> 0f
        }
    }

    fun finish(): Pair<String, String>? {
        val drag = current
        current = null
        return drag?.takeIf { it.to != it.from }?.let { it.uid to it.uids[it.to] }
    }

    fun cancel() { current = null }
}

/** A prévia só muda a tela; o repositório recebe um movimento ao soltar o dedo. */
@Composable
internal fun DraggablePackageList(
    state: LazyListState,
    uids: List<String>,
    reorderEnabled: Boolean,
    onDrop: (String, String) -> Unit,
    modifier: Modifier = Modifier,
    empty: @Composable () -> Unit,
    row: @Composable (Int, Modifier, Boolean) -> Unit,
) {
    val drag = remember(state) { PackageDragState(state) }
    val latestUids by rememberUpdatedState(uids)
    val latestDrop by rememberUpdatedState(onDrop)
    val active = drag.current?.takeIf { reorderEnabled && it.uids == uids }
    val context = LocalContext.current
    val vibrator = remember(context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            context.getSystemService(VibratorManager::class.java)?.defaultVibrator
        } else {
            context.getSystemService(Vibrator::class.java)
        }
    }
    val gap = with(LocalDensity.current) { 8.dp.toPx() }
    val edge = with(LocalDensity.current) { 48.dp.toPx() }
    val speed = with(LocalDensity.current) { 12.dp.toPx() }
    LaunchedEffect(uids, reorderEnabled) { drag.cancel() }
    LaunchedEffect(drag.current?.uid) {
        while (drag.current != null) {
            withFrameNanos { }
            val delta = drag.scrollDelta(edge, speed)
            if (delta != 0f) {
                state.scrollBy(delta)
                drag.updateTarget()
            }
        }
    }
    Box(modifier.clipToBounds()) {
        LazyColumn(
            state = state,
            modifier = Modifier.fillMaxSize().then(if (reorderEnabled) Modifier.pointerInput(drag) {
                detectDragGesturesAfterLongPress(
                    onDragStart = { if (drag.start(it.y, latestUids)) vibrateDragStart(vibrator) },
                    onDrag = { change, amount -> if (drag.current != null) { change.consume(); drag.move(amount.y) } },
                    onDragEnd = { drag.finish()?.let { (uid, target) -> latestDrop(uid, target) } },
                    onDragCancel = { drag.cancel() },
                )
            } else Modifier),
            contentPadding = PaddingValues(start = EdgePad, end = EdgePad, top = 10.dp, bottom = 16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
            userScrollEnabled = active == null,
        ) {
            itemsIndexed(uids, key = { _, uid -> uid }) { index, uid ->
                val moving = active
                row(index, Modifier.graphicsLayer {
                    alpha = if (uid == moving?.uid) 0f else 1f
                    translationY = moving?.let { packageDragShift(index, it.from, it.to, it.height + gap) } ?: 0f
                }, false)
            }
            if (uids.isEmpty()) item { empty() }
        }
        PixelScrollbar(state, Modifier.fillMaxSize())
        active?.let { moving ->
            Box(Modifier.offset { IntOffset(0, moving.top.roundToInt()) }.padding(horizontal = EdgePad)) {
                row(moving.from, Modifier.drawWithContent {
                    drawContent()
                    val border = 2.dp.toPx()
                    drawRect(Bl.DragBorder, Offset(border / 2, border / 2),
                        Size(size.width - border, size.height - border), style = Stroke(border))
                }, true)
            }
        }
    }
}
