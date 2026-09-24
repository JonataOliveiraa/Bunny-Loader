package dev.bunnyloader.ui

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.wrapContentWidth
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.material3.Text
import kotlinx.coroutines.delay

/**
 * Peças de interface pixelada.
 *
 * Duas regras valem para tudo aqui:
 *
 *  1. `FilterQuality.None`. O padrão do Compose é interpolação linear, que num
 *     sprite de 32x32 ampliado para 64dp vira borrão. Um pixel tem que virar um
 *     quadrado, não um degradê.
 *  2. Painel com BISEL, não com sombra. O contorno escuro vem de fora, a luz
 *     de cima-esquerda e a sombra de baixo-direita, cada um com um pixel. É o
 *     que faz a superfície parecer recortada em vez de desenhada.
 *
 * E uma de toque: tudo que se aperta ganha a borda amarela enquanto o dedo
 * está nele (`pixelClickable`), no lugar da onda do Material, que não tem
 * nada de pixel.
 */

// ------------------------------- sprites -------------------------------

@Composable
fun PixelIcon(res: Int, size: Dp, modifier: Modifier = Modifier, alpha: Float = 1f) {
    Image(
        bitmap = ImageBitmap.imageResource(res),
        contentDescription = null,
        filterQuality = FilterQuality.None,
        alpha = alpha,
        modifier = modifier.size(size),
    )
}

/**
 * Um quadro de uma folha de sprites (Tiles_3 tem 45 plantas de 18x22 em fila).
 *
 * Recorta na hora de desenhar em vez de fatiar o bitmap: a folha inteira fica
 * uma textura só, e trocar de quadro não aloca nada.
 */
@Composable
fun SpriteFrame(res: Int, frameW: Int, frameH: Int, index: Int, size: Dp,
                modifier: Modifier = Modifier) {
    val sheet = ImageBitmap.imageResource(res)
    val cols = (sheet.width / frameW).coerceAtLeast(1)
    val col = index % cols
    val row = index / cols
    Canvas(modifier.size(size, size * frameH / frameW)) {
        drawImage(
            image = sheet,
            srcOffset = IntOffset(col * frameW, row * frameH),
            srcSize = IntSize(frameW, frameH),
            dstOffset = IntOffset.Zero,
            dstSize = IntSize(this.size.width.toInt(), this.size.height.toInt()),
            filterQuality = FilterQuality.None,
        )
    }
}

// -------------------------------- texto --------------------------------

/**
 * Texto branco com contorno preto, do jeito do Terraria.
 *
 * O jogo desenha cada string cinco vezes: preto deslocado um pixel para cada
 * lado e a cor por cima. É o que faz o texto continuar legível sobre céu claro,
 * sobre pedra e sobre grama, sem precisar de caixa atrás.
 *
 * Sombra com desfoque resolveria o contraste mas entregaria que o texto não é
 * do mesmo material que o resto — o mesmo motivo de a sombra dos painéis ser um
 * retângulo sólido.
 */
@Composable
fun PixelText(
    text: String,
    size: Int = Ts.Body,
    color: Color = Color.White,
    modifier: Modifier = Modifier,
    outline: Color = Bl.Ink,
    maxLines: Int = Int.MAX_VALUE,
    overflow: TextOverflow = TextOverflow.Clip,
    align: TextAlign? = null,
) {
    Box(modifier) {
        val body: @Composable (Color, Modifier) -> Unit = { c, m ->
            Text(
                text, color = c, modifier = m,
                fontFamily = PixelFont, fontSize = size.sp,
                maxLines = maxLines, overflow = overflow, textAlign = align,
            )
        }
        for ((dx, dy) in OUTLINE_OFFSETS) {
            body(outline, Modifier.offset(dx.dp, dy.dp))
        }
        body(color, Modifier)
    }
}

// Quatro lados. As diagonais engrossariam o contorno a ponto de fechar os
// vãos da fonte, que é pixelada e estreita.
private val OUTLINE_OFFSETS = listOf(-1 to 0, 1 to 0, 0 to -1, 0 to 1)

// ------------------------------- painéis -------------------------------

/**
 * Sombra projetada, do jeito que arte de pixel faz: um retângulo sólido
 * deslocado, sem desfoque nenhum. Desfoque num layout de pixel entrega que a
 * interface não é do mesmo material que o resto.
 *
 * Vem ANTES do pixelPanel na corrente — quem desenha primeiro fica atrás.
 */
