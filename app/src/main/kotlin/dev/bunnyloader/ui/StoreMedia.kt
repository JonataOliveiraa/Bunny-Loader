package dev.bunnyloader.ui

import androidx.compose.runtime.MutableState
import androidx.compose.runtime.mutableStateOf
import dev.bunnyloader.mods.Catalog

/** One observable cell per uid: changing a photo cannot invalidate other mods. */
internal class StoreMedia {
    private val cells = mutableMapOf<String, MutableState<Catalog.Entry?>>()

    private fun cell(uid: String) = cells.getOrPut(uid) { mutableStateOf(null) }

    fun entryFor(entry: Catalog.Entry): Catalog.Entry {
        val remote = entry.remote ?: return entry
        return cell(entry.uid).value?.takeIf {
            it.remote?.download?.sha256 == remote.download.sha256
        } ?: entry
    }

    fun publish(entry: Catalog.Entry) {
        val state = cell(entry.uid)
        // A text/photo arrival matters even if icon/banner paths did not change.
        state.value = entry.copy(mediaRevision = (state.value?.mediaRevision ?: 0) + 1)
    }
}
