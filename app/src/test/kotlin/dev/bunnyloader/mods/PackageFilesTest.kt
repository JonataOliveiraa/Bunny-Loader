package dev.bunnyloader.mods

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PackageFilesTest {
    @Test fun relativePathsAreResolvedForBundledAndInstalledPackages() {
        assertEquals("mods/Teste/docs/uso.md", resolvePackagePath("mods/Teste", false, "./docs/uso.md"))
        val root = File("build/test-pack").absolutePath
        assertEquals(File(root, "authors/foto.png").canonicalPath,
            resolvePackagePath(root, true, "authors\\foto.png"))
    }

    @Test fun pathsCannotEscapeOrReferenceOtherOrigins() {
        for (path in listOf("", ".", "../segredo", "authors/../../segredo", "\\segredo",
            "/segredo", "C:/segredo", "https://host/foto.png", "foto\u0000.png")) {
            assertNull(path, resolvePackagePath("mods/Teste", false, path))
            assertNull(path, resolvePackagePath(File("build/test-pack").absolutePath, true, path))
        }
    }
}
