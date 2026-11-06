package dev.bunnyloader.game

internal class MusicQueue(
    private val backend: Backend,
    private val clock: () -> Long = System::nanoTime,
    private val threaded: Boolean = true,
    private val stopDelayMs: Long = 500,
    private val volumeIntervalMs: Long = 33,
) {
    interface Backend {
        fun prepare(id: Int, path: String)
        fun volume(id: Int, value: Float)
        fun start(id: Int)
        fun pause(id: Int)
        fun rewind(id: Int)
        fun failed(id: Int, error: Exception)
    }

    companion object {
        const val FAILED = -1
        const val STOPPED = 0
        const val PREPARING = 1
        const val PLAYING = 2
        private const val MS = 1_000_000L
    }

    private class Track(val id: Int, val path: String) {
        var desired = 0f
        var silentSince = 0L
        var created = false
        var prepared = false
        var failed = false
        var playing = false
        var sent = -1f
        var sentAt = 0L
    }

    private sealed interface Work {
        val track: Track
        class Prepare(override val track: Track) : Work
        class Volume(override val track: Track, val value: Float) : Work
        class Start(override val track: Track) : Work
        class Pause(override val track: Track) : Work
        class Stop(override val track: Track) : Work
    }

    private val lock = Object()
    private val tracks = LinkedHashMap<Int, Track>()
    private var nextId = 1
    private var paused = false
    private var revision = 0L
    private var worker: Thread? = null

    fun register(path: String): Int = synchronized(lock) {
        val id = nextId++
        tracks[id] = Track(id, path)
        id
    }

    fun setVolume(id: Int, volume: Float) = synchronized(lock) {
        val t = tracks[id] ?: return@synchronized
        if (t.failed || t.desired == volume) return@synchronized
        if (volume <= 0f && t.desired > 0f) t.silentSince = clock()
        t.desired = volume
        ensureWorker()
        wake()
    }

    fun state(id: Int): Int = synchronized(lock) {
        val t = tracks[id] ?: return@synchronized FAILED
        when {
            t.failed -> FAILED
            paused || t.desired <= 0f -> STOPPED
            t.playing -> PLAYING
            else -> PREPARING
        }
    }

    fun onPrepared(id: Int) = synchronized(lock) {
        tracks[id]?.prepared = true
        wake()
    }

    fun onFailed(id: Int) = synchronized(lock) {
        tracks[id]?.failed = true
        wake()
    }

    fun pause() = synchronized(lock) {
        paused = true
        ensureWorker()
        wake()
    }

    fun resume() = synchronized(lock) {
        paused = false
        ensureWorker()
        wake()
    }

    private fun wake() {
        revision++
        lock.notifyAll()
    }

    private fun ensureWorker() {
        if (threaded && worker == null) {
            worker = Thread(::run, "Bunny-Music").apply {
                isDaemon = true
                start()
            }
        }
    }

    private fun workFor(t: Track, now: Long): Work? {
        if (t.failed) return null
        if (!t.created) return if (t.desired > 0f && !paused) Work.Prepare(t) else null
        if (!t.prepared) return null
        if (paused) return if (!t.playing) null else if (t.desired > 0f) Work.Pause(t) else Work.Stop(t)
        if (t.desired > 0f) {
            if (t.sent != t.desired && (t.sent <= 0f || !t.playing || now - t.sentAt >= volumeIntervalMs * MS)) return Work.Volume(t, t.desired)
            return if (t.playing) null else Work.Start(t)
        }
        if (!t.playing) return null
        if (t.sent != 0f) return Work.Volume(t, 0f)
        return if (now - t.silentSince >= stopDelayMs * MS) Work.Stop(t) else null
    }

    internal fun step(): Boolean {
        val work = synchronized(lock) {
            val now = clock()
            val found = tracks.values.firstNotNullOfOrNull { workFor(it, now) } ?: return false
            val t = found.track
            when (found) {
                is Work.Prepare -> t.created = true
                is Work.Volume -> {
                    t.sent = found.value
                    t.sentAt = now
                }
                is Work.Start -> t.playing = true
                is Work.Pause, is Work.Stop -> t.playing = false
            }
            found
        }
        val id = work.track.id
        try {
            when (work) {
                is Work.Prepare -> backend.prepare(id, work.track.path)
                is Work.Volume -> backend.volume(id, work.value)
                is Work.Start -> backend.start(id)
                is Work.Pause -> backend.pause(id)
                is Work.Stop -> {
                    backend.pause(id)
                    backend.rewind(id)
                }
            }
        } catch (e: Exception) {
            onFailed(id)
            backend.failed(id, e)
        }
        return true
    }

    private fun waitMs(now: Long): Long? {
        var next: Long? = null
        for (t in tracks.values) {
            if (t.failed || !t.prepared || paused) continue
            val at = when {
                t.desired > 0f && t.sent != t.desired -> t.sentAt + volumeIntervalMs * MS
                t.desired <= 0f && t.playing -> t.silentSince + stopDelayMs * MS
                else -> continue
            }
            next = minOf(next ?: at, at)
        }
        return next?.let { maxOf(1, (it - now) / MS + 1) }
    }

    private fun run() {
        while (true) {
            val observed = synchronized(lock) { revision }
            if (step()) continue
            synchronized(lock) {
                if (observed == revision) {
                    val ms = waitMs(clock())
                    if (ms == null) lock.wait() else lock.wait(ms)
                }
            }
        }
    }
}
