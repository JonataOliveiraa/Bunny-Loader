package dev.bunnyloader.mods

import java.io.ByteArrayOutputStream

/**
 * Um GIF já montado: cada quadro é a tela inteira (ARGB), do jeito que deve
 * aparecer, com o tempo que fica na tela.
 */
class GifImage(val width: Int, val height: Int, val frames: List<IntArray>, val delaysMs: IntArray)

/**
 * Lê GIF (87a e 89a) para o ícone animado do mod (`icon.gif`).
 *
 * Feito aqui, e não com o ImageDecoder do Android, por dois motivos: o
 * ImageDecoder só existe do Android 9 em diante (o app vai até o 7), e o
 * quadro sai como pixels para o launcher desenhar sem filtro — arte de pixel
 * continua quadrada, como no ícone PNG.
 *
 * Monta os quadros como o navegador: tabela de cores global ou do quadro,
 * transparência, entrelaçamento e os descartes 0-3 (manter, limpar o
 * retângulo, voltar ao anterior). O GIF vem de um pacote de terceiros: tela
 * grande demais é recusada, e passado o limite de pixels os quadros que
 * sobram ficam de fora (a animação recomeça antes, mas o app não estoura a
 * memória).
 */
object GifDecoder {
    /** Lado máximo da tela do GIF: um ícone não precisa de mais. */
    const val MAX_SIDE = 512

    /** Soma de pixels de todos os quadros (~16 MB em ARGB). */
    const val MAX_TOTAL_PIXELS = 4_000_000

    /** Quadro sem tempo (ou de 10 ms) fica 100 ms, como nos navegadores. */
    private const val DEFAULT_DELAY_MS = 100

    fun isGif(bytes: ByteArray): Boolean =
        bytes.size >= 6 && bytes[0] == 'G'.code.toByte() && bytes[1] == 'I'.code.toByte() &&
            bytes[2] == 'F'.code.toByte()

    /** null quando não é GIF, está cortado antes do primeiro quadro ou é grande demais. */
    fun decode(bytes: ByteArray, maxOutputSide: Int = MAX_SIDE, maxOutputPixels: Int = MAX_TOTAL_PIXELS): GifImage? {
        require(maxOutputSide > 0 && maxOutputPixels > 0)
        return runCatching { Reader(bytes, minOf(maxOutputSide, MAX_SIDE), maxOutputPixels).read() }.getOrNull()
    }

    private class Reader(private val b: ByteArray, private val maxOutputSide: Int, private val maxOutputPixels: Int) {
        private var pos = 0

        private fun u8(): Int {
            if (pos >= b.size) throw IllegalStateException("GIF cortado")
            return b[pos++].toInt() and 0xFF
        }

        private fun u16(): Int = u8() or (u8() shl 8)

        private fun colorTable(size: Int): IntArray = IntArray(size) {
            0xFF000000.toInt() or (u8() shl 16) or (u8() shl 8) or u8()
        }

        /** Os sub-blocos (tamanho + dados, até um de tamanho 0) num só. */
        private fun subBlocks(keep: Boolean): ByteArray? {
            val out = if (keep) ByteArrayOutputStream() else null
            while (true) {
                val n = u8()
                if (n == 0) break
                if (pos + n > b.size) throw IllegalStateException("GIF cortado")
                out?.write(b, pos, n)
                pos += n
            }
            return out?.toByteArray()
        }

        fun read(): GifImage? {
            if (!isGif(b)) return null
            pos = 6
            val w = u16()
            val h = u16()
            if (w <= 0 || h <= 0 || w > MAX_SIDE || h > MAX_SIDE) return null
            val sample = ((maxOf(w, h) + maxOutputSide - 1) / maxOutputSide).coerceAtLeast(1)
            val ow = ((w + sample - 1) / sample).coerceAtLeast(1)
            val oh = ((h + sample - 1) / sample).coerceAtLeast(1)
            val packed = u8()
            u8() // cor de fundo: o fundo do ícone é transparente
            u8() // proporção do pixel
            val global = if (packed and 0x80 != 0) colorTable(2 shl (packed and 7)) else null

            val frames = ArrayList<IntArray>()
            val delays = ArrayList<Int>()
            val canvas = IntArray(w * h)
            var total = 0

            // Do Graphic Control Extension: vale para o próximo quadro.
            var delay = 0
            var dispose = 0
            var transparent = -1

            // GIF cortado no meio fica com os quadros que vieram inteiros.
            loop@ while (pos < b.size) {
                try {
                    when (u8()) {
                        0x21 -> {
                            val label = u8()
                            if (label == 0xF9) {
                                val block = subBlocks(true)!!
                                if (block.size >= 4) {
                                    val p = block[0].toInt() and 0xFF
                                    dispose = (p shr 2) and 7
                                    delay = ((block[1].toInt() and 0xFF) or ((block[2].toInt() and 0xFF) shl 8)) * 10
                                    transparent = if (p and 1 != 0) block[3].toInt() and 0xFF else -1
                                }
                            } else {
                                subBlocks(false)
                            }
                        }
                        0x2C -> {
                            val fx = u16()
                            val fy = u16()
                            val fw = u16()
                            val fh = u16()
                            val fp = u8()
                            val table = if (fp and 0x80 != 0) colorTable(2 shl (fp and 7)) else global
                            val interlaced = fp and 0x40 != 0
                            val minCode = u8()
                            val data = subBlocks(true)!!
                            if (table == null || minCode !in 1..11) return finish(ow, oh, frames, delays)

                            if (total + w * h > MAX_TOTAL_PIXELS ||
                                (frames.size + 1) * ow * oh > maxOutputPixels || frames.size >= 256) break@loop
                            if (fw <= 0 || fh <= 0 || fw.toLong() * fh > MAX_SIDE * MAX_SIDE) break@loop
                            val before = if (dispose == 3) canvas.copyOf() else null
                            val indices = lzw(data, minCode, fw * fh)
                            draw(canvas, w, h, indices, fx, fy, fw, fh, table, transparent, interlaced)

                            frames += if (sample == 1) canvas.copyOf() else IntArray(ow * oh) { i ->
                                canvas[(i / ow * sample).coerceAtMost(h - 1) * w + (i % ow * sample).coerceAtMost(w - 1)]
                            }
                            delays += if (delay <= 10) DEFAULT_DELAY_MS else delay
                            total += w * h

                            when (dispose) {
                                2 -> clear(canvas, w, h, fx, fy, fw, fh)
                                3 -> before!!.copyInto(canvas)
                            }
                            delay = 0
                            dispose = 0
                            transparent = -1
                        }
                        0x3B -> break@loop
                        else -> break@loop
                    }
                } catch (_: IllegalStateException) {
                    break@loop
                }
            }
            return finish(ow, oh, frames, delays)
        }

