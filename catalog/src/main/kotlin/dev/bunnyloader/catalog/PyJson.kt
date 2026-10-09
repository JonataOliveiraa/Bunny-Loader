package dev.bunnyloader.catalog

import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

/**
 * JSON escrito como o `json.dumps(..., ensure_ascii=False, indent=2)` do
 * publish.py: o index.json publicado pelo app e pelo PC sai igual, e o diff do
 * branch mods-index mostra só o que mudou.
 */
object PyJson {
    fun dump(element: JsonElement): String = StringBuilder().also { write(it, element, 0) }.append('\n').toString()

    private fun write(out: StringBuilder, e: JsonElement, depth: Int) {
        when (e) {
            is JsonObject -> if (e.isEmpty()) out.append("{}") else {
                out.append("{\n")
                var i = 0
                for ((key, value) in e) {
                    indent(out, depth + 1)
                    quote(out, key)
                    out.append(": ")
                    write(out, value, depth + 1)
                    out.append(if (++i < e.size) ",\n" else "\n")
                }
                indent(out, depth)
                out.append('}')
            }
            is JsonArray -> if (e.isEmpty()) out.append("[]") else {
                out.append("[\n")
                e.forEachIndexed { i, value ->
                    indent(out, depth + 1)
                    write(out, value, depth + 1)
                    out.append(if (i < e.size - 1) ",\n" else "\n")
                }
                indent(out, depth)
                out.append(']')
            }
            JsonNull -> out.append("null")
            is JsonPrimitive -> if (e.isString) quote(out, e.content) else out.append(e.content)
        }
    }

    private fun indent(out: StringBuilder, depth: Int) {
        repeat(depth * 2) { out.append(' ') }
    }

    private fun quote(out: StringBuilder, s: String) {
        out.append('"')
        for (c in s) {
            when (c) {
                '"' -> out.append("\\\"")
                '\\' -> out.append("\\\\")
                '\n' -> out.append("\\n")
                '\r' -> out.append("\\r")
                '\t' -> out.append("\\t")
                '\b' -> out.append("\\b")
                '\u000C' -> out.append("\\f")
                else -> if (c < ' ') out.append("\\u%04x".format(c.code)) else out.append(c)
            }
        }
        out.append('"')
    }
}
