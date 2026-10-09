package dev.bunnyloader.mods

import kotlinx.serialization.Serializable
import kotlinx.serialization.builtins.ListSerializer
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.JsonTransformingSerializer

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
    /** Forma antiga e curta de `authors`: um nome só. */
    val author: String = "",
    /**
     * Quem fez o mod, na ordem em que aparecem. Cada um é um nome solto
     * (`"Fulano"`) ou um objeto com foto, papel, cor e link (ver Author). A
     * foto mora em `authors/` no pacote.
     */
    @Serializable(with = AuthorListSerializer::class)
    val authors: List<Author> = emptyList(),
    /** Textura, Armas, Jogabilidade, Cheat, Utilidade... O pacote escolhe. */
    val category: String = "Mod",
    /**
     * O tipo do pacote, que separa as listas do launcher: `"mod"` (o padrão),
     * `"texture"` ou `"font"`. Opcional; sem ele, ou com um valor que o app
     * não conhece, o pacote é mod. Ver [PackType].
     */
    val type: String = "",
    /** Uma linha, para o cartão da lista. */
    val summary: String = "",
    /**
     * Texto da ficha quando o pacote não tem `description.md`. É a forma
     * antiga: o Markdown ganha (títulos, listas, imagens, cores).
     */
    val description: String = "",
    /** Nome curto da licença ("MIT", "CC BY-NC 4.0"); o texto vai em `license.md`. */
    val license: String = "",
    /** Botões de link na ficha: código-fonte, Discord, vídeo... */
    val links: List<PackLink> = emptyList(),
    /** Abas a mais na ficha, cada uma um `.md` do pacote. */
    val pages: List<PackPage> = emptyList(),
    /** Cores da ficha do mod. Campo vazio = a cor do launcher. */
    val theme: PackTheme = PackTheme(),
    /** AAAA-MM-DD da última atualização. */
    val updated: String = "",
    /** Pede lugar no destaque da tela inicial. */
    val featured: Boolean = false,
    /**
     * O formato do pacote. 2: `content/` com Assets/, Common/, Content/ e
     * Localization/, e o arquivo de entrada com `export default class ...
     * extends Mod`. O 1 (sem a classe Mod) não carrega mais.
     */
    val blVersion: Int = 1,
    val gameVersion: List<Long> = emptyList(),
    /** Caminho do arquivo de entrada, relativo a `content/`. */
    val entry: String = "main.js",
    val dependencies: List<String> = emptyList(),
) {
    val hasValidUid: Boolean get() = isValidUid(uid)

    /** Mod, textura ou fonte: o `type` do manifesto, mod quando falta. */
    val packType: PackType get() = PackType.of(type)

    /** `authors`, ou o `author` antigo quando a lista não veio. */
    val credits: List<Author>
        get() = authors.filter { it.name.isNotBlank() }.ifEmpty {
            if (author.isBlank()) emptyList() else listOf(Author(author))
        }

    /** "A, B e C" — a linha "por ..." dos cartões. */
    val authorLine: String
        get() {
            val names = credits.map { it.name }
            return when (names.size) {
                0 -> "autor desconhecido"
                1 -> names[0]
                else -> names.dropLast(1).joinToString(", ") + " e " + names.last()
            }
        }

    /** Pacote de um formato que este Bunny Loader não carrega mais. */
    val isOutdated: Boolean get() = blVersion < ModRepository.MIN_BL_VERSION

    companion object {
        /**
         * `dfac5a5e-dd9a-4e57-a306-4147d34693cd` — UUID canônico, minúsculo.
         *
         * A forma é conferida, e não só a presença, porque o uid vira NOME DE
         * PASTA em `bunny_packs/`. Sem isto, um manifesto com
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

/**
 * Um autor do pacote.
 *
 * `avatar` é o nome do arquivo dentro de `authors/` (`"potato.png"`); sem ele,
 * o app procura `authors/<nome>.png` e `authors/<nome-em-minusculas-com-hifen>.png`.
 * Sem foto nenhuma, a ficha desenha a inicial do nome.
 */
@Serializable
data class Author(
    val name: String,
    /** "Código", "Arte", "Port para o Bunny Loader"... */
    val role: String = "",
    val avatar: String = "",
    /** Cor do nome, `#RRGGBB`. */
    val color: String = "",
    val link: String = "",
)

@Serializable
data class PackLink(
    val title: String,
    val url: String,
)

/** Uma aba a mais na ficha: o título e o `.md` do pacote que ela mostra. */
@Serializable
data class PackPage(
    val title: String,
    val file: String,
)

/**
 * As cores da ficha, todas `#RRGGBB` (ou `#AARRGGBB`). Vale só dentro da
 * ficha deste mod; a lista e o resto do launcher seguem a paleta do app.
 */
@Serializable
data class PackTheme(
    /** Aba escolhida, links, títulos de seção e o destaque do changelog. */
    val accent: String = "",
    /** Fundo dos painéis da ficha. */
    val panel: String = "",
    /** Texto corrido. */
    val text: String = "",
    /** Títulos (#, ##, ###) do Markdown. */
    val heading: String = "",
    /** Fundo dos botões da ficha (baixar, remover, exportar, links). */
    val button: String = "",
    /** Texto dos botões. */
    val buttonText: String = "",
    /**
     * O fundo da tela atrás da ficha, no lugar do cenário do launcher: uma cor
     * (`#RRGGBB`) ou uma imagem do pacote (`background.png`, `.gif` anima).
     */
    val background: String = "",
    /** `cover` (enche a tela, cortando) ou `tile` (repete, como uma textura). */
    val backgroundMode: String = "",
    /**
     * No `tile`, quantos pixels da tela (dp) cada pixel da imagem ocupa.
     * Padrão 2. Número ou texto: um `"2"` entre aspas não derruba o manifesto.
     */
    val backgroundScale: JsonPrimitive? = null,
    /** 0 a 1: o quanto o fundo escurece, para o texto se destacar. Padrão 0. */
    val backgroundDim: JsonPrimitive? = null,
) {
    val tileScale: Float get() = backgroundScale?.content?.toFloatOrNull()?.takeIf { it > 0f }?.coerceAtMost(16f) ?: 2f
    val dim: Float get() = backgroundDim?.content?.toFloatOrNull()?.coerceIn(0f, 1f) ?: 0f
}

/**
 * Aceita `"authors": "Fulano"`, `["Fulano", "Ciclano"]` e a lista de objetos.
 * O nome solto é o caso comum, e exigir objeto para ele só daria trabalho a
 * quem faz mod sozinho.
 */
object AuthorListSerializer :
    JsonTransformingSerializer<List<Author>>(ListSerializer(Author.serializer())) {
    override fun transformDeserialize(element: JsonElement): JsonElement {
        val items = if (element is JsonArray) element else JsonArray(listOf(element))
        return JsonArray(items.map {
            if (it is JsonPrimitive) JsonObject(mapOf("name" to it)) else it
        })
    }
}
