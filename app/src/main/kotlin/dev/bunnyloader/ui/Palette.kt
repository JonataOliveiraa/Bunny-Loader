package dev.bunnyloader.ui

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
 */
object Bl {
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

    /** Contorno de todo painel. */
    val Outline = Color(0xFF131625)
    val Night = Color(0xFF0D0F1A)

    /** Sombra projetada dos cartões. Preta e opaca — sem desfoque. */
    val Shadow = Color(0xB3000000)

    /** Contorno do texto. Preto puro: qualquer cinza some sobre o fundo. */
    val Ink = Color(0xFF000000)

    /** A barra de baixo: o painel de dentro do jogo. */
    val GamePanel = Color(0xFF3F5297)

    // --- texto ---
    // Branco puro na frente, porque todo texto da interface leva contorno
    // preto (ver PixelText); os secundários puxam para o azul da paleta.
    val Text = Color.White
    val TextDim = Color(0xFFD5DAF2)
    val TextFaint = Color(0xFFA6AED6)
    val TextMuted = Color(0xFF7C85B6)
    /** Recusa e erro. O vermelho de dano do Terraria. */
    val Bad = Color(0xFFE0603F)
}

/** Fonte pixelada de _icons/font.TTF — a mesma identidade do título. */
val PixelFont = FontFamily(Font(R.font.bunny))

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
