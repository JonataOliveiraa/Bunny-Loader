package dev.bunnyloader.patch

import java.io.BufferedOutputStream
import java.io.OutputStream
import java.io.RandomAccessFile
import java.util.zip.CRC32
import java.util.zip.Deflater

/**
 * Reescreve um APK RÁPIDO: copia os dados já comprimidos CRUS (sem re-inflar/
 * re-deflacionar), tocando só nas entradas de `modifiers`. Escreve o zip na mão
 * pra controlar isso e o alinhamento (STORED a 4 bytes, ex.: resources.arsc).
 */
object RawApk {

    private class CEntry(
        val name: String, val method: Int, val crc: Long,
        val compSize: Long, val uncompSize: Long, val localOff: Long,
    )

    fun rebuild(
        sources: List<String>, out: java.io.File,
        modifiers: Map<String, () -> ByteArray>, extra: Map<String, java.io.File>,
        onProgress: (ApkPatcher.Progress) -> Unit,
    ) {
        val cw = CountingOutput(BufferedOutputStream(out.outputStream(), 1 shl 20))
        val central = ArrayList<ByteArray>()
        val written = HashSet<String>()
        var done = 0

        // Copia crua de cada fonte (base + splits), deduplicando por nome.
        for (src in sources) {
            RandomAccessFile(src, "r").use { raf ->
                for (e in readCentralDir(raf)) {
                    if (isOldSig(e.name) || e.name in written || e.name in modifiers) continue
                    if (e.name.endsWith("/")) continue  // pula diretórios
                    written.add(e.name)
                    raf.seek(e.localOff)
                    val lh = ByteArray(30); raf.readFully(lh)
                    val dataOff = e.localOff + 30 + u16(lh, 26) + u16(lh, 28)
                    val off = cw.count
                    writeLocal(cw, e.name, e.method, e.crc, e.compSize, e.uncompSize, alignFor(e.method, cw.count, e.name))
                    raf.seek(dataOff)
                    copyN(raf, cw, e.compSize)
                    central.add(centralRec(e.name, e.method, e.crc, e.compSize, e.uncompSize, off))
                    if (++done % 40 == 0) onProgress(ApkPatcher.Progress("Montando", 5 + minOf(done, 300) / 4))
                }
            }
        }

        // entradas modificadas
        for ((name, provider) in modifiers) {
            writeMember(cw, central, name, provider(), stored = name == "resources.arsc")
        }
        onProgress(ApkPatcher.Progress("Adicionando libs", 82))
        for ((name, file) in extra) if (name !in written) writeMember(cw, central, name, file.readBytes(), false)

        // diretório central + EOCD
        val cdOff = cw.count
        for (rec in central) cw.write(rec)
        writeEocd(cw, central.size, cw.count - cdOff, cdOff)
        cw.flush(); cw.close()
    }

    // --- membros novos (deflate, ou store+align p/ resources.arsc) ---
    private fun writeMember(
        cw: CountingOutput, central: ArrayList<ByteArray>,
        name: String, raw: ByteArray, stored: Boolean,
    ) {
        val crc = CRC32().apply { update(raw) }.value
        val off = cw.count
        if (stored) {
            writeLocal(cw, name, 0, crc, raw.size.toLong(), raw.size.toLong(), alignFor(0, cw.count, name))
            cw.write(raw)
            central.add(centralRec(name, 0, crc, raw.size.toLong(), raw.size.toLong(), off))
        } else {
            val comp = deflate(raw)
            writeLocal(cw, name, 8, crc, comp.size.toLong(), raw.size.toLong(), 0)
            cw.write(comp)
            central.add(centralRec(name, 8, crc, comp.size.toLong(), raw.size.toLong(), off))
        }
    }

    private fun deflate(data: ByteArray): ByteArray {
        val d = Deflater(Deflater.DEFAULT_COMPRESSION, true); d.setInput(data); d.finish()
        val out = java.io.ByteArrayOutputStream(data.size / 2 + 64)
        val buf = ByteArray(1 shl 16)
        while (!d.finished()) out.write(buf, 0, d.deflate(buf))
        d.end(); return out.toByteArray()
    }

    /** padding no extra do local header pra alinhar dados STORED a 4 bytes. */
    private fun alignFor(method: Int, pos: Long, name: String): Int {
        if (method != 0) return 0
        val headerEnd = pos + 30 + name.toByteArray(Charsets.UTF_8).size
        return ((4 - (headerEnd % 4)) % 4).toInt()
    }

