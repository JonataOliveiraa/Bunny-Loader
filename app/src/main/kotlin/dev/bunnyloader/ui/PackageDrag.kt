package dev.bunnyloader.ui

import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.snap
import androidx.compose.animation.core.tween
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.foundation.gestures.scrollBy
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListItemInfo
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
import androidx.compose.ui.zIndex
import androidx.compose.ui.unit.dp
import kotlin.math.roundToInt

/** Quanto o cartão leva para chegar ao lugar novo. */
internal const val MOVE_MS = 420

/**
 * A lista começa com um item vazio, de 1 dp. A LazyColumn segura a
 * rolagem pela chave do primeiro item visível: sem ele, ligar o cartão do topo
 * levava a tela junto com o cartão. Fixar a rolagem com `requestScrollToItem`
 * resolvia isso, mas também cancela as animações de posição.
 */
private const val LIST_HEAD = 1
private const val HEAD_KEY = "list-head"

/** O índice em `uids` de um item visível (o item vazio do topo dá -1). */
private val LazyListItemInfo.uidIndex get() = index - LIST_HEAD

/** O item do topo é a âncora, a não ser que a lista esteja rolada para baixo dele. */
internal fun LazyListState.anchoredAt(uid: String): Boolean =
    layoutInfo.visibleItemsInfo.firstOrNull { it.index == firstVisibleItemIndex }?.key == uid

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
    val enabledUids: Set<String>, val allowedIndices: Set<Int>,
    val moved: Boolean = false,
)

private class PackageDragState(private val list: LazyListState) {
    var current by mutableStateOf<PackageDrag?>(null)
        private set

    fun start(y: Float, uids: List<String>, enabledUids: Set<String>): Boolean {
        val info = list.layoutInfo
        val contentY = y + info.viewportStartOffset
        val item = info.visibleItemsInfo.firstOrNull { contentY >= it.offset && contentY < it.offset + it.size }
            ?: return false
        val uid = item.key as? String ?: return false
        if (uids.getOrNull(item.uidIndex) != uid) return false
        val allowed = uids.indices.filter { (uids[it] in enabledUids) == (uid in enabledUids) }.toSet()
        current = PackageDrag(uid, item.uidIndex, item.size, (item.offset - info.viewportStartOffset).toFloat(),
            item.uidIndex, uids, enabledUids, allowed)
        return true
    }

    fun move(delta: Float) {
        current = current?.let { it.copy(top = it.top + delta, moved = it.moved || delta != 0f) }
        updateTarget()
    }

    fun updateTarget() {
        val drag = current ?: return
        val info = list.layoutInfo
        val slots = info.visibleItemsInfo.filter { it.uidIndex >= 0 }
            .map { PackageDragSlot(it.uidIndex, it.offset - info.viewportStartOffset, it.size) }
        if (slots.none { it.index in drag.allowedIndices }) return
        current = drag.copy(to = packageDropIndex(drag.from, drag.top + drag.height / 2f, slots, drag.allowedIndices))
    }

