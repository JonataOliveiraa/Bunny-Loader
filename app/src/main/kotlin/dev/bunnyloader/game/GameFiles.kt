package dev.bunnyloader.game

import android.content.Context
import android.net.Uri
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
 * Copiamos a pilha nativa da Unity — e a `libpairipcore.so` SÓ se a libunity
 * daquela versão realmente linkar contra ela:
 *
 *   1.4.5.6.4 → não linka. Descartamos a .so.
 *   1.4.5.8.6 → linka, e sem ela o dlopen falha:
 *       dlopen failed: library "libpairipcore.so" not found: needed by libunity.so
 *     ...mas COM ela o anti-tamper derruba o processo (SIGSEGV). É esse o beco
 *     sem saída que o pinning resolve.
 *
 * A .so está no APK das duas versões, então "existe" não é critério; quem
 * decide é o DT_NEEDED da libunity (ver [ElfInfo]). Descartá-la quando ninguém
 * precisa é o que a cópia distribuída pelo TL Pro também faz.
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

    /** Só entra na cópia quando a libunity da versão pede — ver [needsPairip]. */
    private val OPTIONAL_LIBS = listOf(PAIRIP)

    fun libDir(ctx: Context, abi: String): File =
        File(ctx.filesDir, "game/lib/$abi")

    // --- versão congelada ----------------------------------------------------
    //
    // A cópia congelada é NOSSA e fica no nosso armazenamento. Nem a Play nem uma
    // atualização do jogo alcançam esses arquivos: é isso que garante que uma
    // versão nova do Terraria não muda o que roda aqui.
    //
    // O layout é base + splits porque é assim que a Play instala o jogo (as .so
    // vêm no split de ABI, os assets no base). Congelar só o base pegaria um
    // conjunto incompleto.

    private fun pinnedDir(ctx: Context): File = File(ctx.getExternalFilesDir(null), "pinned")

    /** Layout antigo: um arquivo único solto. Migrado na primeira leitura. */
    private fun legacyPinned(ctx: Context): File =
        File(ctx.getExternalFilesDir(null), "terraria.apk")

    /**
     * APKs congelados, base primeiro, ou vazio se não há versão congelada.
     */
    fun pinnedApks(ctx: Context): List<File> {
        val dir = pinnedDir(ctx)
        val base = File(dir, "base.apk")

        val legacy = legacyPinned(ctx)
        if (legacy.isFile && !base.isFile) {
            dir.mkdirs()
            legacy.renameTo(base)
        }
        if (!base.isFile) return emptyList()

        val splits = dir.listFiles { f -> f.name.startsWith("split") && f.name.endsWith(".apk") }
            ?.sortedBy { it.name }?.toList() ?: emptyList()
        return listOf(base) + splits
    }

    fun isPinned(ctx: Context): Boolean = pinnedApks(ctx).isNotEmpty()

    /** Tamanho total no disco, para a UI. */
    private fun pinnedSize(ctx: Context): Long = pinnedApks(ctx).sumOf { it.length() }

    /**
     * Importa o APK escolhido pelo usuário como a versão congelada.
     *
     * Existe porque desde o Android 11 nenhum gerenciador de arquivos escreve em
     * `Android/data/...`: mandar o usuário "copiar o APK para lá" não funciona no
     * aparelho dele. Com o seletor de documentos ele aponta o arquivo (Downloads,
     * Drive, o que for) e nós é que gravamos no nosso diretório.
     *
     * Aqui exigimos um APK COMPLETO (com as libs dentro), porque um arquivo
     * escolhido à mão não tem splits para acompanhá-lo. Congelar o que está
     * instalado é o caminho para installs divididos — ver [pinInstalled].
     *
     * @return descrição da versão aceita.
     */
    fun importPinned(ctx: Context, uri: Uri): String {
        val dir = pinnedDir(ctx).apply { mkdirs() }
        val tmp = File(dir, "base.apk.part")
        tmp.delete()
        ctx.contentResolver.openInputStream(uri).use { input ->
            requireNotNull(input) { "não consegui ler o arquivo escolhido" }
            tmp.outputStream().use { input.copyTo(it, 1 shl 16) }
        }

        val info = validate(ctx, tmp, needsLibs = true) { tmp.delete() }
        replacePinned(ctx, dir) { check(tmp.renameTo(File(dir, "base.apk"))) { "não consegui gravar" } }
        return "${info.versionName} (${info.longVersionCode})"
    }

    /**
     * Congela a versão instalada: copia o APK do jogo (base + splits) para a
     * nossa área.
     *
     * A partir daí o jogo pode atualizar à vontade — o que roda é esta cópia.
     * É a mesma garantia de versão fixa que o TL Pro tem, com a diferença de que
     * a cópia é do próprio usuário e nunca sai deste aparelho.
     *
     * @return descrição da versão congelada.
     */
    fun pinInstalled(ctx: Context, install: GameInstall, onStep: (String) -> Unit = {}): String {
        val dir = pinnedDir(ctx).apply { mkdirs() }
        val staged = ArrayList<Pair<File, File>>()  // temporário -> final
        try {
            install.allApks().forEachIndexed { i, path ->
                val src = File(path)
                val finalName = if (i == 0) "base.apk" else "split-%02d.apk".format(i)
                val tmp = File(dir, "$finalName.part")
                onStep("copiando $finalName (${src.length() / 1_000_000} MB)")
                tmp.delete()
                src.inputStream().use { input ->
                    tmp.outputStream().use { input.copyTo(it, 1 shl 16) }
                }
                staged += tmp to File(dir, finalName)
            }

            val base = staged.first().first
            // Os assets estão no base; as .so podem estar nele ou no split.
            val info = validate(ctx, base, needsLibs = false) { staged.forEach { it.first.delete() } }
            val hasLibs = staged.any { (tmp, _) ->
                ZipFile(tmp).use { it.getEntry("lib/${install.abi}/libil2cpp.so") } != null
            }
            if (!hasLibs) {
                staged.forEach { it.first.delete() }
                error("não achei libil2cpp.so para ${install.abi} no que está instalado")
            }

            replacePinned(ctx, dir) {
                staged.forEach { (tmp, dest) ->
                    dest.delete()
                    check(tmp.renameTo(dest)) { "não consegui gravar ${dest.name}" }
                }
            }
            return "${info.versionName} (${info.longVersionCode})"
        } catch (t: Throwable) {
            staged.forEach { it.first.delete() }
            throw t
        }
    }

    /**
     * A versão INSTALADA é uma que o hosting consegue rodar?
     *
     * Extrai só a libunity para um temporário e lê o DT_NEEDED dela. Precisa ser
     * respondido ANTES de congelar, porque congelar copia 200 MB — e porque a
     * resposta "não" tem de virar mensagem, não o SIGSEGV do anti-tamper.
     */
    fun installedIsSupported(ctx: Context, install: GameInstall): Boolean {
        val probe = File(ctx.cacheDir, "probe").apply { mkdirs() }
        return try {
            val missing = copy(listOf("libunity.so"), install.allApks(), install.abi, probe) {}
            if (missing.isNotEmpty()) false else !needsPairip(probe)
        } finally {
            probe.deleteRecursively()
        }
    }

    /** Rejeita cedo o que não é o Terraria — com o motivo, não um crash depois. */
    private fun validate(
        ctx: Context,
        apk: File,
        needsLibs: Boolean,
        onReject: () -> Unit,
    ): android.content.pm.PackageInfo {
        fun reject(msg: String): Nothing { onReject(); error(msg) }
        val info = ctx.packageManager.getPackageArchiveInfo(apk.absolutePath, 0)
            ?: reject("isso não é um APK válido")
        if (info.packageName != TERRARIA) {
            reject("esse APK é ${info.packageName}, não o Terraria")
        }
        if (needsLibs && ZipFile(apk).use { it.getEntry("lib/arm64-v8a/libil2cpp.so") } == null) {
            reject("esse APK não tem as libs arm64 (é só o base de um install dividido?)")
        }
        return info
    }

    /**
     * Troca o conteúdo congelado, limpando o anterior.
     *
     * Duas coisas têm de sumir juntas: os APKs antigos (senão sobra um split de
     * outra versão) e as libs já extraídas (a extração é idempotente por tamanho,
     * e duas versões podem ter uma .so de tamanho igual — descobrir isso num
     * SIGSEGV sai caro).
     */
    private fun replacePinned(ctx: Context, dir: File, install: () -> Unit) {
        dir.listFiles()?.forEach { if (!it.name.endsWith(".part")) it.delete() }
        install()
        legacyPinned(ctx).delete()
        File(ctx.filesDir, "game/lib").deleteRecursively()
    }

    /** Volta a usar a versão instalada no aparelho. */
    fun clearPinned(ctx: Context) {
        pinnedDir(ctx).deleteRecursively()
        legacyPinned(ctx).delete()
        File(ctx.filesDir, "game/lib").deleteRecursively()
    }

    private const val TERRARIA = "com.and.games505.TerrariaPaid"

    /** APKs de origem: os congelados, se houver; senão o instalado. */
    fun sourceApks(ctx: Context, install: GameInstall): List<String> =
        pinnedApks(ctx).map { it.absolutePath }.ifEmpty { install.allApks() }

    /**
     * De onde saem as libs: do APK FIXADO, se o usuário colocou um; senão do
     * jogo instalado.
     *
     * Por que fixar importa: em 1.4.5.6.4 a libunity não depende da
     * libpairipcore; em 1.4.5.8.6 depende — e essa lib SEGFAULTA ao ser
     * carregada fora do boot legítimo do app (anti-tamper). É por isso que o TL
     * Pro roda 1.4.5.6 mesmo em quem tem 1.4.5.8 instalado.
     *
     * Nada é redistribuído: o APK fixado é uma cópia do próprio usuário, que ele
     * aponta pelo seletor de documentos (ver [importPinned]).
     */
    fun prepare(ctx: Context, install: GameInstall, onStep: (String) -> Unit = {}): File {
        val dest = libDir(ctx, install.abi).apply { mkdirs() }
        val sources = sourceApks(ctx, install)
        onStep(
            if (isPinned(ctx)) "fonte: cópia congelada (${pinnedSize(ctx) / 1_000_000} MB)"
            else "fonte: Terraria instalado (v${install.versionCode})",
        )
        val missing = copy(LIBS, sources, install.abi, dest, onStep)
        if (missing.isNotEmpty()) error("libs não encontradas no APK do jogo: $missing")

        // A libpairipcore está no APK das DUAS versões, então a presença dela
        // não diz nada. Quem decide é a libunity: se ela não linka contra a
        // libpairipcore, copiá-la e carregá-la é acordar o anti-tamper sem
        // necessidade — e a cópia que o TL Pro distribui nem tem essa .so.
        // Por isso a decisão vem DEPOIS da libunity estar aqui, e não antes.
        val pairip = File(dest, PAIRIP)
        if (needsPairip(dest)) {
            onStep("libunity LINKA $PAIRIP (versão nova; é aqui que dá SIGSEGV)")
            copy(listOf(PAIRIP), sources, install.abi, dest, onStep)
        } else if (pairip.exists()) {
            onStep("libunity não precisa de $PAIRIP — descartada")
            pairip.setWritable(true, true)
            pairip.delete()
        }

        Log.i(TAG, "GameFiles: ${LIBS.size} libs em $dest")
        return dest
    }

    private const val PAIRIP = "libpairipcore.so"

    /**
     * Extrai `names` do primeiro APK que os tiver.
     *
     * Idempotente por TAMANHO — o APK de origem é imutável, então tamanho igual
     * significa a mesma .so. (Trocar o APK fixado apaga o diretório inteiro,
     * ver [importPinned], justamente porque aí a premissa deixa de valer.)
     *
     * @return o que não foi encontrado em nenhum APK.
     */
    private fun copy(
        names: List<String>,
        sources: List<String>,
        abi: String,
        dest: File,
        onStep: (String) -> Unit,
    ): Set<String> {
        val pending = names.toMutableSet()
        for (apk in sources) {
            if (pending.isEmpty()) break
            ZipFile(apk).use { zip ->
                for (name in pending.toList()) {
                    val entry = zip.getEntry("lib/$abi/$name") ?: continue
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
        return pending
    }

    /**
     * A libunity desta cópia depende do PairIP?
     *
     *   1.4.5.6.4 → não (só libmain/libandroid/liblog/libz/libEGL/libm/libdl/libc)
     *   1.4.5.8.6 → sim
     *
     * Na dúvida (ELF ilegível) devolvemos `true`: melhor tentar carregar e ver
     * o erro do que deixar a libunity sem uma dependência real.
     */
    fun needsPairip(dir: File): Boolean {
        val deps = ElfInfo.needed(File(dir, "libunity.so"))
        if (deps.isEmpty()) return true
        return PAIRIP in deps
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
                // Ausência da opcional é o caso NORMAL na 1.4.5.6.4: a
                // libunity não a lista em DT_NEEDED e nós não a copiamos.
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

    /** Texto para a UI: qual versão vai rodar, e o que fazer a respeito. */
    fun pinStatus(ctx: Context): String {
        val apks = pinnedApks(ctx)
        if (apks.isEmpty()) {
            return "Nenhuma versão congelada ainda. No primeiro \"Jogar\" o app " +
                "congela sozinho a versão instalada, se ela for suportada.\n" +
                "Suportada é a 1.4.5.6.4; na 1.4.5.8.6 a libunity exige a " +
                "libpairipcore, que derruba o processo fora do boot do jogo — " +
                "aí é preciso apontar o APK da 1.4.5.6.4 abaixo."
        }
        val v = runCatching {
            ctx.packageManager.getPackageArchiveInfo(apks.first().absolutePath, 0)
        }.getOrNull()
        val parts = if (apks.size > 1) ", ${apks.size} arquivos" else ""
        return "Versão CONGELADA: ${v?.versionName ?: "?"} (${v?.longVersionCode ?: "?"})\n" +
            "${pinnedSize(ctx) / 1_000_000} MB$parts — atualizações do jogo não mexem nisto."
    }

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
