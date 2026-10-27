package dev.bunnyloader.catalog

import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.put
import java.io.File
import java.util.zip.ZipEntry
import java.util.zip.ZipFile
import java.util.zip.ZipOutputStream

/**
 * Mods C# com fontes (`"entry": "MeuMod.cs"`) saem do catálogo compilados: o
 * pacote enviado ganha o bin/MeuMod.dll e o selo bin/MeuMod.build.json, e o
 * Bunny Loader de quem baixa só confere o selo em vez de compilar no celular.
 */
class CSharpBuild(private val compiler: CompilerRuntime, private val work: File) {
    class Outcome(val file: File, val info: JsonObject?)

    /**
     * O `file` pronto para publicar: o mesmo arquivo, se não é mod com fontes
     * ou se o bin/ dele já bate com as fontes; senão, reescrito com o bin/ novo.
     * Erro de compilação vira Fail com as primeiras mensagens.
     */
    fun prepare(file: File, log: (String) -> Unit): Outcome {
        val entry = BlPackage(file).manifest.let { (it["entry"] as? JsonPrimitive)?.contentOrNull } ?: return Outcome(file, null)
        if (!entry.endsWith(".cs", ignoreCase = true)) return Outcome(file, null)
        val folder = File(work, file.nameWithoutExtension).apply { deleteRecursively(); mkdirs() }
        try {
            extract(file, folder)
            val report = compiler.build(File(folder, "content/$entry"))
            report.warnings.take(MAX_SHOWN).forEach { log(I18n.tr("  aviso do C#: $it", "  C# warning: $it")) }
            if (!report.success) {
                val shown = report.errors.take(MAX_SHOWN).joinToString("\n") { "  $it" }
                throw Fail(I18n.tr("o C# não compilou (${report.errors.size} erro(s)):\n$shown",
                    "the C# did not compile (${report.errors.size} error(s)):\n$shown"))
            }
            val project = "content/" + entry.substringBeforeLast('/', "").let { if (it.isEmpty()) "" else "$it/" }
            val name = entry.substringAfterLast('/').substringBeforeLast('.')
            val outputs = listOf("${project}bin/$name.dll", "${project}bin/$name.build.json")
            if (report.compiled) {
                rewrite(file, folder, outputs)
                log(I18n.tr("  C#: ${report.files} arquivo(s), ${report.lines} linhas compilados em ${report.milliseconds} ms",
                    "  C#: ${report.files} file(s), ${report.lines} lines compiled in ${report.milliseconds} ms"))
            } else {
                log(I18n.tr("  C#: o bin/ do pacote já bate com as fontes", "  C#: the package's bin/ already matches the sources"))
            }
            return Outcome(file, buildJsonObject {
                put("status", report.status)
                put("files", report.files)
                put("lines", report.lines)
                put("ms", report.milliseconds)
                put("dll", outputs[0])
                put("warnings", buildJsonArray { report.warnings.take(MAX_SHOWN).forEach { add(JsonPrimitive(it)) } })
            })
        } finally {
            folder.deleteRecursively()
        }
    }

    private fun extract(file: File, target: File) {
        val root = target.canonicalFile
        ZipFile(file).use { zip ->
            for (e in zip.entries()) {
                val out = File(root, e.name).canonicalFile
                if (!out.path.startsWith(root.path + File.separator)) throw Fail("${file.name}: '${e.name}' escapes the package")
                if (e.isDirectory) {
                    out.mkdirs()
                    continue
                }
                out.parentFile?.mkdirs()
                zip.getInputStream(e).use { input -> out.outputStream().use { input.copyTo(it) } }
            }
        }
    }

    /** O mesmo zip, na mesma ordem, com as saídas do compilador no lugar das velhas (ou no fim). */
    private fun rewrite(file: File, folder: File, outputs: List<String>) {
        val temp = File(file.path + ".tmp")
        ZipFile(file).use { zip ->
            ZipOutputStream(temp.outputStream().buffered()).use { out ->
                for (e in zip.entries()) {
                    if (e.isDirectory || e.name in outputs) continue
                    out.putNextEntry(ZipEntry(e.name).apply { time = e.time })
                    zip.getInputStream(e).use { it.copyTo(out) }
                    out.closeEntry()
                }
                for (path in outputs) {
                    out.putNextEntry(ZipEntry(path))
                    File(folder, path).inputStream().use { it.copyTo(out) }
                    out.closeEntry()
                }
            }
        }
        if (!temp.renameTo(file)) {
            file.delete()
            if (!temp.renameTo(file)) throw Fail(I18n.tr("não consegui gravar o pacote compilado", "could not write the compiled package"))
        }
    }

    companion object {
        private const val MAX_SHOWN = 12
    }
}
