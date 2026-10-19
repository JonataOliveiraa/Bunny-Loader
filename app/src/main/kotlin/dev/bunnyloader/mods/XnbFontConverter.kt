package dev.bunnyloader.mods

import android.graphics.Bitmap
import java.io.File
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

/**
 * Converte as fontes de um resource pack do PC (Content/Fonts/<Fonte>.xnb, ReLogic
 * DynamicSpriteFont) para o formato que o nucleo le: <Fonte>.json (blfont/1)
 * + paginas PNG de ate 2048, na resolucao original (scale 1). E o mesmo que
 * `tools/mods/fonts.py xnb` faz.
 */
internal object XnbFontConverter {
    private const val FIELDS = "code,page,bx,by,bw,bh,cx,cy,cw,ch,leftBearing,width,rightBearing"
    private const val PAGE = 2048
    private const val PADDING = 2

    /** As fontes do jogo que um pacote pode trocar (o nome do .xnb do PC). */
    private val TARGETS = listOf("Mouse_Text", "Item_Stack", "Death_Text", "Combat_Text", "Combat_Crit")

    class Page(val width: Int, val height: Int, val rgba: ByteArray)

    class Glyph(val code: Int, val page: Int, val bounds: IntArray, val cropping: IntArray, val kerning: FloatArray)

    class Font(val spacing: Float, val lineSpacing: Int, val pages: List<Page>, val glyphs: List<Glyph>)

    /**
     * Converte os .xnb de [folder] no lugar e apaga os originais convertidos.
     * Devolve quantas fontes ficaram prontas e o motivo de cada uma que falhou.
     */
    fun convertFolder(folder: File): Pair<Int, List<String>> {
        var converted = 0
        val failures = mutableListOf<String>()
        val files = folder.listFiles().orEmpty().filter { it.isFile && it.extension.equals("xnb", ignoreCase = true) }
        for (file in files) {
            val name = TARGETS.firstOrNull { canonical(it) == canonical(file.nameWithoutExtension) }
            if (name == null) {
                failures += "${file.name}: not a game font"
                continue
            }
            runCatching { write(read(file.readBytes()), folder, name) }
                .onSuccess { converted++; file.delete() }
                .onFailure { failures += "${file.name}: ${it.message}" }
        }
        return converted to failures
    }

    private fun canonical(name: String) = name.lowercase().filter { it != '_' && it != ' ' && it != '-' }

    private fun write(font: Font, folder: File, name: String) {
        val packed = pack(font)
        val pages = packed.pageHeights.mapIndexed { index, height ->
            val file = "$name.$index.png"
            File(folder, file).outputStream().use { out ->
                drawPage(font, packed, index, height).compress(Bitmap.CompressFormat.PNG, 100, out)
            }
            file
        }
        val glyphs = font.glyphs.mapIndexed { i, g ->
            val spot = packed.spots[i]
            JsonArray(
                listOf(g.code, spot[0], spot[1], spot[2], g.bounds[2], g.bounds[3]).map(::JsonPrimitive) +
                    g.cropping.map(::JsonPrimitive) +
                    g.kerning.map { JsonPrimitive(Math.round(it * 1000) / 1000.0) },
            )
        }
        val meta = buildJsonObject {
            put("format", "blfont/1")
            put("scale", 1)
            put("lineSpacing", font.lineSpacing)
            put("spacing", font.spacing)
            put("pages", JsonArray(pages.map(::JsonPrimitive)))
            put("fields", FIELDS)
            put("glyphs", JsonArray(glyphs))
        }
        File(folder, "$name.json").writeText(meta.toString())
    }

    /** [page, x, y] de cada glifo na pagina nova; glifo vazio fica em [0, 0, 0]. */
    private class Packed(val spots: Array<IntArray>, val pageHeights: List<Int>)

    /**
     * Reempacota os glifos em paginas de ate PAGE x PAGE, em prateleiras. Um
     * pacote com CJK tem ~100 paginas pequenas no XNB: cada uma viraria uma
     * textura (o jogo congelava segundos ao aplicar) e quebraria o lote de
     * desenho a cada troca de pagina.
     */
    private fun pack(font: Font): Packed {
        val spots = Array(font.glyphs.size) { IntArray(3) }
        val heights = mutableListOf<Int>()
        var x = PADDING
        var y = PADDING
        var shelf = 0
        var used = 0
        val order = font.glyphs.indices.filter { font.glyphs[it].bounds[2] > 0 && font.glyphs[it].bounds[3] > 0 }
            .sortedWith(compareBy({ -font.glyphs[it].bounds[3] }, { -font.glyphs[it].bounds[2] }))
        for (i in order) {
            val w = font.glyphs[i].bounds[2]
            val h = font.glyphs[i].bounds[3]
            require(w + 2 * PADDING <= PAGE && h + 2 * PADDING <= PAGE) { "glyph ${w}x$h is too big" }
            if (x + w + PADDING > PAGE) {
                x = PADDING
                y += shelf + PADDING
                shelf = 0
            }
            if (y + h + PADDING > PAGE) {
                heights += used
                x = PADDING
                y = PADDING
                shelf = 0
            }
            spots[i][0] = heights.size
            spots[i][1] = x
            spots[i][2] = y
            x += w + PADDING
            shelf = maxOf(shelf, h)
            used = y + shelf + PADDING
        }
        heights += maxOf(used, 1)
        return Packed(spots, heights)
    }

