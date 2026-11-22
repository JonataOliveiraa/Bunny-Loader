package dev.bunnyloader.game

import android.app.ActivityManager
import android.app.ApplicationExitInfo
import android.content.Context
import android.os.Build
import android.util.Log
import dev.bunnyloader.TAG
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Trail of the hosted boot, written to a file in filesDir (shared by both app
 * processes) so the launcher can show it without adb.
 */
object BootLog {
    private const val FILE = "hosted-boot.log"
    private const val MOD_LINES = 200
    private const val EXIT_REASONS = 5

    private fun file(ctx: Context) = File(ctx.filesDir, FILE)

    fun reset(ctx: Context) {
        runCatching { file(ctx).writeText("") }
    }

    fun add(ctx: Context, line: String) {
        Log.i(TAG, "boot: $line")
        runCatching { file(ctx).appendText(line + "\n") }
    }

    fun fail(ctx: Context, what: String, t: Throwable?) {
        val root = t?.let { generateSequence(it) { c -> c.cause }.last() }
        val detail = root?.let { "${it.javaClass.simpleName}: ${it.message}" } ?: "no details"
        add(ctx, "FAILED: $what")
        add(ctx, "  -> $detail")
        root?.stackTrace?.take(6)?.forEach { add(ctx, "     at $it") }
    }

