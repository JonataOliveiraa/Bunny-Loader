package dev.bunnyloader.ui

import android.content.Context
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModRepository

/**
 * O estado que as quatro abas compartilham.
 *
 * Instalado e ligado são conjuntos de id observáveis, recalculados a cada
 * mudança, em vez de cada tela perguntar ao disco quando bem entende: assim a
 * lista de Pacotes e o botão da ficha do mod nunca discordam.
 */
class Shell(context: Context) {
    val catalog = Catalog(context)
    private val repo = ModRepository(context)

    var installed by mutableStateOf(emptySet<String>())
        private set
    var enabled by mutableStateOf(emptySet<String>())
        private set

    init {
        catalog.seedOnFirstRun()
        refresh()
    }

    val entries get() = catalog.entries
    fun entry(id: String) = catalog.entries.firstOrNull { it.id == id }

    fun install(entry: Catalog.Entry) {
        catalog.install(entry)
        refresh()
    }

    fun uninstall(id: String) {
        catalog.uninstall(id)
        refresh()
    }

    fun setEnabled(id: String, on: Boolean) {
        repo.setEnabled(id, on)
        refresh()
    }

    private fun refresh() {
        installed = catalog.entries.filter { catalog.isInstalled(it.id) }.map { it.id }.toSet()
        enabled = installed.filter { repo.isEnabled(it) }.toSet()
    }
}