fun Modifier.pixelShadow(dx: Dp = 4.dp, dy: Dp = 5.dp): Modifier = drawBehind {
    drawRect(Bl.Shadow, Offset(dx.toPx(), dy.toPx()), size)
}

/**
 * Painel com bisel. `raised = false` afunda (para campo de entrada, poço de
 * lista), `true` levanta (para cartão, botão).
 *
 * A cor é chapada: é a da paleta, e um degradê por cima a tiraria do tom. Quem
 * dá o volume é a borda de três camadas — contorno, luz e sombra — que é o que
 * o próprio Terraria usa nos painéis dele.
 */
fun Modifier.pixelPanel(
    fill: Color = Bl.Panel,
    raised: Boolean = true,
    outline: Color = Bl.Outline,
    light: Color = fill.mix(Color.White, 0.14f),
    dark: Color = fill.mix(Bl.Night, 0.45f),
    gradient: Boolean = false,
): Modifier = drawBehind {
    val b = 2.dp.toPx()
    val e = 2.dp.toPx()
    val w = size.width
    val h = size.height
    drawRect(outline)
    val inner = Size(w - 2 * b, h - 2 * b)
    if (gradient) {
        drawRect(
            Brush.verticalGradient(
                listOf(fill.mix(light, 0.22f), fill, fill.mix(Bl.Night, 0.18f)),
                startY = b, endY = h - b,
            ),
            Offset(b, b), inner,
        )
    } else {
        drawRect(fill, Offset(b, b), inner)
    }
    val top = if (raised) light else dark
    val bottom = if (raised) dark else light
    // Luz em cima e à esquerda; sombra embaixo e à direita.
    drawRect(top, Offset(b, b), Size(w - 2 * b, e))
    drawRect(top, Offset(b, b), Size(e, h - 2 * b))
    drawRect(bottom, Offset(b, h - b - e), Size(w - 2 * b, e))
    drawRect(bottom, Offset(w - b - e, b), Size(e, h - 2 * b))
}

/**
 * Moldura de imagem: borda lisa e fundo escuro, sem bisel. Um bisel em volta
 * de uma arte compete com ela; a moldura só a separa do painel.
 */
fun Modifier.framePanel(
    border: Color = Bl.FrameBorder,
    fill: Color = Bl.FrameFill,
): Modifier = drawBehind {
    val b = 2.dp.toPx()
    drawRect(border)
    drawRect(fill, Offset(b, b), Size(size.width - 2 * b, size.height - 2 * b))
}

/**
 * Clique com a borda amarela do toque, desenhada POR CIMA do conteúdo: assim
 * vale para cartão, moldura e botão sem cada um saber disso.
 */
fun Modifier.pixelClickable(onClick: () -> Unit): Modifier = composed {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    drawWithContent {
        drawContent()
        if (pressed) drawPressBorder()
    }.clickable(interaction, indication = null, onClick = onClick)
}

fun DrawScope.drawPressBorder() {
    val b = 2.dp.toPx()
    drawRect(Bl.PressedBorder, Offset(b / 2, b / 2), Size(size.width - b, size.height - b),
        style = Stroke(b))
}

fun Color.mix(other: Color, t: Float) = Color(
    red + (other.red - red) * t,
    green + (other.green - green) * t,
    blue + (other.blue - blue) * t,
    alpha,
)

/**
 * O recipiente padrão. Com sombra: sem ela o cartão vira um retângulo pintado
 * no fundo, e a tela inteira fica com cara de esboço.
 */
@Composable
fun PixelCard(
    modifier: Modifier = Modifier,
    fill: Color = Bl.Panel,
    shadow: Boolean = true,
    onClick: (() -> Unit)? = null,
    content: @Composable BoxScope.() -> Unit,
) {
    val base = (if (shadow) modifier.pixelShadow() else modifier).pixelPanel(fill = fill)
    Box(if (onClick != null) base.pixelClickable(onClick) else base, content = content)
}

/**
 * Botão de ação. Enquanto pressionado, afunda até a sombra, clareia e ganha a
 * borda amarela — o retorno que um botão de pixel dá.
 */
