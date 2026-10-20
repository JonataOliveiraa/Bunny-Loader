package dev.bunnyloader.mods

import java.io.File
import java.io.InputStream
import java.util.Locale
import java.util.UUID
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json

/** Projetos locais: edita fontes e empacota um .bl, sem compilar JavaScript. */
class ModSources(val root: File, private val basicSwordImage: (() -> InputStream)? = null) {
    data class Project(
        val key: String,
        val name: String,
        val manifest: ModManifest?,
        val error: String? = null,
        val sourceRoot: String = "",
    )

    private val json = Json { ignoreUnknownKeys = true; encodeDefaults = true; prettyPrint = true }

    fun list(): List<Project> = operation("Não foi possível listar os projetos") {
        if (!root.exists()) return@operation emptyList()
        checkRoot()
        children(root).filter { it.isDirectory && !hidden(it) }.map { directory ->
            var manifest: ModManifest? = null
            try {
                val project = projectDirectory(directory.name)
                manifest = loadManifest(project)
                validate(project, manifest)
                Project(directory.name, manifest.name, manifest)
            } catch (e: Exception) {
                Project(directory.name, manifest?.name?.takeIf { it.isNotBlank() } ?: directory.name,
                    manifest, (e as? SourceException)?.message ?: "Não foi possível ler o projeto")
            }
        }.sortedWith(compareBy(String.CASE_INSENSITIVE_ORDER) { it.key })
    }

    fun create(
        id: String,
        name: String,
        author: String,
        basicSword: String = "",
        language: String = "pt-BR",
    ): Project = operation("Não foi possível criar o projeto") {
        check(ID.matches(id), "ID inválido: use uma letra seguida de até 63 letras, números ou sublinhados")
        check(name.isNotBlank(), "O nome do mod não pode ficar vazio")
        check(author.isNotBlank(), "O autor do mod não pode ficar vazio")
        val sword = basicSword.trim()
        check(sword.isEmpty() || ID.matches(sword),
            "Nome da espada inválido: use uma letra seguida de até 63 letras, números ou sublinhados")
        check(sword.isEmpty() || sword !in JS_RESERVED_WORDS,
            "Nome da espada inválido: escolha um nome que não seja palavra reservada do JavaScript")
        check(sword.isEmpty() || basicSwordImage != null, "A imagem da espada não está disponível")
        check(!root.exists() || root.isDirectory, "A raiz dos projetos não é uma pasta")
        if (!root.exists()) check(root.mkdirs(), "Não foi possível criar a pasta dos projetos")
        checkRoot()
        check(children(root).none { it.name.equals(id, ignoreCase = true) }, "Já existe um projeto com esse ID")
        val directory = safePath(root, id)
        check(directory.mkdir(), "Não foi possível criar a pasta do projeto")
        val manifest = ModManifest(
            uid = UUID.randomUUID().toString(), id = id, name = name.trim(), version = "1.0.0",
            author = author.trim(), type = "mod", blVersion = 2,
            localizedNames = mapOf(modCulture(language) to name.trim()),
        )
        File(directory, "manifest.json").writeText(json.encodeToString(manifest), Charsets.UTF_8)
        listOf("Assets", "Common", "Content", "Localization").forEach {
            check(File(directory, "content/$it").mkdirs(), "Não foi possível criar as pastas do projeto")
        }
        val className = if (id in JS_RESERVED_WORDS) "${id}Mod" else id
        File(directory, "content/main.js").writeText(
            "export default class $className extends Mod {\n    Load() {}\n}\n", Charsets.UTF_8)
        if (sword.isNotEmpty()) {
            val swordDir = File(directory, "content/Content/Items")
            check(swordDir.isDirectory || swordDir.mkdirs(), "Não foi possível criar a pasta da espada")
            File(swordDir, "$sword.js").writeText(
                "// Arma inicial criada pelo assistente de novo Mod.\n" +
                    "// A textura fica ao lado deste arquivo, como no tModLoader.\n" +
                    "export class $sword extends ModItem {\n" +
                    "    SetDefaults() {\n" +
                    "        this.Item.melee = true;\n" +
                    "        this.Item.width = 40;\n" +
                    "        this.Item.height = 40;\n" +
                    "        this.SetWeaponValues(50, 6, 0);\n" +
                    "        this.SetDefaultWeaponStyle(20, true);\n" +
                    "        this.Item.value = Terraria.Item.buyPrice(0, 0, 1, 0);\n" +
                    "        this.Item.rare = ItemRarityID.Blue;\n" +
                    "        this.Item.UseSound = Terraria.ID.SoundID.Item1;\n" +
                    "    }\n\n" +
                    "    // 10 blocos de terra numa bancada.\n" +
                    "    AddRecipes() {\n" +
                    "        this.CreateRecipe()\n" +
                    "            .AddIngredient(Terraria.ID.ItemID.DirtBlock, 10)\n" +
                    "            .AddTile(Terraria.ID.TileID.WorkBenches)\n" +
                    "            .Register();\n" +
                    "    }\n" +
                    "}\n", Charsets.UTF_8)
            basicSwordImage!!.invoke().use { input ->
                File(swordDir, "$sword.png").outputStream().use { output -> input.copyTo(output) }
            }
            val culture = templateCulture(language)
            val names = mapOf("ItemName" to mapOf(sword to displaySwordName(sword)))
            val content = json.encodeToString(names)
            File(directory, "content/Localization/en-US.json").writeText(content, Charsets.UTF_8)
            if (culture != "en-US") {
                File(directory, "content/Localization/$culture.json").writeText(content, Charsets.UTF_8)
            }
        }
        val culture = templateCulture(language)
        val baseDescription = "# ${manifest.name}\n\n${descriptionPrompt("en-US", manifest.name)}\n"
        File(directory, "description.md").writeText(baseDescription, Charsets.UTF_8)
        if (culture != "en-US") {
            val localizedDescription = "# ${manifest.name}\n\n${descriptionPrompt(culture, manifest.name)}\n"
            File(directory, "description_$culture.md").writeText(localizedDescription, Charsets.UTF_8)
        }
        Project(id, manifest.name, manifest)
    }

