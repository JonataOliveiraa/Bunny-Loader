package dev.bunnyloader.mods

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ModUpdatesTest {
    @Test fun onlyInstalledModsWithNewerVersionsAreAnnounced() {
        assertEquals(setOf("old"), unseenModUpdates(
            mapOf("old" to "1.0", "current" to "2.0", "ahead" to "3.0"),
            mapOf("old" to "1.1", "current" to "2.0", "ahead" to "2.0", "new" to "1.0"),
            emptyMap(),
        ))
    }

    @Test fun reopeningDoesNotRepeatTheSameNotice() {
        assertTrue(unseenModUpdates(mapOf("a" to "1.0"), mapOf("a" to "1.1"),
            mapOf("a" to "1.1")).isEmpty())
    }

    @Test fun nextReleaseIsAnnouncedEvenWhenThePreviousUpdateWasDeclined() {
        assertEquals(setOf("a"), unseenModUpdates(mapOf("a" to "1.0"),
            mapOf("a" to "1.2"), mapOf("a" to "1.1")))
    }

    @Test fun oldCachedCatalogDoesNotRepeatANewerNotice() {
        assertTrue(unseenModUpdates(mapOf("a" to "1.0"), mapOf("a" to "1.1"),
            mapOf("a" to "1.2")).isEmpty())
    }

    @Test fun versionNumbersAreComparedNumericallyAndEquivalentVersionsAreIgnored() {
        assertEquals(setOf("a"), unseenModUpdates(mapOf("a" to "1.9", "b" to "1.0"),
            mapOf("a" to "1.10", "b" to "v1.0.0"), emptyMap()))
        assertTrue(unseenModUpdates(mapOf("a" to "1.0"), mapOf("a" to "v1.1.0"),
            mapOf("a" to "1.1")).isEmpty())
    }

    @Test fun eachModTracksItsOwnRelease() {
        assertEquals(setOf("b"), unseenModUpdates(mapOf("a" to "1.0", "b" to "1.0"),
            mapOf("a" to "2.0", "b" to "2.0"), mapOf("a" to "2.0")))
    }
}
