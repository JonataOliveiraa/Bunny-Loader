package dev.bunnyloader.compile

import android.app.ActivityManager
import android.app.ApplicationExitInfo
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.Process
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import dev.bunnyloader.GameActivity
import dev.bunnyloader.RestartActivity
import dev.bunnyloader.game.BundledRuntime
import dev.bunnyloader.game.DotnetRuntime
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.mods.SourceBuild
import dev.bunnyloader.ui.Bl
import dev.bunnyloader.ui.PixelButton
import dev.bunnyloader.ui.PixelDialog
import dev.bunnyloader.ui.PixelText
import dev.bunnyloader.ui.Ts
import dev.bunnyloader.ui.UiLocalization
import dev.bunnyloader.ui.tr
import java.io.File

object CompilerNative {
    init {
        System.loadLibrary("blcompile")
    }

    external fun start(dotnetDir: String, libDir: String): String

    external fun build(entry: String, runtimeDir: String, references: String): ByteArray
}

class CompileActivity : ComponentActivity() {

    private val handler = Handler(Looper.getMainLooper())
    private var current by mutableStateOf("")
    private var errors by mutableStateOf<List<String>>(emptyList())

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        UiLocalization.initialize(this)
        setContent { Screen() }
        val builds = intent.getStringArrayListExtra(EXTRA_BUILDS).orEmpty().map(SourceBuild::decode)
        val marker = crashMarker(this)
        marker.writeText((attempts(marker) + 1).toString())
        Thread(null, { compile(builds) }, "compile", STACK_BYTES).start()
    }

    override fun onDestroy() {
        super.onDestroy()
        if (isFinishing) {
            crashMarker(this).delete()
            Process.killProcess(Process.myPid())
        }
    }

    private fun compile(builds: List<SourceBuild>) {
        val failures = mutableListOf<String>()
        val dotnetDir = runCatching { DotnetRuntime.prepare(this) }.getOrDefault("")
        val startError = if (dotnetDir.isEmpty()) "no .NET runtime in this build"
        else CompilerNative.start(dotnetDir, BundledRuntime.libDir(this).absolutePath)
        if (startError.isNotEmpty()) {
            Log.e(TAG, "compile: $startError; the game compiles the mods instead")
        } else {
            for (build in builds) {
                handler.post { current = build.name }
                val report = String(
                    CompilerNative.build(build.entry, dotnetDir, build.references.joinToString("\n")),
                    Charsets.UTF_8,
                ).lines()
                report.filter { it.startsWith("status ") || it.startsWith("reason ") || it.startsWith("stats ") }
                    .let { Log.i(TAG, "compile: ${build.name}: ${it.joinToString("; ")}") }
                report.filter { it.startsWith("warning ") }
                    .forEach { Log.w(TAG, "compile: ${build.name}: ${it.removePrefix("warning ")}") }
                if (report.firstOrNull() == "status failed") {
                    val messages = report.filter { it.startsWith("error ") }.map { it.removePrefix("error ") }
                    messages.forEach { Log.e(TAG, "compile: ${build.name}: $it") }
                    failures += "${build.name}:"
                    failures += messages
                }
            }
        }
        crashMarker(this).delete()
        handler.post { finishWith(failures) }
    }

    private fun finishWith(failures: List<String>) {
        when {
            failures.isEmpty() -> done()
            intent.getBooleanExtra(EXTRA_RETURN_RESULT, false) -> {
                setResult(RESULT_FAILED, Intent().putExtra(EXTRA_ERRORS, failures.joinToString("\n")))
                finish()
            }
            else -> errors = failures
        }
    }

    private fun done() {
        if (intent.getBooleanExtra(EXTRA_RETURN_RESULT, false)) setResult(RESULT_OK)
        else startActivity(Intent(this, GameActivity::class.java))
        finish()
    }

    @androidx.compose.runtime.Composable
    private fun Screen() {
        Box(Modifier.fillMaxSize().background(Color.Black), contentAlignment = Alignment.Center) {
            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                AndroidView({ RestartActivity.LoadingStrip(it) }, Modifier.size(84.dp))
                PixelText(tr("CompilingMod", current), size = Ts.Body)
            }
        }
        if (errors.isNotEmpty()) {
            PixelDialog(tr("CompileFailed"), onDismiss = { finish() }) {
                Column(Modifier.heightIn(max = 240.dp).verticalScroll(rememberScrollState())) {
                    errors.forEach { PixelText(it, size = Ts.Small, color = Bl.TextFaint) }
                }
                Row(Modifier.fillMaxWidth().padding(top = 4.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    PixelButton(tr("Close"), { finish() }, Modifier.weight(1f), fontSize = Ts.Body)
                    PixelButton(tr("OpenGameAnyway"), { done() }, Modifier.weight(1f), fontSize = Ts.Body)
                }
            }
        }
    }

    companion object {
        private const val TAG = "BunnyLoader"
        private const val STACK_BYTES = 64L * 1024 * 1024
        private const val EXTRA_BUILDS = "builds"
        const val EXTRA_RETURN_RESULT = "returnResult"
        const val EXTRA_ERRORS = "errors"
        const val RESULT_FAILED = RESULT_FIRST_USER

        private const val BLIND_ATTEMPTS = 2
        private val CRASH_REASONS = setOf(
            ApplicationExitInfo.REASON_CRASH,
            ApplicationExitInfo.REASON_CRASH_NATIVE,
            ApplicationExitInfo.REASON_ANR,
            ApplicationExitInfo.REASON_INITIALIZATION_FAILURE,
        )

        private fun crashMarker(context: Context) = File(context.filesDir, "compile.running")

        private fun attempts(marker: File): Int =
            if (marker.exists()) runCatching { marker.readText().trim().toInt() }.getOrDefault(1) else 0

        private fun compilerCrashed(context: Context, marker: File): Boolean {
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.R) return attempts(marker) >= BLIND_ATTEMPTS
            val process = "${context.packageName}:compile"
            val exit = context.getSystemService(ActivityManager::class.java)
                .getHistoricalProcessExitReasons(context.packageName, 0, 0)
                .firstOrNull { it.processName == process && it.timestamp >= marker.lastModified() }
                ?: return false
            return exit.reason in CRASH_REASONS
        }

        private fun pending(context: Context): List<SourceBuild>? {
            val marker = crashMarker(context)
            if (marker.exists()) {
                if (compilerCrashed(context, marker)) {
                    Log.w(TAG, "compile: the last compile crashed; the game compiles the mods instead")
                    marker.delete()
                    return null
                }
                Log.w(TAG, "compile: the last compile was interrupted; compiling again")
            }
            val builds = ModRepository(context).enabledSourceBuilds().takeIf { list -> list.any { !it.looksCurrent() } }
            if (builds == null) marker.delete()
            return builds
        }

        fun compileIntent(context: Context): Intent? = pending(context)?.let { builds ->
            Intent(context, CompileActivity::class.java)
                .putStringArrayListExtra(EXTRA_BUILDS, ArrayList(builds.map { it.encode() }))
        }

        fun gameIntent(context: Context): Intent =
            compileIntent(context) ?: Intent(context, GameActivity::class.java)
    }
}
