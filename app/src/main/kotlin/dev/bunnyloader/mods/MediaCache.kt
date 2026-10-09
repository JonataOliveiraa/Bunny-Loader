package dev.bunnyloader.mods

import java.util.concurrent.Semaphore

/** Shared byte budget and decode slots for still images and animations. */
internal class MediaCache<K, V>(
    private val maxBytes: Int,
    private val maxEntries: Int = 256,
    parallelism: Int = 2,
    private val weight: (V) -> Int,
) {
    init {
        require(maxBytes > 0 && maxEntries > 0 && parallelism > 0)
    }

    private class Entry<V>(val value: V?, val bytes: Int)

    private val lock = Object()
    private val slots = Semaphore(parallelism, true)
    private val entries = LinkedHashMap<K, Entry<V>>(16, 0.75f, true)
    private val loading = mutableSetOf<K>()
    private var bytes = 0

    /** Blocking: call off the UI thread. Concurrent readers share one decode. */
    fun get(key: K, load: () -> V?): V? {
        synchronized(lock) {
            while (key in loading) lock.wait()
            entries[key]?.let { return it.value }
            loading.add(key)
        }

        try {
            slots.acquire()
            val value = try {
                load()
            } finally {
                slots.release()
            }
            val size = value?.let(weight)?.coerceAtLeast(0) ?: 0

            synchronized(lock) {
                // Failed decodes are cached too; a new file revision gets a new key.
                if (size <= maxBytes) {
                    entries[key] = Entry(value, size)
                    bytes += size
                    val iterator = entries.entries.iterator()
                    while (bytes > maxBytes || entries.size > maxEntries) {
                        bytes -= iterator.next().value.bytes
                        iterator.remove()
                    }
                }
            }
            return value
        } finally {
            synchronized(lock) {
                loading.remove(key)
                lock.notifyAll()
            }
        }
    }

    internal fun retainedBytes(): Int = synchronized(lock) { bytes }
    internal fun retainedEntries(): Int = synchronized(lock) { entries.size }
}
