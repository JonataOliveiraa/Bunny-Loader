package dev.bunnyloader.game

import java.io.File
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * Leitura das dependências (DT_NEEDED) de uma .so ELF64.
 *
 * Existe por um motivo bem específico: saber se a `libunity.so` da versão que
 * vamos rodar linka contra a `libpairipcore.so`.
 *
 *   1.4.5.6.4 → libmain, libandroid, liblog, libz, libEGL, libm, libdl, libc
 *   1.4.5.8.6 → ...os mesmos + libpairipcore.so
 *
 * Nas duas versões a `libpairipcore.so` ESTÁ dentro do APK, então "existe no
 * APK" não serve para decidir se devemos carregá-la. Carregá-la sem que nada
 * precise é acordar um anti-tamper à toa — e é exatamente o que o TL Pro não
 * faz (a cópia que eles distribuem simplesmente não tem essa .so).
 *
 * Lê só os cabeçalhos e a .dynamic; não carrega os 13 MB do arquivo.
 */
object ElfInfo {
    private const val PT_LOAD = 1
    private const val PT_DYNAMIC = 2
    private const val DT_NULL = 0L
    private const val DT_NEEDED = 1L
    private const val DT_STRTAB = 5L

    /** Segmento carregável: usado para converter vaddr em offset de arquivo. */
    private class Load(val off: Long, val vaddr: Long, val filesz: Long)

    /**
     * @return os nomes em DT_NEEDED, ou lista vazia se o arquivo não for um
     *   ELF64 legível. Nunca lança: quem chama trata "não sei" como "assume o
     *   comportamento conservador".
     */
    fun needed(f: File): List<String> = runCatching {
        RandomAccessFile(f, "r").use { raf ->
            fun read(at: Long, n: Int): ByteBuffer {
                val b = ByteArray(n)
                raf.seek(at)
                raf.readFully(b)
                return ByteBuffer.wrap(b).order(ByteOrder.LITTLE_ENDIAN)
            }

            val eh = read(0, 0x40)
            if (eh.getInt(0) != 0x464c457f || eh.get(4).toInt() != 2) return emptyList()
            val phoff = eh.getLong(0x20)
            val phentsize = eh.getShort(0x36).toInt() and 0xffff
            val phnum = eh.getShort(0x38).toInt() and 0xffff
            if (phoff <= 0 || phentsize < 0x38 || phnum == 0) return emptyList()

            val ph = read(phoff, phentsize * phnum)
            val loads = ArrayList<Load>()
            var dynOff = 0L
            var dynSize = 0L
            for (i in 0 until phnum) {
                val o = i * phentsize
                when (ph.getInt(o)) {
                    PT_LOAD -> loads += Load(ph.getLong(o + 8), ph.getLong(o + 16), ph.getLong(o + 0x20))
                    PT_DYNAMIC -> { dynOff = ph.getLong(o + 8); dynSize = ph.getLong(o + 0x20) }
                }
            }
            if (dynOff == 0L || dynSize == 0L) return emptyList()

            // Duas passadas: a DT_STRTAB pode vir depois das DT_NEEDED.
            val dyn = read(dynOff, dynSize.toInt())
            var strtabVaddr = -1L
            val offsets = ArrayList<Long>()
            var o = 0
            while (o + 16 <= dynSize) {
                val tag = dyn.getLong(o)
                val value = dyn.getLong(o + 8)
                if (tag == DT_NULL) break
                if (tag == DT_STRTAB) strtabVaddr = value
                if (tag == DT_NEEDED) offsets += value
                o += 16
            }
            if (strtabVaddr < 0 || offsets.isEmpty()) return emptyList()

            val load = loads.firstOrNull { strtabVaddr >= it.vaddr && strtabVaddr < it.vaddr + it.filesz }
                ?: return emptyList()
            val strtabOff = load.off + (strtabVaddr - load.vaddr)

            offsets.mapNotNull { rel ->
                runCatching {
                    raf.seek(strtabOff + rel)
                    buildString {
                        while (true) {
                            val c = raf.read()
                            if (c <= 0) break
                            append(c.toChar())
                        }
                    }.ifBlank { null }
                }.getOrNull()
            }
        }
    }.getOrDefault(emptyList())
}
