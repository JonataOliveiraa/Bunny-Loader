package dev.bunnyloader.mods

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import java.io.File

class SourceBuildTest {
    @get:Rule
    val folder = TemporaryFolder()

    private val past = System.currentTimeMillis() - 60_000

    private fun project(): File {
        val dir = folder.newFolder("mod")
        File(dir, "Mod.cs").writeText("class Mod {}")
        File(dir, "Content").mkdirs()
        File(dir, "Content/Item.cs").writeText("class Item {}")
        File(dir, "bin").mkdirs()
        File(dir, "bin/Mod.dll").writeText("dll")
        File(dir, "bin/Mod.build.json").writeText("{}")
        dir.walkTopDown().forEach { it.setLastModified(past) }
        File(dir, "bin/Mod.build.json").setLastModified(past + 1000)
        return dir
    }

    private fun build(dir: File) = SourceBuild("Mod", File(dir, "Mod.cs").path, emptyList())

    @Test
    fun anUntouchedProjectIsCurrent() {
        assertTrue(build(project()).looksCurrent())
    }

    @Test
    fun noBuildIsNotCurrent() {
        val dir = project()
        File(dir, "bin/Mod.dll").delete()
        assertFalse(build(dir).looksCurrent())
    }

    @Test
    fun anEditedSourceIsNotCurrent() {
        val dir = project()
        File(dir, "Content/Item.cs").setLastModified(past + 5000)
        assertFalse(build(dir).looksCurrent())
    }

    @Test
    fun aDeletedSourceIsNotCurrent() {
        val dir = project()
        File(dir, "Content/Item.cs").delete()
        assertFalse(build(dir).looksCurrent())
    }

    @Test
    fun otherFilesDoNotCount() {
        val dir = project()
        File(dir, "Content/Item.png").apply { writeText("png") }.setLastModified(past)
        File(dir, "Content").setLastModified(past)
        File(dir, "Content/Item.png").setLastModified(past + 5000)
        assertTrue(build(dir).looksCurrent())
    }

    @Test
    fun theBuildFolderDoesNotCount() {
        val dir = project()
        File(dir, "bin/Mod.dll").setLastModified(past + 5000)
        File(dir, "bin").setLastModified(past + 5000)
        assertTrue(build(dir).looksCurrent())
    }

    @Test
    fun encodingKeepsTheReferences() {
        val build = SourceBuild("Mod", "/mods/a/Mod.cs", listOf("/mods/b/bin/B.dll", "/mods/c/C.dll"))
        assertEquals(build, SourceBuild.decode(build.encode()))
        val alone = SourceBuild("Mod", "/mods/a/Mod.cs", emptyList())
        assertEquals(alone, SourceBuild.decode(alone.encode()))
    }
}