    /** Somente arquivos que o editor pode abrir; recursos binários ainda entram no pacote. */
    fun files(key: String): List<String> = operation("Não foi possível listar os arquivos") {
        val directory = projectDirectory(key)
        sourceFiles(directory).filter { editable(it.first) && it.second.length() <= MAX_TEXT_BYTES }
            .map { it.first }
    }

    fun readText(key: String, path: String): String = operation("Não foi possível ler o arquivo") {
        val file = textFile(projectDirectory(key), path)
        check(file.isFile, "O arquivo não existe")
        readLimited(file)
    }

    fun writeText(key: String, path: String, text: String): Unit = operation("Não foi possível salvar o arquivo") {
        val file = textFile(projectDirectory(key), path)
        val bytes = text.toByteArray(Charsets.UTF_8)
        check(bytes.size <= MAX_TEXT_BYTES, "O texto ultrapassa o limite de 1 MB em UTF-8")
        check(!file.exists() || file.isFile, "O caminho não é um arquivo")
        val parent = requireNotNull(file.parentFile)
        check(parent.isDirectory || parent.mkdirs(), "Não foi possível criar a pasta do arquivo")
        file.writeBytes(bytes)
    }

    /** Exporta um ZIP padrão; a identidade do manifesto nunca é regenerada. */
    fun build(key: String, output: File): ModManifest = operation("Não foi possível empacotar o mod") {
        val directory = projectDirectory(key)
        val manifest = loadManifest(directory)
        validate(directory, manifest)
        val destination = output.canonicalFile
        val sourceRoot = root.canonicalFile
        check(destination != sourceRoot && !destination.path.startsWith(sourceRoot.path + File.separator),
            "O pacote de saída deve ficar fora da pasta dos projetos")
        check(destination.extension.equals("bl", ignoreCase = true), "O pacote de saída deve ter a extensão .bl")
        check(!symbolicLink(output), "A saída não pode ser um link simbólico")
        val entries = sourceFiles(directory)
        val parent = requireNotNull(destination.parentFile)
        check(parent.isDirectory || parent.mkdirs(), "Não foi possível criar a pasta de saída")
        val temporary = File.createTempFile(".bunny-mod-", ".tmp", parent)
        try {
            ZipOutputStream(temporary.outputStream().buffered()).use { zip ->
                for ((path, file) in entries) {
                    zip.putNextEntry(ZipEntry(path).apply { time = 0L })
                    file.inputStream().use { it.copyTo(zip) }
                    zip.closeEntry()
                }
            }
            // Android/Linux substitui diretamente. Windows exige liberar o destino antes;
            // nesse caso, mantém o pacote antigo como backup até concluir a troca.
            if (!temporary.renameTo(destination)) {
                check(destination.isFile, "Não foi possível substituir o pacote de saída")
                val backup = File.createTempFile(".bunny-backup-", ".bl", parent)
                check(backup.delete() && destination.renameTo(backup), "Não foi possível guardar o pacote anterior")
                try {
                    check(temporary.renameTo(destination), "Não foi possível substituir o pacote de saída")
                    backup.delete()
                } catch (e: Exception) {
                    check(backup.renameTo(destination), "Não foi possível restaurar o pacote anterior: ${backup.path}")
                    throw e
                }
            }
        } finally {
            temporary.delete()
        }
        manifest
    }

