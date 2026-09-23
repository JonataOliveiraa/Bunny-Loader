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

    /** Painel dentro do JOGO (a tela de ferramentas, sobre o cenário). */
    val GamePanel = Color(0xFF3F5297)

    // --- texto ---
    val Text = Stone4
    val TextDim = Stone3
    val TextFaint = Stone2
}

/** Fonte pixelada de _icons/font.TTF — a mesma identidade do título. */
val PixelFont = FontFamily(Font(R.font.bunny))
