package dev.bunnyloader.game

import android.app.Activity
import android.os.Handler
import android.os.HandlerThread
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import dev.bunnyloader.BuildConfig
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.nativebridge.NativeBridge
import dev.bunnyloader.ui.Prefs
import java.util.concurrent.atomic.AtomicBoolean

object DiscordPresence {
    private const val TAG = "BunnyDiscord"
    private const val APP_NAME = "Bunny Loader"
    private const val LARGE_IMAGE = "https://cdn.discordapp.com/app-assets/1571933957866393661/1571945539908669441.png"
    private const val CALLBACK_INTERVAL_MS = 500L
    private const val NAMED_MODS = 2
    private const val MAX_NAME_LENGTH = 40
    private const val FLUSH_ROUNDS = 5
    private const val FLUSH_STEP_MS = 50L
    private const val FLUSH_TIMEOUT_MS = 1500L

    private var thread: HandlerThread? = null
    private var handler: Handler? = null
    private var state = ""
    private var startedAt = 0L
    private var hidden = false

    fun start(activity: Activity, modUids: List<String>) {
        if (handler != null) return
        if (!Prefs(activity).discordPresence || BuildConfig.DISCORD_APP_ID == 0L) return
        if (!NativeBridge.ensureLoaded()) return
        val init = runCatching { Class.forName("com.discord.socialsdk.DiscordSocialSdkInit") }.getOrNull() ?: run {
            Log.i(TAG, "Discord SDK not in this build")
            return
        }
        val linked = runCatching {
            init.getMethod("setEngineActivity", Activity::class.java).invoke(null, activity)
        }
        if (linked.isFailure) {
            Log.w(TAG, "Discord SDK init failed", linked.exceptionOrNull())
            return
        }
        val repo = ModRepository(activity.applicationContext)
        startedAt = System.currentTimeMillis()
        hidden = false

        val worker = HandlerThread("BunnyDiscord").also { it.start() }
        val h = Handler(worker.looper)
        thread = worker
        handler = h
        h.post {
            if (!nativeStart(BuildConfig.DISCORD_APP_ID)) {
                stop()
                return@post
            }
            val names = modUids.asSequence().mapNotNull(repo::nameOf).take(NAMED_MODS).toList()
            state = modsLine(names, modUids.size)
            nativeUpdate(APP_NAME, state, LARGE_IMAGE, APP_NAME, startedAt)
            h.post(object : Runnable {
                override fun run() {
                    nativeRunCallbacks()
                    h.postDelayed(this, CALLBACK_INTERVAL_MS)
                }
            })
        }
    }

    fun show() {
        val h = handler ?: return
        if (!hidden) return
        hidden = false
        h.post { nativeUpdate(APP_NAME, state, LARGE_IMAGE, APP_NAME, startedAt) }
    }

    fun hide() {
        val h = handler ?: return
        if (hidden) return
        hidden = true
        h.post { nativeClear() }
    }

    fun stop(then: () -> Unit = {}) {
        val h = handler ?: return then()
        handler = null
        h.removeCallbacksAndMessages(null)
        val main = Handler(Looper.getMainLooper())
        val done = AtomicBoolean()
        val finish = Runnable { if (done.compareAndSet(false, true)) then() }
        h.post {
            nativeClear()
            repeat(FLUSH_ROUNDS) {
                nativeRunCallbacks()
                SystemClock.sleep(FLUSH_STEP_MS)
            }
            main.post(finish)
        }
        main.postDelayed(finish, FLUSH_TIMEOUT_MS)
        thread?.quitSafely()
        thread = null
    }

    private fun modsLine(names: List<String>, total: Int): String {
        val shown = names.map(::shorten)
        val rest = total - shown.size
        return when {
            shown.isEmpty() -> "No mods"
            rest > 0 -> "${shown.joinToString(", ")} and +$rest more"
            else -> shown.joinToString(" and ")
        }
    }

    private fun shorten(name: String): String =
        if (name.length <= MAX_NAME_LENGTH) name else name.take(MAX_NAME_LENGTH - 1).trimEnd() + "…"

    private external fun nativeStart(applicationId: Long): Boolean
    private external fun nativeUpdate(name: String, state: String, largeImage: String, largeText: String, startMs: Long)
    private external fun nativeClear()
    private external fun nativeRunCallbacks()
}
