package dev.bunnyloader.mods

import kotlinx.serialization.Serializable

/**
 * manifest.json — o que o pacote diz sobre si mesmo.
 *
 * A vitrine do launcher é montada daqui, não de uma tabela à parte: categoria,
 * descrição e data saem do próprio mod. Assim um mod de terceiro aparece na
 * lista com a mesma cara dos nossos, sem o app precisar conhecê-lo.
 */
@Serializable
data class ModManifest(
    /**
     * Identidade do pacote no mundo, e o único nome pelo qual ele é instalado,
     * ligado e desinstalado. **Obrigatório**: pacote sem uid não carrega.
     *
     * O `id` é um apelido legível e dois autores podem escolher o mesmo — se a
     * identidade fosse ele, instalar o "vidacheia" de alguém apagaria o seu. O
     * uid é emitido uma vez pelo site do Bunny Loader e nunca muda: é o que
     * permite reconhecer uma ATUALIZAÇÃO do mesmo mod em vez de um mod
     * diferente, e o que impede um autor de sequestrar o pacote de outro.
     *
     * Formato: UUID na forma canônica, minúsculo. O campo é lido como opcional
     * de propósito — um manifesto sem uid tem de ser recusado com uma frase que
     * o autor entenda, não estourar um erro de desserialização.
     */
    val uid: String = "",
    val id: String,
    val name: String,
    val version: String,
    val author: String = "",
    /** Textura, Armas, Jogabilidade, Cheat, Utilidade... O pacote escolhe. */
    val category: String = "Mod",
    /** Uma linha, para o cartão da lista. */
    val summary: String = "",
    /** Parágrafo, para a tela do mod. */
    val description: String = "",
    /** AAAA-MM-DD da última atualização. */
    val updated: String = "",
    /** Pede lugar no destaque da tela inicial. */
    val featured: Boolean = false,
    val blVersion: Int = 1,
    val gameVersion: List<Long> = emptyList(),
    /** Caminho do arquivo de entrada, relativo a `content/`. */
    val entry: String = "main.js",
    val dependencies: List<String> = emptyList(),
) {
    val hasValidUid: Boolean get() = isValidUid(uid)

    companion object {
        /**
         * `dfac5a5e-dd9a-4e57-a306-4147d34693cd` — UUID canônico, minúsculo.
         *
         * A forma é conferida, e não só a presença, porque o uid vira NOME DE
         * PASTA em `filesDir/mods/`. Sem isto, um manifesto com
         * `"uid": "../databases"` faria o import apagar e reescrever fora da
         * pasta de mods — o guarda de zip slip cuida das entradas do zip, mas
         * o diretório de destino sai daqui. Hexadecimal e hífen não escapam de
         * pasta nenhuma.
         *
         * Qualquer versão de UUID serve: quem emite é o site, e prender a
         * versão aqui seria amarrar o app a uma decisão que é dele.
         */
        private val UID = Regex(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"
        )

        fun isValidUid(uid: String): Boolean = UID.matches(uid)
    }
}
