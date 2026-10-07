package dev.bunnyloader.game

/** Bounded SFX dispatch. Android calls never run under the producer's lock. */
internal class SfxQueue(
    private val backend: Backend,
    private val clock: () -> Long = System::nanoTime,
    private val threaded: Boolean = true,
    private val capacity: Int = 32,
    private val maxAgeMs: Long = 50,
) {
    init {
        require(capacity in 1..32)
        require(maxAgeMs >= 0)
    }

    interface Backend {
        fun play(id: Int, left: Float, right: Float, rate: Float): Int
        fun stop(stream: Int)
        fun pause()
        fun resume()
    }

    companion object {
        const val FAILED = -1
        const val FINISHED = 0
        const val PENDING = 1
        const val PLAYING = 2
        const val PAUSED = 3
        const val CANCELLED = 4
        const val EXPIRED = 5
        private const val STARTING = 6
        private const val HISTORY = 256
        private const val STREAMS = 32
        private const val MS = 1_000_000L
    }

    private class Request(
        val handle: Int, val group: Int, val id: Int,
        val left: Float, val right: Float, val rate: Float,
        val duration: Long, val submitted: Long, val epoch: Long,
    ) {
        var state = PENDING
        var inFlight = false
        var stream = 0
        var end = 0L
    }

    private sealed interface Work {
        class Play(val request: Request) : Work
        class Stop(val stream: Int) : Work
        class Pause(val value: Boolean) : Work
    }

    private val lock = Object()
    private val requests = LinkedHashMap<Int, Request>()
    private val current = LinkedHashSet<Request>()
    private var pending = 0
    private var nextHandle = 1
    private var epoch = 0L
    private var revision = 0L
    private var desiredPause = false
    private var appliedPause = false
    private var pauseStart = 0L
    private var pauseTotal = 0L
    private var worker: Thread? = null
    private var closed = false
    private var accepted = 0L
    private var rejected = 0L
    private var started = 0L
    private var failed = 0L
    private var expired = 0L
    private var cancelled = 0L
    private var backendErrors = 0L
    private var backendNs = 0L
    private var highWater = 0
    private val waitBins = LongArray(7) // <= 1, 2, 5, 10, 20, 50 ms, overflow

    fun enqueue(group: Int, id: Int, left: Float, right: Float, rate: Float,
                durationMs: Int, maxInstances: Int, ignoreNew: Boolean): Int = synchronized(lock) {
        expireFinished()
        if (closed || desiredPause || id <= 0 || nextHandle <= 0 ||
            !left.isFinite() || !right.isFinite() || !rate.isFinite()) {
            rejected++
            return@synchronized 0
        }

        var matching = 0
        var oldest: Request? = null
        for (r in current) {
            if (maxInstances > 0 && r.group == group && live(r)) {
                matching++
                if (oldest == null) oldest = r
            }
        }
        if (maxInstances > 0 && matching >= maxInstances && ignoreNew) {
            rejected++
            return@synchronized 0
        }

        // Replacing a queued request releases its slot; an in-flight call retains one.
        val replace = if (maxInstances > 0 && matching >= maxInstances) oldest else null
        val replacingPending = replace?.state == PENDING
        if (pending - (if (replacingPending) 1 else 0) >= capacity) {
            rejected++
            return@synchronized 0
        }
        if (replace != null) cancel(replace)

        val handle = nextHandle++
        val speed = rate.coerceIn(0.5f, 2f)
        val request = Request(handle, group, id, left.coerceIn(0f, 1f), right.coerceIn(0f, 1f),
            speed, ((if (durationMs > 0) durationMs else 1000) * MS / speed).toLong(), clock(), epoch)
        requests[handle] = request
        current.add(request)
        pending++
        accepted++
        highWater = maxOf(highWater, pending)
        trimHistory()
        ensureWorker()
        wake()
        handle
    }

    fun stop(handle: Int) = synchronized(lock) {
        requests[handle]?.let { if (live(it)) cancel(it) }
        wake()
    }

    fun state(handle: Int): Int = synchronized(lock) {
        expireFinished()
        val state = requests[handle]?.state ?: FINISHED
        when {
            state == STARTING -> PENDING
            state == PLAYING && (desiredPause || appliedPause) -> PAUSED
            else -> state
        }
    }

    fun active(group: Int): Int = synchronized(lock) {
        expireFinished()
        if (desiredPause || appliedPause) return@synchronized 0
        current.lastOrNull { it.group == group && it.state == PLAYING }?.handle ?: 0
    }

    fun pause() = synchronized(lock) {
        desiredPause = true
        epoch++
        requests.values.forEach { if (it.state == PENDING || it.state == STARTING) cancel(it) }
        ensureWorker()
        wake()
    }

    fun resume() = synchronized(lock) {
        desiredPause = false
        ensureWorker()
        wake()
    }

    fun close() = synchronized(lock) {
        closed = true
        requests.values.forEach { if (live(it)) cancel(it) }
        wake()
    }

    fun stats(): String = synchronized(lock) {
        expireFinished()
        """{"accepted":$accepted,"rejected":$rejected,"started":$started,"failed":$failed,"expired":$expired,"cancelled":$cancelled,"backendErrors":$backendErrors,"backendMs":${backendNs / 1e6},"pending":$pending,"active":${current.count { it.state == PLAYING }},"retained":${requests.size},"highWater":$highWater,"pausedMs":${(pauseTotal + (if (appliedPause) clock() - pauseStart else 0)) / 1e6},"waitBins":[${waitBins.joinToString(",")}]}"""
    }

    private fun live(r: Request) = r.state == PENDING || r.state == STARTING || r.state == PLAYING

    private fun cancel(r: Request) {
        if (r.state == PENDING) pending--
        r.state = CANCELLED
        current.remove(r)
        cancelled++
    }

    private fun wake() {
        revision++
        lock.notifyAll()
    }

    private fun ensureWorker() {
        if (threaded && worker == null) {
            worker = Thread(::run, "Bunny-SFX").apply {
                isDaemon = true
                start()
            }
        }
    }

    private fun audioNow(): Long = (if (appliedPause) pauseStart else clock()) - pauseTotal

    private fun expireFinished() {
        val now = audioNow()
        val iterator = current.iterator()
        while (iterator.hasNext()) {
            val r = iterator.next()
            if (r.state == PLAYING && r.end <= now) {
                r.state = FINISHED
                r.stream = 0 // A one-shot SoundPool stream ends by itself.
                iterator.remove()
            }
        }
    }

    private fun trimHistory() {
        val it = requests.entries.iterator()
        while (requests.size > HISTORY && it.hasNext()) {
            val r = it.next().value
            if (!live(r) && !r.inFlight && r.stream == 0) it.remove()
        }
    }

    /** One worker operation; exposed internally for deterministic race/clock tests. */
    internal fun step(): Boolean {
        val work = synchronized(lock) {
            expireFinished()
            val stopped = requests.values.firstOrNull { !live(it) && it.stream != 0 }
            when {
                stopped != null -> Work.Stop(stopped.stream).also { stopped.stream = 0 }
                desiredPause != appliedPause -> Work.Pause(desiredPause)
                desiredPause || closed -> null
                else -> {
                    val r = current.firstOrNull { it.state == PENDING }
                    val age = if (r != null) clock() - r.submitted else 0
                    if (r != null && age > maxAgeMs * MS) {
                        r.state = EXPIRED
                        current.remove(r)
                        pending--
                        expired++
                        // Return work on the next step, without dispatching an old effect.
                        return true
                    }
                    r?.let {
                        it.state = STARTING
                        it.inFlight = true
                        val ms = age / 1e6
                        val bin = when {
                            ms <= 1 -> 0
                            ms <= 2 -> 1
                            ms <= 5 -> 2
                            ms <= 10 -> 3
                            ms <= 20 -> 4
                            ms <= 50 -> 5
                            else -> 6
                        }
                        waitBins[bin]++
                        Work.Play(it)
                    }
                }
            }
        } ?: return false

        when (work) {
            is Work.Stop -> safely { backend.stop(work.stream) }
            is Work.Pause -> {
                safely { if (work.value) backend.pause() else backend.resume() }
                synchronized(lock) {
                    if (work.value) pauseStart = clock() else pauseTotal += clock() - pauseStart
                    appliedPause = work.value
                }
            }
            is Work.Play -> {
                val r = work.request
                val begin = clock()
                val stream = safely { backend.play(r.id, r.left, r.right, r.rate) } ?: 0
                synchronized(lock) {
                    r.inFlight = false
                    pending--
                    backendNs += clock() - begin
                    if (r.state != STARTING || desiredPause || r.epoch != epoch || closed) {
                        r.stream = stream // Worker stops any sound returned after cancellation.
                    } else if (stream <= 0) {
                        r.state = FAILED
                        current.remove(r)
                        failed++
                    } else {
                        // Mirror SoundPool's 32 equal-priority stream limit after a successful start.
                        val playing = current.filter { it.state == PLAYING }
                        if (playing.size >= STREAMS) {
                            val oldest = playing.first()
                            oldest.state = FINISHED
                            oldest.stream = 0
                            current.remove(oldest)
                        }
                        r.stream = stream
                        r.state = PLAYING
                        r.end = audioNow() + r.duration
                        started++
                    }
                }
            }
        }
        return true
    }

    private fun <T> safely(action: () -> T): T? = try {
        action()
    } catch (_: Exception) {
        synchronized(lock) { backendErrors++ }
        null
    }

    private fun run() {
        while (true) {
            val observed = synchronized(lock) { revision }
            if (step()) continue
            synchronized(lock) {
                if (closed) return
                if (observed == revision) {
                    val end = if (appliedPause) null else current.filter { it.state == PLAYING }.minOfOrNull { it.end }
                    if (end == null) lock.wait() else lock.wait(maxOf(1, (end - audioNow()) / MS + 1))
                }
            }
        }
    }
}
