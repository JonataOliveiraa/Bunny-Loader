package dev.bunnyloader.game

import android.content.Context
import android.util.Log
import dev.bunnyloader.TAG
import java.io.File
import java.util.zip.ZipFile

/**
 * Cópia própria dos binários do jogo — o "transfer" do modelo TL Pro.
 *
 * Por que copiar em vez de usar direto os do app instalado:
 *  1. **Linker namespace.** Cada ClassLoader tem o seu; não dá para `System.load`
 *     uma .so do diretório de outro app. Da NOSSA pasta, dá.
 *  2. **Versão fixa.** Quem controla a cópia controla a versão que roda, que é o
 *     que mantém mods estáveis quando o jogo atualiza.
 *
 * Copiamos só a pilha nativa da Unity. `libpairipcore.so` fica de fora de
 * propósito: conferido que ninguém depende dela (libmain/libunity/libil2cpp só
 * dependem de libs do sistema), e é o PairIP que derrubou as tentativas
 * anteriores. Nada do dex do jogo é carregado.
 *
 * Não redistribui nada: a fonte é a cópia instalada do próprio usuário.
 */
object GameFiles {
    /** Pilha nativa da Unity. Sem libpairipcore — nada depende dela. */
    private val LIBS = listOf(
        "libmain.so",        // ponto de entrada; libunity depende dele
        "libunity.so",
        "libil2cpp.so",      // o jogo em si (não é cifrado)
        "libc++_shared.so",
    )

    fun libDir(ctx: Context, abi: String): File =
        File(ctx.filesDir, "game/lib/$abi")

    /**
     * Garante a cópia local das libs, extraindo do APK do jogo (base + splits).
     * Idempotente: pula o que já está lá com o mesmo tamanho.
     *
     * @return diretório com as libs.
     */
    fun prepare(ctx: Context, install: GameInstall, onStep: (String) -> Unit = {}): File {
        val dest = libDir(ctx, install.abi).apply { mkdirs() }
        val sources = install.allApks()
        val pending = LIBS.toMutableSet()

        // Já copiado antes? Tamanho igual basta — o APK de origem é imutável.
        for (apk in sources) {
            if (pending.isEmpty()) break
            ZipFile(apk).use { zip ->
                for (name in pending.toList()) {
                    val entry = zip.getEntry("lib/${install.abi}/$name") ?: continue
                    val out = File(dest, name)
                    if (out.exists() && out.length() == entry.size) {
                        pending -= name
                        continue
                    }
                    onStep("copiando $name")
                    zip.getInputStream(entry).use { input ->
                        out.outputStream().use { input.copyTo(it, 1 shl 16) }
                    }
                    pending -= name
                }
            }
        }

        if (pending.isNotEmpty()) error("libs não encontradas no APK do jogo: $pending")
        Log.i(TAG, "GameFiles: ${LIBS.size} libs em $dest")
        return dest
    }

    /**
     * Torna `dir` visível para `System.loadLibrary` no ClassLoader informado.
     *
     * O caminho de busca de .so é fixado quando o ClassLoader nasce, então
     * acrescentamos o nosso ao DexPathList por reflection — é assim que a Unity
     * consegue achar a libmain.so da nossa cópia.
     */
    /**
     * Carrega as .so explicitamente, em ordem de dependência, relatando cada
     * uma. Sem isto um dlopen que falha vira SIGABRT mudo lá dentro da Unity.
     *
     * Suspeita principal de falha: desde o Android 10 o SELinux nega `execute`
     * em `app_data_file` para apps de targetSdk alto — ou seja, .so no nosso
     * diretório de dados pode simplesmente não poder ser mapeada executável.
     * (No emulador o SELinux é permissive, por isso lá passa.)
     *
     * @return null se tudo carregou; senão a descrição do primeiro erro.
     */
    fun preload(dir: File): String? {
        for (name in listOf("libc++_shared.so", "libmain.so")) {
            val f = File(dir, name)
            if (!f.exists()) return "$name ausente em $dir"
            try {
                System.load(f.absolutePath)
                Log.i(TAG, "GameFiles: carregou $name")
            } catch (t: Throwable) {
                return "$name: ${t.javaClass.simpleName}: ${t.message}"
            }
        }
        return null
    }

    fun addLibraryPath(loader: ClassLoader, dir: File): Boolean = runCatching {
        val pathList = Class.forName("dalvik.system.BaseDexClassLoader")
            .getDeclaredField("pathList").apply { isAccessible = true }.get(loader)!!

        val dirsField = pathList.javaClass
            .getDeclaredField("nativeLibraryDirectories").apply { isAccessible = true }
        @Suppress("UNCHECKED_CAST")
        val dirs = dirsField.get(pathList) as MutableList<File>
        if (!dirs.contains(dir)) dirs.add(0, dir)

        // O ART também mantém a lista já "resolvida" em elementos; refazemos.
        val elementsField = pathList.javaClass
            .getDeclaredField("nativeLibraryPathElements").apply { isAccessible = true }
        val makeElements = pathList.javaClass.getDeclaredMethod(
            "makePathElements", List::class.java,
        ).apply { isAccessible = true }
        elementsField.set(pathList, makeElements.invoke(null, dirs))
        true
    }.getOrElse {
        Log.e(TAG, "GameFiles: não consegui estender o caminho de libs: ${it.javaClass.simpleName}: ${it.message}")
        false
    }
}
