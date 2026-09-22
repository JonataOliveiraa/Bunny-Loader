package dev.bunnyloader.game

import android.content.Context
import java.io.File

/**
 * O runtime do jogo que vem DENTRO do Bunny Loader.
 *
 * As `.so` chegam pelo split de ABI que o Play gera a partir de
 * `src/main/jniLibs/arm64-v8a/`, e os assets pelo asset pack install-time. Ou
 * seja: para o Android, este app É um app Unity. Não há outro pacote para
 * hospedar, nenhum `createPackageContext`, nenhum caminho de APK alheio — o
 * `nativeLibraryDir` e o `AssetManager` do próprio processo já apontam para o
 * lugar certo.
 *
 * É por isso que este arquivo é tão curto comparado ao [GameFiles] que ele
 * substitui: quase tudo lá existia só para contornar o fato de os binários
 * viverem em outro app.
 *
 * Sem interface com uma implementação só: quando (e se) aparecer uma segunda
 * fonte de runtime, extrair a interface é mecânico.
 */
object BundledRuntime {
    /** Ordem de dependência; a libunity precisa da libmain, que precisa da libc++. */
    private val LIBS = listOf("c++_shared", "main", "unity", "il2cpp")

    /** Versão que esta build carrega. Confira contra o que o jogo reporta. */
    const val VERSION_NAME = "1.4.5.6.4"
    const val VERSION_CODE = 301543L

    fun libDir(ctx: Context): File = File(ctx.applicationInfo.nativeLibraryDir)

    /**
     * O runtime está nesta build?
     *
     * Um APK de desenvolvimento sem os arquivos integrados compila e instala
     * normalmente — e só descobriria o problema num SIGSEGV dentro da Unity.
     * Melhor dizer isso na cara.
     */
    fun isPresent(ctx: Context): Boolean = File(libDir(ctx), "libil2cpp.so").isFile

    /**
     * Carrega a pilha nativa do jogo.
     *
     * Por nome, e não por caminho absoluto: as libs estão no nosso próprio
     * `nativeLibraryDir`, que já está no caminho de busca do ClassLoader. Era
     * justamente o que não dava para fazer com os binários do outro app.
     *
     * @return null se tudo carregou; senão a descrição do primeiro erro.
     */
    fun load(): String? {
        for (name in LIBS) {
            try {
                System.loadLibrary(name)
            } catch (t: Throwable) {
                return "lib$name.so: ${t.javaClass.simpleName}: ${t.message}"
            }
        }
        return null
    }

    /**
     * A libunity integrada não pode depender da libpairipcore.
     *
     * Checagem de sanidade da build, não de segurança: se alguém integrar por
     * engano os binários de uma 1.4.5.8, o sintoma seria um SIGSEGV do
     * anti-tamper no meio do boot da Unity. Aqui vira mensagem.
     */
    fun pairipCheck(ctx: Context): String? {
        val unity = File(libDir(ctx), "libunity.so")
        if (!unity.isFile) return "libunity.so ausente"
        val deps = ElfInfo.needed(unity)
        if (deps.isEmpty()) return null  // ELF ilegível: não é motivo para barrar
        return if (deps.any { it == "libpairipcore.so" }) {
            "a libunity integrada depende de libpairipcore.so — runtime errado nesta build"
        } else {
            null
        }
    }

    /** Listagem para a trilha de boot. */
    fun describe(ctx: Context): String =
        libDir(ctx).listFiles()?.filter { it.name.endsWith(".so") }?.sortedBy { it.name }
            ?.joinToString(", ") { "${it.name}=${it.length()}" }
            ?: "(nativeLibraryDir vazio/inacessível)"
}
