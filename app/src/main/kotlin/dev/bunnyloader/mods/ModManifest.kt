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
     * Identidade do pacote no mundo. É por ela que o mod é instalado, ligado e
     * desinstalado.
     *
     * O `id` é um apelido legível e dois autores podem escolher o mesmo — se a
     * identidade fosse ele, instalar o "vidacheia" de alguém apagaria o seu. O
     * uid é gerado uma vez, ao empacotar, e nunca muda: é o que permite
     * reconhecer uma ATUALIZAÇÃO do mesmo mod em vez de um mod diferente.
     *
     * Formato: `<apelido>.<8 hex>`. Pacote antigo sem uid cai de volta no id.
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
    /** A identidade de verdade, com a saída para pacote antigo. */
    val key: String get() = uid.ifBlank { id }
}
