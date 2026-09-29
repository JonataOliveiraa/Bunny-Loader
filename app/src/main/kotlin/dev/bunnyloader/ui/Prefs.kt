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
     * Lido por GameActivity: o log de sessão ganha o detalhe do núcleo (cada
     * tabela, hook e tipo registrado). Desligado, o arquivo fica com o resumo
     * de cada sistema e as linhas dos mods.
     */
    var verboseLog: Boolean
        get() = p.getBoolean(VERBOSE_LOG, false)
        set(v) = p.edit().putBoolean(VERBOSE_LOG, v).apply()

    /**
     * Canal de comando por arquivo (adb push). Desligado, o `cmdPath` vai
     * vazio no NativeConfig e o núcleo nem tenta abrir o arquivo a cada quadro.
     */
    var devChannel: Boolean
        get() = p.getBoolean(DEV_CHANNEL, false)
        set(v) = p.edit().putBoolean(DEV_CHANNEL, v).apply()

    /**
     * Lido por GameActivity: o splash acaba quando o jogo termina de carregar
     * e, com [quickPlayer] e [quickWorld], o jogo entra direto no mundo.
     */
    var quickStart: Boolean
        get() = p.getBoolean(QUICK_START, false)
        set(v) = p.edit().putBoolean(QUICK_START, v).apply()

    /** O arquivo do personagem do início rápido (`Bench.plr`), em Players/. */
    var quickPlayer: String
        get() = p.getString(QUICK_PLAYER, "") ?: ""
        set(v) = p.edit().putString(QUICK_PLAYER, v).apply()

    /**
     * O arquivo do mundo do início rápido, em Worlds/. [TITLE_ONLY]: só a
     * abertura rápida, parando no título. Vazio: ainda não escolhido.
     */
    var quickWorld: String
        get() = p.getString(QUICK_WORLD, "") ?: ""
        set(v) = p.edit().putString(QUICK_WORLD, v).apply()

    /**
     * Lidas pelo Mod Menu dentro do jogo (CheatBridge.readDevSettings), pelo
     * nome da chave: o botão do coelho, o Editor de JS e o Reiniciar.
     */
    var devModMenu: Boolean
        get() = p.getBoolean(DEV_MOD_MENU, true)
        set(v) = p.edit().putBoolean(DEV_MOD_MENU, v).apply()

    var devEditor: Boolean
        get() = p.getBoolean(DEV_EDITOR, true)
        set(v) = p.edit().putBoolean(DEV_EDITOR, v).apply()

    var devRestart: Boolean
        get() = p.getBoolean(DEV_RESTART, true)
        set(v) = p.edit().putBoolean(DEV_RESTART, v).apply()

    /** O launcher já conferiu (e, se faltava, avisou) o Terraria instalado. */
    var gameCheckDone: Boolean
        get() = p.getBoolean(GAME_CHECK_DONE, false)
        set(v) = p.edit().putBoolean(GAME_CHECK_DONE, v).apply()

    /** O mundo do início rápido a entregar ao núcleo: vazio = parar no título. */
    val quickWorldFile: String
        get() = quickWorld.takeIf { quickStart && it != TITLE_ONLY } ?: ""

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
        const val VERBOSE_LOG = "verboseLog"
        const val QUICK_START = "quickStart"
        const val QUICK_PLAYER = "quickPlayer"
        const val QUICK_WORLD = "quickWorld"
        const val DEV_MOD_MENU = "devModMenu"
        const val DEV_EDITOR = "devEditor"
        const val DEV_RESTART = "devRestart"
        const val GAME_CHECK_DONE = "gameCheckDone"
        /** Nenhum arquivo de mundo tem este nome (o jogo grava `*.wld`). */
        const val TITLE_ONLY = "-"
    }
}