@Composable
fun PixelButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    fill: Color = Bl.Button,
    textColor: Color = Color.White,
    icon: Int? = null,
    fontSize: Int = Ts.Item,
) {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    // Pressionado: o botão desce até a sombra e a sombra some. É o mesmo
    // deslocamento, não uma animação — o olho lê como o botão afundando.
    Box(
        modifier
            .then(if (pressed) Modifier.padding(start = 3.dp, top = 4.dp)
                  else Modifier.pixelShadow(3.dp, 4.dp))
            .pixelPanel(
                fill = if (pressed) Bl.ButtonPressed else fill,
                raised = !pressed,
                outline = if (pressed) Bl.PressedBorder else Bl.Outline,
            )
            .clickable(interaction, indication = null, onClick = onClick)
            .padding(horizontal = 18.dp, vertical = 10.dp),
        contentAlignment = Alignment.Center,
    ) {
        androidx.compose.foundation.layout.Row(verticalAlignment = Alignment.CenterVertically) {
            if (icon != null) {
                PixelIcon(icon, 18.dp, Modifier.padding(end = 8.dp))
            }
            Text(
                text,
                fontFamily = PixelFont,
                fontSize = fontSize.sp,
                fontWeight = FontWeight.Normal,
                color = textColor,
            )
        }
    }
}

/** Etiqueta de categoria (Textura, Armas, Cheat...). */
@Composable
fun PixelTag(text: String, fill: Color = Bl.TitleFill, modifier: Modifier = Modifier) {
    Box(
        modifier
            .pixelShadow(2.dp, 3.dp)
            .pixelPanel(fill = fill)
            .padding(horizontal = 8.dp, vertical = 3.dp),
    ) {
        Text(text, fontFamily = PixelFont, fontSize = Ts.Small.sp, color = Color.White)
    }
}

// ------------------------------ scrollbar ------------------------------
//
// Aparece ao rolar e some sozinha. Sem ela, uma lista longa não dá pista
// nenhuma de tamanho — e as listas de mods e de itens são longas.

@Composable
fun PixelScrollbar(state: ScrollState, modifier: Modifier = Modifier) {
    Scrollbar(
        value = { state.value },
        max = { state.maxValue },
        viewport = { state.viewportSize },
        modifier = modifier,
    )
}

@Composable
fun PixelScrollbar(state: LazyListState, modifier: Modifier = Modifier) {
    Scrollbar(
        value = {
            val info = state.layoutInfo
            val visible = info.visibleItemsInfo
            val avg = if (visible.isEmpty()) 0 else visible.sumOf { it.size } / visible.size
            state.firstVisibleItemIndex * avg + state.firstVisibleItemScrollOffset
        },
        max = {
            val info = state.layoutInfo
            val visible = info.visibleItemsInfo
            val avg = if (visible.isEmpty()) 0 else visible.sumOf { it.size } / visible.size
            (info.totalItemsCount * avg - info.viewportSize.height).coerceAtLeast(1)
        },
        viewport = { state.layoutInfo.viewportSize.height },
        modifier = modifier,
    )
}

@Composable
private fun Scrollbar(
    value: () -> Int,
    max: () -> Int,
    viewport: () -> Int,
    modifier: Modifier = Modifier,
) {
    var show by remember { mutableStateOf(false) }
    val alpha by animateFloatAsState(if (show) 1f else 0f, label = "scrollbar")

    LaunchedEffect(value()) {
        show = true
        delay(900)
        show = false
    }

    Canvas(modifier.fillMaxHeight().wrapContentWidth(Alignment.End).width(4.dp)) {
        val v = value()
        val m = max().coerceAtLeast(1)
        val port = viewport().coerceAtLeast(1)
        val total = port + m
        val thumb = size.height * (port.toFloat() / total).coerceAtMost(1f)
        val y = (v.toFloat() / m).coerceIn(0f, 1f) * (size.height - thumb)
        drawRect(Bl.ButtonPressed.copy(alpha = alpha * 0.9f), Offset(0f, y), Size(size.width, thumb))
    }
}


// ------------------------------- seletor -------------------------------

/**
 * Campo de escolha: o valor no meio e as setas do jogo (TexturePackButtons)
 * nas pontas. Afundado, como campo de entrada, no tom próprio dos seletores.
 */
@Composable
fun PixelSelect(value: String, onPrev: () -> Unit, onNext: () -> Unit,
                modifier: Modifier = Modifier) {
    androidx.compose.foundation.layout.Row(
        modifier.fillMaxWidth().pixelPanel(fill = Bl.Select, raised = false).padding(4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(Modifier.pixelClickable(onPrev).padding(8.dp)) {
            PixelIcon(dev.bunnyloader.R.drawable.ic_seta_esq, 22.dp)
        }
        PixelText(value, size = Ts.Item, align = TextAlign.Center,
            modifier = Modifier.weight(1f))
        Box(Modifier.pixelClickable(onNext).padding(8.dp)) {
            PixelIcon(dev.bunnyloader.R.drawable.ic_seta_dir, 22.dp)
        }
    }
}
