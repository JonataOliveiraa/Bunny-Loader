package dev.bunnyloader.mods

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * O decodificador contra o Pillow: os GIFs e os quadros esperados saem de
 * `resources/gif/make.py`, montados por um leitor de GIF de verdade.
 */
class GifDecoderTest {
    private fun bytes(name: String): ByteArray =
        javaClass.classLoader!!.getResourceAsStream("gif/$name")!!.use { it.readBytes() }

    private fun check(name: String) {
        val (head, delayLine) = bytes("$name.txt").decodeToString().trim().lines()
        val (w, h, n) = head.split(" ").map { it.toInt() }
        val gif = requireNotNull(GifDecoder.decode(bytes("$name.gif"))) { "$name não decodificou" }
        assertEquals(w, gif.width)
        assertEquals(h, gif.height)
        assertEquals(n, gif.frames.size)
        // Quadro sem tempo (o Pillow diz 0) fica 100 ms, como nos navegadores.
        val delays = delayLine.split(" ").map { it.toInt().let { d -> if (d <= 10) 100 else d } }
        assertArrayEquals(delays.toIntArray(), gif.delaysMs)

        val expected = bytes("$name.rgba")
        for ((f, frame) in gif.frames.withIndex()) {
            for (i in 0 until w * h) {
                val o = (f * w * h + i) * 4
                val a = expected[o + 3].toInt() and 0xFF
                val px = frame[i]
                if (a == 0) {
                    if (px ushr 24 != 0) fail("$name quadro $f pixel $i: devia ser transparente, veio ${Integer.toHexString(px)}")
                    continue
                }
                val want = (a shl 24) or ((expected[o].toInt() and 0xFF) shl 16) or
                    ((expected[o + 1].toInt() and 0xFF) shl 8) or (expected[o + 2].toInt() and 0xFF)
                if (px != want) {
                    fail("$name quadro $f pixel (${i % w}, ${i / w}): ${Integer.toHexString(want)} esperado, veio ${Integer.toHexString(px)}")
                }
            }
        }
    }

    @Test fun partialFramesWithTransparencyAndRestorePrevious() = check("restore")

    @Test fun clearToBackgroundBetweenFrames() = check("sprite")

    @Test fun manyColorsGrowAndResetTheLzwTable() = check("noise")

    @Test fun interlacedRowsGoBackInPlace() = check("interlaced")

    @Test fun interlacedRowOrder() {
        // Altura 10: passada 1 (0, 8), 2 (4), 3 (2, 6), 4 (1, 3, 5, 7, 9).
        assertEquals(listOf(0, 8, 4, 2, 6, 1, 3, 5, 7, 9), (0 until 10).map { GifDecoder.interlacedRow(it, 10) })
    }

    @Test fun notAGifOrCutShort() {
        assertNull(GifDecoder.decode(ByteArray(0)))
        assertNull(GifDecoder.decode("\u0089PNG\r\n\u001a\n".toByteArray(Charsets.ISO_8859_1)))
        val gif = bytes("sprite.gif")
        assertNull(GifDecoder.decode(gif.copyOf(20)))
    }

    @Test fun cutAfterTheFirstFrameKeepsWhatCameWhole() {
        val gif = bytes("sprite.gif")
        val partial = requireNotNull(GifDecoder.decode(gif.copyOf(gif.size - 60)))
        assertTrue("${partial.frames.size} quadros", partial.frames.isNotEmpty() && partial.frames.size < 4)
    }

    @Test fun refusesAHugeCanvas() {
        val gif = bytes("sprite.gif").copyOf()
        gif[6] = 0x10; gif[7] = 0x27 // largura 10000
        assertNull(GifDecoder.decode(gif))
    }
}
