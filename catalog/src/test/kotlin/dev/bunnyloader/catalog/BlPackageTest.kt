package dev.bunnyloader.catalog

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

class BlPackageTest {
    @get:Rule val temp = TemporaryFolder()

    private val manifest = """{"uid":"12345678-1234-1234-1234-123456789abc","id":"test","version":"1.0.0",""" +
        """"name":"Test","blVersion":2,"theme":{"background":"docs/bg.gif"}}"""

    /** Um .bl válido; a descrição vai sem compressão, com \n (o que a cópia como texto estraga). */
    private fun valid(): ByteArray {
        val out = ByteArrayOutputStream()
        ZipOutputStream(out).use { zip ->
            fun put(name: String, data: ByteArray, stored: Boolean = false) {
                val entry = ZipEntry(name)
                if (stored) {
                    entry.method = ZipEntry.STORED
                    entry.size = data.size.toLong()
                    entry.crc = CRC32().apply { update(data) }.value
                }
                zip.putNextEntry(entry)
                zip.write(data)
                zip.closeEntry()
            }
            put("manifest.json", manifest.toByteArray())
            put("content/main.js", "export default class Test extends Mod {}\n".toByteArray())
            put("icon.png", byteArrayOf(1, 2, 3))
            put("banner.gif", byteArrayOf(4, 5, 6))
            put("docs/bg.gif", byteArrayOf(7, 8, 9))
            put("description.md", "# Mod\n\nlinha 1\nlinha 2\n".repeat(20).toByteArray(), stored = true)
            put("description_zh-Hans.md", "# 中文\n![预览](docs/english.png)".toByteArray())
            put("changelog_zh-Hans.md", "## 新版本\n中文更新日志".toByteArray())
            put("docs/english.png", byteArrayOf(1))
            put("description_invalid/path.md", "Não publicar".toByteArray())
        }
        return out.toByteArray()
    }

    private fun failure(data: ByteArray): String {
        val file = File(temp.root, "pacote.bl").apply { writeBytes(data) }
        try {
            BlPackage(file)
        } catch (e: Fail) {
            return e.message.orEmpty()
        }
        fail("deveria recusar")
        return ""
    }

    @Test
    fun validPackageOpensAndStoreTakesBannerAndBackground() {
        val file = File(temp.root, "ok.bl").apply { writeBytes(valid()) }
        val files = BlPackage(file).storeFiles()
        assertTrue(files.toString(), "banner.gif" in files && "docs/bg.gif" in files)
        assertTrue(files.containsAll(listOf("description_zh-Hans.md", "changelog_zh-Hans.md", "docs/english.png")))
        assertTrue("description_invalid/path.md" !in files)
    }

    @Test
    fun textConversionIsExplained() {
        val data = valid()
        val crlf = String(data, Charsets.ISO_8859_1).replace("\n", "\r\n").toByteArray(Charsets.ISO_8859_1)
        val message = failure(crlf)
        assertTrue(message, "tratado como texto" in message)
    }

    @Test
    fun cutFileIsExplained() {
        val data = valid()
        val message = failure(data.copyOf(data.size / 2))
        assertTrue(message, "cortado" in message)
    }

    @Test
    fun webPageEmptyAndOtherFilesAreExplained() {
        assertTrue("página da web" in failure("<!DOCTYPE html><html><body>Discord</body></html>".toByteArray()))
        assertTrue("vazio" in failure(ByteArray(0)))
        assertEquals("pacote.bl: não é zip: não começa como zip (os primeiros bytes são ef bb bf 7b, um .bl começa com 50 4b)",
            failure(byteArrayOf(0xEF.toByte(), 0xBB.toByte(), 0xBF.toByte(), '{'.code.toByte(), '}'.code.toByte())))
    }
}
