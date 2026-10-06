package dev.bunnyloader.game

import org.junit.Assert.assertEquals
import org.junit.Test
import java.io.File
import java.nio.file.Files

class SaveFilesTest {
    @Test fun freeNameKeepsTheNameOrAddsTheFirstFreeSuffix() {
        val dir = Files.createTempDirectory("saves").toFile()
        try {
            assertEquals("Mundo", SaveFiles.freeName(dir, "Mundo", ".wld"))
            File(dir, "Mundo.wld").writeText("x")
            assertEquals("Mundo_2", SaveFiles.freeName(dir, "Mundo", ".wld"))
            File(dir, "Mundo_2.wld").writeText("x")
            assertEquals("Mundo_3", SaveFiles.freeName(dir, "Mundo", ".wld"))
            // Outra extensão não conta: um personagem e um mundo podem ter o mesmo nome.
            assertEquals("Mundo", SaveFiles.freeName(dir, "Mundo", ".plr"))
        } finally {
            dir.deleteRecursively()
        }
    }
}
