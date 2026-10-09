package dev.bunnyloader.game

import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.RemoteDownload
import dev.bunnyloader.mods.RemoteMod
import org.junit.Assert.*
import org.junit.Test

class ServerModSyncTest {
    private val uid = "11111111-1111-1111-1111-111111111111"
    private val other = "22222222-2222-2222-2222-222222222222"
    private fun host(version: String = "1.1.0", id: String = uid) = ServerSync.HostMod(id, "probe", "Probe", version)
    private fun local(version: String = "1.0.0", id: String = uid) =
        ModManifest(uid = id, id = "probe", name = "Probe", version = version, blVersion = 2)
    private fun remote(version: String = "1.1.0") = RemoteMod(uid, version,
        RemoteDownload("https://example.invalid/probe.bl", "a".repeat(64)), manifest = local(version))
    private fun plan(required: ServerSync.HostMod = host(), installed: ModManifest = local(), store: List<RemoteMod> = listOf(remote()), enabled: Set<String> = setOf(uid)) =
        ServerModSync.plan(listOf(required), listOf(installed), store, enabled)

    private fun rejected(body: () -> Unit) {
        try {
            body()
            fail("An incompatible package/session was accepted")
        } catch (_: IllegalArgumentException) {
        } catch (_: IllegalStateException) {
        }
    }

    @Test fun olderClientDownloadsExactHostVersion() {
        val result = plan()
        assertEquals("1.1.0", result.download.single().second.version)
        assertTrue(result.missing.isEmpty())
    }

    @Test fun newerClientDownloadsHostVersionInsteadOfKeepingNewerOne() {
        val result = plan(host("1.0.0"), local("1.1.0"), listOf(remote("1.0.0")))
        assertEquals("1.0.0", result.download.single().second.version)
    }

    @Test fun wrongStoreVersionCannotBeUsedAsReplacement() {
        val result = plan(store = listOf(remote("1.2.0")))
        assertTrue(result.download.isEmpty())
        assertTrue(result.missing.single().contains("v1.1.0"))
        assertTrue(result.missing.single().contains("você tem a 1.0.0"))
        assertTrue(result.missing.single().contains("a loja tem a 1.2.0"))
    }

    @Test fun unavailableRequiredVersionBlocksOfflineSync() {
        assertEquals(1, plan(store = emptyList()).missing.size)
    }

    @Test fun exactInstalledVersionWorksOfflineWithoutDownload() {
        val result = plan(installed = local("1.1.0"), store = emptyList())
        assertTrue(result.download.isEmpty())
        assertTrue(result.enable.isEmpty())
        assertTrue(result.missing.isEmpty())
    }

    @Test fun disabledExactVersionIsEnabledForTheSession() {
        assertEquals(listOf(host()), plan(installed = local("1.1.0"), enabled = emptySet()).enable)
    }

    @Test fun blankVersionsCannotMatchEvenWhenBothSidesAreBlank() {
        for (version in listOf("", " ", "\t")) {
            val result = plan(host(version), local(version), listOf(remote(version)))
            assertTrue(result.download.isEmpty())
            assertEquals(1, result.missing.size)
            rejected { ServerModSync.requireMatch(host(version), local(version)) }
        }
    }

    @Test fun preReleaseAndBuildVersionsMustMatchExactly() {
        for (version in listOf("1.1.0-beta", "1.1.0+build1", "1.1", "v1.1.0")) {
            assertEquals(1, plan(installed = local(version), store = emptyList()).missing.size)
        }
        assertTrue(plan(host("1.1.0-beta"), local("1.1.0-beta"), emptyList()).missing.isEmpty())
    }

    @Test fun downloadedPackageMustMatchUidVersionTypeAndFormat() {
        val expected = host()
        for (manifest in listOf(local("1.0.0"), local("1.1.0", other),
            local("1.1.0").copy(type = "texture"), local("1.1.0").copy(blVersion = 1),
            local("1.1.0").copy(blVersion = 3))) {
            rejected { ServerModSync.requireMatch(expected, manifest) }
        }
        ServerModSync.requireMatch(expected, local("1.1.0"))
    }

    @Test fun inconsistentStoreManifestIsNotScheduled() {
        assertEquals(1, plan(store = listOf(remote().copy(manifest = local("1.0.0")))).missing.size)
        assertEquals(1, plan(store = listOf(remote().copy(manifest = local("1.1.0", other)))).missing.size)
    }

    @Test fun oldIndexWithoutEmbeddedManifestCanDownloadAndValidateLater() {
        assertEquals(1, plan(store = listOf(remote().copy(manifest = null))).download.size)
    }

    @Test fun sessionRechecksVersionsAfterPlanning() {
        val required = listOf(host())
        val initially = listOf(local("1.1.0"))
        assertTrue(ServerModSync.plan(required, initially, emptyList(), setOf(uid)).missing.isEmpty())
        rejected { ServerModSync.sessionSpecs(required, listOf(local("1.0.0")), emptyList()) }
        rejected { ServerModSync.sessionSpecs(required, emptyList(), emptyList()) }
    }

    @Test fun sessionPreservesHostOrderEntryPointsAndOnlyValidTextures() {
        val installed = listOf(local("1.1.0"), local("1.1.0", other).copy(entry = "start.js"),
            local("5", "33333333-3333-3333-3333-333333333333").copy(type = "texture"))
        val textures = listOf(uid, "missing", installed.last().uid)
        assertEquals(listOf("$other=start.js", "$uid=main.js", "${installed.last().uid}=@texture"),
            ServerModSync.sessionSpecs(listOf(host(id = other), host()), installed, textures))
    }

    @Test fun extraModsAreDisabledForThisSessionButTexturesStayOutsideTheModPlan() {
        val texture = local(id = "33333333-3333-3333-3333-333333333333").copy(type = "texture")
        val extra = local(id = other)
        val result = ServerModSync.plan(listOf(host()), listOf(local("1.1.0"), extra, texture), emptyList(),
            setOf(uid, other, texture.uid))
        assertEquals(listOf(extra), result.disable)
    }

    @Test fun invalidOrDuplicateHostIdentityIsRejected() {
        rejected { ServerModSync.plan(listOf(host(id = "../mods")), emptyList(), emptyList(), emptySet()) }
        rejected { ServerModSync.plan(listOf(host(), host()), emptyList(), emptyList(), emptySet()) }
    }

    @Test fun emptyHostModListProducesAnEmptyModSession() {
        val result = ServerModSync.plan(emptyList(), listOf(local()), emptyList(), setOf(uid))
        assertEquals(listOf(local()), result.disable)
        assertTrue(ServerModSync.sessionSpecs(emptyList(), listOf(local()), emptyList()).isEmpty())
    }
}
