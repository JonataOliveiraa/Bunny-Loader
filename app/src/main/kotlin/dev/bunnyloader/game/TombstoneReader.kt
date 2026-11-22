package dev.bunnyloader.game

import java.io.InputStream

object TombstoneReader {
    private const val MAX_FRAMES = 32
    private const val MAX_LOGS = 25

    private class Field(val number: Int, val value: Long, val data: ByteArray?)

    private class Message(fields: List<Field>) {
        private val byNumber = fields.groupBy { it.number }
        fun long(n: Int): Long? = byNumber[n]?.lastOrNull()?.value
        fun string(n: Int): String? = byNumber[n]?.lastOrNull()?.data?.let { String(it, Charsets.UTF_8) }
        fun strings(n: Int): List<String> = byNumber[n].orEmpty().mapNotNull { f -> f.data?.let { String(it, Charsets.UTF_8) } }
        fun message(n: Int): Message? = byNumber[n]?.lastOrNull()?.data?.let { Message(parse(it)) }
        fun messages(n: Int): List<Message> = byNumber[n].orEmpty().mapNotNull { f -> f.data?.let { Message(parse(it)) } }
    }

    fun read(stream: InputStream, indent: String = "   "): String {
        val bytes = stream.use { it.readBytes() }
        if (bytes.isEmpty()) return "${indent}(empty tombstone)"
        if (looksLikeText(bytes)) return fromText(String(bytes, Charsets.UTF_8), indent)
        return runCatching { fromProto(bytes, indent) }
            .getOrElse { "${indent}(unreadable tombstone: ${it.javaClass.simpleName})" }
    }

    private fun looksLikeText(bytes: ByteArray): Boolean {
        val head = String(bytes, 0, minOf(bytes.size, 256), Charsets.ISO_8859_1)
        return head.startsWith("*** ***") || head.contains("Build fingerprint:")
    }

    private fun fromText(text: String, indent: String): String {
        val frame = Regex("""^\s+#\d\d """)
        val useful = text.lineSequence()
            .filter {
                it.startsWith("signal ") || it.contains("Abort message") ||
                    it.contains("Cause:") || it.startsWith("pid: ") || frame.containsMatchIn(it)
            }
            .take(MAX_FRAMES + 4)
            .joinToString("\n") { indent + it.trim() }
        return useful.ifBlank { "${indent}(tombstone has no useful lines)" }
    }

    private fun fromProto(bytes: ByteArray, indent: String): String {
        val root = Message(parse(bytes))
        val pid = root.long(5)
        val tid = root.long(6)
        return buildString {
            root.message(10)?.let { append(indent).append(signalLine(it)).append('\n') }
            root.string(14)?.takeIf { it.isNotBlank() }?.let {
                append(indent).append("Abort message: ").append(it.trim()).append('\n')
            }
            root.messages(15).mapNotNull { it.string(1) }.filter { it.isNotBlank() }.forEach {
                append(indent).append("Cause: ").append(it).append('\n')
            }
            val threads = root.messages(16).mapNotNull { entry ->
                val key = entry.long(1) ?: return@mapNotNull null
                val thread = entry.message(2) ?: return@mapNotNull null
                key to thread
            }
            val crashing = threads.firstOrNull { it.first == tid }?.second ?: threads.firstOrNull()?.second
            if (crashing != null) {
                append(indent).append("Thread: '").append(crashing.string(2).orEmpty())
                    .append("' tid ").append(crashing.long(1) ?: tid ?: 0)
                if (pid != null) append(" (pid ").append(pid).append(')')
                append('\n')
                crashing.strings(7).filter { it.isNotBlank() }.forEach {
                    append(indent).append("Note: ").append(it).append('\n')
                }
                val frames = crashing.messages(4)
                if (frames.isNotEmpty()) {
                    append(indent).append("Backtrace:\n")
                    frames.take(MAX_FRAMES).forEachIndexed { i, f ->
                        append(indent).append("  ").append(frameLine(i, f)).append('\n')
                    }
                    if (frames.size > MAX_FRAMES) {
                        append(indent).append("  (+").append(frames.size - MAX_FRAMES).append(" more frames)\n")
                    }
                }
            }
            val logs = root.messages(18).flatMap { it.messages(2) }
                .filter { (it.long(4) ?: 0) >= 5 }
                .takeLast(MAX_LOGS)
            if (logs.isNotEmpty()) {
                append(indent).append("Last warnings/errors before the crash:\n")
                logs.forEach { append(indent).append("  ").append(logLine(it)).append('\n') }
            }
        }.trimEnd().ifBlank { "${indent}(tombstone has no useful fields)" }
    }

    private fun signalLine(s: Message): String = buildString {
        append("Signal ").append(s.long(1) ?: 0)
        s.string(2)?.let { append(" (").append(it).append(')') }
        s.long(3)?.let { code ->
            append(", code ").append(code)
            s.string(4)?.let { append(" (").append(it).append(')') }
        }
        if ((s.long(8) ?: 0) != 0L) append(", fault addr 0x").append(java.lang.Long.toHexString(s.long(9) ?: 0))
    }

    private fun frameLine(index: Int, f: Message): String = buildString {
        append('#').append(index.toString().padStart(2, '0'))
        append(" pc ").append(java.lang.Long.toHexString(f.long(1) ?: 0).padStart(16, '0'))
        append("  ").append(shortFile(f.string(6).orEmpty()))
        val name = f.string(4).orEmpty()
        if (name.isNotEmpty()) {
            append(" (").append(name)
            val offset = f.long(5) ?: 0
            if (offset != 0L) append("+").append(offset)
            append(')')
        }
    }

    private fun shortFile(path: String): String = when {
        path.isEmpty() -> "<unknown>"
        path.startsWith("[") -> path
        path.startsWith("/data/") || path.startsWith("/storage/") -> path.substringAfterLast('/')
        else -> path
    }

    private fun logLine(m: Message): String {
        val level = when (m.long(4)?.toInt()) {
            5 -> 'W'; 6 -> 'E'; 7 -> 'F'; else -> '?'
        }
        val time = m.string(1).orEmpty().substringAfter(' ').substringBefore('.')
        return "$time $level/${m.string(5).orEmpty()}: ${m.string(6).orEmpty().trim()}"
    }

    private fun parse(buf: ByteArray): List<Field> {
        val out = ArrayList<Field>()
        var p = 0
        fun varint(): Long {
            var result = 0L
            var shift = 0
            while (true) {
                require(p < buf.size)
                val b = buf[p++].toInt()
                result = result or ((b and 0x7F).toLong() shl shift)
                if (b and 0x80 == 0) return result
                shift += 7
                require(shift < 64)
            }
        }
        fun fixed(size: Int): Long {
            require(p + size <= buf.size)
            var v = 0L
            for (i in 0 until size) v = v or ((buf[p + i].toLong() and 0xFF) shl (8 * i))
            p += size
            return v
        }
        while (p < buf.size) {
            val key = varint()
            val number = (key ushr 3).toInt()
            when ((key and 7).toInt()) {
                0 -> out += Field(number, varint(), null)
                1 -> out += Field(number, fixed(8), null)
                2 -> {
                    val len = varint()
                    require(len >= 0 && p + len <= buf.size)
                    out += Field(number, 0, buf.copyOfRange(p, p + len.toInt()))
                    p += len.toInt()
                }
                5 -> out += Field(number, fixed(4), null)
                else -> throw IllegalArgumentException("wire type ${key and 7}")
            }
        }
        return out
    }
}
