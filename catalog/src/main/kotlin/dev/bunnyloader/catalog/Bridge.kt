package dev.bunnyloader.catalog

import android.util.Log
import android.webkit.JavascriptInterface
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.put
import java.io.IOException
import java.util.concurrent.Executors

/**
 * O `/api/...` da página, respondido aqui em vez do manager.py. Um pedido de
 * cada vez, numa thread só: todos mexem no mesmo branch.
 */
class Bridge(private val publisher: Publisher, private val reply: (Int, String) -> Unit) {
    private val worker = Executors.newSingleThreadExecutor()

    @JavascriptInterface
    fun post(id: Int, path: String, body: String) {
        worker.execute { reply(id, handle(path, body).toString()) }
    }

    private fun handle(path: String, body: String): JsonObject {
        val log = StringBuilder()
        publisher.log = { log.append(it).append('\n') }
        val started = System.currentTimeMillis()
        return try {
            val data = Json.parseToJsonElement(body.ifBlank { "{}" }).jsonObject
            fun arg(key: String) = (data[key] as? JsonPrimitive)?.contentOrNull ?: ""
            I18n.english = arg("lang") == "en"
            val result: JsonElement = when (path) {
                "/api/state" -> publisher.state()
                "/api/sync" -> { publisher.sync(); JsonNull }
                "/api/upload" -> publisher.check(publisher.saveUpload(arg("name"), arg("data")))
                "/api/publish-upload" -> { publisher.publish(publisher.uploadFile(arg("file"))); JsonNull }
                "/api/publish-dry" -> { publisher.publish(publisher.uploadFile(arg("file")), dry = true); JsonNull }
                "/api/remove" -> { publisher.remove(arg("uid")); JsonNull }
                "/api/discord-test" -> { publisher.discordTest(); JsonNull }
                "/api/discord-retry" -> { publisher.discordRetry(); JsonNull }
                else -> throw Fail(I18n.tr("$path não existe no app", "$path does not exist in the app"))
            }
            buildJsonObject {
                put("ok", true)
                put("ms", System.currentTimeMillis() - started)
                put("log", log.toString())
                put("result", result)
            }
        } catch (e: Exception) {
            if (e !is Fail) Log.e("BLCatalog", "falha em $path", e)
            buildJsonObject {
                put("ok", false)
                put("ms", System.currentTimeMillis() - started)
                put("log", log.toString())
                put("error", when (e) {
                    is Fail -> e.message
                    is IOException -> I18n.tr("sem conexão com o servidor", "no connection to the server") +
                        " (${e.javaClass.simpleName}: ${e.message})"
                    else -> "${e.javaClass.simpleName}: ${e.message}"
                })
            }
        }
    }
}