        private fun finish(w: Int, h: Int, frames: List<IntArray>, delays: List<Int>): GifImage? =
            if (frames.isEmpty()) null else GifImage(w, h, frames, delays.toIntArray())
    }

    private fun draw(
        canvas: IntArray, w: Int, h: Int, indices: ByteArray,
        fx: Int, fy: Int, fw: Int, fh: Int,
        table: IntArray, transparent: Int, interlaced: Boolean,
    ) {
        for (row in 0 until fh) {
            val y = fy + if (interlaced) interlacedRow(row, fh) else row
            if (y >= h) continue
            for (col in 0 until fw) {
                val x = fx + col
                if (x >= w) break
                val index = indices[row * fw + col].toInt() and 0xFF
                if (index == transparent || index >= table.size) continue
                canvas[y * w + x] = table[index]
            }
        }
    }

    /** A linha de verdade da n-ésima linha guardada: passadas de 8, 8, 4 e 2. */
    internal fun interlacedRow(n: Int, height: Int): Int {
        val pass1 = (height + 7) / 8
        val pass2 = (height + 3) / 8
        val pass3 = (height + 1) / 4
        return when {
            n < pass1 -> n * 8
            n < pass1 + pass2 -> (n - pass1) * 8 + 4
            n < pass1 + pass2 + pass3 -> (n - pass1 - pass2) * 4 + 2
            else -> (n - pass1 - pass2 - pass3) * 2 + 1
        }
    }

    private fun clear(canvas: IntArray, w: Int, h: Int, fx: Int, fy: Int, fw: Int, fh: Int) {
        for (y in fy until minOf(fy + fh, h)) {
            for (x in fx until minOf(fx + fw, w)) canvas[y * w + x] = 0
        }
    }

    /**
     * O LZW do GIF: devolve um índice de cor por pixel. Dado que acaba antes
     * deixa o resto no índice 0, e código fora da tabela para a leitura, como
     * os navegadores fazem com GIF malformado.
     */
    internal fun lzw(data: ByteArray, minCodeSize: Int, pixels: Int): ByteArray {
        val out = ByteArray(pixels)
        val clear = 1 shl minCodeSize
        val end = clear + 1
        val prefix = IntArray(4096)
        val suffix = ByteArray(4096)
        val stack = ByteArray(4097)
        for (c in 0 until clear) suffix[c] = c.toByte()

        var codeSize = minCodeSize + 1
        var mask = (1 shl codeSize) - 1
        var avail = clear + 2
        var old = -1
        var first = 0
        var datum = 0
        var bits = 0
        var pos = 0
        var op = 0

        while (op < pixels) {
            while (bits < codeSize) {
                if (pos >= data.size) return out
                datum = datum or ((data[pos++].toInt() and 0xFF) shl bits)
                bits += 8
            }
            var code = datum and mask
            datum = datum ushr codeSize
            bits -= codeSize

            if (code == clear) {
                codeSize = minCodeSize + 1
                mask = (1 shl codeSize) - 1
                avail = clear + 2
                old = -1
                continue
            }
            if (code == end) break
            if (old == -1) {
                if (code >= clear) break
                out[op++] = suffix[code]
                old = code
                first = code
                continue
            }
            if (code > avail) break

            val current = code
            var sp = 0
            if (code == avail) {
                stack[sp++] = first.toByte()
                code = old
            }
            while (code >= clear) {
                stack[sp++] = suffix[code]
                code = prefix[code]
            }
            first = suffix[code].toInt() and 0xFF
            stack[sp++] = first.toByte()

            if (avail < 4096) {
                prefix[avail] = old
                suffix[avail] = first.toByte()
                avail++
                if (avail and mask == 0 && avail < 4096) {
                    codeSize++
                    mask = (1 shl codeSize) - 1
                }
            }
            old = current
            while (sp > 0 && op < pixels) out[op++] = stack[--sp]
        }
        return out
    }
}
