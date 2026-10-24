package dev.bunnyloader.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.calculateCentroid
import androidx.compose.foundation.gestures.calculatePan
import androidx.compose.foundation.gestures.calculateZoom
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.input.pointer.positionChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import dev.bunnyloader.R
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.IconAnimation
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.math.max
import kotlin.math.min

/** Uma imagem do visualizador: o caminho que o Catalog lê e, se tiver, um rótulo ("Ícone", "Capa"). */
data class ViewerImage(val path: String, val label: String = "")

private const val MAX_ZOOM = 6f
private const val DOUBLE_TAP_ZOOM = 2.5f

/**
 * As imagens do pacote em tela cheia: desliza para a próxima, pinça e toque
 * duplo para o zoom, e a faixa de miniaturas embaixo para pular direto. Um
 * Dialog, e não uma camada da ficha: a ficha fica presa à largura do celular
 * e à barra de abas, e o voltar do sistema já fecha o Dialog.
 */
@Composable
fun PictureViewer(
    images: List<ViewerImage>,
    start: Int,
    catalog: Catalog,
    version: String,
    onDismiss: () -> Unit,
) {
    if (images.isEmpty()) return
    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false, decorFitsSystemWindows = false),
    ) {
        val pager = rememberPagerState(start.coerceIn(0, images.lastIndex)) { images.size }
        val scope = rememberCoroutineScope()
        // O zoom da página da frente: com ele ligado, o arraste move a imagem
        // em vez de trocar de página.
        var zoomed by remember { mutableStateOf(false) }
        LaunchedEffect(pager.currentPage) { zoomed = false }

        // A imagem fica entre a barra de cima e as miniaturas, sem nada por cima dela.
        Column(Modifier.fillMaxSize().background(Color.Black.copy(alpha = 0.94f)).safeDrawingPadding()) {
            Row(
                Modifier.fillMaxWidth().padding(10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                val current = images[pager.currentPage]
                PixelText(
                    if (current.label.isNotBlank()) current.label
                    else "${pager.currentPage + 1} / ${images.size}",
                    size = Ts.Item, color = Bl.Text,
                    modifier = Modifier.weight(1f).padding(start = 4.dp),
                )
                Box(
                    Modifier.size(42.dp).framePanel().pixelClickable(onDismiss),
                    contentAlignment = Alignment.Center,
                ) {
                    PixelIcon(R.drawable.ic_fechar, 22.dp)
                }
            }
            HorizontalPager(
                pager, Modifier.weight(1f).fillMaxWidth(), pageSpacing = 16.dp,
                userScrollEnabled = !zoomed,
                key = { images[it].path },
            ) { page ->
                ZoomablePicture(
                    catalog, images[page].path, version,
                    active = page == pager.currentPage,
                    onZoomChange = { if (page == pager.currentPage) zoomed = it },
                    onTapOutside = onDismiss,
                )
            }
            if (images.size > 1) {
                ViewerStrip(images, pager.currentPage, catalog, version) { index ->
                    scope.launch { pager.animateScrollToPage(index) }
                }
            }
        }
    }
}

/**
 * Uma página: a imagem inteira na tela e, com pinça ou toque duplo, ampliada
 * em volta do dedo. Um dedo só, sem zoom, fica para o pager trocar de página.
 * Arte pequena (pixel art) amplia sem filtro; foto grande encolhe com filtro.
 */
