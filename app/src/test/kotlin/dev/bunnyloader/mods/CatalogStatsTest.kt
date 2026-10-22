package dev.bunnyloader.mods

import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Test

class CatalogStatsTest {
    private val json = Json { ignoreUnknownKeys = true }

    @Test fun countsStayShortAndNeverRoundUp() {
        assertEquals("0", formatCount(0))
        assertEquals("987", formatCount(987))
        assertEquals("1k", formatCount(1_000))
        assertEquals("1.2k", formatCount(1_299))
        assertEquals("9.9k", formatCount(9_999))
        assertEquals("10k", formatCount(10_000))
        assertEquals("345k", formatCount(345_678))
        assertEquals("1M", formatCount(1_000_000))
        assertEquals("1.5M", formatCount(1_550_000))
        assertEquals("12M", formatCount(12_345_678))
    }

    @Test fun indexFeaturedIsOptional() {
        val old = json.decodeFromString<RemoteIndex>("""{"format":1,"mods":[]}""")
        assertEquals(emptyList<String>(), old.featured)
        val curated = json.decodeFromString<RemoteIndex>(
            """{"format":1,"featured":["dae471c1-68ae-4d6a-b49e-a5b3f5a0cd36"],"mods":[]}""")
        assertEquals(listOf("dae471c1-68ae-4d6a-b49e-a5b3f5a0cd36"), curated.featured)
    }

    @Test fun statsFileParsesWithUnknownFields() {
        val stats = json.decodeFromString<RemoteStats>("""
            {"format":1,"updatedAt":"2026-10-21T12:00:00Z","source":"x",
             "downloads":{"dae471c1-68ae-4d6a-b49e-a5b3f5a0cd36":37}}
        """.trimIndent())
        assertEquals(37L, stats.downloads["dae471c1-68ae-4d6a-b49e-a5b3f5a0cd36"])
    }
}
