package dev.bunnyloader.ui

import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.Author
import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.PackType
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

    @Test fun allGroupsEnabledFirstAndPreservesEachGroupsManualOrder() {
        assertEquals(listOf("c", "b", "a"), show(PackageStatus.ALL, PackageSort.LOAD))
    }

    @Test fun enabledAndDisabledAreComplementary() {
        assertEquals(listOf("c", "b"), show(PackageStatus.ENABLED, PackageSort.LOAD))
        assertEquals(listOf("a"), show(PackageStatus.DISABLED, PackageSort.LOAD))
    }

    @Test fun sizeUsesBytesAndSupportsBothDirections() {
        assertEquals(listOf("b", "c", "a"), show(PackageStatus.ALL, PackageSort.SIZE_ASC))
        assertEquals(listOf("c", "b", "a"), show(PackageStatus.ALL, PackageSort.SIZE_DESC))
    }

    @Test fun namesAreAlphabeticalRegardlessOfCaseAndAccents() {
        assertEquals(listOf("b", "c", "a"), show(PackageStatus.ALL, PackageSort.NAME))
        assertEquals(listOf("a", "b", "c"), filterPackages(packages, packages.map { it.uid }.toSet(),
            PackageStatus.ALL, PackageSort.NAME).map { it.uid })
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

    @Test fun typeFilterKeepsOnlyThatTypeAndDefaultsToEverything() {
        fun typed(uid: String, type: String) = Catalog.Entry(
            ModManifest(uid = uid, id = uid, name = uid, version = "1", blVersion = 2, type = type),
            "", 1, emptyList(), null, null,
        )
        val mixed = listOf(typed("m", ""), typed("t", "texture"), typed("f", "fonte"), typed("m2", "mod"))
        fun show(type: PackType?) =
            filterPackages(mixed, emptySet(), PackageStatus.ALL, PackageSort.LOAD, type).map { it.uid }
        assertEquals(listOf("m", "m2"), show(PackType.MOD))
        assertEquals(listOf("t"), show(PackType.TEXTURE))
        assertEquals(listOf("f"), show(PackType.FONT))
        assertEquals(listOf("m", "t", "f", "m2"), show(null))
    }

    @Test fun togglingStatusRegroupsImmediatelyInEverySort() {
        for (sort in PackageSort.entries) {
            val off = filterPackages(packages, setOf("a"), PackageStatus.ALL, sort).map { it.uid }
            assertEquals("a", off.first())
            val on = filterPackages(packages, setOf("a", "b"), PackageStatus.ALL, sort).map { it.uid }
            assertEquals(setOf("a", "b"), on.take(2).toSet())
            assertEquals("c", on.last())
        }
    }

    @Test fun searchIgnoresAccentsCaseAndExtraWhitespace() {
        assertEquals(listOf("a"), searchPackages(packages, "  ARVORE  ").map { it.uid })
        assertEquals(packages, searchPackages(packages, " \t\n "))
        assertEquals(emptyList<String>(), searchPackages(packages, "inexistente").map { it.uid })
    }

    @Test fun searchCombinesTermsAcrossNameCategorySummaryAndAuthors() {
        val entry = mod("test-uid", "Árvore Mágica", 10).let {
            it.copy(manifest = it.manifest.copy(id = "forest-tools", category = "Utilidade",
                summary = "Novas opções para o mundo", authors = listOf(Author("João Silva"))))
        }
        val index = PackageSearchIndex(listOf(entry))
        for (query in listOf("arvore JOAO", "silva utilidade", "opcoes mundo", "forest-tools", "test-uid")) {
            assertEquals(listOf(entry), searchPackages(listOf(entry), query, index = index))
        }
        assertEquals(emptyList<Catalog.Entry>(), searchPackages(listOf(entry), "arvore ausente", index = index))
    }

    @Test fun searchSupportsLegacyAuthorAndKeepsSourceOrder() {
        val authored = packages.map { it.copy(manifest = it.manifest.copy(author = "José")) }
        assertEquals(authored, searchPackages(authored, "jose"))
        assertEquals(listOf("c", "a", "b"), authored.map { it.uid })
    }

    @Test fun searchTypeCategoryAndStatusComposeWithoutChangingLoadGroups() {
        val mixed = listOf(
            mod("a", "Espada", 10).let { it.copy(manifest = it.manifest.copy(category = "Armas")) },
            mod("b", "Espada azul", 20).let { it.copy(manifest = it.manifest.copy(category = "Armas")) },
            mod("c", "Espada", 30).let { it.copy(manifest = it.manifest.copy(type = "texture", category = "Armas")) },
            mod("d", "Espada", 40).let { it.copy(manifest = it.manifest.copy(category = "Utilidade")) },
        )
        val index = PackageSearchIndex(mixed)
        assertEquals(listOf("b", "a"), filterPackages(mixed, setOf("b", "c"), PackageStatus.ALL,
            PackageSort.LOAD, PackType.MOD, "espada", "armas", index).map { it.uid })
        assertEquals(listOf("a"), filterPackages(mixed, setOf("b", "c"), PackageStatus.DISABLED,
            PackageSort.NAME, PackType.MOD, "espada", "Armas", index).map { it.uid })
        assertEquals(emptyList<Catalog.Entry>(), filterPackages(mixed, setOf("b"), PackageStatus.ENABLED,
            PackageSort.LOAD, PackType.MOD, "espada", "Utilidade", index))
    }

    @Test fun clearingSearchAndCategoryRestoresAllPackages() {
        assertEquals(listOf("a"), filterPackages(packages, setOf("b", "c"), PackageStatus.ALL,
            PackageSort.LOAD, query = "arvore", category = "Mod").map { it.uid })
        assertEquals(listOf("c", "b", "a"), filterPackages(packages, setOf("b", "c"), PackageStatus.ALL,
            PackageSort.LOAD, query = "", category = null).map { it.uid })
    }
}