    /** A pagina nova: RGBA pre-multiplicado do XNB -> ARGB direto (glifo branco com transparencia). */
    private fun drawPage(font: Font, packed: Packed, index: Int, height: Int): Bitmap {
        val pixels = IntArray(PAGE * height) { 0x00FFFFFF }
        for ((i, g) in font.glyphs.withIndex()) {
            val spot = packed.spots[i]
            if (spot[0] != index || g.bounds[2] <= 0 || g.bounds[3] <= 0) continue
            val source = font.pages[g.page]
            for (row in 0 until g.bounds[3]) for (col in 0 until g.bounds[2]) {
                val o = ((g.bounds[1] + row) * source.width + g.bounds[0] + col) * 4
                val a = source.rgba[o + 3].toInt() and 0xFF
                if (a == 0) continue
                val r = unpremultiply(source.rgba[o].toInt() and 0xFF, a)
                val gr = unpremultiply(source.rgba[o + 1].toInt() and 0xFF, a)
                val b = unpremultiply(source.rgba[o + 2].toInt() and 0xFF, a)
                pixels[(spot[2] + row) * PAGE + spot[1] + col] = (a shl 24) or (r shl 16) or (gr shl 8) or b
            }
        }
        val bitmap = Bitmap.createBitmap(PAGE, height, Bitmap.Config.ARGB_8888)
        bitmap.isPremultiplied = false
        bitmap.setPixels(pixels, 0, PAGE, 0, 0, PAGE, height)
        return bitmap
    }

    private fun unpremultiply(c: Int, a: Int) = minOf(255, (c * 255 + a / 2) / a)

    // ---- Leitura do XNB ----

    private class Reader(val data: ByteArray, var pos: Int) {
        private val buffer = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN)

        fun take(n: Int): ByteArray {
            require(n >= 0 && pos + n <= data.size) { "truncated XNB" }
            return data.copyOfRange(pos, pos + n).also { pos += n }
        }

        fun u8(): Int {
            require(pos < data.size) { "truncated XNB" }
            return data[pos++].toInt() and 0xFF
        }

        fun i32(): Int {
            require(pos + 4 <= data.size) { "truncated XNB" }
            return buffer.getInt(pos).also { pos += 4 }
        }

        fun f32(): Float = java.lang.Float.intBitsToFloat(i32())

        fun var7(): Int {
            var value = 0
            var shift = 0
            while (true) {
                val b = u8()
                value = value or ((b and 0x7F) shl shift)
                shift += 7
                if (b and 0x80 == 0) return value
                require(shift < 35) { "bad XNB length" }
            }
        }

        fun text(): String = String(take(var7()), Charsets.UTF_8)

        /** Um char gravado pelo BinaryWriter: UTF-8 de 1 a 4 bytes. */
        fun char(): Int {
            require(pos < data.size) { "truncated XNB" }
            val first = data[pos].toInt() and 0xFF
            val n = if (first < 0x80) 1 else if (first < 0xE0) 2 else if (first < 0xF0) 3 else 4
            return String(take(n), Charsets.UTF_8).codePointAt(0)
        }

