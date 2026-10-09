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

    @Test fun reducedFramesPreserveCompositingTransparencyAndTiming() {
        for (name in listOf("restore", "sprite", "interlaced", "noise")) {
            val full = requireNotNull(GifDecoder.decode(bytes("$name.gif")))
            val side = maxOf(full.width, full.height) / 2
            val small = requireNotNull(GifDecoder.decode(bytes("$name.gif"), maxOutputSide = side))
            val sample = (maxOf(full.width, full.height) + side - 1) / side
            assertTrue(small.width <= side && small.height <= side)
            assertEquals(full.frames.size, small.frames.size)
            assertArrayEquals(full.delaysMs, small.delaysMs)
            for (f in full.frames.indices) {
                for (y in 0 until small.height) for (x in 0 until small.width) {
                    assertEquals(full.frames[f][y * sample * full.width + x * sample], small.frames[f][y * small.width + x])
                }
            }
        }
    }

    @Test fun outputBudgetStopsBeforeAllocatingExtraFrames() {
        val full = requireNotNull(GifDecoder.decode(bytes("restore.gif")))
        val limited = requireNotNull(GifDecoder.decode(bytes("restore.gif"), maxOutputPixels = full.width * full.height))
        assertEquals(1, limited.frames.size)
        assertArrayEquals(full.frames[0], limited.frames[0])
        assertNull(GifDecoder.decode(bytes("restore.gif"), maxOutputPixels = 1))
    }

    @Test fun malformedFrameCannotAllocateBeyondCanvasLimit() {
        val gif = bytes("sprite.gif").copyOf()
        val packed = gif[10].toInt() and 0xFF
        var frame = 13 + if (packed and 0x80 != 0) 3 * (2 shl (packed and 7)) else 0
        while (gif[frame] == 0x21.toByte()) {
            frame += 2
            while (gif[frame] != 0.toByte()) frame += 1 + (gif[frame].toInt() and 0xFF)
            frame++
        }
        assertEquals(0x2C.toByte(), gif[frame])
        gif[frame + 5] = 0xFF.toByte()
        gif[frame + 6] = 0x7F
        gif[frame + 7] = 0xFF.toByte()
        gif[frame + 8] = 0x7F
        assertNull(GifDecoder.decode(gif))
    }

    @Test fun tinyFramesCannotCreateAnUnboundedObjectList() {
        val header = "GIF89a".toByteArray() + byteArrayOf(1, 0, 1, 0, 0x80.toByte(), 0, 0, 0, 0, 0, -1, -1, -1)
        val frame = byteArrayOf(0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 1, 0)
        val bytes = java.io.ByteArrayOutputStream().apply {
            write(header)
            repeat(1000) { write(frame) }
            write(0x3B)
        }.toByteArray()
        val gif = requireNotNull(GifDecoder.decode(bytes))
        assertEquals(256, gif.frames.size)
        assertEquals(256, gif.delaysMs.size)
    }
}
