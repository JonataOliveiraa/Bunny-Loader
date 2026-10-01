package dev.bunnyloader.mods

import java.io.File

/** Caminhos dos documentos e imagens, sempre relativos à raiz do pacote. */
internal fun resolvePackagePath(base: String, onDisk: Boolean, relative: String): String? {
    val path = relative.replace('\\', '/')
    if (path.startsWith('/') || ':' in path || path.any { it.isISOControl() }) return null
    val parts = path.split('/').filter { it.isNotEmpty() && it != "." }
    if (parts.isEmpty() || parts.any { it == ".." }) return null
    val normalized = parts.joinToString("/")
    if (!onDisk) return "$base/$normalized"

    // Também confere links simbólicos em pacotes colocados à mão no disco.
    return runCatching {
        val root = File(base).canonicalFile
        File(root, normalized).canonicalFile.takeIf {
            it.path.startsWith(root.path + File.separator)
        }?.path
    }.getOrNull()
}
