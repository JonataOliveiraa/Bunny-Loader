package dev.bunnyloader.game

import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicLong

class MusicQueueTest {
    private class Audio : MusicQueue.Backend {
        val calls = mutableListOf<String>()
        var throwOn: String? = null
        var errors = 0

        private fun call(name: String) {
            calls += name
            if (name == throwOn) throw IllegalStateException(name)
        }

        override fun prepare(id: Int, path: String) = call("prepare")
        override fun volume(id: Int, value: Float) = call("volume:$value")
        override fun start(id: Int) = call("start")
        override fun pause(id: Int) = call("pause")
        override fun rewind(id: Int) = call("rewind")
        override fun failed(id: Int, error: Exception) { errors++ }
    }

    private class Fixture {
        val time = AtomicLong()
        val audio = Audio()
        val queue = MusicQueue(audio, time::get, threaded = false)
        val id = queue.register("track.ogg")
        fun advance(ms: Long) { time.addAndGet(ms * 1_000_000) }
        fun drain() { var n = 0; while (queue.step()) assertTrue("worker did not settle", n++ < 1000) }
        fun playing() {
            queue.setVolume(id, 0.5f)
            drain()
            queue.onPrepared(id)
            drain()
            audio.calls.clear()
        }
    }

    @Test fun nothingIsLoadedUntilTheTrackIsAudible() {
        val f = Fixture()
        f.queue.setVolume(f.id, 0f)
        f.drain()
        assertTrue(f.audio.calls.isEmpty())
        assertEquals(MusicQueue.STOPPED, f.queue.state(f.id))
    }

    @Test fun producerNeverTouchesTheBackend() {
        val f = Fixture()
        f.queue.setVolume(f.id, 0.5f)
        f.queue.state(f.id)
        f.queue.setVolume(f.id, 0f)
        assertTrue(f.audio.calls.isEmpty())
    }

    @Test fun preparesOnceThenSetsVolumeBeforeStarting() {
        val f = Fixture()
        f.queue.setVolume(f.id, 0.5f)
        f.drain()
        assertEquals(listOf("prepare"), f.audio.calls)
        assertEquals(MusicQueue.PREPARING, f.queue.state(f.id))
        f.queue.onPrepared(f.id)
        f.drain()
        assertEquals(listOf("prepare", "volume:0.5", "start"), f.audio.calls)
        assertEquals(MusicQueue.PLAYING, f.queue.state(f.id))
    }

    @Test fun volumeBurstIsCoalescedIntoTheLatestValue() {
        val f = Fixture()
        f.playing()
        for (step in 1..10) {
            f.queue.setVolume(f.id, step / 10f)
            f.advance(1)
            f.drain()
        }
        assertTrue(f.audio.calls.isEmpty())
        f.advance(33)
        f.drain()
        assertEquals(listOf("volume:1.0"), f.audio.calls)
    }

    @Test fun rapidTogglingNeverPausesOrRestarts() {
        val f = Fixture()
        f.playing()
        repeat(50) {
            f.queue.setVolume(f.id, 0f)
            f.drain()
            f.advance(100)
            f.queue.setVolume(f.id, 0.01f)
            f.drain()
            f.advance(100)
        }
        assertFalse(f.audio.calls.any { it == "pause" || it == "rewind" || it == "start" })
    }

    @Test fun staysSilentThenStopsAndRewindsAfterTheDelay() {
        val f = Fixture()
        f.playing()
        f.queue.setVolume(f.id, 0f)
        f.drain()
        assertEquals(listOf("volume:0.0"), f.audio.calls)
        assertEquals(MusicQueue.STOPPED, f.queue.state(f.id))
        f.advance(499)
        f.drain()
        assertEquals(1, f.audio.calls.size)
        f.advance(1)
        f.drain()
        assertEquals(listOf("volume:0.0", "pause", "rewind"), f.audio.calls)
        f.audio.calls.clear()
        f.queue.setVolume(f.id, 0.2f)
        f.drain()
        assertEquals(listOf("volume:0.2", "start"), f.audio.calls)
    }

    @Test fun appPauseKeepsThePositionAndResumeStartsAgain() {
        val f = Fixture()
        f.playing()
        f.queue.pause()
        f.drain()
        assertEquals(listOf("pause"), f.audio.calls)
        assertEquals(MusicQueue.STOPPED, f.queue.state(f.id))
        f.queue.setVolume(f.id, 0.7f)
        f.drain()
        assertEquals(listOf("pause"), f.audio.calls)
        f.audio.calls.clear()
        f.queue.resume()
        f.drain()
        assertEquals(listOf("volume:0.7", "start"), f.audio.calls)
    }

    @Test fun appPauseOfASilentTrackRewinds() {
        val f = Fixture()
        f.playing()
        f.queue.setVolume(f.id, 0f)
        f.drain()
        f.audio.calls.clear()
        f.queue.pause()
        f.drain()
        assertEquals(listOf("pause", "rewind"), f.audio.calls)
    }

    @Test fun trackIsNotPreparedWhileTheAppIsPaused() {
        val f = Fixture()
        f.queue.pause()
        f.queue.setVolume(f.id, 0.5f)
        f.drain()
        assertTrue(f.audio.calls.isEmpty())
        f.queue.resume()
        f.drain()
        assertEquals(listOf("prepare"), f.audio.calls)
    }

    @Test fun backendExceptionFailsOnlyThatTrack() {
        val f = Fixture()
        val other = f.queue.register("other.ogg")
        f.audio.throwOn = "prepare"
        f.queue.setVolume(f.id, 0.5f)
        f.drain()
        assertEquals(MusicQueue.FAILED, f.queue.state(f.id))
        assertEquals(1, f.audio.errors)
        f.audio.throwOn = null
        f.audio.calls.clear()
        f.queue.setVolume(f.id, 0.9f)
        f.queue.setVolume(other, 0.5f)
        f.drain()
        assertEquals(listOf("prepare"), f.audio.calls)
        assertEquals(MusicQueue.PREPARING, f.queue.state(other))
    }

    @Test fun playerErrorFailsTheTrack() {
        val f = Fixture()
        f.playing()
        f.queue.onFailed(f.id)
        f.queue.setVolume(f.id, 0.9f)
        f.drain()
        assertTrue(f.audio.calls.isEmpty())
        assertEquals(MusicQueue.FAILED, f.queue.state(f.id))
    }

    @Test fun unknownTrackIsFailed() {
        assertEquals(MusicQueue.FAILED, Fixture().queue.state(99))
    }

    @Test fun workerAppliesTheLatestStateOffTheCallingThread() {
        val started = CountDownLatch(1)
        lateinit var startThread: Thread
        lateinit var queue: MusicQueue
        val audio = object : MusicQueue.Backend {
            override fun prepare(id: Int, path: String) = queue.onPrepared(id)
            override fun volume(id: Int, value: Float) {}
            override fun start(id: Int) {
                startThread = Thread.currentThread()
                started.countDown()
            }
            override fun pause(id: Int) {}
            override fun rewind(id: Int) {}
            override fun failed(id: Int, error: Exception) {}
        }
        queue = MusicQueue(audio)
        val id = queue.register("track.ogg")
        queue.setVolume(id, 0.5f)
        assertTrue(started.await(5, TimeUnit.SECONDS))
        assertNotSame(Thread.currentThread(), startThread)
    }
}
