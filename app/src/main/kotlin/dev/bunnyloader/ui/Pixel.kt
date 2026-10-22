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
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.draw.drawWithCache
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Outline
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.drawscope.clipPath
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.drawText
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.material3.Text
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.collectLatest

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
    var layout by remember { mutableStateOf<TextLayoutResult?>(null) }
    Text(
        text, color = color,
        modifier = modifier.drawBehind {
            val measured = layout ?: return@drawBehind
            for ((dx, dy) in OUTLINE_OFFSETS) {
                drawText(measured, color = outline, topLeft = Offset(dx.dp.toPx(), dy.dp.toPx()))
            }
        },
        fontFamily = PixelFont, fontSize = size.sp,
        maxLines = maxLines, overflow = overflow, textAlign = align,
        onTextLayout = { layout = it },
    )
}

// Quatro lados. As diagonais engrossariam o contorno a ponto de fechar os
// vãos da fonte, que é pixelada e estreita.
private val OUTLINE_OFFSETS = listOf(-1 to 0, 1 to 0, 0 to -1, 0 to 1)

// ------------------------------- painéis -------------------------------

/**
 * O canto dos painéis do Terraria, em pixels da arte (2 dp): quanto cada linha
 * do topo entra. LARGE é o do slot do inventário (Inventory_Back: 4, 2, 1, 1);
 * SMALL, o de botões e etiquetas. O degrau é inteiro, nunca curva suavizada:
 * o canto continua pixelado como o resto.
 */
enum class PixelCorner(internal vararg val steps: Int) {
    NONE(), SMALL(2, 1), LARGE(4, 2, 1, 1);
}

/** O tamanho de um pixel da arte na interface: também a espessura do contorno. */
private val ArtPixel = 2.dp

/**
 * O contorno em degraus de um retângulo, no sentido horário. O canto de cima à
 * esquerda é montado uma vez e espelhado nos outros três.
 */
internal fun steppedOutline(size: Size, unit: Float, corner: PixelCorner): Path {
    val w = size.width
    val h = size.height
    val path = Path()
    val steps = corner.steps
    if (steps.isEmpty() || w < unit * steps[0] * 2 + unit || h < unit * steps.size * 2 + unit) {
        path.addRect(Rect(0f, 0f, w, h))
        return path
    }
    // Da borda esquerda (abaixo do canto) até a de cima (depois dele).
    val n = steps.size
    val corner0 = buildList {
        add(Offset(0f, n * unit))
        for (k in n - 1 downTo 0) {
            add(Offset(steps[k] * unit, (k + 1) * unit))
            add(Offset(steps[k] * unit, k * unit))
        }
    }
    path.moveTo(corner0[0].x, corner0[0].y)
    corner0.forEach { path.lineTo(it.x, it.y) }
    corner0.asReversed().forEach { path.lineTo(w - it.x, it.y) }
    corner0.forEach { path.lineTo(w - it.x, h - it.y) }
    corner0.asReversed().forEach { path.lineTo(it.x, h - it.y) }
    path.close()
    return path
}

/** O mesmo degrau como Shape, para recortar o que vai dentro (efeitos do tema, imagens). */
class PixelShape(private val corner: PixelCorner) : Shape {
    override fun createOutline(size: Size, layoutDirection: LayoutDirection, density: Density): Outline =
        if (corner == PixelCorner.NONE) Outline.Rectangle(Rect(0f, 0f, size.width, size.height))
        else Outline.Generic(steppedOutline(size, with(density) { ArtPixel.toPx() }, corner))
}

/**
 * Painel com bisel. `raised = false` afunda (para campo de entrada, poço de
 * lista), `true` levanta (para cartão, botão).
 *
 * A cor é chapada: é a da paleta, e um degradê por cima a tiraria do tom. Quem
 * dá o volume é a borda de três camadas — contorno, luz e sombra — que é o que
 * o próprio Terraria usa nos painéis dele. Sem sombra projetada embaixo: o
 * painel do jogo não tem, e ela engrossava um lado só.
 *
 * O degrau do canto é recortado (o bisel não vaza) e o contorno segue o degrau:
 * um traço de 2 pixels de arte centrado na borda, metade dele cortada fora.
 */
