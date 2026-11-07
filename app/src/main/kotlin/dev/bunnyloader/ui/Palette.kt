package dev.bunnyloader.ui

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import dev.bunnyloader.R

/**
 * A paleta do launcher, usada por FUNÇÃO.
 *
 * Painel, botão, campo, moldura de imagem e faixa de título têm cada um o seu
 * tom, e o amarelo aparece só no toque.
 */
object Bl {
    val BackdropTop = Color(0xFF12152B)
    val BackdropBottom = Color(0xFF232A50)

    /** Fundo de painel, cartão e linha de lista. */
    val Panel = Color(0xFF2B335A)

    /** Botão em repouso, e o botão enquanto o dedo está nele. */
    val Button = Color(0xFF323471)
    val ButtonPressed = Color(0xFF717BD5)

    /** Campo de escolha e de texto (seletor, busca). */
    val Select = Color(0xFF32407D)

    /** Moldura e fundo de tudo que mostra imagem: ícone, banner, prévia. */
    val FrameBorder = Color(0xFF4E5275)
    val FrameFill = Color(0xFF1A1E39)

    /** Faixa atrás de título de seção, e etiqueta. */
    val TitleFill = Color(0xFF4A5DAA)

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
    val GamePanel = Color(0xFF3F5297)

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
    val DisabledPanel = Panel

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
