package dev.bunnyloader.catalog

import android.util.Base64
import java.security.MessageDigest

/** O token que o build pôs no BuildConfig, desembaralhado (a mesma máscara do catalog/build.gradle.kts). */
object EmbeddedToken {
    fun value(): String {
        val hidden = Base64.decode(BuildConfig.EMBEDDED_TOKEN, Base64.NO_WRAP)
        val mask = mask(hidden.size)
        return String(ByteArray(hidden.size) { (hidden[it].toInt() xor mask[it].toInt()).toByte() })
    }

    private fun mask(size: Int): ByteArray {
        val out = ByteArray(size)
        var block = "bunny-loader-catalog".toByteArray()
        var i = 0
        while (i < size) {
            block = MessageDigest.getInstance("SHA-256").digest(block)
            for (b in block) if (i < size) out[i++] = b
        }
        return out
    }
}
