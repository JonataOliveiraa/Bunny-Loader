package dev.bunnyloader

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Rect
import android.graphics.Typeface
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.util.TypedValue
import android.view.Gravity
import android.view.View
import android.widget.LinearLayout
import android.widget.TextView
import java.io.File

/**
 * O "Reiniciar" do Mod Menu: fecha o jogo e abre de novo, para os mods serem
 * lidos outra vez da pasta.
 *
 * Mora no processo do launcher, e não no :game. O Mod Menu abre esta tela e
 * mata o processo do jogo logo em seguida; aqui se espera o processo antigo
 * sumir (`/proc/<pid>`) antes de abrir a GameActivity, para ela nascer num
 * processo novo, com a Unity, o IL2CPP e o motor JS do zero.
 *
 * Tela preta com o girassol de carregamento do jogo: é o que aparece entre o
 * jogo fechar e a Unity desenhar o primeiro quadro.
 */
class RestartActivity : Activity() {

    private val handler = Handler(Looper.getMainLooper())

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(buildView())

        val pid = intent.getIntExtra(EXTRA_PID, 0)
        val start = SystemClock.uptimeMillis()
        handler.post(object : Runnable {
            override fun run() {
                val alive = pid > 0 && File("/proc/$pid").exists()
                if (alive && SystemClock.uptimeMillis() - start < WAIT_MS) {
                    handler.postDelayed(this, POLL_MS)
                    return
                }
                startActivity(Intent(this@RestartActivity, GameActivity::class.java))
                finish()
                @Suppress("DEPRECATION")
                overridePendingTransition(0, 0)
            }
        })
    }

    private fun buildView(): View {
        val box = LinearLayout(this)
        box.orientation = LinearLayout.VERTICAL
        box.gravity = Gravity.CENTER
        box.setBackgroundColor(Color.BLACK)
        val density = resources.displayMetrics.density
        box.addView(Sunflower(this), LinearLayout.LayoutParams((52 * 1.6f * density).toInt(), (54 * 1.6f * density).toInt()))
        val label = TextView(this)
        label.text = "Reiniciando..."
        label.setTextColor(Color.WHITE)
        label.setTextSize(TypedValue.COMPLEX_UNIT_SP, 18f)
        label.typeface = runCatching { Typeface.createFromAsset(assets, "fonte/bunny.ttf") }.getOrDefault(Typeface.DEFAULT)
        label.setPadding(0, (12 * density).toInt(), 0, 0)
        box.addView(label)
        return box
    }

    /** O girassol do jogo abrindo e fechando: spr_loading, 19 quadros de 52x54. */
    private class Sunflower(context: Context) : View(context) {
        private val strip: Bitmap? = runCatching {
            BitmapFactory.decodeResource(context.resources, R.drawable.spr_loading,
                BitmapFactory.Options().apply { inScaled = false })
        }.getOrNull()
        private val paint = Paint().apply { isFilterBitmap = false }
        private val src = Rect()
        private val dst = Rect()
        private var frame = 0

        override fun onDraw(canvas: Canvas) {
            val s = strip ?: return
            val y = frame * FRAME_H
            src.set(0, y, FRAME_W, minOf(s.height, y + FRAME_H))
            dst.set(0, 0, width, height * src.height() / FRAME_H)
            canvas.drawBitmap(s, src, dst, paint)
            frame = (frame + 1) % FRAMES
            postInvalidateDelayed(FRAME_MS)
        }
    }

    companion object {
        /** O pid do processo do jogo que está fechando. */
        const val EXTRA_PID = "pid"
        private const val POLL_MS = 40L
        /** Se ele não sumir nesse tempo, abre assim mesmo. */
        private const val WAIT_MS = 5000L
        private const val FRAMES = 19
        private const val FRAME_W = 52
        private const val FRAME_H = 54
        private const val FRAME_MS = 60L
    }
}
