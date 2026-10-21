package dev.bunnyloader.mods

import android.database.Cursor
import android.database.MatrixCursor
import android.os.CancellationSignal
import android.os.ParcelFileDescriptor
import android.provider.DocumentsProvider
import android.provider.DocumentsContract.Document
import android.provider.DocumentsContract.Root
import android.util.Base64
import dev.bunnyloader.R
import android.webkit.MimeTypeMap
import java.io.File
import java.io.FileNotFoundException
import java.io.IOException

/** SAF view of the app-private ModSources directory. */
class ModSourcesDocumentsProvider : DocumentsProvider() {
    private val rootId = "mod-sources"

    private fun rootDirectory(): File {
        val context = requireNotNull(context)
        val base = context.getExternalFilesDir(null) ?: context.filesDir
        val root = File(base, "ModSources")
        if (!root.exists() && !root.mkdirs()) throw FileNotFoundException("Cannot create ModSources")
        if (!root.isDirectory || isSymlink(root)) throw FileNotFoundException("Invalid ModSources root")
        return root.canonicalFile
    }

    override fun onCreate(): Boolean = true

    override fun queryRoots(projection: Array<out String>?): Cursor {
        val columns = projection ?: ROOT_COLUMNS
        return MatrixCursor(columns).apply {
            val values = mapOf<String, Any?>(
                Root.COLUMN_ROOT_ID to rootId, Root.COLUMN_DOCUMENT_ID to rootId,
                Root.COLUMN_TITLE to "ModSources", Root.COLUMN_SUMMARY to "Mod source projects",
                Root.COLUMN_FLAGS to (Root.FLAG_SUPPORTS_CREATE or Root.FLAG_LOCAL_ONLY),
                Root.COLUMN_MIME_TYPES to "*/*", Root.COLUMN_AVAILABLE_BYTES to rootDirectory().usableSpace,
                Root.COLUMN_ICON to R.mipmap.ic_launcher,
            )
            newRow().also { row -> columns.forEach { row.add(values[it]) } }
        }
    }

    override fun queryDocument(documentId: String, projection: Array<out String>?): Cursor =
        documentCursor(documentId, projection)

    override fun queryChildDocuments(parentDocumentId: String, projection: Array<out String>?, sortOrder: String?): Cursor {
        val parent = resolve(parentDocumentId)
        if (!parent.isDirectory) throw FileNotFoundException("Not a folder")
        val columns = projection ?: DOCUMENT_COLUMNS
        val result = MatrixCursor(columns)
        val children = parent.listFiles() ?: throw FileNotFoundException("Cannot list folder")
        children.sortedBy { it.name.lowercase() }.forEach { child ->
            if (!child.name.startsWith('.') && !isSymlink(child)) addDocumentRow(result, child)
        }
        return result
    }

    override fun createDocument(parentDocumentId: String, mimeType: String, displayName: String): String {
        val parent = resolve(parentDocumentId)
        requireDirectory(parent)
        validateName(displayName)
        val file = File(parent, displayName)
        verifyContained(file)
        if (file.exists()) throw FileNotFoundException("Document already exists")
        val created = if (mimeType == Document.MIME_TYPE_DIR) file.mkdir() else file.createNewFile()
        if (!created) throw IOException("Cannot create document")
        verifyContained(file)
        return encode(file)
    }

    override fun deleteDocument(documentId: String) {
        val file = resolve(documentId)
        if (file == rootDirectory()) throw FileNotFoundException("Cannot delete root")
        fun deleteSafely(item: File) {
            verifyContained(item)
            if (item.isDirectory) {
                (item.listFiles() ?: throw IOException("Cannot list document")).forEach { deleteSafely(it) }
            }
            if (!item.delete()) throw IOException("Cannot delete document")
        }
        deleteSafely(file)
    }

    override fun renameDocument(documentId: String, displayName: String): String {
        val file = resolve(documentId)
        if (file == rootDirectory()) throw FileNotFoundException("Cannot rename root")
        validateName(displayName)
        val destination = File(file.parentFile, displayName)
        verifyContained(destination)
        if (destination.exists() || !file.renameTo(destination)) throw IOException("Cannot rename document")
        return encode(destination)
    }

    override fun isChildDocument(parentDocumentId: String, documentId: String): Boolean {
        val parent = resolve(parentDocumentId).canonicalFile
        val child = resolve(documentId).canonicalFile
        return child != parent && child.path.startsWith(parent.path + File.separator)
    }

    override fun openDocument(documentId: String, mode: String, signal: CancellationSignal?): ParcelFileDescriptor {
        val file = resolve(documentId)
        if (file.isDirectory) throw FileNotFoundException("Cannot open a folder")
        val flags = when (mode) {
            "r" -> ParcelFileDescriptor.MODE_READ_ONLY
            "w", "wt" -> ParcelFileDescriptor.MODE_WRITE_ONLY or ParcelFileDescriptor.MODE_CREATE or ParcelFileDescriptor.MODE_TRUNCATE
            "wa" -> ParcelFileDescriptor.MODE_WRITE_ONLY or ParcelFileDescriptor.MODE_CREATE or ParcelFileDescriptor.MODE_APPEND
            "rw" -> ParcelFileDescriptor.MODE_READ_WRITE or ParcelFileDescriptor.MODE_CREATE
            "rwt" -> ParcelFileDescriptor.MODE_READ_WRITE or ParcelFileDescriptor.MODE_CREATE or ParcelFileDescriptor.MODE_TRUNCATE
            else -> throw IllegalArgumentException("Unsupported mode")
        }
        return ParcelFileDescriptor.open(file, flags)
    }

