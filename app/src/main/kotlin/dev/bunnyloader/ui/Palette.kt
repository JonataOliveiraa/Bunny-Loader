package dev.bunnyloader.ui

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import dev.bunnyloader.R

/**
 * As três paletas do projeto, usadas por FUNÇÃO e não por decoração.
 *
 * A ideia é a mesma de um mundo de Terraria visto de lado: pedra é a estrutura
 * (painéis, cartões, texto), terra é o chão em que a interface se apoia (o
 * rodapé, as divisões), grama é o que está vivo (ação, ligado, selecionado).
 * Assim a cor diz o que a coisa É, e não só como ela parece.
 *
 * O azul #3f5297 fica de fora de propósito: é o painel DENTRO do jogo, onde
 * ele precisa competir com o cenário. Aqui fora ele não teria função.
 */
object Bl {
    // --- terra: o chão ---
    val Dirt0 = Color(0xFF644730)
    val Dirt1 = Color(0xFF875F41)
    val Dirt2 = Color(0xFF976B4B)
    val Dirt3 = Color(0xFFAC7A66)

    // --- grama: o que está vivo ---
    val Grass0 = Color(0xFF0D632B)
    val Grass1 = Color(0xFF1D9045)
    val Grass2 = Color(0xFF22A851)
    val Grass3 = Color(0xFF1BCF5A)
    val Grass4 = Color(0xFF2EED52)

    // --- pedra: a estrutura ---
    val Stone0 = Color(0xFF404040)
    val Stone1 = Color(0xFF636363)
    val Stone2 = Color(0xFF808080)
    val Stone3 = Color(0xFFA5A5A5)
    val Stone4 = Color(0xFFCBCBCB)

    /** Contorno de todo painel, e o fundo da tela: a noite lá no fundo. */
    val Outline = Color(0xFF131625)
    val Night = Color(0xFF0D0F1A)

    /** Sombra projetada dos cartões. Preta e opaca — sem desfoque. */
    val Shadow = Color(0xB3000000)

    /** Contorno do texto. Preto puro: qualquer cinza some sobre o fundo. */
    val Ink = Color(0xFF000000)

    /** Painel dentro do JOGO (a tela de ferramentas, sobre o cenário). */
    val GamePanel = Color(0xFF3F5297)

    // --- texto ---
    // Branco puro na frente, porque todo texto da interface leva contorno
    // preto (ver PixelText): com o contorno, cinza claro so perde legibilidade.
    val Text = Color.White
    val TextDim = Stone4
    val TextFaint = Stone3
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
