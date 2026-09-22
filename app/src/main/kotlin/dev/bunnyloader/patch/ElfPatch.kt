package dev.bunnyloader.patch

import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * Adiciona um DT_NEEDED a uma .so ELF64, SEM resize (porta de tools/elfpatch.py).
 *
 * Grava a string na folga zerada da .dynamic (regiao ja mapeada por um PT_LOAD
 * legivel) e troca o DT_NULL terminador por DT_NEEDED apontando pra ela; a folga
 * seguinte (zerada) vira o novo DT_NULL. O linker resolve strtab+offset por
 * vaddr em runtime, entao a string nao precisa estar dentro da .dynstr.
 */
object ElfPatch {
    private const val DT_NULL = 0L
    private const val DT_NEEDED = 1L
    private const val DT_STRTAB = 5L
    private const val PT_LOAD = 1
    private const val PT_DYNAMIC = 2

    /** Modifica `data` no lugar e o devolve, com `libName` em DT_NEEDED. */
    fun addNeeded(data: ByteArray, libName: String): ByteArray {
        val bb = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN)
        require(data.size > 0x40 && data[0] == 0x7f.toByte() &&
                data[1] == 'E'.code.toByte() && data[4].toInt() == 2) { "não é ELF64" }

        val ePhoff = bb.getLong(0x20)
        val ePhentsize = (bb.getShort(0x36).toInt() and 0xffff)
        val ePhnum = (bb.getShort(0x38).toInt() and 0xffff)

        var dynOff = 0L; var dynVaddr = 0L; var dynFilesz = 0L
        for (i in 0 until ePhnum) {
            val o = (ePhoff + i.toLong() * ePhentsize).toInt()
            if (bb.getInt(o) == PT_DYNAMIC) {
                dynOff = bb.getLong(o + 8)
                dynVaddr = bb.getLong(o + 16)
                dynFilesz = bb.getLong(o + 0x20)
            }
        }
        require(dynOff != 0L) { "sem PT_DYNAMIC" }

        var strtabVaddr = -1L
        var nullOff = -1
        var o = dynOff.toInt()
        while (true) {
            val tag = bb.getLong(o)
            if (tag == DT_STRTAB) strtabVaddr = bb.getLong(o + 8)
            if (tag == DT_NULL) { nullOff = o; break }
            o += 16
        }
        require(strtabVaddr >= 0) { "sem DT_STRTAB" }

        val cap = (dynOff + dynFilesz).toInt()
        val need = 16 + libName.length + 1  // nova DT_NULL + string
        require(nullOff + 16 + need <= cap) { "sem folga na .dynamic" }

        val strOff = cap - (libName.length + 1)
        val nameBytes = libName.toByteArray(Charsets.US_ASCII)
        System.arraycopy(nameBytes, 0, data, strOff, nameBytes.size)
        data[strOff + nameBytes.size] = 0
        val strVaddr = dynVaddr + (strOff - dynOff)

        bb.putLong(nullOff, DT_NEEDED)
        bb.putLong(nullOff + 8, strVaddr - strtabVaddr)
        return data
    }
}
