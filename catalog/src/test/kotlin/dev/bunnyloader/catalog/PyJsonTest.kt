package dev.bunnyloader.catalog

import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assume.assumeTrue
import org.junit.Test
import java.io.File

class PyJsonTest {
    @Test
    fun rewritesPublishedIndexByteForByte() {
        val index = File("../build/mods-repo/index.json")
        assumeTrue("sem build/mods-repo (rode o gerenciador uma vez)", index.isFile)
        val text = index.readText()
        assertEquals(text, PyJson.dump(Json.parseToJsonElement(text)))
    }

    @Test
    fun escapesLikePython() {
        val json = Json.parseToJsonElement("""{"a":"x\"y\\z\n\t\u0001é/","b":[],"c":{},"d":[1,2.5,true,null]}""")
        val expected = "{\n  \"a\": \"x\\\"y\\\\z\\n\\t\\u0001é/\",\n  \"b\": [],\n  \"c\": {},\n" +
            "  \"d\": [\n    1,\n    2.5,\n    true,\n    null\n  ]\n}\n"
        assertEquals(expected, PyJson.dump(json))
    }
}
