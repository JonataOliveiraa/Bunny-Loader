package dev.bunnyloader.ui

import bunny.TranslationCatalog
import java.io.File
import java.util.Locale
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LocalizationTest {
    private val rows = Json.parseToJsonElement(
        File("src/main/assets/localization/menu.json").readText()
    ).jsonObject.mapValues { (_, value) -> value.jsonArray.map { it.jsonPrimitive.content }.toTypedArray() }
    private val catalog = TranslationCatalog(rows)

    @Test fun regionalAndAutomaticChoicesResolveToSupportedLanguages() {
        assertEquals("en", TranslationCatalog.resolveLanguage("en-GB", Locale.FRANCE))
        assertEquals("ru", TranslationCatalog.resolveLanguage("ru_RU", Locale.FRANCE))
        assertEquals("es", TranslationCatalog.resolveLanguage("", Locale.forLanguageTag("es-MX")))
        assertEquals("zh", TranslationCatalog.resolveLanguage("", Locale.SIMPLIFIED_CHINESE))
        assertEquals("zh", TranslationCatalog.resolveLanguage("zh-Hans-CN", Locale.US))
        assertEquals("pt-BR", TranslationCatalog.resolveLanguage("", Locale.JAPAN))
        assertEquals("pt-BR", TranslationCatalog.resolveLanguage("invalid", Locale.US))
    }

    @Test fun missingOrInvalidTranslationsKeepTheInterfaceUsable() {
        assertEquals("Texto novo", catalog.text("ru", "Texto novo"))
        val incomplete = TranslationCatalog(mapOf("Olá %1\$s" to arrayOf("Hello %1\$s", "", "%2\$s")))
        assertEquals("Olá Bunny", incomplete.text("ru", "Olá %1\$s", "Bunny"))
        assertEquals("Olá Bunny", incomplete.text("es", "Olá %1\$s", "Bunny"))
    }

    @Test fun everyTranslationPreservesPlaceholdersAndUnicode() {
        val placeholder = Regex("%([1-9][0-9]*\\\$)?[sd]")
        for ((source, values) in rows) {
            assertEquals(source, 4, values.size)
            val expected = placeholder.findAll(source).map { it.value }.sorted().toList()
            val args = Array<Any>(expected.size) { "Тест ñ %" }
            for ((index, language) in listOf("en", "ru", "es", "zh").withIndex()) {
                assertTrue("$language: $source", values[index].isNotBlank())
                assertEquals("$language: $source", expected,
                    placeholder.findAll(values[index]).map { it.value }.sorted().toList())
                assertEquals("$language: $source", String.format(Locale.forLanguageTag(language), values[index], *args),
                    catalog.text(language, source, *args))
            }
            assertEquals(source, source, catalog.text("pt-BR", source))
        }
        assertEquals("Суперспособности", catalog.text("ru", "Superpoderes"))
        assertEquals("Descargando 42%", catalog.text("es", "Baixando %1\$s%%", 42))
        assertEquals("正在下载 42%", catalog.text("zh", "Baixando %1\$s%%", 42))
    }
}
