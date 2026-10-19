package dev.bunnyloader.game

import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.mods.PackType
import dev.bunnyloader.mods.RemoteMod

/** Exact host versions, shared by planning, package admission and session validation. */
internal object ServerModSync {
    data class Plan(
        val download: List<Pair<ServerSync.HostMod, RemoteMod>>,
        val enable: List<ServerSync.HostMod>,
        val disable: List<ModManifest>,
        val missing: List<String>,
    )

    fun label(mod: ServerSync.HostMod): String =
        mod.name.ifEmpty { mod.id.ifEmpty { mod.uid } } +
            if (mod.version.isNotEmpty()) " v${mod.version}" else ""

    private fun matches(mod: ServerSync.HostMod, manifest: ModManifest): Boolean =
        mod.version.isNotBlank() && manifest.uid == mod.uid && manifest.version == mod.version &&
            manifest.packType == PackType.MOD &&
            manifest.blVersion in ModRepository.MIN_BL_VERSION..ModRepository.BL_VERSION

    fun requireMatch(mod: ServerSync.HostMod, manifest: ModManifest) {
        require(matches(mod, manifest)) {
            "${label(mod)}: o pacote encontrado é ${manifest.name} v${manifest.version} " +
                "(uid ${manifest.uid}, tipo ${manifest.packType.label}, formato ${manifest.blVersion}); " +
                "é necessária a versão exata do servidor"
        }
    }

    fun plan(
        required: List<ServerSync.HostMod>,
        installed: List<ModManifest>,
        shop: List<RemoteMod>,
        enabled: Set<String>,
    ): Plan {
        require(required.all { ModManifest.isValidUid(it.uid) }) { "o servidor informou um uid inválido" }
        val hostUids = required.map { it.uid }.toSet()
        require(hostUids.size == required.size) { "o servidor repetiu um mod na lista" }
        val local = installed.associateBy { it.uid }
        val available = shop.associateBy { it.uid }
        val download = mutableListOf<Pair<ServerSync.HostMod, RemoteMod>>()
        val enable = mutableListOf<ServerSync.HostMod>()
        val missing = mutableListOf<String>()

        for (mod in required) {
            val have = local[mod.uid]
            val remote = available[mod.uid]
            when {
                mod.version.isBlank() -> missing += label(mod) + " (o servidor não informou a versão)"
                have != null && matches(mod, have) -> {
                    if (mod.uid !in enabled) enable += mod
                }
                remote != null && remote.version == mod.version &&
                    (remote.manifest == null || matches(mod, remote.manifest)) -> download += mod to remote
                else -> {
                    val details = buildList {
                        if (have != null) add("você tem a ${have.version}")
                        if (remote != null) add("a loja tem a ${remote.version}")
                    }
                    missing += label(mod) + if (details.isEmpty()) "" else " (${details.joinToString("; ")})"
                }
            }
        }

        val disable = installed.filter {
            it.packType == PackType.MOD && it.uid !in hostUids && it.uid in enabled && !it.isOutdated
        }
        return Plan(download, enable, disable, missing)
    }

    /** Re-read versions after downloads: the repository may have changed since planning. */
    fun sessionSpecs(
        required: List<ServerSync.HostMod>, installed: List<ModManifest>, textures: List<String>, fonts: List<String> = emptyList(),
    ): List<String> {
        val byUid = installed.associateBy { it.uid }
        return required.map { mod ->
            val manifest = byUid[mod.uid] ?: error("${label(mod)} não ficou instalado")
            requireMatch(mod, manifest)
            "${mod.uid}=${manifest.entry}"
        } + textures.filter { byUid[it]?.packType == PackType.TEXTURE }.map { "$it=@texture" } +
            fonts.filter { byUid[it]?.packType == PackType.FONT }.map { "$it=@font" }
    }
}
