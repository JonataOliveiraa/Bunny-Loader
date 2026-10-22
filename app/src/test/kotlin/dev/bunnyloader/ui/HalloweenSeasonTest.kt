package dev.bunnyloader.ui

import java.util.Calendar
import java.util.GregorianCalendar
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class HalloweenSeasonTest {
    private fun day(month: Int, day: Int) = GregorianCalendar(2026, month, day)

    @Test fun seasonRunsFromOctoberTwentiethToNovemberSecond() {
        assertFalse(isHalloweenSeason(day(Calendar.OCTOBER, 19)))
        assertTrue(isHalloweenSeason(day(Calendar.OCTOBER, 20)))
        assertTrue(isHalloweenSeason(day(Calendar.OCTOBER, 31)))
        assertTrue(isHalloweenSeason(day(Calendar.NOVEMBER, 2)))
        assertFalse(isHalloweenSeason(day(Calendar.NOVEMBER, 3)))
        assertFalse(isHalloweenSeason(day(Calendar.MARCH, 25)))
    }
}