    /** Catches exceptions from any thread; Unity throws on its own threads after onCreate. */
    fun installCrashHandler(ctx: Context) {
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, t ->
            runCatching {
                add(ctx, "CRASH on thread '${thread.name}'")
                var e: Throwable? = t
                var depth = 0
                while (e != null && depth++ < 3) {
                    add(ctx, "  ${e.javaClass.name}: ${e.message}")
                    e.stackTrace.take(24).forEach { add(ctx, "    at $it") }
                    e = e.cause?.also { add(ctx, "  caused by:") }
                }
            }
            previous?.uncaughtException(thread, t)
        }
    }

    private fun logcatFile(ctx: Context) = File(ctx.filesDir, "hosted-logcat.log")

    /** Captures this process's own logcat; Unity prints the reason right before aborting. */
    fun captureLogcat(ctx: Context) {
        val out = logcatFile(ctx)
        runCatching { out.writeText("") }
        Thread {
            runCatching {
                val pid = android.os.Process.myPid()
                val p = Runtime.getRuntime().exec(
                    arrayOf("logcat", "-v", "brief", "--pid=$pid"),
                )
                p.inputStream.bufferedReader().forEachLine { line ->
                    val keep = line.contains(TAG) ||
                        line.contains("Unity") || line.contains("IL2CPP") ||
                        line.contains("DEBUG") || line.contains("libc") ||
                        line.contains("Fatal") || line.contains("FATAL") ||
                        line.contains("Error") || line.contains("error") ||
                        line.contains("JNI") || line.contains("dlopen") ||
                        line.contains("art")
                    if (keep && out.length() < 60_000) out.appendText(line + "\n")
                }
            }
        }.apply { isDaemon = true }.start()
    }

    /**
     * The Diagnostics report a player copies and sends after a crash: header,
     * why the game closed, then the session details. The cause comes first so a
     * phone screenshot already shows it.
     */
    fun read(ctx: Context): String = clean(
        header(ctx) +
            section("Why the game closed (latest first)") + exitReasons(ctx) +
            section("Last session: errors and warnings") + sessionProblems(ctx) +
            section("Last session: mod log lines") + sessionModLines(ctx) +
            section("Game boot") +
            (runCatching { file(ctx).readText() }.getOrDefault("").ifBlank { "(no entries yet)\n" }) +
            section("Game process logcat") + lastLogcat(ctx),
    )

    private fun section(title: String) = "\n--- $title ---\n"

    private fun clean(text: String): String = buildString(text.length) {
        for (c in text) {
            if (c == '\n' || c == '\t' || (!Character.isISOControl(c) && c != '�')) append(c)
        }
    }.lines().joinToString("\n") { it.trimEnd() }.trimEnd() + "\n"

    private fun header(ctx: Context): String = buildString {
        append("=== Bunny Loader report ===\n")
        append("Bunny Loader v").append(dev.bunnyloader.BuildConfig.VERSION_NAME)
        append(" · Terraria ").append(BundledRuntime.VERSION_NAME)
        append(" (").append(BundledRuntime.VERSION_CODE).append(")\n")
        append("Device: ").append(Build.MANUFACTURER).append(' ').append(Build.MODEL)
        append(" · Android ").append(Build.VERSION.RELEASE).append(" (API ").append(Build.VERSION.SDK_INT).append(')')
        append(" · ").append(Build.SUPPORTED_ABIS.firstOrNull() ?: "?").append('\n')
        append("Generated: ").append(timestamp(System.currentTimeMillis())).append('\n')
        val mods = runCatching {
            val repo = dev.bunnyloader.mods.ModRepository(ctx)
            repo.list().filter { repo.isEnabled(it.uid) }.map { "${it.name} ${it.version} (${it.id})" }
        }.getOrNull()
        when {
            mods == null -> append("Enabled mods: (could not read)\n")
            mods.isEmpty() -> append("Enabled mods: none\n")
            else -> {
                append("Enabled mods (").append(mods.size).append("):\n")
                mods.forEach { append("  - ").append(it).append('\n') }
            }
        }
    }

    private fun timestamp(millis: Long): String =
        SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).format(Date(millis))

    /** Newest session log (`logs/bunny_<date>.txt`); after a crash it is the session that fell. */
    private fun lastSessionLog(ctx: Context): File? {
        val base = ctx.getExternalFilesDir(null)?.parentFile ?: return null
        return File(base, "logs").listFiles { f -> f.name.startsWith("bunny_") && f.name.endsWith(".txt") }
            .orEmpty().maxByOrNull { it.name }
    }

    private fun sessionModLines(ctx: Context): String = runCatching {
        val last = lastSessionLog(ctx) ?: return@runCatching "(no session log)\n"
        val lines = last.readLines().filter { "[mod]" in it }.takeLast(MOD_LINES)
        if (lines.isEmpty()) "(none)\n" else lines.joinToString("\n", postfix = "\n")
    }.getOrElse { "(could not read: ${it.javaClass.simpleName})\n" }

    private fun sessionProblems(ctx: Context): String = runCatching {
        val last = lastSessionLog(ctx) ?: return@runCatching "(no session log)\n"
        val lines = last.readLines()
        val problems = lines.filter { it.length > 13 && (it[13] == 'E' || it[13] == 'W') }.takeLast(30)
        buildString {
            append(last.name).append('\n')
            if (problems.isEmpty()) append("(no errors or warnings)\n")
            else problems.forEach { append(it).append('\n') }
            append(">>> end of log:\n")
            lines.takeLast(15).forEach { append(it).append('\n') }
        }
    }.getOrElse { "(could not read: ${it.javaClass.simpleName})\n" }

    /** Cause lines first: after a JNI FatalError, ART dumps a huge stack that buries the message. */
    private fun lastLogcat(ctx: Context): String = runCatching {
        val lines = logcatFile(ctx).readLines()
        if (lines.isEmpty()) return@runCatching "(empty)\n"
        val causes = lines.filter { l ->
            l.contains("FatalError called") || l.contains("JNI DETECTED") ||
                l.contains("Abort message") || l.contains("UnsatisfiedLink") ||
                l.contains("dlopen") || l.contains("Unable to") ||
                l.contains("Failed to") || l.contains("E/Unity") ||
                l.contains("E Unity") || l.contains("could not") ||
                l.startsWith("E/$TAG")
        }.distinct().take(15)
        buildString {
            if (causes.isNotEmpty()) {
                append(">>> likely cause:\n")
                causes.forEach { append("  ").append(it.trim()).append('\n') }
                append(">>> tail:\n")
            }
            lines.filterNot { it.contains("native: #") || it.contains("runtime.cc:") || isUnityNoise(it) }
                .takeLast(25).forEach { append(it).append('\n') }
        }
    }.getOrElse { "(not captured)\n" }

    private val unityFrame = Regex("""^[\w.`<>\[\],+]+:[\w.`<>|]+\(.*\)$""")

    private fun isUnityNoise(line: String): Boolean {
        if (!line.startsWith("I/Unity")) return false
        val text = line.substringAfter("):", "").trim()
        return text.isEmpty() || unityFrame.matches(text)
    }

    /** Why the app's processes died (Android 11+); native crashes also carry the tombstone. */
    private fun exitReasons(ctx: Context): String = runCatching {
        if (Build.VERSION.SDK_INT < 30) return@runCatching "(requires Android 11+)\n"
        val am = ctx.getSystemService(ActivityManager::class.java)
        val list = am.getHistoricalProcessExitReasons(ctx.packageName, 0, EXIT_REASONS)
        if (list.isEmpty()) return@runCatching "(no records)\n"
        var tombstoneShown = false
        buildString {
            list.forEach { info ->
                append('[').append(timestamp(info.timestamp)).append("] ")
                append(info.processName.removePrefix(ctx.packageName).ifEmpty { "launcher" })
                append(": ").append(reasonName(info.reason))
                append(" (status ").append(info.status).append(')')
                info.description?.trim()?.takeIf { it.isNotEmpty() && !it.equals("crash", ignoreCase = true) }
                    ?.let { append(" - ").append(it.lineSequence().first().take(160)) }
                append('\n')
                if (info.reason == ApplicationExitInfo.REASON_CRASH_NATIVE && !tombstoneShown) {
                    tombstoneShown = true
                    append(tombstone(info)).append('\n')
                }
            }
        }
    }.getOrElse { "(could not read: ${it.javaClass.simpleName})\n" }

    private fun tombstone(info: ApplicationExitInfo): String = runCatching {
        val stream = info.traceInputStream ?: return "   (no tombstone)"
        TombstoneReader.read(stream)
    }.getOrElse { "   (unreadable tombstone: ${it.javaClass.simpleName})" }

    private fun reasonName(r: Int): String = when (r) {
        ApplicationExitInfo.REASON_CRASH -> "Java crash"
        ApplicationExitInfo.REASON_CRASH_NATIVE -> "native crash"
        ApplicationExitInfo.REASON_ANR -> "ANR (not responding)"
        ApplicationExitInfo.REASON_LOW_MEMORY -> "killed for low memory"
        ApplicationExitInfo.REASON_SIGNALED -> "killed by signal"
        ApplicationExitInfo.REASON_EXIT_SELF -> "exited normally"
        ApplicationExitInfo.REASON_USER_REQUESTED -> "closed by the user"
        ApplicationExitInfo.REASON_USER_STOPPED -> "force-stopped by the user"
        ApplicationExitInfo.REASON_DEPENDENCY_DIED -> "dependency died"
        ApplicationExitInfo.REASON_EXCESSIVE_RESOURCE_USAGE -> "excessive resource usage"
        ApplicationExitInfo.REASON_INITIALIZATION_FAILURE -> "initialization failure"
        ApplicationExitInfo.REASON_PERMISSION_CHANGE -> "permission changed"
        ApplicationExitInfo.REASON_OTHER -> "other"
        else -> "reason $r"
    }
}
