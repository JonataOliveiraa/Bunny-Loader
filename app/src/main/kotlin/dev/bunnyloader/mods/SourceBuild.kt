package dev.bunnyloader.mods

import java.io.File

data class SourceBuild(val name: String, val entry: String, val references: List<String>) {

    fun looksCurrent(): Boolean {
        val project = File(entry).parentFile ?: return false
        val stamp = File(project, "bin/$name.build.json")
        if (!stamp.isFile || !File(project, "bin/$name.dll").isFile) return false
        val builtAt = stamp.lastModified()
        return project.walkTopDown()
            .onEnter { it == project || it.name !in IGNORED }
            .none { (it.isDirectory || it.extension.lowercase() in COMPILED) && it.lastModified() > builtAt }
    }

    fun encode(): String = (listOf(name, entry) + references).joinToString("\n")

    companion object {
        private val IGNORED = setOf("bin", "obj")
        private val COMPILED = setOf("cs", "dll")

        fun decode(value: String): SourceBuild {
            val parts = value.split('\n')
            return SourceBuild(parts[0], parts[1], parts.drop(2))
        }
    }
}
