package dev.bunnyloader.game

import android.content.Context
import java.io.File
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec

/**
 * Os personagens e mundos salvos, para o início rápido escolher.
 *
 * Moram onde o jogo salva: `Android/data/com.bunnyloader/Players` e `Worlds`,
 * ao lado de bunny_packs. O nome mostrado vem de DENTRO do arquivo, que é o
 * que o jogo mostra nas listas ("Interior Pálido"); o nome do arquivo troca
 * espaço por `_`. Arquivo que não dá para ler fica com o nome do arquivo.
 */
object SaveFiles {

    /**
     * @param file o nome do arquivo (`Bench.plr`): é por ele que o núcleo acha
     *   o save na lista do jogo.
     * @param journey personagem ou mundo do modo Jornada. O jogo só deixa um
     *   entrar no outro quando os dois são, ou os dois não são.
     */
    class Save(val file: String, val name: String, val journey: Boolean)

    fun players(context: Context): List<Save> = list(context, "Players", ".plr", ::readPlayer)
    fun worlds(context: Context): List<Save> = list(context, "Worlds", ".wld", ::readWorld)

    /** O jogado por último primeiro: é o palpite certo quase sempre. */
    private fun list(context: Context, dir: String, ext: String, read: (File) -> Save?): List<Save> {
        val base = context.getExternalFilesDir(null)?.parentFile ?: return emptyList()
        return File(base, dir).listFiles { f -> f.isFile && f.name.endsWith(ext) }.orEmpty()
            .sortedByDescending { it.lastModified() }
            .map { f ->
                runCatching { read(f) }.getOrNull()
                    ?: Save(f.name, f.nameWithoutExtension.replace('_', ' '), false)
            }
    }

    // ------------------------------------------------------------- importar

    enum class Kind(val dir: String, val ext: String, val label: String) {
        PLAYER("Players", ".plr", "personagem"),
        WORLD("Worlds", ".wld", "mundo"),
    }

    /** Um arquivo escolhido no seletor: o nome que ele tinha e o conteúdo. */
    class Picked(val name: String, val open: () -> java.io.InputStream?)

    /**
     * Copia personagens ou mundos para onde o jogo procura (Players/ ou
     * Worlds/). Cada arquivo principal (`.plr`, `.wld`) é conferido lendo o
     * nome de dentro dele; os arquivos do Bunny Loader que vêm junto
     * (`Mundo.wld.bl`, `Mundo.wld.tiles.bl`, `Mundo.wld.walls.bl`,
     * `Heroi.plr.bl`...) acompanham o principal com o mesmo nome. Um nome que
     * já existe ganha um sufixo (`Mundo_2.wld`) em vez de sobrescrever.
     *
     * @return uma linha por arquivo principal: o nome importado ou o erro.
     */
    fun import(context: Context, kind: Kind, picked: List<Picked>): List<Result<String>> {
        val base = context.getExternalFilesDir(null)?.parentFile
            ?: return listOf(Result.failure(IllegalStateException("sem pasta de saves")))
        val dir = File(base, kind.dir).apply { mkdirs() }
        val temp = File(context.cacheDir, "import-save").apply { deleteRecursively(); mkdirs() }
        try {
            val files = picked.mapIndexedNotNull { i, p ->
                val safe = p.name.substringAfterLast('/').replace(Regex("""[^\p{L}\p{N} ._()-]"""), "_")
                    .ifBlank { "arquivo$i" }
                val out = File(temp, "$i").apply { mkdirs() }.let { File(it, safe) }
                runCatching { p.open()?.use { input -> out.outputStream().use { input.copyTo(it) } } }
                out.takeIf { it.isFile }
            }
            val mains = files.filter { it.name.endsWith(kind.ext, ignoreCase = true) }
            if (mains.isEmpty()) {
                val other = if (kind == Kind.WORLD) Kind.PLAYER else Kind.WORLD
                val hint = if (files.any { it.name.endsWith(other.ext, ignoreCase = true) })
                    " (isso é um ${other.label}: use Importar ${other.label})" else ""
                return listOf(Result.failure(IllegalArgumentException(
                    "nenhum arquivo ${kind.ext} entre os escolhidos$hint")))
            }
            return mains.map { main ->
                runCatching {
                    val save = runCatching { if (kind == Kind.PLAYER) readPlayer(main) else readWorld(main) }.getOrNull()
                    requireNotNull(save) { "${main.name} não é um ${kind.label} do Terraria" }
                    val stem = main.name.dropLast(kind.ext.length)
                    val target = freeName(dir, stem, kind.ext)
                    main.copyTo(File(dir, target + kind.ext))
                    // Os arquivos do Bunny Loader do mesmo save: <nome><ext>.*
                    val prefix = main.name.lowercase() + "."
                    for (side in files) {
                        if (side == main || !side.name.lowercase().startsWith(prefix)) continue
                        side.copyTo(File(dir, target + kind.ext + side.name.substring(main.name.length)), overwrite = true)
                    }
                    save.name
                }
            }
        } finally {
            temp.deleteRecursively()
        }
    }

