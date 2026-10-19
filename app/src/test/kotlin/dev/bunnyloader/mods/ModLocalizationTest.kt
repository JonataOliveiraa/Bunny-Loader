package dev.bunnyloader.mods

import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Test

class ModLocalizationTest {
    private val manifest = ModManifest(
        id = "forest", name = "Forest", version = "1", summary = "A mod",
        description = "Default description",
        localizedNames = mapOf("pt-BR" to "Floresta", "zh-Hans" to "森林"),
    )

    @Test fun localizedNamesKeepManifestFallback() {
        val json = Json { encodeDefaults = true }
        val restored = json.decodeFromString<ModManifest>(json.encodeToString(ModManifest.serializer(), manifest))
        assertEquals("森林", restored.nameFor("zh"))
        assertEquals("Floresta", restored.nameFor("pt-BR"))
        assertEquals("Forest", restored.nameFor("en"))
        assertEquals("forest", restored.id)
    }

    @Test fun translatedMarkdownFallsBackToBaseMarkdownAndManifest() {
        val files = mapOf(
            "description_zh-Hans.md" to "# 中文描述",
            "description.md" to "# Default description",
        )
        assertEquals("# 中文描述", readModDescription(manifest, "zh", files::get))
        assertEquals("# Default description", readModDescription(manifest, "en", files::get))
        assertEquals("Default description", readModDescription(manifest, "en") { null })
    }

    @Test fun localizedChangelogFallsBackToBaseMarkdown() {
        val files = mapOf(
            "changelog_zh-Hans.md" to "## 中文更新",
            "changelog.md" to "## Default changes",
        )
        assertEquals("## 中文更新", readModChangelog("zh", files::get))
        assertEquals("## Default changes", readModChangelog("en", files::get))
        assertEquals(null, readModChangelog("en") { null })
    }
}
