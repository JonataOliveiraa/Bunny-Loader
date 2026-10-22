package dev.bunnyloader.mods

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ModUpdatesTest {
    private val now = 10 * UPDATE_REMIND_MS
    private fun seen(version: String, ago: Long = 0) = UpdateNotice(version, now - ago)

    @Test fun onlyInstalledModsWithNewerVersionsAreAnnounced() {
        assertEquals(setOf("old"), dueModUpdates(
            mapOf("old" to "1.0", "current" to "2.0", "ahead" to "3.0"),
            mapOf("old" to "1.1", "current" to "2.0", "ahead" to "2.0", "new" to "1.0"),
            emptyMap(), now,
        ))
    }

    @Test fun reopeningSoonDoesNotRepeatTheSameNotice() {
        assertTrue(dueModUpdates(mapOf("a" to "1.0"), mapOf("a" to "1.1"),
            mapOf("a" to seen("1.1", UPDATE_REMIND_MS - 1)), now).isEmpty())
    }

    @Test fun aDeclinedUpdateIsRemindedLater() {
        assertEquals(setOf("a"), dueModUpdates(mapOf("a" to "1.0"), mapOf("a" to "1.1"),
            mapOf("a" to seen("1.1", UPDATE_REMIND_MS)), now))
    }

    @Test fun noticeFromBeforeTheReminderIsDue() {
        assertEquals(setOf("a"), dueModUpdates(mapOf("a" to "1.0"), mapOf("a" to "1.1"),
            mapOf("a" to UpdateNotice("1.1", 0)), now))
    }

    @Test fun clockGoingBackDoesNotRepeatTheNotice() {
        assertTrue(dueModUpdates(mapOf("a" to "1.0"), mapOf("a" to "1.1"),
            mapOf("a" to seen("1.1", -UPDATE_REMIND_MS)), now).isEmpty())
    }

    @Test fun nextReleaseIsAnnouncedEvenWhenThePreviousUpdateWasDeclined() {
        assertEquals(setOf("a"), dueModUpdates(mapOf("a" to "1.0"),
            mapOf("a" to "1.2"), mapOf("a" to seen("1.1")), now))
    }

    @Test fun oldCachedCatalogDoesNotRepeatANewerNotice() {
        assertTrue(dueModUpdates(mapOf("a" to "1.0"), mapOf("a" to "1.1"),
            mapOf("a" to seen("1.2")), now).isEmpty())
    }

    @Test fun versionNumbersAreComparedNumericallyAndEquivalentVersionsAreIgnored() {
        assertEquals(setOf("a"), dueModUpdates(mapOf("a" to "1.9", "b" to "1.0"),
            mapOf("a" to "1.10", "b" to "v1.0.0"), emptyMap(), now))
        assertTrue(dueModUpdates(mapOf("a" to "1.0"), mapOf("a" to "v1.1.0"),
            mapOf("a" to seen("1.1")), now).isEmpty())
    }

    @Test fun eachModTracksItsOwnRelease() {
        assertEquals(setOf("b"), dueModUpdates(mapOf("a" to "1.0", "b" to "1.0"),
            mapOf("a" to "2.0", "b" to "2.0"), mapOf("a" to seen("2.0")), now))
    }
}
