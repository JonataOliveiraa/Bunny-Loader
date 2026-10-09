package dev.bunnyloader.mods

import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger

class MediaCacheTest {
    @Test fun failedDecodeIsRememberedButNewRevisionCanLoad() {
        val cache = MediaCache<String, String>(100) { it.length }
        var calls = 0
        repeat(100) { assertNull(cache.get("icon@1") { calls++; null }) }
        assertEquals(1, calls)
        assertEquals("valid", cache.get("icon@2") { "valid" })
    }

    @Test fun byteBudgetEvictsLeastRecentlyUsedAcrossMediaTypes() {
        val cache = MediaCache<String, String>(8) { it.length }
        cache.get("gif") { "1234" }
        cache.get("png") { "abcd" }
        assertEquals("1234", cache.get("gif") { fail("unexpected decode"); null })
        cache.get("avatar") { "xyz" }
        assertEquals(7, cache.retainedBytes())
        assertEquals("new", cache.get("png") { "new" })
        assertTrue(cache.retainedBytes() <= 8)
    }

    @Test fun negativeEntriesHaveACountLimit() {
        val cache = MediaCache<Int, String>(100, maxEntries = 16) { it.length }
        repeat(10_000) { assertNull(cache.get(it) { null }) }
        assertEquals(16, cache.retainedEntries())
        assertEquals(0, cache.retainedBytes())
    }

    @Test fun oversizedValuesDoNotDisplaceUsefulCache() {
        val cache = MediaCache<String, String>(4) { it.length }
        cache.get("small") { "ok" }
        assertEquals("oversized", cache.get("large") { "oversized" })
        assertEquals(2, cache.retainedBytes())
        assertEquals("ok", cache.get("small") { fail("evicted"); null })
    }

    @Test fun exceptionDoesNotPoisonKeyOrLeakDecodeSlot() {
        val cache = MediaCache<String, String>(100, parallelism = 1) { it.length }
        try {
            cache.get("broken") { error("decode") }
            fail("exception expected")
        } catch (_: IllegalStateException) {
            assertEquals("recovered", cache.get("broken") { "recovered" })
        }
    }

    @Test fun concurrentRequestsForOneKeyDecodeOnce() {
        val cache = MediaCache<String, String>(100) { it.length }
        val calls = AtomicInteger()
        val started = CountDownLatch(1)
        val release = CountDownLatch(1)
        val executor = Executors.newFixedThreadPool(8)
        try {
            val results = (0 until 8).map {
                executor.submit<String?> {
                    cache.get("shared") {
                        calls.incrementAndGet()
                        started.countDown()
                        assertTrue(release.await(5, TimeUnit.SECONDS))
                        "same"
                    }
                }
            }
            assertTrue(started.await(5, TimeUnit.SECONDS))
            release.countDown()
            results.forEach { assertEquals("same", it.get(5, TimeUnit.SECONDS)) }
            assertEquals(1, calls.get())
        } finally {
            release.countDown()
            executor.shutdownNow()
        }
    }

    @Test fun differentMediaShareTwoDecodeSlots() {
        val cache = MediaCache<Int, String>(100, parallelism = 2) { it.length }
        val active = AtomicInteger()
        val peak = AtomicInteger()
        val started = CountDownLatch(2)
        val release = CountDownLatch(1)
        val executor = Executors.newFixedThreadPool(12)
        try {
            val results = (0 until 12).map { key ->
                executor.submit<String?> {
                    cache.get(key) {
                        val count = active.incrementAndGet()
                        peak.accumulateAndGet(count, ::maxOf)
                        started.countDown()
                        try {
                            assertTrue(release.await(5, TimeUnit.SECONDS))
                            "image"
                        } finally {
                            active.decrementAndGet()
                        }
                    }
                }
            }
            assertTrue(started.await(5, TimeUnit.SECONDS))
            assertEquals(2, active.get())
            release.countDown()
            results.forEach { assertEquals("image", it.get(5, TimeUnit.SECONDS)) }
            assertEquals(2, peak.get())
        } finally {
            release.countDown()
            executor.shutdownNow()
        }
    }
}
