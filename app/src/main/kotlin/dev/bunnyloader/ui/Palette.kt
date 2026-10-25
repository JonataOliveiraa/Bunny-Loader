package dev.bunnyloader.ui

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import dev.bunnyloader.R

/**
 * A paleta do launcher: o azul dos painéis do Terraria, usado por FUNÇÃO.
 *
 * Painel, botão, campo, moldura de imagem e faixa de título têm cada um o seu
 * tom, e o amarelo aparece só no toque — é o destaque que o próprio jogo usa
 * quando o dedo (ou o mouse) está sobre alguma coisa. Com o cenário vivo atrás,
 * a interface fica numa família só de cor e quem dá variedade é o fundo.
 *
 * Os tons de painel, botão, campo, moldura, título e barra vêm do tema
 * ([UiPalette], escolhido pelo [Biome]): lidos por estado, a troca de tema
 * repinta a interface inteira sem cada tela saber dele.
 */
object Bl {
    /** A paleta do tema atual. Quem escreve é o launcher, ao resolver o cenário. */
    var palette by mutableStateOf(UiPalette.CLASSIC)

    /** Fundo de painel, cartão e linha de lista. */
    val Panel get() = palette.panel

    /** Botão em repouso, e o botão enquanto o dedo está nele. */
    val Button get() = palette.button
    val ButtonPressed get() = palette.buttonPressed

    /** Campo de escolha e de texto (seletor, busca). */
    val Select get() = palette.select

    /** Moldura e fundo de tudo que mostra imagem: ícone, banner, prévia. */
    val FrameBorder get() = palette.frameBorder
    val FrameFill get() = palette.frameFill

    /** Faixa atrás de título de seção, e etiqueta. */
    val TitleFill get() = palette.titleFill

    /** Toque: a borda de qualquer botão e o texto de botão sem caixa. */
    val PressedBorder = Color(0xFFEBE27F)
    val PressedText = Color(0xFFF9E141)

    /** Cartão selecionado para arrastar. */
    val DragBorder = Color(0xFF74E65B)

    /** Contorno de todo painel. */
    val Outline = Color(0xFF131625)
    val Night = Color(0xFF0D0F1A)

    /** Contorno do texto. Preto puro: qualquer cinza some sobre o fundo. */
    val Ink = Color(0xFF000000)

    /** A barra de baixo: o painel de dentro do jogo. */
    val GamePanel get() = palette.gamePanel

    /** A aba atual no rodapé: o slot dourado do item selecionado na barra do jogo. */
    val SlotGold = Color(0xFFE9B43A)
    val SlotGoldOutline = Color(0xFF3A2608)

    // --- texto ---
    // Branco puro na frente, porque todo texto da interface leva contorno
    // preto (ver PixelText); os secundários puxam para o azul da paleta.
    val Text = Color.White
    val TextDim = Color(0xFFD5DAF2)
    val TextFaint = Color(0xFFA6AED6)
    val TextMuted = Color(0xFF7C85B6)
    /** Recusa e erro. O vermelho de dano do Terraria. */
    val Bad = Color(0xFFE0603F)
    /** Novidade boa: a etiqueta de atualização disponível. */
    val Good = Color(0xFF3F8F4E)

    /** Fundo do cartão de um pacote ligado, em Pacotes: o verde do "Ligado" do jogo, escurecido. */
    val EnabledPanel = Color(0xFF2B5A3A)

    /** Mod recém-publicado no topo do Explorar: o dourado de item raro, escurecido. */
    val NewPanel = Color(0xFF5A4A2B)
    val NewTitleFill = Color(0xFFA9822C)

    /**
     * O convite do Discord: o azul da marca, o único cartão fora da família de
     * azuis do Terraria — é ele que tem que saltar aos olhos.
     */
    val Discord = Color(0xFF5865F2)
    val DiscordDeep = Color(0xFF3B44A8)
    val DiscordText = Color(0xFFE3E6FF)

