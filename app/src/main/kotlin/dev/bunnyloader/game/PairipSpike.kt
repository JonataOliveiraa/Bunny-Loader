package dev.bunnyloader.game

import android.content.Context
import android.util.Log
import java.lang.reflect.Modifier

/**
 * ESTÁGIO 0 — confirmar que a libpairipcore popula as strings do app quando o
 * boot acontece no NOSSO processo.
 *
 * O PairIP cifra as constantes de string; elas vivem em classes-depósito
 * (só campos `static String`, zero métodos) preenchidas em runtime pela
 * libpairipcore, que reescreve `VMRunner.invoke` (no dex é um stub
 * `return null`) durante o boot legítimo do app.
 *
 * A Fase 1 falhou por usar um DexClassLoader CASEIRO e chamar o PairIP na mão.
 * A receita que funciona: ClassLoader do SISTEMA (createPackageContext) + deixar
 * o Android criar a Application via `LoadedApk.makeApplication`.
 *
 * A sonda NÃO pode ter nome de classe fixo: o PairIP ofusca com nomes diferentes
 * a cada build do jogo. Então descobrimos as classes-depósito varrendo o dex.
 */
object PairipSpike {
    private const val TAG = "BunnyLoader"
    private const val TERRARIA = "com.and.games505.TerrariaPaid"
    private const val MAX_SCAN = 15000  // agora descartamos muito mais candidatos
    private const val WANT_DEPOSITS = 3 // amostras suficientes

    fun run(host: Context): String {
        val out = StringBuilder()
        fun log(s: String) { Log.i(TAG, "spike: $s"); out.appendLine(s) }

        // 1) Contexto + ClassLoader do SISTEMA para o pacote do jogo.
        val gameCtx = runCatching {
            host.createPackageContext(
                TERRARIA, Context.CONTEXT_INCLUDE_CODE or Context.CONTEXT_IGNORE_SECURITY)
        }.getOrElse { log("1) createPackageContext FALHOU: ${desc(it)}"); return out.toString() }
        val loader = gameCtx.classLoader
        log("1) contexto+classloader do jogo OK")

        // Versão do jogo instalado (o dump nosso é 1.4.5.6.4 / 301543).
        runCatching {
            val pi = host.packageManager.getPackageInfo(TERRARIA, 0)
            log("   Terraria instalado: ${pi.versionName} (${pi.longVersionCode})")
        }

        // 2) Descobre classes-depósito do PairIP pelo FORMATO (sem nome fixo).
        val deposits = runCatching { findDeposits(loader) }
            .getOrElse { log("2) varredura do dex FALHOU: ${desc(it)}"); emptyList() }
        if (deposits.isEmpty()) {
            log("2) nenhum depósito em $lastScanned classes varridas")
        } else {
            log("2) ${deposits.size} depósito(s) em $lastScanned classes varridas:")
            deposits.forEach { log("   ${it.first.name} (${it.second.size} campos)") }
            log("   ANTES: ${summarize(deposits)}")
        }

        // SONDA DIRETA (não depende de heurística nem da versão do jogo):
        // no dex, `VMRunner.invoke` é um stub que retorna null na 1ª instrução;
        // a libpairipcore reescreve o método em memória no boot legítimo. Então
        // "retorna null" = ainda stub; qualquer outro comportamento = reescrito.
        log("   VMRunner.invoke ANTES: ${invokeState(loader)}")

        // 3) O pulo do gato: deixar o Android construir a Application do jogo.
        runCatching { makeApplication(gameCtx) }
            .onSuccess { log("3) LoadedApk.makeApplication() OK") }
            .onFailure { log("3) LoadedApk.makeApplication() FALHOU: ${desc(it)}") }

        log("   VMRunner.invoke DEPOIS: ${invokeState(loader)}")

        if (deposits.isNotEmpty()) {
            val after = summarize(deposits)
            log("   DEPOIS: $after")
            val populated = countNonNull(deposits)
            log(if (populated > 0) "RESULTADO: SUCESSO — $populated string(s) populada(s)"
                else "RESULTADO: falhou — nenhuma string populada")
        }
        return out.toString()
    }

    // --- descoberta das classes-depósito ---

