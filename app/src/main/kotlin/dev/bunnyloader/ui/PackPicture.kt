package dev.bunnyloader.ui

import androidx.compose.foundation.layout.Spacer
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.clipRect
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.repeatOnLifecycle
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.IconAnimation
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import kotlin.math.ceil
import kotlin.math.max
import kotlin.math.min
import kotlin.math.roundToInt

/** Como a imagem ocupa o espaço que recebe. */
sealed interface PictureFit {
    /** Enche cortando o que sobra (ícone, capa larga, fundo). */
    data object Crop : PictureFit
    /** Cabe inteira, centrada; sobra espaço de um lado. */
    data object Fit : PictureFit
    /** Estica até as bordas: quem chama já deu o tamanho na proporção certa. */
    data object Fill : PictureFit
    /** Repete a imagem, cada pixel dela com `pixel` px de lado (fundo de textura). */
    data class Tile(val pixel: Float) : PictureFit
}

/**
 * Uma imagem do pacote, animada se for GIF: do cache na hora, ou lida fora da
 * thread da interface. null enquanto carrega e quando o arquivo não se lê.
 */
@Composable
fun rememberPackagePicture(catalog: Catalog, path: String?, version: String = ""): IconAnimation? {
    val picture by produceState<IconAnimation?>(null, catalog, path, version) {
        value = null
        if (path != null) value = withContext(MediaDispatcher) { catalog.loadPicture(path) }
    }
    return picture
}

/**
 * Desenha os quadros. O quadro troca no tempo de cada um, só com o launcher
 * na frente e sem a economia de efeitos ligada, e é lido no desenho: a troca
 * redesenha a imagem, não recompõe nada.
 */
@Composable
fun PictureFrames(
    picture: IconAnimation,
    modifier: Modifier,
    fit: PictureFit = PictureFit.Fill,
    filter: FilterQuality = FilterQuality.None,
) {
    var frame by remember(picture) { mutableIntStateOf(0) }
    val fx = LocalThemeFx.current
    val paused by remember(fx) { derivedStateOf { fx.paused() } }
    if (picture.frames.size > 1) {
        val lifecycle = LocalLifecycleOwner.current.lifecycle
        LaunchedEffect(picture, lifecycle, paused) {
            if (paused) return@LaunchedEffect
            lifecycle.repeatOnLifecycle(Lifecycle.State.STARTED) {
                while (true) {
                    delay(picture.delaysMs[frame].toLong())
                    frame = (frame + 1) % picture.frames.size
                }
            }
        }
    }
    Spacer(modifier.drawBehind { drawPicture(picture.frames[frame], fit, filter) })
}

private fun DrawScope.drawPicture(image: ImageBitmap, fit: PictureFit, filter: FilterQuality) {
    if (size.width <= 0f || size.height <= 0f) return
    val dst = IntSize(size.width.roundToInt(), size.height.roundToInt())
    when (fit) {
        PictureFit.Fill -> drawImage(image, dstSize = dst, filterQuality = filter)
        PictureFit.Crop -> {
            val scale = max(size.width / image.width, size.height / image.height)
            val sw = (size.width / scale).roundToInt().coerceIn(1, image.width)
            val sh = (size.height / scale).roundToInt().coerceIn(1, image.height)
            drawImage(
                image,
                srcOffset = IntOffset((image.width - sw) / 2, (image.height - sh) / 2),
                srcSize = IntSize(sw, sh),
                dstSize = dst,
                filterQuality = filter,
            )
        }
        PictureFit.Fit -> {
            val scale = min(size.width / image.width, size.height / image.height)
            val w = (image.width * scale).roundToInt()
            val h = (image.height * scale).roundToInt()
            drawImage(
                image,
                dstOffset = IntOffset((dst.width - w) / 2, (dst.height - h) / 2),
                dstSize = IntSize(w, h),
                filterQuality = filter,
            )
        }
        is PictureFit.Tile -> {
            val w = (image.width * fit.pixel).roundToInt().coerceAtLeast(1)
            val h = (image.height * fit.pixel).roundToInt().coerceAtLeast(1)
            clipRect {
                for (y in 0 until ceil(size.height / h).toInt()) {
                    for (x in 0 until ceil(size.width / w).toInt()) {
                        drawImage(image, dstOffset = IntOffset(x * w, y * h), dstSize = IntSize(w, h),
                            filterQuality = filter)
                    }
                }
            }
        }
    }
}
