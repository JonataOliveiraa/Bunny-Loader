package dev.bunnyloader.mods

import android.content.Context
import android.net.Uri
import android.provider.DocumentsContract as Docs
import java.io.File
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.json.Json

/** SAF trees remain authoritative. A fresh private snapshot is used only while packaging. */
class SourceLocations(private val context: Context, val local: ModSources) {
    data class Location(val id: String, val label: String)
    private val json = Json { ignoreUnknownKeys = true }
    private val resolver get() = context.contentResolver
    private val projects = mutableMapOf<String, Pair<Uri, Node>>()
    var trees: Set<String> = emptySet()
    val locations get() = listOf(Location("", local.root.absolutePath)) + trees.sorted().map {
        Location(it, runCatching { node(root(Uri.parse(it))).name }.getOrDefault(it))
    }

    private data class Node(val uri: Uri, val name: String, val directory: Boolean)
    private fun root(tree: Uri) = Docs.buildDocumentUriUsingTree(tree, Docs.getTreeDocumentId(tree))
    private fun node(uri: Uri): Node = resolver.query(uri, arrayOf(
        Docs.Document.COLUMN_DISPLAY_NAME, Docs.Document.COLUMN_MIME_TYPE), null, null, null)?.use { cursor ->
        check(cursor.moveToFirst()) { "Source folder is unavailable" }
        Node(uri, cursor.getString(0), cursor.getString(1) == Docs.Document.MIME_TYPE_DIR)
    } ?: error("Source folder is unavailable")

    private fun children(directory: Uri): List<Node> {
        val treeChildren = Docs.buildChildDocumentsUriUsingTree(directory, Docs.getDocumentId(directory))
        return resolver.query(treeChildren, arrayOf(Docs.Document.COLUMN_DOCUMENT_ID,
            Docs.Document.COLUMN_DISPLAY_NAME, Docs.Document.COLUMN_MIME_TYPE), null, null, null)?.use { cursor ->
            buildList {
                while (cursor.moveToNext()) {
                    val name = cursor.getString(1)
                    check(name.isNotEmpty() && name != "." && name != ".." && '/' !in name && '\\' !in name) {
                        "Invalid document name"
                    }
                    add(Node(Docs.buildDocumentUriUsingTree(directory, cursor.getString(0)), name,
                        cursor.getString(2) == Docs.Document.MIME_TYPE_DIR))
                }
            }
        } ?: error("Cannot read source folder")
    }

    fun validateTree(tree: Uri) {
        val document = node(root(tree))
        check(document.directory) { "Select a folder" }
        children(document.uri)
    }

    fun list(): List<ModSources.Project> {
        projects.clear()
        val result = local.list().toMutableList()
        for (value in trees.sorted()) {
            val tree = Uri.parse(value)
            try {
                for (directory in children(root(tree)).filter { it.directory && !it.name.startsWith('.') }) {
                    val key = "saf:$value::${Docs.getDocumentId(directory.uri)}"
                    projects[key] = tree to directory
                    var manifest: ModManifest? = null
                    val failure = runCatching {
                        val file = children(directory.uri).singleOrNull { it.name == "manifest.json" && !it.directory }
                            ?: error("Missing manifest.json")
                        val bytes = resolver.openInputStream(file.uri)?.use { input ->
                            val buffer = java.io.ByteArrayOutputStream()
                            val chunk = ByteArray(8192)
                            while (buffer.size() <= 1024 * 1024) {
                                val count = input.read(chunk, 0, minOf(chunk.size, 1024 * 1024 + 1 - buffer.size()))
                                if (count < 0) break
                                buffer.write(chunk, 0, count)
                            }
                            buffer.toByteArray()
                        } ?: error("Cannot read manifest.json")
                        check(bytes.size <= 1024 * 1024) { "Manifest exceeds 1 MB" }
                        manifest = json.decodeFromString<ModManifest>(
                            Charsets.UTF_8.newDecoder().decode(java.nio.ByteBuffer.wrap(bytes)).toString())
                        val sourceManifest = requireNotNull(manifest)
                        check(sourceManifest.hasValidUid && sourceManifest.blVersion == 2 && sourceManifest.type == "mod" &&
                            sourceManifest.name.isNotBlank() && sourceManifest.version.isNotBlank() &&
                            sourceManifest.version.none { it.isISOControl() } &&
                            Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$").matches(sourceManifest.id)) {
                            "Invalid mod manifest"
                        }
                        val content = children(directory.uri).firstOrNull { it.name == "content" && it.directory }
                            ?: error("Missing content folder")
                        val entry = sourceManifest.entry.replace('\\', '/').split('/')
                        check(entry.all { it.isNotBlank() && it != "." && it != ".." &&
                            !it.startsWith('.') && !it.equals("build", true) }) { "Invalid entry path" }
                        var current = content.uri
                        for ((index, part) in entry.withIndex()) {
                            val next = children(current).firstOrNull { it.name == part }
                                ?: error("Missing entry file")
                            if (index == entry.lastIndex) check(!next.directory) { "Entry is a folder" }
                            else check(next.directory) { "Entry parent is not a folder" }
                            current = next.uri
                        }
                    }.exceptionOrNull()?.message
                    result += ModSources.Project(key, manifest?.name ?: directory.name, manifest, failure, value)
                }
            } catch (e: Exception) {
                result += ModSources.Project("unavailable:$value", value, null, e.message ?: "Folder unavailable", value)
            }
        }
        return result.sortedBy { it.name.lowercase() }
    }

