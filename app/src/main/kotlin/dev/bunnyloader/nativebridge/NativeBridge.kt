package dev.bunnyloader.nativebridge

/** Config passada uma única vez ao núcleo nativo, antes da Unity subir. */
class NativeConfig(
    @JvmField val gameLibDir: String,
    @JvmField val modsDir: String,
    /** Um por mod habilitado, no formato `uid=entry`. Ver ModRepository. */
    @JvmField val enabledMods: Array<String>,
    @JvmField val logPath: String,
    /** Canal de dev por arquivo (adb push). Vazio desliga. */
    @JvmField val cmdPath: String,
    /** Painel de erro dentro do jogo (Configuracoes). */
    @JvmField val showErrors: Boolean,
    /** Log detalhado (Configuracoes): o detalhe do núcleo também no arquivo de sessão. */
    @JvmField val verboseLog: Boolean,
    @JvmField val gameVersion: Long,
    /** Início rápido (Configurações > Desenvolvedor): sem a espera do splash. */
    @JvmField val fastBoot: Boolean,
    /** O personagem em que o jogo entra sozinho (`Bench.plr`). Vazio: para no título. */
    @JvmField val quickPlayer: String,
    /** O mundo em que o jogo entra sozinho (`Interior_Pálido.wld`). */
    @JvmField val quickWorld: String,
)

/**
 * Única fronteira entre Kotlin e C++. Chamada uma vez, antes da Unity carregar
 * a libil2cpp.so. Depois disso tudo acontece no nativo.
 */
object NativeBridge {
    @Volatile private var loaded = false

    fun ensureLoaded(): Boolean {
        if (loaded) return true
        return try {
            System.loadLibrary("bunny")
            loaded = true
            true
        } catch (t: Throwable) {
            // Fase 1 roda sem o nativo; não é fatal ainda.
            false
        }
    }

    // TODO(Fase 2): implementar em boot/jni_entry.cpp
    external fun init(config: NativeConfig): Boolean
    external fun lastError(): String
}
