package dev.bunnyloader.ui

import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModManifest
import org.junit.Assert.assertEquals
import org.junit.Test

class PackageFiltersTest {
    private fun mod(uid: String, name: String, bytes: Long) = Catalog.Entry(
        ModManifest(uid = uid, id = uid, name = name, version = "1", blVersion = 2),
        "", bytes, emptyList(), null, null,
    )
    private val packages = listOf(mod("c", "zebra", 4_000_000_000), mod("a", "Árvore", 100), mod("b", "beta", 300))
    private fun show(status: PackageStatus, sort: PackageSort) =
        filterPackages(packages, setOf("b", "c"), status, sort).map { it.uid }

    @Test fun allPreservesTheManualLoadOrder() {
        assertEquals(listOf("c", "a", "b"), show(PackageStatus.ALL, PackageSort.LOAD))
    }

    @Test fun enabledAndDisabledAreComplementary() {
        assertEquals(listOf("c", "b"), show(PackageStatus.ENABLED, PackageSort.LOAD))
        assertEquals(listOf("a"), show(PackageStatus.DISABLED, PackageSort.LOAD))
    }

    @Test fun sizeUsesBytesAndSupportsBothDirections() {
        assertEquals(listOf("a", "b", "c"), show(PackageStatus.ALL, PackageSort.SIZE_ASC))
        assertEquals(listOf("c", "b", "a"), show(PackageStatus.ALL, PackageSort.SIZE_DESC))
    }

    @Test fun namesAreAlphabeticalRegardlessOfCaseAndAccents() {
        assertEquals(listOf("a", "b", "c"), show(PackageStatus.ALL, PackageSort.NAME))
    }

    @Test fun statusAndSortCanBeCombinedWithoutChangingTheSourceOrder() {
        assertEquals(listOf("b", "c"), show(PackageStatus.ENABLED, PackageSort.SIZE_ASC))
        assertEquals(listOf("b", "c"), show(PackageStatus.ENABLED, PackageSort.NAME))
        assertEquals(listOf("c", "a", "b"), packages.map { it.uid })
    }

    @Test fun emptyResultsAndTiesRemainStable() {
        assertEquals(emptyList<String>(), filterPackages(packages, emptySet(), PackageStatus.ENABLED, PackageSort.LOAD).map { it.uid })
        assertEquals(emptyList<String>(), filterPackages(emptyList(), emptySet(), PackageStatus.ALL, PackageSort.NAME).map { it.uid })
        val tied = listOf(mod("b", "Igual", 100), mod("a", "igual", 100))
        for (sort in listOf(PackageSort.SIZE_ASC, PackageSort.SIZE_DESC, PackageSort.NAME)) {
            assertEquals(listOf("b", "a"), filterPackages(tied, emptySet(), PackageStatus.ALL, sort).map { it.uid })
        }
    }
}