    private fun documentCursor(id: String, projection: Array<out String>?): Cursor {
        val file = resolve(id)
        val result = MatrixCursor(projection ?: DOCUMENT_COLUMNS)
        addDocumentRow(result, file)
        return result
    }

    private fun addDocumentRow(cursor: MatrixCursor, file: File) {
        verifyContained(file)
        val id = encode(file)
        val directory = file.isDirectory
        val mime = if (directory) Document.MIME_TYPE_DIR else mimeType(file)
        val flags = (if (directory) Document.FLAG_DIR_SUPPORTS_CREATE else Document.FLAG_SUPPORTS_WRITE) or
            (if (file == rootDirectory()) 0 else (Document.FLAG_SUPPORTS_DELETE or Document.FLAG_SUPPORTS_RENAME))
        val values = mapOf<String, Any?>(
            Document.COLUMN_DOCUMENT_ID to id,
            Document.COLUMN_DISPLAY_NAME to file.name.ifEmpty { "ModSources" },
            Document.COLUMN_MIME_TYPE to mime, Document.COLUMN_FLAGS to flags,
            Document.COLUMN_SIZE to if (directory) 0 else file.length(),
            Document.COLUMN_LAST_MODIFIED to file.lastModified(),
        )
        cursor.newRow().also { row -> cursor.columnNames.forEach { row.add(values[it]) } }
    }

    private fun resolve(id: String): File {
        val root = rootDirectory()
        if (id == rootId) return root
        if (!id.startsWith("d:")) throw FileNotFoundException("Invalid document ID")
        val relative = try {
            String(Base64.decode(id.substring(2), Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING), Charsets.UTF_8)
        } catch (_: Exception) { throw FileNotFoundException("Invalid document ID") }
        val parts = relative.split('/')
        if (relative.length > 4096 || parts.size > 64) throw FileNotFoundException("Document path is too deep")
        if (relative.isEmpty() || parts.any { it.isEmpty() || it == "." || it == ".." || it.startsWith('.') || it.contains('\\') || it.contains(':') || it.any(Char::isISOControl) }) {
            throw FileNotFoundException("Invalid document path")
        }
        val file = File(root, relative)
        verifyContained(file)
        return file
    }

    private fun encode(file: File): String {
        val root = rootDirectory()
        verifyContained(file)
        val relative = file.canonicalFile.relativeTo(root).path.replace(File.separatorChar, '/')
        if (relative.isEmpty()) return rootId
        return "d:" + Base64.encodeToString(relative.toByteArray(Charsets.UTF_8), Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING)
    }

    private fun verifyContained(file: File) {
        val root = rootDirectory()
        var current: File? = file.absoluteFile
        while (current != null && current != root) {
            if (isSymlink(current)) throw FileNotFoundException("Symbolic links are not allowed")
            current = current.parentFile
        }
        val canonical = file.canonicalFile
        if (canonical != root && !canonical.path.startsWith(root.path + File.separator)) {
            throw FileNotFoundException("Document is outside ModSources")
        }
    }

    private fun isSymlink(file: File): Boolean {
        val absolute = file.absoluteFile
        val parent = absolute.parentFile ?: return false
        val expected = File(parent.canonicalFile, absolute.name)
        return expected.canonicalFile != expected.absoluteFile
    }

    private fun requireDirectory(file: File) {
        if (!file.isDirectory) throw FileNotFoundException("Not a folder")
    }

    private fun validateName(name: String) {
        if (name.isBlank() || name == "." || name == ".." || name.startsWith('.') ||
            name.contains('/') || name.contains('\\') || name.contains(':') || name.any(Char::isISOControl)) {
            throw IllegalArgumentException("Invalid document name")
        }
    }

    private fun mimeType(file: File): String = when (file.extension.lowercase()) {
        "js", "json", "md", "txt", "hjson" -> "text/plain"
        "png" -> "image/png"
        "jpg", "jpeg" -> "image/jpeg"
        "zip", "bl" -> "application/zip"
        else -> MimeTypeMap.getSingleton().getMimeTypeFromExtension(file.extension.lowercase()) ?: "application/octet-stream"
    }

    companion object {
        const val AUTHORITY = "com.bunnyloader.documents"
        private val ROOT_COLUMNS = arrayOf(
            Root.COLUMN_ROOT_ID, Root.COLUMN_DOCUMENT_ID, Root.COLUMN_TITLE, Root.COLUMN_SUMMARY,
            Root.COLUMN_FLAGS, Root.COLUMN_MIME_TYPES, Root.COLUMN_AVAILABLE_BYTES, Root.COLUMN_ICON,
        )
        private val DOCUMENT_COLUMNS = arrayOf(
            Document.COLUMN_DOCUMENT_ID, Document.COLUMN_DISPLAY_NAME, Document.COLUMN_MIME_TYPE,
            Document.COLUMN_FLAGS, Document.COLUMN_SIZE, Document.COLUMN_LAST_MODIFIED,
        )
    }
}
