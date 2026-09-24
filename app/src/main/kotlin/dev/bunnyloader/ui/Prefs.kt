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

    /**
     * O cenário do fundo: o nome de um [Biome], ou vazio para trocar a cada vez
     * que o launcher abre (o próximo da lista, guardado em [LAST_SCENERY]).
     */
    var scenery: String
        get() = p.getString(SCENERY, "") ?: ""
        set(v) = p.edit().putString(SCENERY, v).apply()

    /** O cenário que vale agora. No automático, avança um e grava. */
    fun resolveScenery(choice: String): Biome {
        Biome.entries.firstOrNull { it.name == choice }?.let { return it }
        val next = (p.getInt(LAST_SCENERY, -1) + 1).mod(Biome.entries.size)
        p.edit().putInt(LAST_SCENERY, next).apply()
        return Biome.entries[next]
    }

    companion object {
        const val NAME = "settings"
        const val SCENERY = "scenery"
        const val LAST_SCENERY = "lastScenery"
        const val ENABLE_ON_INSTALL = "enableOnInstall"
        const val ERROR_PANEL = "errorPanel"
        const val DEV_CHANNEL = "devChannel"
    }
}