    // --- leitura do diretório central ---
    private fun readCentralDir(raf: RandomAccessFile): List<CEntry> {
        val len = raf.length()
        val tail = ByteArray(minOf(len, 65557L).toInt())
        raf.seek(len - tail.size); raf.readFully(tail)
        var e = tail.size - 22
        while (e >= 0 && !(tail[e] == 0x50.toByte() && tail[e + 1] == 0x4b.toByte() &&
                    tail[e + 2] == 0x05.toByte() && tail[e + 3] == 0x06.toByte())) e--
        require(e >= 0) { "EOCD não encontrado" }
        val total = u16(tail, e + 10)
        var cdOff = u32(tail, e + 16)
        val list = ArrayList<CEntry>(total)
        val hdr = ByteArray(46)
        for (i in 0 until total) {
            raf.seek(cdOff); raf.readFully(hdr)
            require(hdr[0] == 0x50.toByte() && hdr[1] == 0x4b.toByte() &&
                    hdr[2] == 0x01.toByte() && hdr[3] == 0x02.toByte()) { "CDH inválido" }
            val method = u16(hdr, 10); val crc = u32(hdr, 16)
            val comp = u32(hdr, 20); val uncomp = u32(hdr, 24)
            val nLen = u16(hdr, 28); val xLen = u16(hdr, 30); val cLen = u16(hdr, 32)
            val localOff = u32(hdr, 42)
            val nameB = ByteArray(nLen); raf.seek(cdOff + 46); raf.readFully(nameB)
            list.add(CEntry(String(nameB, Charsets.UTF_8), method, crc, comp, uncomp, localOff))
            cdOff += 46 + nLen + xLen + cLen
        }
        return list
    }

    // --- escrita de headers ---
    private fun writeLocal(
        cw: CountingOutput, name: String, method: Int, crc: Long,
        comp: Long, uncomp: Long, extraPad: Int,
    ) {
        val nb = name.toByteArray(Charsets.UTF_8)
        val h = ByteArray(30)
        putU32(h, 0, 0x04034b50); putU16(h, 4, 20); putU16(h, 6, 0); putU16(h, 8, method)
        putU16(h, 10, 0); putU16(h, 12, 0); putU32(h, 14, crc)
        putU32(h, 18, comp); putU32(h, 22, uncomp); putU16(h, 26, nb.size); putU16(h, 28, extraPad)
        cw.write(h); cw.write(nb); if (extraPad > 0) cw.write(ByteArray(extraPad))
    }

    private fun centralRec(
        name: String, method: Int, crc: Long, comp: Long, uncomp: Long, localOff: Long,
    ): ByteArray {
        val nb = name.toByteArray(Charsets.UTF_8)
        val h = ByteArray(46 + nb.size)
        putU32(h, 0, 0x02014b50); putU16(h, 4, 20); putU16(h, 6, 20); putU16(h, 8, 0)
        putU16(h, 10, method); putU16(h, 12, 0); putU16(h, 14, 0); putU32(h, 16, crc)
        putU32(h, 20, comp); putU32(h, 24, uncomp); putU16(h, 28, nb.size)
        putU16(h, 30, 0); putU16(h, 32, 0); putU16(h, 34, 0); putU16(h, 36, 0)
        putU32(h, 38, 0); putU32(h, 42, localOff)
        System.arraycopy(nb, 0, h, 46, nb.size)
        return h
    }

    private fun writeEocd(cw: CountingOutput, count: Int, cdSize: Long, cdOff: Long) {
        val h = ByteArray(22)
        putU32(h, 0, 0x06054b50); putU16(h, 4, 0); putU16(h, 6, 0)
        putU16(h, 8, count); putU16(h, 10, count); putU32(h, 12, cdSize); putU32(h, 16, cdOff); putU16(h, 20, 0)
        cw.write(h)
    }

    private fun copyN(raf: RandomAccessFile, out: OutputStream, n: Long) {
        val buf = ByteArray(1 shl 16); var left = n
        while (left > 0) {
            val r = raf.read(buf, 0, minOf(left, buf.size.toLong()).toInt()); require(r > 0)
            out.write(buf, 0, r); left -= r
        }
    }

    private fun isOldSig(n: String) = n.startsWith("META-INF/") &&
        (n.endsWith(".SF") || n.endsWith(".RSA") || n.endsWith(".DSA") ||
         n.endsWith(".EC") || n == "META-INF/MANIFEST.MF")

    // little-endian helpers
    private fun u16(b: ByteArray, o: Int) = (b[o].toInt() and 0xff) or ((b[o + 1].toInt() and 0xff) shl 8)
    private fun u32(b: ByteArray, o: Int) = ((b[o].toInt() and 0xff).toLong()) or
        ((b[o + 1].toInt() and 0xff).toLong() shl 8) or
        ((b[o + 2].toInt() and 0xff).toLong() shl 16) or
        ((b[o + 3].toInt() and 0xff).toLong() shl 24)
    private fun putU16(b: ByteArray, o: Int, v: Int) {
        b[o] = (v and 0xff).toByte(); b[o + 1] = ((v shr 8) and 0xff).toByte()
    }
    private fun putU32(b: ByteArray, o: Int, v: Long) {
        b[o] = (v and 0xff).toByte(); b[o + 1] = ((v shr 8) and 0xff).toByte()
        b[o + 2] = ((v shr 16) and 0xff).toByte(); b[o + 3] = ((v shr 24) and 0xff).toByte()
    }

    private class CountingOutput(val out: OutputStream) : OutputStream() {
        var count = 0L; private set
        override fun write(b: Int) { out.write(b); count++ }
        override fun write(b: ByteArray) { out.write(b); count += b.size }
        override fun write(b: ByteArray, off: Int, len: Int) { out.write(b, off, len); count += len }
        override fun flush() { out.flush() }
        override fun close() { out.close() }
    }
}