    private fun loadManifest(directory: File): ModManifest {
        val file = textFile(directory, "manifest.json")
        check(file.isFile, "O projeto não tem manifest.json")
        val text = readLimited(file)
        return try { json.decodeFromString<ModManifest>(text) }
        catch (e: Exception) { throw SourceException("O manifest.json é inválido", e) }
    }

    private fun validate(directory: File, manifest: ModManifest) {
        check(manifest.hasValidUid, "O UID do manifesto deve ser um UUID canônico em minúsculas")
        check(ID.matches(manifest.id), "O ID do manifesto é inválido")
        check(manifest.name.isNotBlank(), "O nome do mod não pode ficar vazio")
        check(manifest.version.isNotBlank() && manifest.version.none { it.isISOControl() }, "A versão do mod é inválida")
        check(manifest.blVersion == 2, "O manifesto deve usar blVersion 2")
        check(manifest.type == "mod", "O tipo do manifesto deve ser mod")
        val content = safePath(directory, "content")
        check(content.isDirectory, "O projeto não tem a pasta content")
        val entry = safePath(content, manifest.entry)
        check(!excluded(manifest.entry.replace('\\', '/').split('/')), "O arquivo de entrada está em uma pasta excluída")
        check(entry.isFile, "O arquivo de entrada não existe em content")
    }

    fun projectDirectoryPath(key: String): File {
        checkRoot()
        check(key.isNotBlank() && '/' !in key && '\\' !in key && key != "." && key != "..",
            "A chave do projeto é inválida")
        val directory = safePath(root, key)
        check(directory.parentFile?.canonicalFile == root.canonicalFile && directory.isDirectory,
            "O projeto não existe")
        return directory
    }

    private fun projectDirectory(key: String): File {
        checkRoot()
        check(ID.matches(key), "A chave do projeto é inválida")
        val directory = safePath(root, key)
        check(directory.isDirectory, "O projeto não existe")
        return directory
    }

    private fun checkRoot() {
        check(root.isDirectory, "A pasta dos projetos não existe")
        check(!symbolicLink(root), "A pasta dos projetos não pode ser um link simbólico")
    }

    private fun textFile(directory: File, path: String): File {
        val file = safePath(directory, path)
        check(editable(path), "Extensão não permitida: use js, json, md, txt ou hjson")
        return file
    }

    private fun safePath(base: File, path: String): File {
        val resolved = resolvePackagePath(base.path, true, path)
        check(resolved != null, "Caminho inválido: o arquivo deve ficar dentro do projeto")
        val parts = path.replace('\\', '/').split('/').filter { it.isNotEmpty() && it != "." }
        var current = base
        for (part in parts) {
            current = File(current, part)
            check(!hidden(current), "Arquivos e pastas ocultos não são permitidos")
            check(!symbolicLink(current), "Links simbólicos não são permitidos")
        }
        return File(resolved!!)
    }