@Composable
private fun ZoomablePicture(
    catalog: Catalog,
    path: String,
    version: String,
    active: Boolean,
    onZoomChange: (Boolean) -> Unit,
    onTapOutside: () -> Unit,
) {
    val picture = rememberViewerPicture(catalog, path, version)
    var scale by remember { mutableFloatStateOf(1f) }
    var offset by remember { mutableStateOf(Offset.Zero) }
    LaunchedEffect(active) {
        if (!active) {
            scale = 1f
            offset = Offset.Zero
        }
    }

    BoxWithConstraints(Modifier.fillMaxSize().clipToBounds(), contentAlignment = Alignment.Center) {
        val density = LocalDensity.current
        val boxW = with(density) { maxWidth.toPx() }
        val boxH = with(density) { maxHeight.toPx() }
        val first = picture?.frames?.first()
        if (picture == null || first == null) return@BoxWithConstraints
        val fit = min(boxW / first.width, boxH / first.height)
        val fittedW = first.width * fit
        val fittedH = first.height * fit

        fun clamp(o: Offset, s: Float) = Offset(
            o.x.coerceIn(-max(0f, (fittedW * s - boxW) / 2), max(0f, (fittedW * s - boxW) / 2)),
            o.y.coerceIn(-max(0f, (fittedH * s - boxH) / 2), max(0f, (fittedH * s - boxH) / 2)),
        )

        fun apply(newScale: Float, newOffset: Offset) {
            val wasZoomed = scale > 1f
            scale = newScale
            offset = clamp(newOffset, newScale)
            if (wasZoomed != scale > 1f) onZoomChange(scale > 1f)
        }

        val center = Offset(boxW / 2, boxH / 2)
        val gestures = Modifier
            .pointerInput(fittedW, fittedH) {
                detectTapGestures(
                    onDoubleTap = { tap ->
                        if (scale > 1f) {
                            apply(1f, Offset.Zero)
                        } else {
                            val q = tap - center
                            apply(DOUBLE_TAP_ZOOM, q * (1 - DOUBLE_TAP_ZOOM))
                        }
                    },
                    onTap = { tap ->
                        // Toque fora da imagem fecha, como nas galerias do celular.
                        val q = tap - center - offset
                        val outside = kotlin.math.abs(q.x) > fittedW * scale / 2 ||
                            kotlin.math.abs(q.y) > fittedH * scale / 2
                        if (outside) onTapOutside()
                    },
                )
            }
            .pointerInput(fittedW, fittedH) {
                awaitEachGesture {
                    awaitFirstDown(requireUnconsumed = false)
                    do {
                        val event = awaitPointerEvent()
                        val fingers = event.changes.count { it.pressed }
                        if (fingers > 1 || scale > 1f) {
                            val zoom = event.calculateZoom()
                            val pan = event.calculatePan()
                            val q = event.calculateCentroid(useCurrent = true)
                                .takeIf { it != Offset.Unspecified }?.minus(center) ?: Offset.Zero
                            val newScale = (scale * zoom).coerceIn(1f, MAX_ZOOM)
                            val z = newScale / scale
                            // O ponto debaixo dos dedos continua debaixo deles.
                            apply(newScale, offset * z + q * (1 - z) + pan)
                            event.changes.forEach { if (it.positionChanged()) it.consume() }
                        }
                    } while (event.changes.any { it.pressed })
                }
            }

        val shownPerPixel = fit * scale
        val filter = if (shownPerPixel >= 1f) FilterQuality.None else FilterQuality.Medium
        Box(Modifier.fillMaxSize().then(gestures), contentAlignment = Alignment.Center) {
            PictureFrames(
                picture,
                Modifier.size(with(density) { fittedW.toDp() }, with(density) { fittedH.toDp() })
                    .graphicsLayer {
                        scaleX = scale
                        scaleY = scale
                        translationX = offset.x
                        translationY = offset.y
                    },
                PictureFit.Fill, filter,
            )
        }
    }
}

/** As miniaturas embaixo: a da frente com a borda de destaque, e a faixa a acompanha. */
@Composable
private fun ViewerStrip(
    images: List<ViewerImage>,
    current: Int,
    catalog: Catalog,
    version: String,
    onSelect: (Int) -> Unit,
) {
    val list = rememberLazyListState()
    LaunchedEffect(current) {
        val visible = list.layoutInfo.visibleItemsInfo
        if (visible.none { it.index == current && it.offset >= 0 &&
                it.offset + it.size <= list.layoutInfo.viewportEndOffset }) {
            list.animateScrollToItem(max(0, current - 1))
        }
    }
    LazyRow(
        Modifier.fillMaxWidth().height(72.dp),
        state = list,
        horizontalArrangement = Arrangement.spacedBy(6.dp),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 10.dp, vertical = 6.dp),
    ) {
        itemsIndexed(images, key = { _, it -> it.path }) { index, image ->
            val picture = rememberPackagePicture(catalog, image.path, version)
            val on = index == current
            Box(
                Modifier.size(60.dp)
                    .framePanel(border = if (on) Bl.PressedBorder else Bl.FrameBorder)
                    .pixelClickable { onSelect(index) }
                    .padding(2.dp)
                    .clipToBounds()
                    .graphicsLayer { alpha = if (on) 1f else 0.6f },
            ) {
                if (picture != null) PictureFrames(picture, Modifier.fillMaxSize(), PictureFit.Crop)
            }
        }
    }
}

@Composable
private fun rememberViewerPicture(catalog: Catalog, path: String, version: String): IconAnimation? {
    val picture by produceState<IconAnimation?>(null, catalog, path, version) {
        value = null
        value = withContext(MediaDispatcher) { catalog.loadViewerPicture(path) }
    }
    return picture
}
