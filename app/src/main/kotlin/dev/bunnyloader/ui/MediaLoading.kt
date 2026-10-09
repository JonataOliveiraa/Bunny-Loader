package dev.bunnyloader.ui

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi

/** Queue suspended work instead of occupying an IO thread per waiting picture. */
@OptIn(ExperimentalCoroutinesApi::class)
internal val MediaDispatcher = Dispatchers.IO.limitedParallelism(2)
