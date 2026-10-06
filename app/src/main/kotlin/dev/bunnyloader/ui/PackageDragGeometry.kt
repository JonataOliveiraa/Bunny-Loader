package dev.bunnyloader.ui

internal data class PackageDragSlot(val index: Int, val top: Int, val height: Int)

internal fun packageDropIndex(
    from: Int, center: Float, slots: List<PackageDragSlot>, allowedIndices: Set<Int>? = null,
): Int {
    val allowed = if (allowedIndices == null) slots else slots.filter { it.index in allowedIndices }
    val crossedAbove = allowed.filter { it.index < from && center < it.top + it.height / 2f }
    if (crossedAbove.isNotEmpty()) return crossedAbove.minOf { it.index }
    return allowed.filter { it.index > from && center > it.top + it.height / 2f }
        .maxOfOrNull { it.index } ?: from
}

internal fun packageDragShift(index: Int, from: Int, to: Int, distance: Float): Float = when {
    to > from && index in (from + 1)..to -> -distance
    to < from && index in to until from -> distance
    else -> 0f
}
