package dev.bunnyloader.mods

import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Test

class ModManifestTest {
    private val json = Json { ignoreUnknownKeys = true }
    private fun manifest(fields: String) = json.decodeFromString<ModManifest>(
        """{"id":"test","name":"Teste","version":"1.0.0",$fields}"""
    )

    @Test fun legacyAuthorAndDescriptionStillLoad() {
        val m = manifest(""""author":"Antigo","description":"Descrição antiga"""")
        assertEquals("Antigo", m.authorLine)
        assertEquals("Descrição antiga", m.description)
    }

    @Test fun themeBackgroundNumbersAcceptTextAndClamp() {
        val m = manifest(""""theme":{"button":"#335","background":"bg.gif","backgroundMode":"tile",
            "backgroundScale":"3","backgroundDim":1.5}""")
        assertEquals("#335", m.theme.button)
        assertEquals(3f, m.theme.tileScale)
        assertEquals(1f, m.theme.dim)
        assertEquals(2f, manifest(""""theme":{"backgroundScale":"x"}""").theme.tileScale)
        assertEquals(0f, manifest(""""theme":{}""").theme.dim)
    }

    @Test fun authorsAcceptNamesObjectsAndASingleName() {
        assertEquals("Solo", manifest(""""authors":"Solo"""").authorLine)
        val m = manifest(""""authors":["A",{"name":"B","avatar":"b.png","role":"Arte"},"C"]""")
        assertEquals("A, B e C", m.authorLine)
        assertEquals("b.png", m.credits[1].avatar)
        assertEquals("Arte", m.credits[1].role)
    }

    @Test fun authorsTakePrecedenceAndBlankNamesUseLegacyFallback() {
        assertEquals("Novo", manifest(""""author":"Antigo","authors":["Novo"]""").authorLine)
        assertEquals("Antigo", manifest(""""author":"Antigo","authors":[" "]""").authorLine)
        assertEquals("autor desconhecido", manifest(""""authors":[]""").authorLine)
    }

    @Test fun licenseAndExtraPagesSurviveSerialization() {
        val m = manifest(""""license":"MIT","pages":[{"title":"Uso","file":"docs/uso.md"}]""")
        assertEquals(m, json.decodeFromString<ModManifest>(json.encodeToString(ModManifest.serializer(), m)))
    }
}
