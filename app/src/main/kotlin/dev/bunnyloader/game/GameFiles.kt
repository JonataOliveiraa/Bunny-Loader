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
 * Copiamos a pilha nativa da Unity — e, quando existir, a `libpairipcore.so`.
 *
 * CORREÇÃO: em 1.4.5.6.4 a libunity NÃO dependia do PairIP, e eu afirmei que
 * dava para deixá-lo fora. Em 1.4.5.8.6 depende:
 *   dlopen failed: library "libpairipcore.so" not found: needed by libunity.so
 * Então ela entra na cópia, como dependência nativa. Isso NÃO traz o PairIP de
 * volta ao jogo: o license check e as strings cifradas vivem na camada Java
 * (com.pairip.*), e o dex do jogo continua sem ser carregado.
 *
 * Não redistribui nada: a fonte é a cópia instalada do próprio usuário.
 */
object GameFiles {
    /** Obrigatórias: sem elas não há jogo. */
    private val LIBS = listOf(
        "libmain.so",        // ponto de entrada; libunity depende dele
        "libunity.so",
        "libil2cpp.so",      // o jogo em si (não é cifrado)
        "libc++_shared.so",
    )

    /** Podem não existir conforme a versão do jogo — copiamos se houver. */
    private val OPTIONAL_LIBS = listOf("libpairipcore.so")

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
        val pending = (LIBS + OPTIONAL_LIBS).toMutableSet()

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
                    out.setWritable(true, true)
                    zip.getInputStream(entry).use { input ->
                        out.outputStream().use { input.copyTo(it, 1 shl 16) }
                    }
                    // O Android avisa: "Attempt to load writable file ... This
                    // will throw on a future Android version". Deixar somente
                    // leitura evita depender desse comportamento.
                    out.setReadOnly()
                    pending -= name
                }
            }
        }

        val missing = pending intersect LIBS.toSet()
        if (missing.isNotEmpty()) error("libs não encontradas no APK do jogo: $missing")
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
        // Ordem de dependência: libunity precisa de libmain, que precisa da
        // libc++_shared. Carregamos TODAS por caminho absoluto de propósito:
        // o System.loadLibrary("unity") da Unity procura por NOME no caminho do
        // ClassLoader e não acha a nossa cópia (estender esse caminho por
        // reflection não surtiu efeito no Android 16). Já carregada, o linker
        // devolve a mesma quando a Unity pedir pelo soname.
        for (name in LIBS_IN_LOAD_ORDER) {
            val f = File(dir, name)
            if (!f.exists()) {
                // Opcional ausente é normal (varia por versão do jogo).
                if (name in OPTIONAL_LIBS) continue
                return "$name ausente em $dir"
            }
            try {
                System.load(f.absolutePath)
                Log.i(TAG, "GameFiles: carregou $name")
            } catch (t: Throwable) {
                return "$name: ${t.javaClass.simpleName}: ${t.message}"
            }
        }
        return null
    }

    /** libpairipcore antes da libunity: nas versões novas ela é dependência. */
    private val LIBS_IN_LOAD_ORDER = listOf(
        "libc++_shared.so", "libpairipcore.so", "libmain.so",
        "libunity.so", "libil2cpp.so",
    )

    /** Listagem para a trilha de boot — confirma o que realmente está lá. */
    fun describe(dir: File): String =
        dir.listFiles()?.sortedBy { it.name }
            ?.joinToString(", ") { "${it.name}=${it.length()}" }
            ?: "(pasta vazia/inacessivel)"

    /**
     * Anexa o dex do jogo ao NOSSO ClassLoader, DEPOIS dos nossos elementos.
     *
     * Motivo: o `JNI_OnLoad` da libpairipcore procura as classes Java dela e,
     * sem achá-las, chama RegisterNatives com jclass nulo — e a ART aborta:
     *   JNI DETECTED ERROR IN APPLICATION: RegisterNatives received NULL jclass
     *
     * Anexar (e não prefixar) é o ponto: `com.unity3d.player.*` continua vindo
     * da NOSSA cópia limpa, que aparece antes na lista; só o que não existe em
     * nós — `com.pairip.*` — cai no dex do jogo.
     *
     * Isto apenas torna as classes RESOLVÍVEIS. Não instanciamos a Application
     * do jogo nem o license check; nada do PairIP Java é executado por nós.
     */
    fun addGameDex(ctx: Context, loader: ClassLoader, apks: List<String>): String {
        return runCatching {
            val pathListField = Class.forName("dalvik.system.BaseDexClassLoader")
                .getDeclaredField("pathList").apply { isAccessible = true }
            val ourPathList = pathListField.get(loader)!!
            val elementsField = ourPathList.javaClass
                .getDeclaredField("dexElements").apply { isAccessible = true }
            val ours = elementsField.get(ourPathList) as Array<*>

            // Um DexClassLoader filho faz o trabalho pesado de abrir/otimizar;
            // pegamos os elementos prontos dele em vez de chamar makeDexElements,
            // cuja assinatura muda de versão para versão do Android.
            val child = dalvik.system.DexClassLoader(
                apks.joinToString(File.pathSeparator),
                File(ctx.codeCacheDir, "gamedex").apply { mkdirs() }.absolutePath,
                null,
                loader,
            )
            val theirs = elementsField.get(pathListField.get(child)!!) as Array<*>

            val merged = java.lang.reflect.Array.newInstance(
                ours.javaClass.componentType, ours.size + theirs.size,
            )
            System.arraycopy(ours, 0, merged, 0, ours.size)
            System.arraycopy(theirs, 0, merged, ours.size, theirs.size)
            elementsField.set(ourPathList, merged)

            val probe = runCatching { loader.loadClass("com.pairip.VMRunner").name }
                .getOrElse { "VMRunner NAO resolveu (${it.javaClass.simpleName})" }
            "dex do jogo anexado (${theirs.size} elementos); $probe"
        }.getOrElse { "FALHOU: ${it.javaClass.simpleName}: ${it.message}" }
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
