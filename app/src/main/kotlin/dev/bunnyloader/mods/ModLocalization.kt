package dev.bunnyloader.mods

internal fun modCulture(language: String): String = bunny.TranslationCatalog.culture(language)

internal fun localizedText(values: Map<String, String>, language: String): String? =
    values[modCulture(language)]?.takeIf(String::isNotBlank)

private fun readLocalizedMarkdown(name: String, language: String, readText: (String) -> String?): String? =
    readText("${name}_${modCulture(language)}.md")?.takeIf(String::isNotBlank)
        ?: readText("$name.md")?.takeIf(String::isNotBlank)

/** Lê a descrição Markdown da cultura ativa, ou volta ao arquivo base. */
fun readModDescription(manifest: ModManifest, language: String, readText: (String) -> String?): String =
    readLocalizedMarkdown("description", language, readText)
        ?: manifest.description.ifBlank { manifest.summaryFor(language) }

/** Lê o changelog Markdown da cultura ativa, ou volta ao arquivo base. */
fun readModChangelog(language: String, readText: (String) -> String?): String? =
    readLocalizedMarkdown("changelog", language, readText)
