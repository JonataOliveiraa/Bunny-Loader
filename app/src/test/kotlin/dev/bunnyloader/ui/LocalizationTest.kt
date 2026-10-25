package dev.bunnyloader.ui

import bunny.TranslationCatalog
import dev.bunnyloader.game.Eligibility
import java.io.File
import java.util.Locale
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LocalizationTest {
    private val folder = File("src/main/assets/localization")
    private val tables = TranslationCatalog.LANGUAGES.associateWith { language ->
        Json.parseToJsonElement(File(folder, "${TranslationCatalog.culture(language)}.json").readText())
            .jsonObject.mapValues { (_, value) -> value.jsonPrimitive.content }
    }
    private val english = tables.getValue(TranslationCatalog.FALLBACK)
    private val catalog = TranslationCatalog(tables)

    @Test fun regionalAndAutomaticChoicesResolveToSupportedLanguages() {
        assertEquals("en", TranslationCatalog.resolveLanguage("en-GB", Locale.FRANCE))
        assertEquals("pt-BR", TranslationCatalog.resolveLanguage("", Locale.forLanguageTag("pt-BR")))
        assertEquals("pt-BR", TranslationCatalog.resolveLanguage("pt-PT", Locale.US))
        assertEquals("ru", TranslationCatalog.resolveLanguage("ru_RU", Locale.FRANCE))
        assertEquals("es", TranslationCatalog.resolveLanguage("", Locale.forLanguageTag("es-MX")))
        assertEquals("zh", TranslationCatalog.resolveLanguage("", Locale.SIMPLIFIED_CHINESE))
        assertEquals("zh", TranslationCatalog.resolveLanguage("zh-Hans-CN", Locale.US))
        assertEquals("vi", TranslationCatalog.resolveLanguage("", Locale.forLanguageTag("vi-VN")))
        assertEquals("th", TranslationCatalog.resolveLanguage("th_TH", Locale.US))
        assertEquals("en", TranslationCatalog.resolveLanguage("", Locale.JAPAN))
        assertEquals("en", TranslationCatalog.resolveLanguage("invalid", Locale.US))
    }

    @Test fun missingOrInvalidTranslationsFallBackToEnglish() {
        assertEquals("NewText", catalog.text("ru", "NewText"))
        val incomplete = TranslationCatalog(mapOf(
            "en" to mapOf("Greeting" to "Hello %1\$s"),
            "ru" to mapOf("Greeting" to ""),
            "es" to mapOf("Greeting" to "%2\$s"),
        ))
        assertEquals("Hello Bunny", incomplete.text("ru", "Greeting", "Bunny"))
        assertEquals("Hello Bunny", incomplete.text("es", "Greeting", "Bunny"))
        assertEquals("Hello Bunny", incomplete.text("zh", "Greeting", "Bunny"))
    }

    @Test fun everyLanguageFileHasTheSameKeys() {
        val expected = TranslationCatalog.LANGUAGES.map { "${TranslationCatalog.culture(it)}.json" }.sorted()
        assertEquals(expected, folder.list { _, name -> name.endsWith(".json") }!!.sorted())
        for ((language, table) in tables) {
            assertEquals("$language: missing", emptySet<String>(), english.keys - table.keys)
            assertEquals("$language: extra", emptySet<String>(), table.keys - english.keys)
        }
        for (key in english.keys) assertTrue(key, Regex("[A-Z][A-Za-z0-9]*").matches(key))
    }

    @Test fun everyKeyUsedInCodeExists() {
        val call = Regex("""\btr\(\s*"([^"]*)"""")
        val sources = listOf(File("src/main/kotlin"), File("src/main/java"), File("../tools/cheatbridge"))
            .flatMap { root -> root.walk().filter { it.extension == "kt" || it.extension == "java" }.toList() }
        val used = sources.flatMap { file -> call.findAll(file.readText()).map { file.name to it.groupValues[1] } }
        assertTrue(used.size > 100)
        for ((file, key) in used) assertTrue("$file: $key", key in english)
        for (status in Eligibility.Status.entries.filter { it != Eligibility.Status.OK }) {
            assertTrue("$status title", status.titleKey in english)
            assertTrue("$status message", status.messageKey in english)
        }
    }

    @Test fun everyTranslationPreservesPlaceholdersAndUnicode() {
        val placeholder = Regex("%([1-9][0-9]*\\\$)?[sd]")
        for ((language, table) in tables) {
            for ((key, value) in table) {
                val expected = placeholder.findAll(english.getValue(key)).map { it.value }.sorted().toList()
                val args = Array<Any>(expected.size) { "Тест ñ %" }
                assertTrue("$language: $key", value.isNotBlank())
                assertEquals("$language: $key", expected, placeholder.findAll(value).map { it.value }.sorted().toList())
                assertEquals("$language: $key", String.format(Locale.forLanguageTag(language), value, *args),
                    catalog.text(language, key, *args))
            }
        }
        assertEquals("Суперспособности", catalog.text("ru", "Superpowers"))
        assertEquals("Superpoderes", catalog.text("pt-BR", "Superpowers"))
        assertEquals("Baixando 42%", catalog.text("pt-BR", "DownloadingPercent", 42))
        assertEquals("Descargando 42%", catalog.text("es", "DownloadingPercent", 42))
        assertEquals("正在下载 42%", catalog.text("zh", "DownloadingPercent", 42))
        assertEquals("Đang tải 42%", catalog.text("vi", "DownloadingPercent", 42))
        assertEquals("กำลังดาวน์โหลด 42%", catalog.text("th", "DownloadingPercent", 42))
    }
}
