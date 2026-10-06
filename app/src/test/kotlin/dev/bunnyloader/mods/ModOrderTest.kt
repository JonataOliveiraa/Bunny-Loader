package dev.bunnyloader.mods

import org.junit.Assert.assertEquals
import org.junit.Test

class ModOrderTest {
    @Test fun activationAppendsToEnabledGroupAndKeepsOtherRelativePositions() {
        val order = listOf("b", "a", "d", "c", "e")
        val activated = ModOrder.activate(order, "c", setOf("a", "b"))
        assertEquals(listOf("b", "a", "c", "d", "e"), activated)
        assertEquals(activated, ModOrder.resolve(order.reversed(), activated, setOf("a", "b", "c")))
    }

    @Test fun reactivationAppendsAfterTheOtherEnabledPackages() {
        val installed = listOf("a", "b", "c", "d")
        val remaining = setOf("a", "c")
        val disabled = ModOrder.resolve(installed, installed, remaining)
        val activated = ModOrder.activate(disabled, "b", remaining)
        assertEquals(listOf("a", "c", "b", "d"), activated)
        assertEquals(activated, ModOrder.resolve(installed, activated, remaining + "b"))
    }

    @Test fun activationIntoAnEmptyGroupBecomesTheFirstPackage() {
        assertEquals(listOf("c", "a", "b"), ModOrder.activate(listOf("a", "b", "c"), "c", emptySet()))
    }

    @Test fun alreadyEnabledOrUnknownPackagesDoNotMove() {
        val order = listOf("b", "a", "c")
        assertEquals(order, ModOrder.activate(order, "b", setOf("a", "b")))
        assertEquals(order, ModOrder.activate(order, "missing", setOf("a", "b")))
        assertEquals(emptyList<String>(), ModOrder.activate(emptyList(), "missing", emptySet()))
    }

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

    @Test fun existingMixedOrderIsGroupedWithoutChangingEitherGroupsRelativeOrder() {
        val installed = listOf("a", "b", "c", "d", "e", "f")
        val saved = listOf("d", "c", "b", "a")
        assertEquals(listOf("c", "a", "e", "d", "b", "f"),
            ModOrder.resolve(installed, saved, setOf("a", "c", "e", "missing")))
        assertEquals(listOf("b", "d", "c", "a", "e", "f"),
            ModOrder.resolve(installed, saved, setOf("b")))
    }

    @Test fun arrowsCannotCrossTheEnabledDisabledBoundary() {
        val order = listOf("a", "b", "c", "d")
        val enabled = setOf("a", "b")
        assertEquals(order, ModOrder.move(order, "b", 1, enabled))
        assertEquals(order, ModOrder.move(order, "c", -1, enabled))
        assertEquals(listOf("b", "a", "c", "d"), ModOrder.move(order, "a", 1, enabled))
        assertEquals(listOf("a", "b", "d", "c"), ModOrder.move(order, "d", -1, enabled))
    }

    @Test fun draggingCanReorderEitherGroupButRejectsOtherGroupsTargets() {
        val order = listOf("a", "b", "c", "d", "e", "f")
        val enabled = setOf("a", "b", "c")
        assertEquals(listOf("b", "c", "a", "d", "e", "f"), ModOrder.moveTo(order, "a", "c", enabled))
        assertEquals(listOf("a", "b", "c", "f", "d", "e"), ModOrder.moveTo(order, "f", "d", enabled))
        for (on in enabled) for (off in order.filter { it !in enabled }) {
            assertEquals(order, ModOrder.moveTo(order, on, off, enabled))
            assertEquals(order, ModOrder.moveTo(order, off, on, enabled))
        }
    }

    @Test fun savedMovesSurviveRestartAndStatusChangesKeepTheEnabledLoadOrder() {
        val installed = listOf("a", "b", "c", "d", "e")
        val enabled = setOf("a", "b", "c")
        val moved = ModOrder.moveTo(installed, "a", "c", enabled)
        assertEquals(moved, ModOrder.resolve(installed.reversed(), moved, enabled))
        assertEquals(listOf("c", "a", "b", "d", "e"), ModOrder.resolve(installed, moved, enabled - "b"))
        assertEquals(moved, ModOrder.resolve(installed, moved, enabled))
    }
}
