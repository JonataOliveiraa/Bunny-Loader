package dev.bunnyloader.mods

/** Só anuncia versões mais novas que a instalada e que o último aviso visto. */
internal fun unseenModUpdates(
    installed: Map<String, String>,
    available: Map<String, String>,
    announced: Map<String, String>,
): Set<String> = available.filter { (uid, version) ->
    val local = installed[uid]
    val seen = announced[uid]
    local != null && compareVersions(version, local) > 0 &&
        (seen == null || compareVersions(version, seen) > 0)
}.keys
