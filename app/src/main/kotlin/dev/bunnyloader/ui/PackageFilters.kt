package dev.bunnyloader.ui

import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.PackType
import java.text.Collator
import java.text.Normalizer
import java.util.Locale

internal enum class PackageStatus(val label: String) {
    ALL("Todos"), ENABLED("Ativados"), DISABLED("Desativados"),
}

internal enum class PackageSort(val label: String) {
    LOAD("Ordem de carga"), SIZE_ASC("Menor tamanho"), SIZE_DESC("Maior tamanho"), NAME("A-Z"),
}

private val SEARCH_MARKS = Regex("\\p{M}+")
private val SEARCH_SPACE = Regex("\\s+")

private fun searchText(value: String): String = SEARCH_MARKS
    .replace(Normalizer.normalize(value, Normalizer.Form.NFD), "")
    .lowercase(Locale.ROOT).trim()

internal class PackageSearchIndex(packages: List<Catalog.Entry>) {
    private val textByUid = packages.associate { entry ->
        val m = entry.manifest
        entry.uid to searchText(listOf(m.name, m.id, m.uid, m.category, m.summary,
            m.credits.joinToString(" ") { it.name }).joinToString(" "))
    }

    fun matches(uid: String, terms: List<String>): Boolean =
        textByUid[uid]?.let { text -> terms.all { it in text } } ?: false
}

internal fun searchPackages(
    packages: List<Catalog.Entry>, query: String = "", type: PackType? = null,
    category: String? = null, index: PackageSearchIndex? = null,
): List<Catalog.Entry> {
    val terms = searchText(query).split(SEARCH_SPACE).filter { it.isNotEmpty() }
    val search = if (terms.isEmpty()) null else index ?: PackageSearchIndex(packages)
    val wantedCategory = category?.let(::searchText)
    return packages.filter {
        (type == null || it.manifest.packType == type) &&
            (wantedCategory == null || searchText(it.manifest.category) == wantedCategory) &&
            (search == null || search.matches(it.uid, terms))
    }
}

internal fun filterPackages(
    packages: List<Catalog.Entry>, enabled: Set<String>, status: PackageStatus, sort: PackageSort,
    type: PackType? = null, query: String = "", category: String? = null,
    index: PackageSearchIndex? = null,
): List<Catalog.Entry> {
    val shown = searchPackages(packages, query, type, category, index).filter {
        when (status) {
            PackageStatus.ALL -> true
            PackageStatus.ENABLED -> it.uid in enabled
            PackageStatus.DISABLED -> it.uid !in enabled
        }
    }
    val sorted = when (sort) {
        PackageSort.LOAD -> shown
        PackageSort.SIZE_ASC -> shown.sortedBy { it.sizeBytes }
        PackageSort.SIZE_DESC -> shown.sortedByDescending { it.sizeBytes }
        PackageSort.NAME -> {
            val names = Collator.getInstance(Locale.forLanguageTag("pt-BR")).apply { strength = Collator.PRIMARY }
            shown.sortedWith { a, b -> names.compare(a.manifest.name, b.manifest.name) }
        }
    }
    return sorted.sortedBy { it.uid !in enabled }
}