fun Modifier.pixelPanel(
    fill: Color = Bl.Panel,
    raised: Boolean = true,
    outline: Color = Bl.Outline,
    light: Color = fill.mix(Color.White, 0.14f),
    dark: Color = fill.mix(Bl.Night, 0.45f),
    gradient: Boolean = false,
    corner: PixelCorner = PixelCorner.SMALL,
): Modifier = drawWithCache {
    val b = ArtPixel.toPx()
    val shape = if (corner == PixelCorner.NONE) null else steppedOutline(size, b, corner)
    val stroke = Stroke(b * 2)
    onDrawBehind {
        val w = size.width
        val h = size.height
        val body: DrawScope.() -> Unit = {
            if (shape == null) drawRect(outline)
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
            drawRect(top, Offset(b, b), Size(w - 2 * b, b))
            drawRect(top, Offset(b, b), Size(b, h - 2 * b))
            drawRect(bottom, Offset(b, h - 2 * b), Size(w - 2 * b, b))
            drawRect(bottom, Offset(w - 2 * b, b), Size(b, h - 2 * b))
            if (shape != null) drawPath(shape, outline, style = stroke)
        }
        if (shape == null) body() else clipPath(shape) { body() }
    }
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
 * vale para cartão, moldura e botão sem cada um saber disso. O `corner` é o do
 * painel por baixo, para a borda amarela seguir o mesmo degrau.
 */
fun Modifier.pixelClickable(onClick: () -> Unit): Modifier = pixelClickable(PixelCorner.SMALL, onClick)

fun Modifier.pixelClickable(corner: PixelCorner, onClick: () -> Unit): Modifier = composed {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    drawWithContent {
        drawContent()
        if (pressed) drawPressBorder(corner)
    }.clickable(interaction, indication = null, onClick = onClick)
}

fun DrawScope.drawPressBorder(corner: PixelCorner = PixelCorner.NONE) {
    val b = ArtPixel.toPx()
    if (corner == PixelCorner.NONE) {
        drawRect(Bl.PressedBorder, Offset(b / 2, b / 2), Size(size.width - b, size.height - b),
            style = Stroke(b))
        return
    }
    val shape = steppedOutline(size, b, corner)
    clipPath(shape) { drawPath(shape, Bl.PressedBorder, style = Stroke(b * 2)) }
}

/**
 * Um disco em pixel de arte, pintado num bitmap de `diameter` pixels para ser
 * ampliado sem filtro: contorno, aro (luz em cima à esquerda, sombra embaixo à
 * direita), miolo e um brilho curto no alto do miolo.
 */
fun pixelDisk(diameter: Int, outline: Color, fill: Color, light: Color, dark: Color): ImageBitmap {
    val bitmap = android.graphics.Bitmap.createBitmap(diameter, diameter, android.graphics.Bitmap.Config.ARGB_8888)
    val r = diameter / 2f
    val colors = intArrayOf(outline.toArgb(), light.toArgb(), dark.toArgb(), fill.toArgb(),
        fill.mix(Color.White, 0.22f).toArgb())
    for (y in 0 until diameter) for (x in 0 until diameter) {
        val dx = x + 0.5f - r
        val dy = y + 0.5f - r
        val d = kotlin.math.sqrt(dx * dx + dy * dy)
        val c = when {
            d > r -> 0
            d > r - 1.2f -> colors[0]
            d > r - 2.4f -> if (dx + dy < 0) colors[1] else colors[2]
            d > r - 4.6f && d < r - 3.4f && dy < -r * 0.45f && dx < 0 -> colors[4]
            else -> colors[3]
        }
        bitmap.setPixel(x, y, c)
    }
    return bitmap.asImageBitmap()
}

fun Color.mix(other: Color, t: Float) = Color(
    red + (other.red - red) * t,
    green + (other.green - green) * t,
    blue + (other.blue - blue) * t,
    alpha,
)

/**
 * O recipiente padrão, com o canto grande do slot do inventário.
 */
@Composable
fun PixelCard(
    modifier: Modifier = Modifier,
    fill: Color = Bl.Panel,
    onClick: (() -> Unit)? = null,
    content: @Composable BoxScope.() -> Unit,
) {
    // O efeito do tema (ThemeFx): o balanço vem por fora de tudo; chuva,
    // bolhas e neve, por cima do conteúdo e recortadas pelo canto.
    val fx = LocalThemeFx.current
    val seed = androidx.compose.runtime.currentCompositeKeyHash
    val base = modifier.cardSway(fx, seed).pixelPanel(fill = fill, corner = PixelCorner.LARGE)
    Box(if (onClick != null) base.pixelClickable(PixelCorner.LARGE, onClick) else base) {
        content()
        CardFxOverlay(fx, seed)
    }
}

/**
 * Botão de ação. Enquanto pressionado, afunda o bisel, clareia e ganha a
 * borda amarela.
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
    pressedFill: Color = Bl.ButtonPressed,
) {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    Box(
        modifier
            .pixelPanel(
                fill = if (pressed) pressedFill else fill,
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

    val position by rememberUpdatedState(value)
    LaunchedEffect(Unit) {
        snapshotFlow { position() }.collectLatest {
            show = true
            delay(900)
            show = false
        }
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
                modifier: Modifier = Modifier, label: String? = null) {
    androidx.compose.foundation.layout.Row(
        modifier.fillMaxWidth().pixelPanel(fill = Bl.Select, raised = false).padding(4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(Modifier.then(if (label != null) Modifier.semantics { contentDescription = tr("Anterior: %1\$s", label) } else Modifier)
            .pixelClickable(onPrev).padding(8.dp)) {
            PixelIcon(dev.bunnyloader.R.drawable.ic_seta_esq, 22.dp)
        }
        PixelText(value, size = Ts.Item, align = TextAlign.Center,
            modifier = Modifier.weight(1f).wrapContentWidth(Alignment.CenterHorizontally))
        Box(Modifier.then(if (label != null) Modifier.semantics { contentDescription = tr("Próximo: %1\$s", label) } else Modifier)
            .pixelClickable(onNext).padding(8.dp)) {
            PixelIcon(dev.bunnyloader.R.drawable.ic_seta_dir, 22.dp)
        }
    }
}
