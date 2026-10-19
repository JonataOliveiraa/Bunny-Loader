package dev.bunnyloader.mods

internal fun modCulture(language: String): String = when (language) {
    "en" -> "en-US"
    "ru" -> "ru-RU"
    "es" -> "es-ES"
    "zh" -> "zh-Hans"
    else -> language
}

internal fun localizedText(values: Map<String, String>, language: String): String? =
    values[modCulture(language)]?.takeIf(String::isNotBlank)

/** Lê a descrição Markdown da cultura ativa, ou volta ao arquivo base. */
fun readModDescription(manifest: ModManifest, language: String, readText: (String) -> String?): String {
    val description = readText("description_${modCulture(language)}.md")?.takeIf(String::isNotBlank)
        ?: readText("description.md")?.takeIf(String::isNotBlank)
    return description ?: manifest.description.ifBlank { manifest.summary }
}
