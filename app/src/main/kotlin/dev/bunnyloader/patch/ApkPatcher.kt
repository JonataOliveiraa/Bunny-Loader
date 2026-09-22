package dev.bunnyloader.patch

import android.content.Context
import com.android.apksig.ApkSigner
import java.io.BufferedOutputStream
import java.io.File
import java.io.RandomAccessFile
import java.security.KeyFactory
import java.security.cert.CertificateFactory
import java.security.cert.X509Certificate
import java.security.spec.PKCS8EncodedKeySpec
import java.util.zip.CRC32
import java.util.zip.Deflater
import java.util.zip.ZipEntry
import java.util.zip.ZipFile

/**
 * Cria, NO APARELHO e RÁPIDO, uma cópia modificada do Terraria instalado.
 *
 * Chave da velocidade: copia os dados JÁ COMPRIMIDOS crus (sem re-inflar/re-
 * deflacionar os ~200MB). Só toca nos poucos arquivos que mudam:
 *   - libmain.so     -> injeta DT_NEEDED libbunny.so (ElfPatch)
 *   - AndroidManifest.xml + resources.arsc -> renomeia o pacote (coexiste)
 *   - + adiciona libbunny.so e libshadowhook.so
 * Depois assina (apksig). Não redistribui o Terraria — usa a cópia do usuário.
 */
object ApkPatcher {
    const val TERRARIA = "com.and.games505.TerrariaPaid"
    const val NEW_PKG = "com.bunnyloader.terraria.paid"  // MESMO tamanho (29)
    private const val ABI = "arm64-v8a"

    data class Progress(val step: String, val pct: Int)

    fun build(ctx: Context, onProgress: (Progress) -> Unit = {}): File {
        val pm = ctx.packageManager
        val gameApk = pm.getApplicationInfo(TERRARIA, 0).sourceDir
        val libDir = ctx.applicationInfo.nativeLibraryDir
        val libbunny = File(libDir, "libbunny.so")
        val libshadow = File(libDir, "libshadowhook.so")
        require(libbunny.exists() && libshadow.exists()) { "libs do loader ausentes em $libDir" }

        val unsigned = File(ctx.cacheDir, "unsigned.apk")
        val signed = File(ctx.getExternalFilesDir(null), "terraria-bunny.apk")

        val t0 = System.currentTimeMillis()
        onProgress(Progress("Lendo Terraria", 5))
        RawApk.rebuild(gameApk, unsigned, ABI, modifiers(gameApk), extra(libbunny, libshadow), onProgress)
        val t1 = System.currentTimeMillis()
        onProgress(Progress("Assinando", 90))
        sign(ctx, unsigned, signed)
        val t2 = System.currentTimeMillis()
        unsigned.delete()
        android.util.Log.i("BunnyLoader",
            "patch: rebuild=${t1 - t0}ms assinar=${t2 - t1}ms total=${t2 - t0}ms -> ${signed.length()} bytes")
        onProgress(Progress("Pronto", 100))
        return signed
    }

    /** Bytes novos para as entradas que mudam (calculados sob demanda). */
    private fun modifiers(gameApk: String): Map<String, () -> ByteArray> = mapOf(
        "lib/$ABI/libmain.so" to {
            ZipFile(gameApk).use { z ->
                ElfPatch.addNeeded(z.getInputStream(z.getEntry("lib/$ABI/libmain.so")).readBytes(), "libbunny.so")
            }
        },
        "AndroidManifest.xml" to { renamePkg(readEntry(gameApk, "AndroidManifest.xml")) },
        "resources.arsc" to { renamePkg(readEntry(gameApk, "resources.arsc")) },
    )

    private fun extra(libbunny: File, libshadow: File): Map<String, File> = mapOf(
        "lib/$ABI/libbunny.so" to libbunny,
        "lib/$ABI/libshadowhook.so" to libshadow,
    )

    private fun readEntry(apk: String, name: String): ByteArray =
        ZipFile(apk).use { it.getInputStream(it.getEntry(name)).readBytes() }

    private fun renamePkg(data: ByteArray): ByteArray {
        var d = replaceBytes(data, TERRARIA.toByteArray(Charsets.US_ASCII), NEW_PKG.toByteArray(Charsets.US_ASCII))
        d = replaceBytes(d, TERRARIA.toByteArray(Charsets.UTF_16LE), NEW_PKG.toByteArray(Charsets.UTF_16LE))
        return d
    }

    private fun replaceBytes(src: ByteArray, from: ByteArray, to: ByteArray): ByteArray {
        require(from.size == to.size)
        val out = src.copyOf()
        var i = indexOf(out, from, 0)
        while (i >= 0) { System.arraycopy(to, 0, out, i, to.size); i = indexOf(out, from, i + to.size) }
        return out
    }

    private fun indexOf(hay: ByteArray, needle: ByteArray, start: Int): Int {
        outer@ for (i in start..hay.size - needle.size) {
            for (j in needle.indices) if (hay[i + j] != needle[j]) continue@outer
            return i
        }
        return -1
    }

    private fun sign(ctx: Context, input: File, output: File) {
        // Chave (PKCS8 DER) + cert (DER) crus nos assets — evita o problema de
        // MAC do KeyStore PKCS12 no BouncyCastle do Android.
        val key = KeyFactory.getInstance("RSA").generatePrivate(
            PKCS8EncodedKeySpec(ctx.assets.open("signing.key").use { it.readBytes() }))
        val cert = ctx.assets.open("signing.crt").use {
            CertificateFactory.getInstance("X.509").generateCertificate(it) as X509Certificate
        }
        val cfg = ApkSigner.SignerConfig.Builder("bunny", key, listOf(cert)).build()
        if (output.exists()) output.delete()
        ApkSigner.Builder(listOf(cfg))
            .setInputApk(input).setOutputApk(output)
            .setV1SigningEnabled(false).setV2SigningEnabled(true)
            .build().sign()
    }
}
