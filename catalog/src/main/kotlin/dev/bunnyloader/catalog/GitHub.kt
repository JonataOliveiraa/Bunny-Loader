package dev.bunnyloader.catalog

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

object Repo {
    const val OWNER = "JonataOliveiraa"
    const val NAME = "Bunny-Loader"
    const val BRANCH = "mods-index"
    const val API = "https://api.github.com/repos/$OWNER/$NAME"
    const val UPLOADS = "https://uploads.github.com/repos/$OWNER/$NAME"
    const val DOWNLOAD = "https://github.com/$OWNER/$NAME/releases/download"
    const val RAW = "https://raw.githubusercontent.com/$OWNER/$NAME"
}

class GitHub(private val token: String) {
    /** null no 404, como o gh() do publish.py; outro erro vira Fail com o que o GitHub disse. */
    fun call(method: String, url: String, body: JsonElement? = null): JsonElement? =
        send(method, url, body?.toString()?.toByteArray(), "application/json")

    fun upload(url: String, data: ByteArray, contentType: String): JsonElement? =
        send("POST", url, data, contentType)

    private fun send(method: String, url: String, data: ByteArray?, contentType: String): JsonElement? {
        val request = Request.Builder().url(url)
            .header("Authorization", "Bearer $token")
            .header("Accept", "application/vnd.github+json")
            .header("X-GitHub-Api-Version", "2022-11-28")
            .header("User-Agent", USER_AGENT)
            .method(method, data?.toRequestBody(contentType.toMediaType()))
            .build()
        client.newCall(request).execute().use { response ->
            if (response.code == 404) return null
            val text = response.body?.string() ?: ""
            if (!response.isSuccessful) throw Fail("GitHub $method $url: ${response.code} ${text.take(300)}")
            return if (text.isBlank()) null else Json.parseToJsonElement(text)
        }
    }

    companion object {
        private const val USER_AGENT = "bunny-loader-catalog"
        private val client = OkHttpClient.Builder()
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(3, TimeUnit.MINUTES)
            .writeTimeout(3, TimeUnit.MINUTES)
            .build()

        /** Sem token: o índice público e o .bl de um Release. */
        fun download(url: String): ByteArray {
            val request = Request.Builder().url(url).header("User-Agent", USER_AGENT).build()
            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) throw Fail("GET $url: ${response.code}")
                return response.body?.bytes() ?: ByteArray(0)
            }
        }
    }
}