    fun folderUri(key: String): Uri {
        projects[key]?.let { (tree, folder) ->
            check(tree.toString() in trees) { "Source location was removed" }
            return folder.uri
        }
        check(!key.startsWith("saf:") && !key.startsWith("unavailable:")) { "Project is unavailable; refresh the source list" }
        val directory = local.projectDirectoryPath(key)
        check(directory.parentFile?.canonicalFile == local.root.canonicalFile) {
            "This local folder is not exposed by the default document provider"
        }
        val id = "d:" + android.util.Base64.encodeToString(directory.name.toByteArray(Charsets.UTF_8),
            android.util.Base64.URL_SAFE or android.util.Base64.NO_WRAP or android.util.Base64.NO_PADDING)
        return Docs.buildDocumentUri(ModSourcesDocumentsProvider.AUTHORITY, id)
    }

    fun build(key: String, output: File): ModManifest {
        val external = projects[key] ?: run {
            check(!key.startsWith("saf:") && !key.startsWith("unavailable:")) { "Project is unavailable; refresh the source list" }
            return local.build(key, output)
        }
        check(external.first.toString() in trees) { "Source location was removed" }
        val scratch = File(context.cacheDir, "source-snapshots").apply { check(isDirectory || mkdirs()) }
        val staging = File(scratch, java.util.UUID.randomUUID().toString()).apply { check(mkdir()) }
        try {
            val target = File(staging, "Project").apply { check(mkdir()) }
            copyFromTree(external.second.uri, target, mutableSetOf(), 0)
            return ModSources(staging).build("Project", output)
        } finally {
            // staging is a freshly generated UUID directory under our private cache.
            staging.deleteRecursively()
        }
    }

    private fun copyFromTree(uri: Uri, target: File, visited: MutableSet<String>, depth: Int) {
        check(depth <= 64 && visited.add(Docs.getDocumentId(uri))) { "Cyclic or overly deep source folder" }
        for (child in children(uri)) {
            check(child.name.length <= 255) { "Source path segment is too long" }
            if (child.name.startsWith('.') || child.name.equals("build", true)) continue
            val out = File(target, child.name)
            check(out.canonicalFile.parentFile == target.canonicalFile) { "Invalid source path" }
            if (child.directory) {
                check(out.mkdir()) { "Cannot create snapshot folder" }
                copyFromTree(child.uri, out, visited, depth + 1)
            } else {
                resolver.openInputStream(child.uri)?.use { input -> out.outputStream().use { input.copyTo(it) } }
                    ?: error("Cannot read ${child.name}")
            }
        }
    }

    fun create(id: String, name: String, author: String, sword: String, language: String, location: String): ModSources.Project {
        if (location.isEmpty()) return local.create(id, name, author, sword, language)
        check(Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$").matches(id)) { "Invalid project ID" }
        check(location in trees) { "Source location was removed" }
        val tree = Uri.parse(location)
        val parent = root(tree)
        validateTree(tree)
        check(children(parent).none { it.name.equals(id, true) }) { "A project with this ID already exists" }
        val scratch = File(context.cacheDir, "source-templates").apply { check(isDirectory || mkdirs()) }
        val staging = File(scratch, java.util.UUID.randomUUID().toString()).apply { check(mkdir()) }
        var created: Uri? = null
        try {
            val template = ModSources(staging) { context.assets.open("templates/basic-sword.png") }
            val result = template.create(id, name, author, sword, language)
            created = Docs.createDocument(resolver, parent, Docs.Document.MIME_TYPE_DIR, id)
                ?: error("Cannot create project folder")
            copyToTree(File(staging, id), created)
            return result.copy(sourceRoot = location)
        } catch (e: Exception) {
            created?.let { runCatching { Docs.deleteDocument(resolver, it) } }
            throw e
        } finally {
            staging.deleteRecursively()
        }
    }

    private fun copyToTree(folder: File, parent: Uri) {
        for (file in requireNotNull(folder.listFiles())) {
            val child = Docs.createDocument(resolver, parent,
                if (file.isDirectory) Docs.Document.MIME_TYPE_DIR else "application/octet-stream", file.name)
                ?: error("Cannot create ${file.name}")
            if (file.isDirectory) copyToTree(file, child)
            else resolver.openOutputStream(child, "wt")?.use { output -> file.inputStream().use { it.copyTo(output) } }
                ?: error("Cannot write ${file.name}")
        }
    }
}
