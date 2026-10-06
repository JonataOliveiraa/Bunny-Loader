package dev.bunnyloader.mods

import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class PackTypeTest {
    @Test fun missingOrUnknownTypeIsAMod() {
        assertEquals(PackType.MOD, PackType.of(""))
        assertEquals(PackType.MOD, PackType.of("mod"))
        assertEquals(PackType.MOD, PackType.of("shader"))
    }

    @Test fun texturesAndFontsAcceptEnglishPortugueseAndPlural() {
        for (t in listOf("texture", "Textures", "textura", "TEXTURAS", " resourcepack ")) {
            assertEquals(t, PackType.TEXTURE, PackType.of(t))
        }
        for (t in listOf("font", "fonts", "fonte", "Fontes")) {
            assertEquals(t, PackType.FONT, PackType.of(t))
        }
    }

    @Test fun manifestReadsTheTypeField() {
        val json = Json { ignoreUnknownKeys = true }
        val m = json.decodeFromString<ModManifest>("""{"id":"a","name":"A","version":"1","type":"textura"}""")
        assertEquals(PackType.TEXTURE, m.packType)
        val old = json.decodeFromString<ModManifest>("""{"id":"a","name":"A","version":"1"}""")
        assertEquals(PackType.MOD, old.packType)
    }

    @Test fun indexWithInlineManifestAndTheOldOneBothParse() {
        val json = Json { ignoreUnknownKeys = true }
        val withManifest = json.decodeFromString<RemoteIndex>("""
            {"format":1,"mods":[{"uid":"00895657-564e-49ca-b099-23823ba0e68c","version":"1.0.1",
             "download":{"url":"u","sha256":"s","size":1},"files":["icon.png"],
             "manifest":{"uid":"00895657-564e-49ca-b099-23823ba0e68c","id":"x","name":"X","version":"1.0.1","type":"font"}}]}
        """.trimIndent())
        val manifest = assertNotNullAndGet(withManifest.mods.single().manifest)
        assertEquals(PackType.FONT, manifest.packType)
        val old = json.decodeFromString<RemoteIndex>("""
            {"format":1,"mods":[{"uid":"u","download":{"url":"u","sha256":"s"},"files":["manifest.json"]}]}
        """.trimIndent())
        assertNull(old.mods.single().manifest)
    }

    private fun <T> assertNotNullAndGet(value: T?): T {
        assertNotNull(value)
        return value!!
    }
}
