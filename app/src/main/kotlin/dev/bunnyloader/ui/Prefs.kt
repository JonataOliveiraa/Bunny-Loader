package dev.bunnyloader.ui

import android.content.Context

/**
 * As preferências da aba de Configurações.
 *
 * Só entram aqui ajustes que mudam alguma coisa de verdade: cada um destes é
 * lido em algum ponto do boot. Interruptor que não liga em nada é pior que
 * ajuste nenhum — dá a impressão de controle que o app não tem.
 */
class Prefs(context: Context) {
    private val p = context.getSharedPreferences(NAME, Context.MODE_PRIVATE)

    /** Lido por ModRepository.isEnabled como padrão de um mod ainda sem escolha. */
    var enableOnInstall: Boolean
        get() = p.getBoolean(ENABLE_ON_INSTALL, true)
        set(v) = p.edit().putBoolean(ENABLE_ON_INSTALL, v).apply()

    /** Lido por GameActivity: vira o painel de erro dentro do jogo. */
    var errorPanel: Boolean
        get() = p.getBoolean(ERROR_PANEL, true)
        set(v) = p.edit().putBoolean(ERROR_PANEL, v).apply()

    /**
     * Canal de comando por arquivo (adb push). Desligado, o `cmdPath` vai
     * vazio no NativeConfig e o núcleo nem tenta abrir o arquivo a cada quadro.
     */
    var devChannel: Boolean
        get() = p.getBoolean(DEV_CHANNEL, false)
        set(v) = p.edit().putBoolean(DEV_CHANNEL, v).apply()

    companion object {
        const val NAME = "settings"
        const val ENABLE_ON_INSTALL = "enableOnInstall"
        const val ERROR_PANEL = "errorPanel"
        const val DEV_CHANNEL = "devChannel"
    }
}