    fun scrollDelta(edge: Float, speed: Float): Float {
        val drag = current ?: return 0f
        if (!drag.moved) return 0f
        val info = list.layoutInfo
        val top = info.beforeContentPadding
        val bottom = info.viewportEndOffset - info.viewportStartOffset - info.afterContentPadding
        val first = info.visibleItemsInfo.firstOrNull { it.uidIndex >= 0 }
        val last = info.visibleItemsInfo.lastOrNull { it.uidIndex >= 0 }
        val firstAllowed = drag.allowedIndices.minOrNull() ?: return 0f
        val lastAllowed = drag.allowedIndices.maxOrNull() ?: return 0f
        val moreAbove = first != null && (first.uidIndex > firstAllowed ||
            first.uidIndex == firstAllowed && first.offset - info.viewportStartOffset < top)
        val moreBelow = last != null && (last.uidIndex < lastAllowed ||
            last.uidIndex == lastAllowed && last.offset - info.viewportStartOffset + last.size > bottom)
        // O cartão pode ocupar as duas bordas; o centro escolhe a direção da rolagem.
        val center = drag.top + drag.height / 2f
        return when {
            center < top + edge && moreAbove && list.canScrollBackward -> -speed * ((top + edge - center) / edge).coerceIn(0f, 1f)
            center > bottom - edge && moreBelow && list.canScrollForward -> speed * ((center - bottom + edge) / edge).coerceIn(0f, 1f)
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

/**
 * A prévia só muda a tela; o repositório recebe um movimento ao soltar o dedo.
 *
 * Com `animateMoves`, o cartão que muda de lugar (ligado, desligado, uma
 * seta) desliza até a posição nova. Fica desligado no arraste: ao soltar, os
 * vizinhos já estão no lugar pela prévia, e a animação os puxaria de volta à
 * posição antiga para deslizar de novo.
 */
@Composable
internal fun DraggablePackageList(
    state: LazyListState,
    uids: List<String>,
    enabledUids: Set<String>,
    reorderEnabled: Boolean,
    onDrop: (String, String) -> Unit,
    animateMoves: Boolean = false,
    raisedUid: String? = null,
    modifier: Modifier = Modifier,
    empty: @Composable () -> Unit,
    row: @Composable (Int, Modifier, Boolean) -> Unit,
) {
    val drag = remember(state) { PackageDragState(state) }
    val latestUids by rememberUpdatedState(uids)
    val latestEnabled by rememberUpdatedState(enabledUids)
    val latestDrop by rememberUpdatedState(onDrop)
    val active = drag.current?.takeIf { reorderEnabled && it.uids == uids && it.enabledUids == enabledUids }
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
    LaunchedEffect(uids, enabledUids, reorderEnabled) { drag.cancel() }
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
    PauseListAnimations({ state.isScrollInProgress || drag.current != null }) {
        Box(modifier.clipToBounds()) {
            LazyColumn(
                state = state,
                modifier = Modifier.fillMaxSize().then(if (reorderEnabled) Modifier.pointerInput(drag) {
                    detectDragGesturesAfterLongPress(
                        onDragStart = { if (drag.start(it.y, latestUids, latestEnabled)) vibrateDragStart(vibrator) },
                        onDrag = { change, amount -> if (drag.current != null) { change.consume(); drag.move(amount.y) } },
                        onDragEnd = {
                            if (drag.current?.uids == latestUids && drag.current?.enabledUids == latestEnabled) {
                                drag.finish()?.let { (uid, target) -> latestDrop(uid, target) }
                            } else drag.cancel()
                        },
                        onDragCancel = { drag.cancel() },
                    )
                } else Modifier),
                // O espaço de cima soma o item vazio e o espaçamento depois dele.
                contentPadding = PaddingValues(start = EdgePad, end = EdgePad, top = 1.dp, bottom = 16.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
                userScrollEnabled = active == null,
            ) {
                item(key = HEAD_KEY, contentType = "head") { Spacer(Modifier.fillMaxWidth().height(1.dp)) }
                itemsIndexed(uids, key = { _, uid -> uid }, contentType = { _, _ -> "package" }) { index, uid ->
                    val moving = active
                    // Sempre presa ao cartão, sem fade e, fora dos toques, instantânea
                    // (snap, não null): sem uma animação valendo, a lista não guarda
                    // onde cada cartão estava, e no toque seguinte todos entravam
                    // de baixo da tela como se fossem novos.
                    val place = Modifier.animateItem(
                        fadeInSpec = null,
                        placementSpec = if (animateMoves && moving == null) tween(MOVE_MS, easing = FastOutSlowInEasing) else snap(),
                        fadeOutSpec = null,
                    )
                    // O cartão tocado passa por cima dos vizinhos, não por baixo.
                    row(index, place.zIndex(if (animateMoves && uid == raisedUid) 1f else 0f).graphicsLayer {
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
}