    private fun sourceFiles(directory: File): List<Pair<String, File>> {
        val result = mutableListOf<Pair<String, File>>()
        fun visit(folder: File, prefix: String) {
            for (child in children(folder)) {
                if (hidden(child) || child.name.equals("build", ignoreCase = true)) continue
                val relative = prefix + child.name
                val safe = safePath(directory, relative)
                if (safe.isDirectory) visit(safe, "$relative/")
                else if (safe.isFile) result += relative to safe
                else throw SourceException("O projeto contém um arquivo não suportado")
            }
        }
        visit(directory, "")
        return result.sortedBy { it.first }
    }

    private fun children(directory: File): List<File> = directory.listFiles()?.toList()
        ?: throw SourceException("Não foi possível acessar a pasta")

    private fun readLimited(file: File): String {
        check(file.length() <= MAX_TEXT_BYTES, "O arquivo ultrapassa o limite de 1 MB em UTF-8")
        val bytes = file.inputStream().use { input ->
            val buffer = java.io.ByteArrayOutputStream()
            val chunk = ByteArray(8192)
            while (buffer.size() <= MAX_TEXT_BYTES) {
                val count = input.read(chunk, 0, minOf(chunk.size, MAX_TEXT_BYTES + 1 - buffer.size()))
                if (count < 0) break
                buffer.write(chunk, 0, count)
            }
            buffer.toByteArray()
        }
        check(bytes.size <= MAX_TEXT_BYTES, "O arquivo ultrapassa o limite de 1 MB em UTF-8")
        return Charsets.UTF_8.newDecoder().run {
            try { decode(java.nio.ByteBuffer.wrap(bytes)).toString() }
            catch (e: Exception) { throw SourceException("O arquivo não contém texto UTF-8 válido", e) }
        }
    }

    private fun displaySwordName(id: String): String = id.replace(Regex("([a-z])([A-Z])"), "$1 $2")

    private fun templateCulture(language: String): String = modCulture(language).ifBlank { "pt-BR" }

    private fun descriptionPrompt(culture: String, name: String): String = when {
        culture.startsWith("zh") -> "这是 $name 的 Mod 源码项目。"
        culture.startsWith("en") -> "This is the source project for the $name mod."
        culture.startsWith("ru") -> "Это исходный проект мода $name."
        culture.startsWith("es") -> "Este es el proyecto fuente del mod $name."
        else -> "Este é o projeto-fonte do mod $name."
    }

    private fun editable(path: String) = path.substringAfterLast('.', "").lowercase(Locale.ROOT) in TEXT_EXTENSIONS
    // Resolve somente o pai: diferenças no último componente denunciam links,
    // inclusive links que apontam para outra pasta dentro do próprio projeto.
    private fun symbolicLink(file: File): Boolean {
        val absolute = file.absoluteFile
        val parent = absolute.parentFile ?: return false
        val withCanonicalParent = File(parent.canonicalFile, absolute.name)
        return withCanonicalParent.canonicalFile != withCanonicalParent.absoluteFile
    }

    private fun hidden(file: File) = file.name.startsWith('.') || file.isHidden
    private fun excluded(parts: List<String>) = parts.any { it.startsWith('.') || it.equals("build", ignoreCase = true) }
    private fun check(condition: Boolean, message: String) { if (!condition) throw SourceException(message) }
    private class SourceException(message: String, cause: Throwable? = null) : IllegalArgumentException(message, cause)
    private inline fun <T> operation(message: String, block: () -> T): T = try { block() }
        catch (e: SourceException) { throw e }
        catch (e: Exception) { throw SourceException(message, e) }

    private companion object {
        const val MAX_TEXT_BYTES = 1024 * 1024
        val ID = Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$")
        val TEXT_EXTENSIONS = setOf("js", "json", "md", "txt", "hjson")
        val JS_RESERVED_WORDS = setOf(
            "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default",
            "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for", "function",
            "if", "implements", "import", "in", "instanceof", "interface", "let", "new", "null",
            "package", "private", "protected", "public", "return", "static", "super", "switch",
            "this", "throw", "true", "try", "typeof", "var", "void", "while", "with", "yield",
        )
    }
}
