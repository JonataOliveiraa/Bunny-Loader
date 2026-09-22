package dev.bunnyloader.game

import android.app.Activity
import android.content.Context
import android.content.res.Configuration
import android.view.View
import java.lang.reflect.Proxy

/**
 * Encapsula a criação da UnityPlayer e repassa o ciclo de vida. O construtor
 * muda entre versões da Unity, então a busca é por reflection com alternativas.
 *
 * TODO(Fase 1): abrir o APK do jogo no jadx e comparar com a UnityPlayerActivity
 * original — reproduzir aqui tudo que ela faz em onCreate/onKeyDown/onTouchEvent/
 * onNewIntent.
 */
class UnityHost(
    private val activity: Activity,
    private val context: Context,
    private val loader: ClassLoader,
) {
    private lateinit var playerClass: Class<*>
    private lateinit var player: Any

    fun create(): View {
        playerClass = loader.loadClass("com.unity3d.player.UnityPlayer")
        player = newPlayer()
        return player as View
    }

    private fun newPlayer(): Any {
        val lifecycle = runCatching {
            loader.loadClass("com.unity3d.player.IUnityPlayerLifecycleEvents")
        }.getOrNull()
        if (lifecycle != null) {
            val proxy = Proxy.newProxyInstance(loader, arrayOf(lifecycle)) { _, method, _ ->
                if (method.name == "onUnityPlayerQuitted") activity.finish()
                null
            }
            val ctor = playerClass.getConstructor(Context::class.java, lifecycle)
            return ctor.newInstance(context, proxy)
        }
        return playerClass.getConstructor(Context::class.java).newInstance(context)
    }

    fun resume() = call("resume")
    fun pause() = call("pause")
    fun destroy() = call("destroy")
    fun windowFocusChanged(focus: Boolean) = call("windowFocusChanged", focus)
    fun configurationChanged(config: Configuration) = call("configurationChanged", config)
    fun lowMemory() = call("lowMemory")

    private fun call(name: String, vararg args: Any) {
        val types = args.map {
            when (it) {
                is Boolean -> java.lang.Boolean.TYPE
                is Configuration -> Configuration::class.java
                else -> it.javaClass
            }
        }.toTypedArray()
        runCatching { playerClass.getMethod(name, *types).invoke(player, *args) }
    }
}