    /**
     * Depósito do PairIP = classe cujas strings estáticas começam TODAS `null`
     * (quem preenche é a libpairipcore, de fora).
     *
     * Cuidado: `getDeclaredMethods()` NÃO inclui o `<clinit>`, então "zero
     * métodos" sozinho deixa passar classes de constantes comuns, que já nascem
     * preenchidas. Por isso o critério decisivo é o valor ser null aqui.
     */
    private fun findDeposits(loader: ClassLoader): List<Pair<Class<*>, List<java.lang.reflect.Field>>> {
        val found = ArrayList<Pair<Class<*>, List<java.lang.reflect.Field>>>()
        var scanned = 0
        for (name in dexClassNames(loader)) {
            if (scanned++ > MAX_SCAN || found.size >= WANT_DEPOSITS) break
            // TUDO por classe vai dentro do runCatching: além do forName,
            // getDeclaredMethods()/getDeclaredFields() também estouram
            // (NoClassDefFoundError) quando uma assinatura referencia classe
            // ausente do dex do jogo. Uma classe ruim não pode matar a varredura.
            runCatching {
                // initialize=false: não dispara <clinit> de classes alheias.
                val c = Class.forName(name, false, loader)
                if (c.declaredMethods.isNotEmpty()) return@runCatching
                val fields = c.declaredFields.filter {
                    it.type == String::class.java && Modifier.isStatic(it.modifiers)
                }
                if (fields.size < 3) return@runCatching
                fields.forEach { it.isAccessible = true }
                // Ler força o <clinit>: constante comum vira não-null e é descartada.
                if (fields.all { it.get(null) == null }) found.add(c to fields)
            }
        }
        Log.i(TAG, "spike:    varridas $scanned classes, ${found.size} depósito(s)")
        lastScanned = scanned
        return found
    }

    private var lastScanned = 0

    /** Nomes de classe do dex do jogo (via DexPathList do PathClassLoader). */
    private fun dexClassNames(loader: ClassLoader): Sequence<String> = sequence {
        val pathList = Class.forName("dalvik.system.BaseDexClassLoader")
            .getDeclaredField("pathList").apply { isAccessible = true }.get(loader)!!
        val elements = pathList.javaClass
            .getDeclaredField("dexElements").apply { isAccessible = true }
            .get(pathList) as Array<*>
        for (el in elements) {
            val dexFile = el?.javaClass?.getDeclaredField("dexFile")
                ?.apply { isAccessible = true }?.get(el) ?: continue
            @Suppress("UNCHECKED_CAST")
            val entries = dexFile.javaClass.getMethod("entries")
                .invoke(dexFile) as java.util.Enumeration<String>
            while (entries.hasMoreElements()) yield(entries.nextElement())
        }
    }

    private fun countNonNull(deposits: List<Pair<Class<*>, List<java.lang.reflect.Field>>>): Int =
        deposits.sumOf { (_, fs) -> fs.count { runCatching { it.get(null) != null }.getOrDefault(false) } }

    private fun summarize(deposits: List<Pair<Class<*>, List<java.lang.reflect.Field>>>): String {
        val total = deposits.sumOf { it.second.size }
        val nonNull = countNonNull(deposits)
        val sample = deposits.firstOrNull()?.second?.firstOrNull()
            ?.let { runCatching { it.get(null) as? String }.getOrNull() }
        return "$nonNull/$total preenchidas" + (sample?.let { " (ex.: \"$it\")" } ?: "")
    }

    /**
     * Estado do `com.pairip.VMRunner.invoke`. No dex ele é:
     *     0000: const/4 v0, #int 0
     *     0001: return-object v0      <- corpo real (0002+) inalcançável
     * Se responder null a um nome inexistente, continua stub → a libpairipcore
     * NÃO inicializou. Qualquer outra reação indica que o método foi reescrito.
     */
    private fun invokeState(loader: ClassLoader): String = runCatching {
        val vm = loader.loadClass("com.pairip.VMRunner")
        val m = vm.getMethod("invoke", String::class.java, Array<Any>::class.java)
        val r = m.invoke(null, "__bunny_probe__", arrayOfNulls<Any>(0))
        if (r == null) "null -> AINDA STUB" else "retornou $r -> REESCRITO"
    }.getOrElse { "lançou ${desc(it)} -> REESCRITO" }

    // --- boot legítimo da Application do jogo ---

    private fun makeApplication(gameCtx: Context) {
        val loadedApk = gameCtx.javaClass.getDeclaredField("mPackageInfo")
            .apply { isAccessible = true }.get(gameCtx) ?: error("mPackageInfo nulo")
        val at = Class.forName("android.app.ActivityThread")
            .getMethod("currentActivityThread").invoke(null)
        val instrumentation = at?.javaClass?.getDeclaredField("mInstrumentation")
            ?.apply { isAccessible = true }?.get(at)
        val m = loadedApk.javaClass.declaredMethods.firstOrNull {
            (it.name == "makeApplication" || it.name == "makeApplicationInner") &&
                it.parameterTypes.size == 2
        } ?: error("makeApplication não encontrado")
        m.isAccessible = true
        val app = m.invoke(loadedApk, false, instrumentation)
        Log.i(TAG, "spike:    Application = ${app?.javaClass?.name}")
    }

    private fun desc(t: Throwable): String {
        val r = generateSequence(t) { it.cause }.last()
        return "${r.javaClass.simpleName}: ${r.message}"
    }
}
