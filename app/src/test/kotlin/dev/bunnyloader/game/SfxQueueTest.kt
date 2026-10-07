package dev.bunnyloader.game

import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicLong

class SfxQueueTest {
    private class Audio : SfxQueue.Backend {
        val calls = mutableListOf<String>()
        var result = 100
        var duringPlay: (() -> Unit)? = null
        var throws = false
        var parameters = listOf<Float>()

        override fun play(id: Int, left: Float, right: Float, rate: Float): Int {
            calls += "play:$id"
            parameters = listOf(left, right, rate)
            duringPlay?.invoke()
            if (throws) throw IllegalStateException("backend unavailable")
            return if (result <= 0) result else result++
        }

        override fun stop(stream: Int) { calls += "stop:$stream" }
        override fun pause() { calls += "pause" }
        override fun resume() { calls += "resume" }
    }

    private class Fixture(capacity: Int = 32) {
        val time = AtomicLong()
        val audio = Audio()
        val queue = SfxQueue(audio, time::get, threaded = false, capacity = capacity)
        fun enqueue(group: Int = 1, max: Int = 0, ignore: Boolean = false, duration: Int = 200, rate: Float = 1f) =
            queue.enqueue(group, group + 1, 1f, 1f, rate, duration, max, ignore)
        fun advance(ms: Long) { time.addAndGet(ms * 1_000_000) }
        fun drain() { var n = 0; while (queue.step()) assertTrue("worker did not settle", n++ < 1000) }
        fun count(name: String) = Regex("\"$name\":(\\d+)").find(queue.stats())!!.groupValues[1].toLong()
    }

    @Test fun acceptedRequestIsPendingUntilBackendConfirmsStart() {
        val f = Fixture()
        val h = f.enqueue()
        assertTrue(h > 0)
        assertEquals(SfxQueue.PENDING, f.queue.state(h))
        assertEquals(0, f.queue.active(1))
        f.drain()
        assertEquals(SfxQueue.PLAYING, f.queue.state(h))
        assertEquals(h, f.queue.active(1))
    }

    @Test fun backendZeroIsReportedAsFailureAndReleasesLimit() {
        val f = Fixture()
        f.audio.result = 0
        val h = f.enqueue(max = 1, ignore = true)
        f.drain()
        assertEquals(SfxQueue.FAILED, f.queue.state(h))
        assertEquals(0, f.queue.active(1))
        assertEquals(1, f.count("failed"))
        assertTrue(f.enqueue(max = 1, ignore = true) > 0)
    }

    @Test fun backendExceptionDoesNotKillDispatch() {
        val f = Fixture()
        f.audio.throws = true
        val h = f.enqueue()
        f.drain()
        assertEquals(SfxQueue.FAILED, f.queue.state(h))
        assertEquals(1, f.count("backendErrors"))
        f.audio.throws = false
        val next = f.enqueue()
        f.drain()
        assertEquals(SfxQueue.PLAYING, f.queue.state(next))
    }

    @Test fun fullQueueRejectsAndPreservesExistingPlayingSound() {
        val f = Fixture(capacity = 1)
        val old = f.enqueue(max = 1)
        f.drain()
        assertTrue(f.enqueue(group = 2) > 0)
        assertEquals(0, f.enqueue(max = 1))
        assertEquals(SfxQueue.PLAYING, f.queue.state(old))
    }

    @Test fun oldQueuedEffectIsDiscardedWithoutPlaying() {
        val f = Fixture()
        val h = f.enqueue()
        f.advance(51)
        f.drain()
        assertEquals(SfxQueue.EXPIRED, f.queue.state(h))
        assertTrue(f.audio.calls.isEmpty())
        assertEquals(1, f.count("expired"))
    }

    @Test fun queueDeadlineIsInclusiveAtFiftyMilliseconds() {
        val f = Fixture()
        val h = f.enqueue()
        f.advance(50)
        f.drain()
        assertEquals(SfxQueue.PLAYING, f.queue.state(h))
        f.advance(199)
        assertEquals(SfxQueue.PLAYING, f.queue.state(h))
        f.advance(1)
        assertEquals(SfxQueue.FINISHED, f.queue.state(h))
    }

