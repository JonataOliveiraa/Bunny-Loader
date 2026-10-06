package dev.bunnyloader.mods

import java.io.File
import java.nio.file.Files
import kotlinx.serialization.json.Json
import org.junit.Assert.*
import org.junit.Test

class TexturePackFilesTest {
    private fun temporary(test: (File) -> Unit) {
        val dir = Files.createTempDirectory("texture-pack").toFile()
        try { test(dir) } finally { dir.deleteRecursively() }
    }

    @Test fun texturesNeedImagesAndNoScript() = temporary { dir ->
        assertFalse(TexturePackFiles.hasImages(dir))
        File(dir, "content/Images").mkdirs()
        File(dir, "content/Images/NPC_125.png").writeBytes(byteArrayOf(1))
        assertTrue(TexturePackFiles.hasImages(dir))
        assertFalse(File(dir, "content/main.js").exists())
    }

    @Test fun convertsPcMetadataAndKeepsIdentityAcrossVersions() = temporary { dir ->
        File(dir, "Content/Images").mkdirs()
        File(dir, "Content/Images/NPC_125.png").writeBytes(byteArrayOf(1))
        File(dir, "pack.json").writeText("""{"Name":"Pack","Author":"TdT","Description":"Sprites","Version":{"major":1,"minor":12}}""")
        File(dir, "workshop.json").writeText("""{"SteamEntryId":3798222774}""")
        val m = TexturePackFiles.convertPcPack(dir)!!
        assertEquals("Pack", m.name)
        assertEquals("TdT", m.author)
        assertEquals("1.12.0", m.version)
        assertEquals(PackType.TEXTURE, m.packType)
        assertTrue(m.hasValidUid)
        assertTrue(File(dir, "content/Images/NPC_125.png").isFile)
        assertFalse(File(dir, "content/main.js").exists())
        assertEquals(m, Json.decodeFromString<ModManifest>(File(dir, "manifest.json").readText()))
        File(dir, "manifest.json").delete()
        File(dir, "pack.json").writeText("""{"Name":"Renamed","Author":"TdT","Version":{"major":2,"minor":0}}""")
        assertEquals(m.uid, TexturePackFiles.convertPcPack(dir)!!.uid)
    }

    @Test fun preservesBunnyManifestAndFindsWrappedRoot() = temporary { dir ->
        val nested = File(dir, "Pack/Inside").apply { mkdirs() }
        File(nested, "manifest.json").writeText("original")
        File(nested, "pack.json").writeText("invalid")
        assertEquals(nested, TexturePackFiles.importRoot(dir))
        assertNull(TexturePackFiles.convertPcPack(nested))
        assertEquals("original", File(nested, "manifest.json").readText())
    }

    @Test fun rejectsPcPackWithoutImages() = temporary { dir ->
        File(dir, "pack.json").writeText("""{"Name":"Empty"}""")
        assertThrows(IllegalArgumentException::class.java) { TexturePackFiles.convertPcPack(dir) }
        assertFalse(File(dir, "manifest.json").exists())
    }

    @Test fun acceptsOriginalPcDescriptionWithLiteralLineBreaks() = temporary { dir ->
        File(dir, "Content/Images").mkdirs()
        File(dir, "Content/Images/Item_1.png").writeBytes(byteArrayOf(1))
        File(dir, "pack.json").writeText("""{"Name":"Pack","Description":"First line
Second line","Version":{"major":1,"minor":12}}""")
        assertEquals("First line\nSecond line", TexturePackFiles.convertPcPack(dir)!!.description)
    }
}
