package dev.bunnyloader.game

import android.content.Context
import org.json.JSONObject

object GameTexts {
    private const val DIR = "localization/game"

    fun pack(context: Context): String {
        val byKey = linkedMapOf<String, MutableList<Pair<String, String>>>()
        val files = context.assets.list(DIR).orEmpty().filter { it.endsWith(".json") }.sorted()
        for (file in files) {
            val culture = file.removeSuffix(".json")
            val json = runCatching {
                JSONObject(context.assets.open("$DIR/$file").bufferedReader().use { it.readText() })
            }.getOrNull() ?: continue
            for (key in json.keys()) byKey.getOrPut(key) { mutableListOf() }.add(culture to json.optString(key))
        }
        return byKey.entries.joinToString("\u001C") { (key, texts) ->
            key + "\u001D" + texts.joinToString("\u001E") { (culture, text) -> culture + "\u001F" + text }
        }
    }
}
