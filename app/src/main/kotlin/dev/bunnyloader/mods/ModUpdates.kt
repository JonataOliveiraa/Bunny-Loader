package dev.bunnyloader.mods

/** A última vez que o aviso de atualização de um mod apareceu, e de qual versão. */
internal data class UpdateNotice(val version: String, val shownAt: Long)

/** Uma versão recusada ("Agora não") volta a ser lembrada depois disto. */
internal const val UPDATE_REMIND_MS = 12L * 60 * 60 * 1000

/**
 * Anuncia versões mais novas que a instalada. Uma versão que o usuário já viu
 * só volta depois de [UPDATE_REMIND_MS]; uma versão mais nova que a vista
 * aparece na hora.
 */
internal fun dueModUpdates(
    installed: Map<String, String>,
    available: Map<String, String>,
    announced: Map<String, UpdateNotice>,
    now: Long,
): Set<String> = available.filter { (uid, version) ->
    val local = installed[uid]
    val seen = announced[uid]
    local != null && compareVersions(version, local) > 0 &&
        (seen == null || compareVersions(version, seen.version) > 0 || now - seen.shownAt >= UPDATE_REMIND_MS)
}.keys
