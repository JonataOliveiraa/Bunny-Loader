package dev.bunnyloader.ui

import dev.bunnyloader.mods.Catalog
import java.text.Collator
import java.util.Locale

internal enum class PackageStatus(val label: String) {
    ALL("Todos"), ENABLED("Ativados"), DISABLED("Desativados"),
}

internal enum class PackageSort(val label: String) {
    LOAD("Ordem de carga"), SIZE_ASC("Menor tamanho"), SIZE_DESC("Maior tamanho"), NAME("A-Z"),
}

internal fun filterPackages(
    packages: List<Catalog.Entry>, enabled: Set<String>, status: PackageStatus, sort: PackageSort,
): List<Catalog.Entry> {
    val shown = packages.filter {
        when (status) {
            PackageStatus.ALL -> true
            PackageStatus.ENABLED -> it.uid in enabled
            PackageStatus.DISABLED -> it.uid !in enabled
        }
    }
    return when (sort) {
        PackageSort.LOAD -> shown
        PackageSort.SIZE_ASC -> shown.sortedBy { it.sizeBytes }
        PackageSort.SIZE_DESC -> shown.sortedByDescending { it.sizeBytes }
        PackageSort.NAME -> {
            val names = Collator.getInstance(Locale.forLanguageTag("pt-BR")).apply { strength = Collator.PRIMARY }
            shown.sortedWith { a, b -> names.compare(a.manifest.name, b.manifest.name) }
        }
    }
}
