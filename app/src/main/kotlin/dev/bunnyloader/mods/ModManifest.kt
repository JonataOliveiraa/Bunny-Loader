package dev.bunnyloader.mods

import kotlinx.serialization.Serializable

/** mod.json dentro de um pacote .bmod. */
@Serializable
data class ModManifest(
    val id: String,
    val name: String,
    val version: String,
    val author: String = "",
    val blVersion: Int = 1,
    val gameVersion: List<Long> = emptyList(),
    val entry: String = "main.js",
    val dependencies: List<String> = emptyList(),
)