    @Test fun stopBeforeStartIsIdempotentAndDoesNotCallAndroid() {
        val f = Fixture()
        val h = f.enqueue()
        f.queue.stop(h)
        f.queue.stop(h)
        f.queue.stop(Int.MAX_VALUE)
        f.drain()
        assertEquals(SfxQueue.CANCELLED, f.queue.state(h))
        assertEquals(1, f.count("cancelled"))
        assertTrue(f.audio.calls.isEmpty())
    }

    @Test fun stopUsesNativeStreamAndImmediatelyHidesHandle() {
        val f = Fixture()
        val h = f.enqueue()
        f.drain()
        f.queue.stop(h)
        assertEquals(0, f.queue.active(1))
        f.drain()
        assertEquals(listOf("play:2", "stop:100"), f.audio.calls)
    }

    @Test fun replacingPendingDoesNotPlayObsoleteRequest() {
        val f = Fixture(capacity = 1)
        val a = f.enqueue(max = 1)
        val b = f.enqueue(max = 1)
        assertEquals(SfxQueue.CANCELLED, f.queue.state(a))
        assertEquals(1, f.count("highWater"))
        f.drain()
        assertEquals(b, f.queue.active(1))
        assertEquals(listOf("play:2"), f.audio.calls)
    }

    @Test fun replacingPlayingStopsBeforeStartingNewAndGroupsAreIndependent() {
        val f = Fixture()
        val a = f.enqueue(max = 1)
        val other = f.enqueue(group = 2, max = 1)
        f.drain()
        val b = f.enqueue(max = 1)
        f.drain()
        assertEquals(SfxQueue.CANCELLED, f.queue.state(a))
        assertEquals(b, f.queue.active(1))
        assertEquals(other, f.queue.active(2))
        assertEquals(listOf("play:2", "play:3", "stop:100", "play:2"), f.audio.calls)
    }

    @Test fun ignoreNewCountsPendingRequests() {
        val f = Fixture()
        val a = f.enqueue(max = 1, ignore = true)
        assertEquals(0, f.enqueue(max = 1, ignore = true))
        f.drain()
        assertEquals(a, f.queue.active(1))
    }

    @Test fun pauseCancelsPendingAndRejectsNewRequests() {
        val f = Fixture()
        val h = f.enqueue()
        f.queue.pause()
        assertEquals(0, f.enqueue())
        f.drain()
        assertEquals(SfxQueue.CANCELLED, f.queue.state(h))
        assertEquals(listOf("pause"), f.audio.calls)
        f.queue.resume()
        f.drain()
        assertTrue(f.enqueue() > 0)
    }

    @Test fun longPauseFreezesDurationAndKeepsHandleStoppableAfterResume() {
        val f = Fixture()
        val h = f.enqueue()
        f.drain()
        f.advance(100)
        f.queue.pause()
        f.drain()
        f.advance(60_000)
        assertEquals(SfxQueue.PAUSED, f.queue.state(h))
        assertEquals(0, f.queue.active(1))
        f.queue.resume()
        f.drain()
        assertEquals(h, f.queue.active(1))
        f.queue.stop(h)
        f.drain()
        assertEquals("stop:100", f.audio.calls.last())
    }

    @Test fun resumedSoundUsesRemainingDuration() {
        val f = Fixture()
        val h = f.enqueue()
        f.drain()
        f.advance(100)
        f.queue.pause()
        f.drain()
        f.advance(5000)
        f.queue.resume()
        f.drain()
        f.advance(99)
        assertEquals(SfxQueue.PLAYING, f.queue.state(h))
        f.advance(1)
        assertEquals(SfxQueue.FINISHED, f.queue.state(h))
    }

    @Test fun stoppingPausedStreamPrecedesResume() {
        val f = Fixture()
        val h = f.enqueue()
        f.drain()
        f.queue.pause()
        f.drain()
        f.queue.stop(h)
        f.queue.resume()
        f.drain()
        assertEquals(listOf("play:2", "pause", "stop:100", "resume"), f.audio.calls)
    }

    @Test fun pauseDuringNativePlayStopsItsLateResultEvenIfAlreadyResumed() {
        val f = Fixture()
        val h = f.enqueue()
        f.audio.duringPlay = { f.queue.pause(); f.queue.resume() }
        f.drain()
        assertEquals(SfxQueue.CANCELLED, f.queue.state(h))
        assertEquals(0, f.queue.active(1))
        assertEquals(listOf("play:2", "stop:100"), f.audio.calls)
    }

