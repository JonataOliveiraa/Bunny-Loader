package dev.bunnyloader.game

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.util.Log
import java.io.File
import java.io.FileOutputStream
import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * Imagens dos efeitos de tela (graphics/TextureRegistry.cpp): o PNG do mod
 * vira RGBA cru para o GL, e a LUT neutra vira PNG para o modder editar.
 *
 * A Unity deste build nao entrega os pixels de uma textura sem as icalls que o
 * "Strip Engine Code" tirou; o BitmapFactory decodifica igual em qualquer
 * versao do Android.
 */
object ModImages {
    private const val TAG = "BunnyLoader"

    /**
     * [largura, altura] (int32 little-endian) + RGBA de 8 bits por pixel, alfa
     * reto (nao pre-multiplicado), linhas de cima para baixo. null se nao deu.
     */
    @JvmStatic
    fun decodeRgba(path: String): ByteArray? {
        val options = BitmapFactory.Options().apply {
            inPreferredConfig = Bitmap.Config.ARGB_8888
            inPremultiplied = false
            inScaled = false
        }
        val bitmap = try {
            BitmapFactory.decodeFile(path, options)
        } catch (e: Throwable) {
            Log.e(TAG, "efeitos de tela: imagem $path nao decodificou: $e")
            null
        } ?: return null

        try {
            val out = ByteBuffer.allocate(8 + bitmap.width * bitmap.height * 4).order(ByteOrder.LITTLE_ENDIAN)
            out.putInt(bitmap.width)
            out.putInt(bitmap.height)
            bitmap.copyPixelsToBuffer(out)
            return out.array()
        } finally {
            bitmap.recycle()
        }
    }

    /**
     * A LUT neutra em tira (size*size x size): cada fatia de size x size e um
     * azul, o vermelho cresce para a direita e o verde para BAIXO (a linha de
     * cima e verde 0, como as LUTs do ReShade). Editada num programa de foto e
     * salva, vira a gradacao do ScreenPresets.ColorGrade.
     */
    @JvmStatic
    fun saveNeutralLut(path: String, size: Int): Boolean {
        if (size < 2 || size > 64) return false
        val width = size * size
        val pixels = IntArray(width * size)
        val step = 255f / (size - 1)
        for (g in 0 until size) {
            for (b in 0 until size) {
                for (r in 0 until size) {
                    val red = Math.round(r * step)
                    val green = Math.round(g * step)
                    val blue = Math.round(b * step)
                    pixels[g * width + b * size + r] = (0xFF shl 24) or (red shl 16) or (green shl 8) or blue
                }
            }
        }
        return try {
            val bitmap = Bitmap.createBitmap(pixels, width, size, Bitmap.Config.ARGB_8888)
            File(path).parentFile?.mkdirs()
            FileOutputStream(path).use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
            bitmap.recycle()
            true
        } catch (e: Throwable) {
            Log.e(TAG, "efeitos de tela: LUT neutra em $path nao foi gravada: $e")
            false
        }
    }
}
