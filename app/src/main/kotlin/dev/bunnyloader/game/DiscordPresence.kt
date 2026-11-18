package dev.bunnyloader.game

import android.app.Activity
import android.os.Handler
import android.os.HandlerThread
import android.util.Log
import dev.bunnyloader.BuildConfig
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.nativebridge.NativeBridge
import dev.bunnyloader.ui.Prefs

object DiscordPresence {
    private const val TAG = "BunnyDiscord"
    private const val APP_NAME = "Bunny Loader"
    private const val LARGE_IMAGE = "https://cdn.discordapp.com/app-assets/1571933957866393661/1571945539908669441.png"
    private const val CALLBACK_INTERVAL_MS = 500L
    private const val NAMED_MODS = 2
    private const val MAX_NAME_LENGTH = 40

    private var thread: HandlerThread? = null
    private var handler: Handler? = null

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
        val startedAt = System.currentTimeMillis()

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
            nativeUpdate(APP_NAME, modsLine(names, modUids.size), LARGE_IMAGE, APP_NAME, startedAt)
            h.post(object : Runnable {
                override fun run() {
                    nativeRunCallbacks()
                    h.postDelayed(this, CALLBACK_INTERVAL_MS)
                }
            })
        }
    }

    fun stop() {
        val h = handler ?: return
        handler = null
        h.removeCallbacksAndMessages(null)
        h.post { nativeClear(); nativeRunCallbacks() }
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
