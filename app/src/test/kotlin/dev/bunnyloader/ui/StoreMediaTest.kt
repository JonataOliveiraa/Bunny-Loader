package dev.bunnyloader.ui

import androidx.compose.runtime.snapshots.Snapshot
import androidx.compose.runtime.snapshots.SnapshotStateObserver
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.RemoteDownload
import dev.bunnyloader.mods.RemoteMod
import org.junit.Assert.*
import org.junit.Test

class StoreMediaTest {
    private fun entry(uid: String, sha: String = "a") : Catalog.Entry {
        val manifest = ModManifest(uid = uid, id = uid, name = uid, version = "1.0")
        return Catalog.Entry(manifest, "/store/$uid", 10, emptyList(), null, null,
            remote = RemoteMod(uid, download = RemoteDownload("https://example.invalid", sha), manifest = manifest))
    }

    @Test fun arrivalInvalidatesOnlyReadersOfThatMod() {
        val media = StoreMedia()
        val first = entry("one")
        val second = entry("two")
        val invalidated = mutableListOf<String>()
        val observer = SnapshotStateObserver { it() }
        observer.start()
        try {
            val changed: (String) -> Unit = { invalidated.add(it) }
            observer.observeReads("one", changed) { media.entryFor(first) }
            observer.observeReads("two", changed) { media.entryFor(second) }
            Snapshot.withMutableSnapshot { media.publish(first.copy(iconAsset = "/store/one/icon.png")) }
            Snapshot.sendApplyNotifications()
            assertEquals(listOf("one"), invalidated)
            assertSame(second, media.entryFor(second))
        } finally {
            observer.stop()
            observer.clear()
        }
    }

    @Test fun textOnlyArrivalIsObservableWithoutReplacingCatalogMetadata() {
        val media = StoreMedia()
        val original = entry("one")
        media.publish(original)
        val first = media.entryFor(original)
        media.publish(original)
        val second = media.entryFor(original)
        assertEquals(0, original.mediaRevision)
        assertEquals(first.mediaRevision + 1, second.mediaRevision)
        assertSame(original.manifest, second.manifest)
        assertNotEquals(first, second)
    }

    @Test fun cachedStorefrontFromOldPackageCannotOverrideUpdatedEntry() {
        val media = StoreMedia()
        media.publish(entry("one", "old").copy(iconAsset = "/old/icon.png"))
        val updated = entry("one", "new")
        assertSame(updated, media.entryFor(updated))
        media.publish(updated.copy(iconAsset = "/new/icon.png"))
        assertEquals("/new/icon.png", media.entryFor(updated).iconAsset)
    }
}
