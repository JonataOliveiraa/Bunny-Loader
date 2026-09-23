package dev.bunnyloader.mods

import kotlinx.serialization.Serializable

/**
 * mod.json — o que o pacote diz sobre si mesmo.
 *
 * A vitrine do launcher é montada daqui, não de uma tabela à parte: categoria,
 * descrição e data saem do próprio mod. Assim um mod de terceiro aparece na
 * lista com a mesma cara dos nossos, sem o app precisar conhecê-lo.
 */
@Serializable
data class ModManifest(
    val id: String,
    val name: String,
    val version: String,
    val author: String = "",
    /** Textura, Armas, Jogabilidade, Cheat, Utilidade... O pacote escolhe. */
    val category: String = "Mod",
    /** Uma linha, para o cartão da lista. */
    val summary: String = "",
    /** Parágrafo, para a tela do mod. */
    val description: String = "",
    /** AAAA-MM-DD da última atualização. */
    val updated: String = "",
    /** Pede lugar no destaque da tela inicial. */
    val featured: Boolean = false,
    val blVersion: Int = 1,
    val gameVersion: List<Long> = emptyList(),
    val entry: String = "main.js",
    val dependencies: List<String> = emptyList(),
)
