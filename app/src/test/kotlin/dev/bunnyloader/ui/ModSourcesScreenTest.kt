package dev.bunnyloader.ui

import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.ModSources
import org.junit.Assert.assertEquals
import org.junit.Test

class ModSourcesScreenTest {
    private val projects = listOf(
        ModSources.Project("TestMod", "Test Mod", ModManifest(
            id = "TestMod", name = "Test Mod", version = "1", author = "Alice",
            localizedNames = mapOf("zh-Hans" to "测试模组"))),
        ModSources.Project("Other", "Other Mod", ModManifest(
            id = "Other", name = "Other Mod", version = "1", author = "Bob")),
        ModSources.Project("Broken", "Broken", null, "Missing manifest"),
    )

    @Test fun searchMatchesLocalizedNameKeyAndAuthorWithoutHidingMalformedProjects() {
        fun keys(query: String, language: String = "en") =
            filterSourceProjects(projects, query, language).map { it.key }
        assertEquals(listOf("TestMod"), keys("测试", "zh"))
        assertEquals(listOf("TestMod"), keys("test", "zh"))
        assertEquals(listOf("TestMod"), keys(" alice "))
        assertEquals(listOf("Other"), keys("other"))
        assertEquals(listOf("Broken"), keys("broken"))
        assertEquals(emptyList<String>(), keys("unknown"))
        assertEquals(projects.map { it.key }, keys("   "))
    }
}
