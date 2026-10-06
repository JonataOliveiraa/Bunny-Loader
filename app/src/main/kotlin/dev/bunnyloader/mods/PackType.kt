package dev.bunnyloader.mods

/**
 * O tipo de um pacote, pelo `type` do manifesto. Só separa as listas do
 * launcher (Explorar e Pacotes). Texturas carregam PNGs, sem arquivo JavaScript;
 * fontes ainda nao tem carregador no jogo.
 *
 * O manifesto pode escrever em inglês ou português, no singular ou plural:
 * quem faz pacote não deveria ter de adivinhar a grafia. O que não bate com
 * nada é mod, o mesmo que não escrever o campo.
 */
enum class PackType(val label: String) {
    MOD("Mods"),
    TEXTURE("Texturas"),
    FONT("Fontes");

    companion object {
        private val ALIASES = mapOf(
            "texture" to TEXTURE, "textures" to TEXTURE, "textura" to TEXTURE, "texturas" to TEXTURE,
            "resourcepack" to TEXTURE, "resource-pack" to TEXTURE,
            "font" to FONT, "fonts" to FONT, "fonte" to FONT, "fontes" to FONT,
        )

        fun of(type: String): PackType = ALIASES[type.trim().lowercase()] ?: MOD
    }
}
