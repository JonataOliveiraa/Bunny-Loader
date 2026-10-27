package dev.bunnyloader.catalog

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class CompilerReportTest {
    @Test
    fun readsTheReportOfTheCSharpCompiler() {
        val ok = CompilerRuntime.parse("status compiled\nreason not compiled yet\nstats 1556 2 44\nwarning A.cs(1,1): CS0168: x\n")
        assertTrue(ok.success)
        assertTrue(ok.compiled)
        assertEquals(1556L, ok.milliseconds)
        assertEquals(2, ok.files)
        assertEquals(44L, ok.lines)
        assertEquals(listOf("A.cs(1,1): CS0168: x"), ok.warnings)

        val reused = CompilerRuntime.parse("status reused\nreason up to date\nstats 64 2 0\n")
        assertTrue(reused.success)
        assertFalse(reused.compiled)

        val failed = CompilerRuntime.parse("status failed\nerror Items/T.cs(14,23): CS0103: The name 'Damag' does not exist\n")
        assertFalse(failed.success)
        assertEquals(1, failed.errors.size)
    }
}
