package dev.bunnyloader.ui

import android.content.Context
import android.net.Uri
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModManifest
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

    /**
     * Mods que vieram de fora, lidos do disco.
     *
     * O catálogo embutido é fixo; um pacote importado não está nele, e sem isto
     * ele instalaria e não apareceria em lugar nenhum.
     *
     * Declarado ANTES do init: Kotlin inicializa na ordem do arquivo, e o
     * `refresh()` do init escrevia num campo que ainda era nulo.
     */
    var imported by mutableStateOf(emptyList<ModManifest>())
        private set

    init {
        catalog.seedOnFirstRun()
        refresh()
    }

    val entries get() = catalog.entries
    fun entry(uid: String) = catalog.entries.firstOrNull { it.uid == uid }

    fun install(entry: Catalog.Entry) {
        catalog.install(entry)
        refresh()
    }

    /** @return o nome do mod, ou a mensagem do que deu errado. */
    fun importPackage(uri: Uri): Result<ModManifest> =
        repo.import(uri).also { refresh() }

    fun uninstall(uid: String) {
        catalog.uninstall(uid)
        refresh()
    }

    fun setEnabled(uid: String, on: Boolean) {
        repo.setEnabled(uid, on)
        refresh()
    }

    fun uninstallImported(uid: String) {
        catalog.uninstall(uid)
        refresh()
    }

    private fun refresh() {
        val catalogIds = catalog.entries.map { it.uid }.toSet()
        val onDisk = repo.list()
        installed = onDisk.map { it.key }.toSet()
        imported = onDisk.filter { it.key !in catalogIds }.sortedBy { it.name }
        enabled = installed.filter { repo.isEnabled(it) }.toSet()
    }
}
