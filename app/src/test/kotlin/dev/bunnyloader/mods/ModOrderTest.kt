package dev.bunnyloader.mods

import org.junit.Assert.assertEquals
import org.junit.Test

class ModOrderTest {
    @Test fun defaultOrderIsStableRegardlessOfDirectoryOrder() {
        assertEquals(listOf("a", "b", "c", "d"), ModOrder.resolve(listOf("d", "b", "a", "c"), emptyList()))
    }

    @Test fun savedOrderWinsAndNewPackagesAreAppended() {
        assertEquals(listOf("c", "a", "b", "d"), ModOrder.resolve(listOf("d", "c", "b", "a"), listOf("c", "a")))
    }

    @Test fun removedAndDuplicateEntriesAreIgnored() {
        assertEquals(listOf("b", "a", "c"), ModOrder.resolve(listOf("a", "b", "c"), listOf("gone", "b", "b", "a")))
    }

    @Test fun movesSwapAdjacentPackagesInBothDirections() {
        val initial = listOf("a", "b", "c", "d")
        assertEquals(listOf("a", "c", "b", "d"), ModOrder.move(initial, "c", -1))
        assertEquals(listOf("a", "c", "b", "d"), ModOrder.move(initial, "b", 1))
        assertEquals(initial, ModOrder.move(ModOrder.move(initial, "c", -1), "c", 1))
    }

    @Test fun boundariesUnknownPackagesAndInvalidDirectionsDoNotChangeOrder() {
        val initial = listOf("a", "b")
        for ((uid, direction) in listOf("a" to -1, "b" to 1, "gone" to 1, "a" to 3)) {
            assertEquals(initial, ModOrder.move(initial, uid, direction))
        }
        assertEquals(emptyList<String>(), ModOrder.move(emptyList(), "a", 1))
    }

    @Test fun draggingAcrossSeveralPackagesPreservesOtherRelativePositions() {
        val initial = listOf("a", "b", "c", "d")
        assertEquals(listOf("b", "c", "d", "a"), ModOrder.moveTo(initial, "a", "d"))
        assertEquals(listOf("d", "a", "b", "c"), ModOrder.moveTo(initial, "d", "a"))
        assertEquals(listOf("a", "c", "b", "d"), ModOrder.moveTo(initial, "b", "c"))
        assertEquals(listOf("a", "c", "b", "d"), ModOrder.moveTo(initial, "c", "b"))
        assertEquals(listOf("a", "b", "c", "d"), initial)
    }

    @Test fun invalidOrUnchangedDropDoesNotChangeOrder() {
        val initial = listOf("a", "b")
        assertEquals(initial, ModOrder.moveTo(initial, "a", "a"))
        assertEquals(initial, ModOrder.moveTo(initial, "gone", "b"))
        assertEquals(initial, ModOrder.moveTo(initial, "a", "gone"))
        assertEquals(emptyList<String>(), ModOrder.moveTo(emptyList(), "a", "b"))
    }
}