    @Test fun cancelledInflightRequestCannotBeEvictedBeforeNativeResult() {
        val f = Fixture()
        val h = f.enqueue()
        f.audio.duringPlay = {
            f.queue.stop(h)
            repeat(2000) { f.queue.stop(f.enqueue()) }
            assertEquals(SfxQueue.CANCELLED, f.queue.state(h))
        }
        f.drain()
        assertEquals("stop:100", f.audio.calls.last())
        assertTrue(f.count("retained") <= 256)
    }

    @Test fun tenThousandCancellationsKeepFixedHistoryWithoutCleanupTasks() {
        val f = Fixture()
        val first = f.enqueue()
        f.queue.stop(first)
        repeat(10_000) { f.queue.stop(f.enqueue()) }
        f.drain()
        assertTrue(f.count("retained") <= 256)
        assertEquals(SfxQueue.FINISHED, f.queue.state(first))
        assertEquals(0, f.count("pending"))
        assertTrue(f.audio.calls.isEmpty())
    }

    @Test fun tenThousandRequestsCannotCreateUnboundedPendingWork() {
        val f = Fixture()
        repeat(10_000) { f.enqueue(group = it) }
        assertEquals(32, f.count("accepted"))
        assertEquals(9968, f.count("rejected"))
        assertEquals(32, f.count("highWater"))
        f.advance(51)
        f.drain()
        assertEquals(32, f.count("expired"))
    }

    @Test fun mirrorGlobalStreamLimitAfterSuccessfulStarts() {
        val f = Fixture()
        val handles = (1..33).map {
            val h = f.enqueue(group = it)
            f.audio.result++
            f.drain()
            h
        }
        assertEquals(SfxQueue.FINISHED, f.queue.state(handles.first()))
        assertEquals(SfxQueue.PLAYING, f.queue.state(handles.last()))
        assertEquals(32, f.count("active"))
    }

    @Test fun parametersAreFiniteClampedAndPitchChangesExpiry() {
        val f = Fixture()
        assertEquals(0, f.enqueue(rate = Float.NaN))
        assertEquals(0, f.queue.enqueue(1, 2, Float.POSITIVE_INFINITY, 1f, 1f, 200, 0, false))
        val h = f.queue.enqueue(1, 2, -5f, 5f, 8f, 200, 0, false)
        f.drain()
        assertEquals(listOf(0f, 1f, 2f), f.audio.parameters)
        f.advance(100)
        assertEquals(SfxQueue.FINISHED, f.queue.state(h))
    }

    @Test fun unknownDurationUsesFallbackWithoutStartingExpiryAtSubmission() {
        val f = Fixture()
        val h = f.enqueue(duration = 0)
        f.advance(40)
        f.drain()
        f.advance(999)
        assertEquals(SfxQueue.PLAYING, f.queue.state(h))
        f.advance(1)
        assertEquals(SfxQueue.FINISHED, f.queue.state(h))
    }

    @Test fun producerAndQueriesReturnWhileRealWorkerIsBlockedInBackend() {
        val entered = CountDownLatch(1)
        val release = CountDownLatch(1)
        val stopped = CountDownLatch(1)
        val backend = object : SfxQueue.Backend {
            override fun play(id: Int, left: Float, right: Float, rate: Float): Int {
                entered.countDown()
                assertTrue(release.await(5, TimeUnit.SECONDS))
                return 99
            }
            override fun stop(stream: Int) { assertEquals(99, stream); stopped.countDown() }
            override fun pause() {}
            override fun resume() {}
        }
        val queue = SfxQueue(backend)
        try {
            val h = queue.enqueue(1, 1, 1f, 1f, 1f, 200, 0, false)
            assertTrue(entered.await(3, TimeUnit.SECONDS))
            val producerDone = CountDownLatch(1)
            Thread {
                repeat(1000) { queue.enqueue(2, 2, 1f, 1f, 1f, 200, 0, false) }
                queue.stop(h)
                assertEquals(SfxQueue.CANCELLED, queue.state(h))
                queue.close()
                producerDone.countDown()
            }.start()
            assertTrue("producer waited for SoundPool", producerDone.await(3, TimeUnit.SECONDS))
            release.countDown()
            assertTrue(stopped.await(3, TimeUnit.SECONDS))
        } finally {
            release.countDown()
            queue.close()
        }
    }
}
