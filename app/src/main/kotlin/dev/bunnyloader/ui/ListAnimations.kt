package dev.bunnyloader.ui

import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.staticCompositionLocalOf

val LocalAnimationsPaused = staticCompositionLocalOf<() -> Boolean> { { false } }

@Composable
internal fun PauseListAnimations(isBusy: () -> Boolean, content: @Composable () -> Unit) {
    val parent = LocalAnimationsPaused.current
    val busy by rememberUpdatedState(isBusy)
    val paused = remember(parent) { { parent() || busy() } }
    CompositionLocalProvider(LocalAnimationsPaused provides paused, content = content)
}
