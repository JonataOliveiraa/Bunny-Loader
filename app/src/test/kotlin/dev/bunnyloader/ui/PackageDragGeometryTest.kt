package dev.bunnyloader.ui

import org.junit.Assert.assertEquals
import org.junit.Test

class PackageDragGeometryTest {
    private val slots = listOf(
        PackageDragSlot(0, 10, 100), PackageDragSlot(1, 118, 100),
        PackageDragSlot(2, 226, 140), PackageDragSlot(3, 374, 100),
    )

    @Test fun onlyCrossingAnotherCardsCenterChangesTheDropPosition() {
        assertEquals(0, packageDropIndex(0, 167f, slots))
        assertEquals(1, packageDropIndex(0, 169f, slots))
        assertEquals(2, packageDropIndex(0, 297f, slots))
        assertEquals(3, packageDropIndex(0, 500f, slots))
        assertEquals(3, packageDropIndex(3, 297f, slots))
        assertEquals(2, packageDropIndex(3, 295f, slots))
        assertEquals(0, packageDropIndex(3, -30f, slots))
    }

    @Test fun scrollingPastTheOriginalCardUsesTheVisibleIndices() {
        val scrolled = listOf(PackageDragSlot(5, -50, 100), PackageDragSlot(6, 58, 100))
        assertEquals(6, packageDropIndex(0, 150f, scrolled))
        assertEquals(5, packageDropIndex(9, -1f, scrolled))
        assertEquals(2, packageDropIndex(2, 100f, emptyList()))
    }

    @Test fun previewShiftsOnlyCardsBetweenOriginalAndTargetPositions() {
        assertEquals(listOf(0f, -108f, -108f, -108f, 0f),
            (0..4).map { packageDragShift(it, 0, 3, 108f) })
        assertEquals(listOf(108f, 108f, 108f, 0f, 0f),
            (0..4).map { packageDragShift(it, 3, 0, 108f) })
        assertEquals(0f, packageDragShift(2, 2, 2, 108f))
    }
}