    /** As três primeiras posições de Populares. */
    val Gold = Color(0xFFB8902C)
    val Silver = Color(0xFF8790A8)
    val Bronze = Color(0xFF9C5F32)
}

/**
 * Os tons que cada tema troca. O clássico é o azul dos painéis do Terraria;
 * os outros seguem o bioma (Halloween roxo com laranja, Cogumelos azul-noite,
 * Deserto areia, Sagrado lilás com rosa).
 */
class UiPalette(
    val panel: Color,
    val button: Color,
    val buttonPressed: Color,
    val select: Color,
    val titleFill: Color,
    val gamePanel: Color,
    val frameBorder: Color,
    val frameFill: Color,
) {
    companion object {
        val CLASSIC = UiPalette(
            panel = Color(0xFF2B335A), button = Color(0xFF323471), buttonPressed = Color(0xFF717BD5),
            select = Color(0xFF32407D), titleFill = Color(0xFF4A5DAA), gamePanel = Color(0xFF3F5297),
            frameBorder = Color(0xFF4E5275), frameFill = Color(0xFF1A1E39),
        )
        val HALLOWEEN = UiPalette(
            panel = Color(0xFF34213F), button = Color(0xFF472A5A), buttonPressed = Color(0xFFC0702E),
            select = Color(0xFF452A58), titleFill = Color(0xFFC0621E), gamePanel = Color(0xFF4A2A60),
            frameBorder = Color(0xFF5E4470), frameFill = Color(0xFF1A0F22),
        )
        val MUSHROOM = UiPalette(
            panel = Color(0xFF1C2A57), button = Color(0xFF22357A), buttonPressed = Color(0xFF4F8FE0),
            select = Color(0xFF23397E), titleFill = Color(0xFF2E6FC4), gamePanel = Color(0xFF20366F),
            frameBorder = Color(0xFF3B5590), frameFill = Color(0xFF0C1330),
        )
        val DESERT = UiPalette(
            panel = Color(0xFF5A4327), button = Color(0xFF6B4F2C), buttonPressed = Color(0xFFB98A45),
            select = Color(0xFF6A4E2E), titleFill = Color(0xFFA8702E), gamePanel = Color(0xFF7A5A30),
            frameBorder = Color(0xFF7D6440), frameFill = Color(0xFF2A1D0F),
        )
        val HALLOW = UiPalette(
            panel = Color(0xFF3B3169), button = Color(0xFF4A3D86), buttonPressed = Color(0xFFB27AD8),
            select = Color(0xFF4B3F8E), titleFill = Color(0xFFB0559E), gamePanel = Color(0xFF54479A),
            frameBorder = Color(0xFF6A5DA6), frameFill = Color(0xFF1C1636),
        )
    }
}

private val BunnyFont = FontFamily(Font(R.font.bunny))

/**
 * Fonte pixelada de _icons/font.TTF — a mesma identidade do título.
 *
 * Em vietnamita a tela inteira usa a fonte do sistema: a pixelada não tem
 * ă, đ, ơ, ư nem os tons com gancho e ponto, e o fallback por letra trocava
 * de fonte no meio da palavra ("Việt"). Ler o idioma aqui recompõe os textos.
 */
val PixelFont: FontFamily
    get() = if (UiLocalization.language == "vi") FontFamily.Default else BunnyFont

/**
 * Escala de texto, num lugar só.
 *
 * Esta fonte desenha pequeno para o corpo que declara: 12sp nela parece 10sp de
 * uma fonte comum. Como o ajuste vale para a tela inteira, os tamanhos ficam
 * nomeados aqui — mexer na escala é mexer nesta lista, não caçar `fontSize` em
 * quatro arquivos.
 */
object Ts {
    val Tiny = 12   // legenda de grupo, id
    val Small = 14  // autor, etiqueta, rótulo de campo
    val Body = 16   // texto corrido, descrição
    val Item = 18   // nome numa linha de lista
    val Head = 22   // título de seção, nome do mod
    val Big = 26    // título de aba
}