    /** `stem`, ou `stem_2`, `stem_3`... o primeiro que não existe na pasta. */
    internal fun freeName(dir: File, stem: String, ext: String): String {
        if (!File(dir, stem + ext).exists()) return stem
        var n = 2
        while (File(dir, "${stem}_$n$ext").exists()) n++
        return "${stem}_$n"
    }

    // --------------------------------------------------------- personagem

    /** A chave do .plr, a mesma do PC: "h3y_gUyZ" em UTF-16, como chave e IV. */
    private val PLAYER_KEY = "h3y_gUyZ".toByteArray(Charsets.UTF_16LE)

    /**
     * O .plr é cifrado inteiro (Rijndael de 128 bits, CBC). O começo basta:
     * versão, metadados, nome e a dificuldade logo depois (3 = Jornada).
     */
    private fun readPlayer(file: File): Save? {
        val head = ByteArray(256)
        val n = file.inputStream().use { input ->
            var got = 0
            while (got < head.size) {
                val r = input.read(head, got, head.size - got)
                if (r < 0) break
                got += r
            }
            got
        }
        if (n < 32) return null
        val cipher = Cipher.getInstance("AES/CBC/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(PLAYER_KEY, "AES"), IvParameterSpec(PLAYER_KEY))
        val plain = ByteBuffer.wrap(cipher.doFinal(head, 0, n - n % 16)).order(ByteOrder.LITTLE_ENDIAN)
        if (!plain.skipMetadata()) return null
        val name = plain.dotNetString()
        val difficulty = plain.get().toInt()
        return Save(file.name, name, difficulty == 3)
    }

    // -------------------------------------------------------------- mundo

    /**
     * O .wld não é cifrado. O cabeçalho aponta onde começa cada seção; a
     * primeira é a do mundo, e o nome é o primeiro campo dela. O modo de jogo
     * vem alguns campos depois (WorldFile.LoadHeader + LoadWorldFlags).
     */
    private fun readWorld(file: File): Save? = RandomAccessFile(file, "r").use { f ->
        val head = ByteBuffer.wrap(ByteArray(64).also { f.readFully(it) }).order(ByteOrder.LITTLE_ENDIAN)
        val version = head.getInt(0)
        if (!head.skipMetadata()) return null
        head.getShort()                        // quantas seções
        val header = head.getInt().toLong()    // onde começa a primeira
        f.seek(header)
        val bytes = ByteArray(minOf(1024L, f.length() - header).toInt())
        f.readFully(bytes)
        val b = ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN)
        val name = b.dotNetString()
        // O modo de jogo (e a Jornada) só existe no arquivo desde a 209.
        var journey = false
        if (version >= 209) {
            b.dotNetString()                        // a semente
            b.getLong()                             // versão do gerador
            b.position(b.position() + 16)           // UniqueId
            b.position(b.position() + 4 * 7)        // WorldId, as 4 bordas, maxTilesY, maxTilesX
            journey = b.getInt() == 3               // GameMode
        }
        Save(file.name, name, journey)
    }

    // ------------------------------------------------------------- formato

    /** Versão e, desde a 135, "relogic" + tipo + revisão + favorito (20 bytes). */
    private fun ByteBuffer.skipMetadata(): Boolean {
        val version = getInt()
        if (version < 135) return true
        val magic = ByteArray(7).also { get(it) }
        if (String(magic, Charsets.US_ASCII) != "relogic") return false
        position(position() + 13)
        return true
    }

    /** O BinaryReader.ReadString do .NET: tamanho em 7 bits por byte, e UTF-8. */
    private fun ByteBuffer.dotNetString(): String {
        var length = 0
        var shift = 0
        while (true) {
            val b = get().toInt() and 0xFF
            length = length or ((b and 0x7F) shl shift)
            if (b and 0x80 == 0) break
            shift += 7
            require(shift < 35) { "tamanho de texto inválido" }
        }
        require(length in 0..remaining()) { "texto além do lido" }
        val bytes = ByteArray(length).also { get(it) }
        return String(bytes, Charsets.UTF_8)
    }
}