        fun count(): Int = i32().also { require(it in 0..0x10000) { "bad XNB list size" } }
    }

    fun read(data: ByteArray): Font {
        require(data.size > 10 && data[0] == 'X'.code.toByte() && data[1] == 'N'.code.toByte() && data[2] == 'B'.code.toByte()) {
            "not an XNB file"
        }
        require(data[4].toInt() == 5) { "XNB version ${data[4]} (expected 5)" }
        require(data[5].toInt() and 0xC0 == 0) { "compressed XNB; decompress it first (e.g. with xnbcli)" }
        val r = Reader(data, 10)
        val readers = List(r.var7()) { r.text().also { r.i32() } }
        require(readers.firstOrNull()?.contains("DynamicSpriteFontReader") == true) { "not a ReLogic DynamicSpriteFont" }
        r.var7()   // recursos compartilhados
        r.var7()   // leitor do objeto principal
        val spacing = r.f32()
        val lineSpacing = r.i32()
        r.char()   // caractere padrao
        val pages = mutableListOf<Page>()
        val glyphs = mutableListOf<Glyph>()
        val seen = HashSet<Int>()
        repeat(r.count()) { index ->
            r.var7()
            val format = r.i32()
            val width = r.i32()
            val height = r.i32()
            val mips = r.i32()
            require(width in 1..8192 && height in 1..8192) { "bad texture size ${width}x$height" }
            val pixels = r.take(r.i32())
            repeat(mips - 1) { r.take(r.i32()) }
            pages += Page(width, height, decode(pixels, width, height, format))

            r.var7()
            val bounds = List(r.count()) { IntArray(4) { r.i32() } }
            r.var7()
            val cropping = List(r.count()) { IntArray(4) { r.i32() } }
            r.var7()
            val chars = List(r.count()) { r.char() }
            r.var7()
            val kerning = List(r.count()) { FloatArray(3) { r.f32() } }
            for (i in chars.indices) {
                val code = chars[i]
                if (i >= bounds.size || i >= cropping.size || i >= kerning.size) break
                if (code <= 0 || code >= 0xFFFF || !seen.add(code)) continue
                val (x, y, w, h) = bounds[i].toList()
                if (x < 0 || y < 0 || w < 0 || h < 0 || x + w > width || y + h > height) continue
                glyphs += Glyph(code, index, bounds[i], cropping[i], kerning[i])
            }
        }
        require(pages.isNotEmpty() && glyphs.isNotEmpty()) { "font without glyphs" }
        return Font(spacing, lineSpacing, pages, glyphs)
    }

    // ---- Texturas: SurfaceFormat 0 Color, 4 Dxt1, 5 Dxt3, 6 Dxt5 ----

    private fun decode(data: ByteArray, width: Int, height: Int, format: Int): ByteArray = when (format) {
        0 -> {
            require(data.size >= width * height * 4) { "truncated texture" }
            data.copyOf(width * height * 4)
        }
        4, 5, 6 -> decodeDxt(data, width, height, format)
        else -> throw IllegalArgumentException("texture format $format not supported")
    }

    private fun decodeDxt(data: ByteArray, width: Int, height: Int, format: Int): ByteArray {
        val blockSize = if (format == 4) 8 else 16
        val bw = (width + 3) / 4
        val bh = (height + 3) / 4
        require(data.size >= bw * bh * blockSize) { "truncated texture" }
        val out = ByteArray(width * height * 4)
        val palette = Array(4) { IntArray(4) }
        val alpha = IntArray(16)
        for (by in 0 until bh) for (bx in 0 until bw) {
            val base = (by * bw + bx) * blockSize
            val color = base + blockSize - 8
            val c0 = u16(data, color)
            val c1 = u16(data, color + 2)
            rgb565(c0, palette[0])
            rgb565(c1, palette[1])
            val punch = format == 4 && c0 <= c1
            for (k in 0..2) {
                if (punch) {
                    palette[2][k] = (palette[0][k] + palette[1][k]) / 2
                    palette[3][k] = 0
                } else {
                    palette[2][k] = (2 * palette[0][k] + palette[1][k]) / 3
                    palette[3][k] = (palette[0][k] + 2 * palette[1][k]) / 3
                }
            }
            val bits = u32(data, color + 4)
            when (format) {
                4 -> for (i in 0 until 16) alpha[i] = if (punch && ((bits ushr (2 * i)) and 3L) == 3L) 0 else 255
                5 -> for (i in 0 until 16) alpha[i] = ((data[base + i / 2].toInt() ushr (4 * (i % 2))) and 15) * 17
                else -> dxt5Alpha(data, base, alpha)
            }
            for (i in 0 until 16) {
                val x = bx * 4 + i % 4
                val y = by * 4 + i / 4
                if (x >= width || y >= height) continue
                val p = palette[((bits ushr (2 * i)) and 3L).toInt()]
                val o = (y * width + x) * 4
                out[o] = p[0].toByte()
                out[o + 1] = p[1].toByte()
                out[o + 2] = p[2].toByte()
                out[o + 3] = alpha[i].toByte()
            }
        }
        return out
    }

    private fun dxt5Alpha(data: ByteArray, base: Int, alpha: IntArray) {
        val a0 = data[base].toInt() and 0xFF
        val a1 = data[base + 1].toInt() and 0xFF
        val table = IntArray(8)
        table[0] = a0
        table[1] = a1
        if (a0 > a1) {
            for (i in 1..6) table[i + 1] = ((7 - i) * a0 + i * a1) / 7
        } else {
            for (i in 1..4) table[i + 1] = ((5 - i) * a0 + i * a1) / 5
            table[6] = 0
            table[7] = 255
        }
        var bits = 0L
        for (i in 0 until 6) bits = bits or ((data[base + 2 + i].toLong() and 0xFF) shl (8 * i))
        for (i in 0 until 16) alpha[i] = table[((bits ushr (3 * i)) and 7L).toInt()]
    }

    private fun rgb565(c: Int, out: IntArray) {
        val r = (c shr 11) and 31
        val g = (c shr 5) and 63
        val b = c and 31
        out[0] = (r shl 3) or (r shr 2)
        out[1] = (g shl 2) or (g shr 4)
        out[2] = (b shl 3) or (b shr 2)
    }

    private fun u16(d: ByteArray, i: Int) = (d[i].toInt() and 0xFF) or ((d[i + 1].toInt() and 0xFF) shl 8)

    private fun u32(d: ByteArray, i: Int): Long = (u16(d, i).toLong()) or (u16(d, i + 2).toLong() shl 16)
}
