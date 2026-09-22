package dev.bunnyloader.game

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.res.Configuration
import android.util.Log
import android.view.InputEvent
import android.view.View
import dev.bunnyloader.TAG
import dev.bunnyloader.describe
import dev.bunnyloader.rootCause
import java.lang.reflect.Method
import java.lang.reflect.Proxy

/**
 * Cria a UnityPlayer e repassa o ciclo de vida. Tudo por reflection, porque a
 * classe vem do DexClassLoader do jogo.
 *
 * Assinaturas conferidas no dex de Terraria 1.4.5.6.4 (Unity 2021.3.56f2):
 *   UnityPlayer extends android.widget.FrameLayout
 *   <init>(Context) / <init>(Context, IUnityPlayerLifecycleEvents)
 *   onStart/onResume/onPause/onStop/destroy/quit/lowMemory : ()V
 *   windowFocusChanged(boolean) / configurationChanged(Configuration)
 *   newIntent(Intent) / injectEvent(InputEvent):boolean / getView():View
 *
 * A UnityPlayerActivity de fábrica usa onStart/onResume/onPause/onStop — NÃO os
 * legados resume()/pause(). Seguimos a de fábrica.
 */
class UnityHost(
    private val activity: Activity,
    private val loader: ClassLoader,
) {
    private lateinit var playerClass: Class<*>
    private lateinit var player: Any
    private val methods = HashMap<String, Method?>()

    var onQuit: (() -> Unit)? = null

    /**
     * @param context precisa ser a própria Activity (ver GameEnvironment).
     * @throws Throwable já desembrulhado — reflection esconde a causa real
     *   dentro de InvocationTargetException, que tem message nula.
     */
    fun create(context: Context): View {
        Log.i(TAG, "UnityHost: carregando com.unity3d.player.UnityPlayer")
        playerClass = try {
            loader.loadClass("com.unity3d.player.UnityPlayer")
        } catch (t: Throwable) {
            throw IllegalStateException("nao carregou a classe UnityPlayer: ${t.describe()}", t)
        }
        Log.i(TAG, "UnityHost: classe ok (super=${playerClass.superclass?.name})")

        player = newPlayer(context)
        Log.i(TAG, "UnityHost: UnityPlayer instanciada")

        val view = runCatching { method("getView")?.invoke(player) as? View }.getOrNull()
            ?: player as? View
            ?: error("UnityPlayer nao e uma View e getView() nao respondeu")
        Log.i(TAG, "UnityHost: view obtida (${view.javaClass.name})")
        return view
    }

    private fun newPlayer(context: Context): Any {
        val lifecycle = runCatching {
            loader.loadClass("com.unity3d.player.IUnityPlayerLifecycleEvents")
        }.getOrNull()

        try {
            if (lifecycle != null) {
                val proxy = Proxy.newProxyInstance(loader, arrayOf(lifecycle)) { _, m, _ ->
                    // onUnityPlayerQuitted() / onUnityPlayerUnloaded()
                    Log.i(TAG, "UnityPlayer lifecycle: ${m.name}")
                    if (m.name == "onUnityPlayerQuitted") onQuit?.invoke()
                    null
                }
                Log.i(TAG, "UnityHost: ctor(Context, IUnityPlayerLifecycleEvents)")
                return playerClass.getConstructor(Context::class.java, lifecycle)
                    .newInstance(context, proxy)
            }
            Log.i(TAG, "UnityHost: ctor(Context)")
            return playerClass.getConstructor(Context::class.java).newInstance(context)
        } catch (t: Throwable) {
            // Desembrulha aqui: quem chama precisa da causa, não do wrapper.
            throw t.rootCause()
        }
    }

    // --- ciclo de vida ---
    fun start() = call("onStart")
    fun resume() = call("onResume")
    fun pause() = call("onPause")
    fun stop() = call("onStop")
    fun destroy() = call("destroy")
    fun lowMemory() = call("lowMemory")
    fun windowFocusChanged(focus: Boolean) = call("windowFocusChanged", Boolean::class.java, focus)
    fun configurationChanged(c: Configuration) = call("configurationChanged", Configuration::class.java, c)
    fun newIntent(intent: Intent) = call("newIntent", Intent::class.java, intent)

    /** Encaminha teclas/toque/motion, como a UnityPlayerActivity faz. */
    fun injectEvent(event: InputEvent): Boolean =
        runCatching {
            method("injectEvent", InputEvent::class.java)?.invoke(player, event) as? Boolean
        }.getOrNull() ?: false

    // --- reflection ---
    private fun method(name: String, vararg types: Class<*>): Method? {
        val key = name + types.joinToString(",") { it.name }
        return methods.getOrPut(key) {
            runCatching { playerClass.getMethod(name, *types) }.getOrNull()
        }
    }

    private fun call(name: String) {
        runCatching { method(name)?.invoke(player) }
            .onFailure { Log.w(TAG, "UnityPlayer.$name() falhou: ${it.describe()}") }
    }

    private fun <T> call(name: String, type: Class<*>, arg: T) {
        runCatching { method(name, type)?.invoke(player, arg) }
            .onFailure { Log.w(TAG, "UnityPlayer.$name() falhou: ${it.describe()}") }
    }
}
