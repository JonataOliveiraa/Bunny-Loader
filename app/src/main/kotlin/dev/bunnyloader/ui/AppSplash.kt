package dev.bunnyloader.ui

import android.app.Activity
import android.os.SystemClock
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.colorResource
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R
import kotlin.math.roundToInt
import kotlinx.coroutines.delay

private val SplashFrames = intArrayOf(
    R.drawable.splash_bunny_0,
    R.drawable.splash_bunny_1,
    R.drawable.splash_bunny_2,
    R.drawable.splash_bunny_3,
    R.drawable.splash_bunny_4,
    R.drawable.splash_bunny_5,
    R.drawable.splash_bunny_6,
    R.drawable.splash_bunny_7,
)
private const val FRAME_MILLIS = 120L
private const val FADE_MILLIS = 220
private const val MAX_WAIT_MILLIS = 8_000L
private val SplashArtHeight = 144.dp

@Composable
fun AppSplash(ready: () -> Boolean, onFinished: () -> Unit) {
    val frames = SplashFrames.map { ImageBitmap.imageResource(it) }
    var frame by remember { mutableIntStateOf(0) }
    val opacity = remember { Animatable(1f) }
    val finish by rememberUpdatedState(onFinished)
    val background = colorResource(R.color.splash_background)
    val window = (LocalContext.current as Activity).window
    DisposableEffect(window) {
        val statusBar = window.statusBarColor
        val navigationBar = window.navigationBarColor
        window.statusBarColor = background.toArgb()
        window.navigationBarColor = background.toArgb()
        onDispose {
            window.statusBarColor = statusBar
            window.navigationBarColor = navigationBar
        }
    }
    LaunchedEffect(Unit) {
        val start = SystemClock.uptimeMillis()
        var shown = 0
        while (shown < frames.size || (!ready() && SystemClock.uptimeMillis() - start < MAX_WAIT_MILLIS)) {
            frame = shown % frames.size
            delay(FRAME_MILLIS)
            shown++
        }
        opacity.animateTo(0f, tween(FADE_MILLIS))
        finish()
    }
    Canvas(
        Modifier
            .fillMaxSize()
            .graphicsLayer { alpha = opacity.value }
            .background(background)
            .clickable(remember { MutableInteractionSource() }, indication = null) {},
    ) {
        val image = frames[frame]
        val scale = (SplashArtHeight.toPx() / image.height).roundToInt().coerceAtLeast(1)
        val width = image.width * scale
        val height = image.height * scale
        drawImage(
            image,
            dstOffset = IntOffset((size.width.roundToInt() - width) / 2, (size.height.roundToInt() - height) / 2),
            dstSize = IntSize(width, height),
            filterQuality = FilterQuality.None,
        )
    }
}
