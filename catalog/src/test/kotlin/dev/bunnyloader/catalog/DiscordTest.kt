package dev.bunnyloader.catalog

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.io.File

class DiscordTest {
    private val hook = "https://discord.com/api/webhooks/123/abc-DEF_1.x"

    @Test
    fun acceptsOnlyDiscordWebhooks() {
        assertEquals("$hook?wait=true", Discord.validUrl(hook))
        assertEquals("$hook?thread_id=9&wait=true", Discord.validUrl("$hook?thread_id=9&wait=false"))
        assertNull(Discord.validUrl("http://discord.com/api/webhooks/123/abc"))
        assertNull(Discord.validUrl("https://evil.com/api/webhooks/123/abc"))
        assertNull(Discord.validUrl("https://discord.com/api/webhooks/123/abc#x"))
        assertNull(Discord.validUrl(""))
    }

    @Test
    fun cleansLikeThePythonVersion() {
        assertEquals("Mod \\*bold\\* name", Discord.clean("  Mod *bold* name 😀 "))
        assertEquals("ab", Discord.clean("a<:smile:123>b"))
        assertEquals("abcd...", Discord.clean("abcdefgh", 7))
    }

    private fun mod(uid: String, version: String, name: String = uid) = Json.parseToJsonElement(
        """{"uid":"$uid","id":"$uid","name":"$name","version":"$version",
           "download":{"url":"https://x/$uid.bl","size":2048},"files":["icon.png"],
           "manifest":{"authors":["Ana"]}}""").jsonObject

    @Test
    fun oneEventPerChangedMod() {
        val discord = Discord(hook, File.createTempFile("discord", ".json").apply { delete() })
        val before = listOf(mod("a", "1.0"), mod("b", "1.0"), mod("c", "1.0"))
        val after = listOf(mod("a", "1.0"), mod("b", "1.1"), mod("d", "1.0"))
        val events = discord.events(before, after, "old", "new", listOf("mods/a/icon.png"))
        val titles = events.map {
            it["payload"]!!.jsonObject["embeds"]!!.jsonArray[0].jsonObject["title"].let { t -> (t as JsonPrimitive).content }
        }
        assertEquals(listOf(
            "Catalog listing updated: a",
            "Package updated: b",
            "Package removed: c",
            "New package published: d",
        ), titles)
        assertEquals(listOf("new:a", "new:b", "new:c", "new:d"), events.map { (it["id"] as JsonPrimitive).content })
        val removed = events[2]["payload"]!!.jsonObject["embeds"]!!.jsonArray[0] as JsonObject
        assertEquals("${Repo.RAW}/old/mods/c/icon.png", (removed["thumbnail"] as JsonObject)["url"].let { (it as JsonPrimitive).content })
    }
}
