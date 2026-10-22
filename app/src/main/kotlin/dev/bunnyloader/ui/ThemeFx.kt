package dev.bunnyloader.ui

import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.State
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.TransformOrigin
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.sin

/**
 * O que cada tema faz com os cartões (o fundo de cada tema fica em Scenery).
 *
 * Cada tema tem a sua personalidade, e nenhum é só cor:
 *  - Floresta: o cartão balança com o vento, em rajadas;
 *  - Oceano (chuva): uma faixa de brilho molhado escorre pelo cartão, com gotas;
 *  - Lago: bolhas pequenas, as do Terraria, sobem de dentro do cartão;
 *  - Neve: um montinho de neve em cima do cartão e um brilho de gelo que pisca;
 *  - Halloween: teia no canto de alguns cartões, com a aranha descendo no fio;
 *  - Cogumelos: esporos azuis subindo; Deserto: areia passando rente ao pé;
 *  - Sagrado: cruzinhas coloridas piscando.
 *
 * Tudo é função do tempo ([ThemeFx.time]) e do lugar do cartão na composição:
 * nada é guardado por partícula, nada recompõe — só redesenha.
 */
enum class CardFx { NONE, SWAY, WET, BUBBLES, FROST, WEB, SPORES, SAND, SPARKLE }

class ThemeFx(val card: CardFx, val time: State<Float>, val paused: () -> Boolean = { false })

private val CardShape = PixelShape(PixelCorner.LARGE)

val LocalThemeFx = staticCompositionLocalOf { ThemeFx(CardFx.NONE, mutableFloatStateOf(0f)) }

/** Durante gestos, o orçamento de desenho fica disponível para a lista. */
@Composable
internal fun PauseListAnimations(isBusy: () -> Boolean, content: @Composable () -> Unit) {
    val parent = LocalThemeFx.current
    val busy by rememberUpdatedState(isBusy)
    val fx = remember(parent) { ThemeFx(parent.card, parent.time) { parent.paused() || busy() } }
    CompositionLocalProvider(LocalThemeFx provides fx, content = content)
}

fun Biome.cardFx(): CardFx = when (this) {
    Biome.FOREST -> CardFx.SWAY
    Biome.OCEAN -> CardFx.WET
    Biome.LAKE -> CardFx.BUBBLES
    Biome.SNOW -> CardFx.FROST
    Biome.HALLOWEEN -> CardFx.WEB
    Biome.MUSHROOM -> CardFx.SPORES
    Biome.DESERT -> CardFx.SAND
    Biome.HALLOW -> CardFx.SPARKLE
}

/** Um número de 0 a 1 que não muda para o mesmo cartão. */
private fun unit(seed: Int, salt: Int): Float {
    var h = seed * 31 + salt * 0x9E3779B1.toInt()
    h = h xor (h ushr 16)
    h *= 0x45d9f3b
    h = h xor (h ushr 16)
    return (h and 0xFFFF) / 65535f
}

/**
 * O balanço do tema Floresta. Fica numa camada (graphicsLayer) e só muda as
 * propriedades dela a cada quadro: o conteúdo do cartão não é redesenhado.
 */
fun Modifier.cardSway(fx: ThemeFx, seed: Int): Modifier =
    if (fx.card != CardFx.SWAY) this else graphicsLayer {
        if (fx.paused()) {
            rotationZ = 0f
            translationX = 0f
            return@graphicsLayer
        }
        // Preso no meio de cima, como uma placa pendurada: a rajada vem e vai.
        val t = fx.time.value
        val phase = unit(seed, 1) * 6.28f
        val gust = 0.55f + 0.45f * sin(t * 0.37f + unit(seed, 2))
        transformOrigin = TransformOrigin(0.5f, 0f)
        rotationZ = sin(t * 1.25f + phase) * 0.75f * gust
        translationX = sin(t * 0.8f + phase) * 1.6.dp.toPx() * gust
    }

/**
 * Chuva, bolhas e neve por cima do cartão, num nó só delas. Era um
 * drawWithContent no próprio cartão: o relógio do tema invalidava o desenho
 * dele, e cada quadro regravava o cartão inteiro (texto, ícone, moldura) —
 * a lista travava no arraste. Aqui o quadro novo redesenha só este nó.
 */
@Composable
fun BoxScope.CardFxOverlay(fx: ThemeFx, seed: Int) {
    val web = if (fx.card == CardFx.WEB) ImageBitmap.imageResource(R.drawable.spr_web) else null
    val draw: (DrawScope.(Float) -> Unit) = when (fx.card) {
        CardFx.WET -> { t -> drawWet(t, seed) }
        CardFx.BUBBLES -> { t -> drawBubbles(t, seed) }
        CardFx.FROST -> { t -> drawFrost(t, seed) }
        CardFx.WEB -> if (unit(seed, 4) < 0.4f) { t -> drawWeb(t, seed, web!!) } else return
        CardFx.SPORES -> { t -> drawCardSpores(t, seed) }
        CardFx.SAND -> { t -> drawCardSand(t, seed) }
        CardFx.SPARKLE -> { t -> drawSparkles(t, seed) }
        else -> return
    }
    Spacer(Modifier.matchParentSize().clip(CardShape).drawBehind { if (!fx.paused()) draw(fx.time.value) })
}

/** Uma faixa clara que desce devagar, e gotas escorrendo. */
private fun DrawScope.drawWet(t: Float, seed: Int) {
    val h = size.height
    val w = size.width
    val band = h * 0.55f
    val p = ((t * 0.18f + unit(seed, 3)) % 1f) * (h + band * 2) - band
    drawRect(
        Brush.verticalGradient(
            listOf(Color.Transparent, Color(0x38BFE3FF), Color(0x14BFE3FF), Color.Transparent),
            startY = p, endY = p + band,
        ),
        Offset(2.dp.toPx(), 2.dp.toPx()), Size(w - 4.dp.toPx(), h - 4.dp.toPx()),
    )
    val px = 2.dp.toPx()
    for (k in 0 until 3) {
        val x = w * (0.1f + 0.8f * unit(seed, 10 + k))
        val speed = 0.12f + 0.1f * unit(seed, 20 + k)
        val q = (t * speed + unit(seed, 30 + k)) % 1f
        val y = q * (h + 12.dp.toPx()) - 6.dp.toPx()
        val a = 0.7f * sin(q * PI.toFloat())
        drawRect(Color(0xFFCDE9FF).copy(alpha = a), Offset(x, y), Size(px, px * 3))
    }
}

/** Bolhas do Terraria: um aro claro e um pixel de brilho, subindo e sumindo. */
private fun DrawScope.drawBubbles(t: Float, seed: Int) {
    val w = size.width
    val h = size.height
    for (k in 0 until 3) {
        val period = 3.5f + 2f * unit(seed, 40 + k)
        val q = ((t + unit(seed, 50 + k) * period) / period) % 1f
        val r = (2.5f + 2f * unit(seed, 60 + k)).dp.toPx()
        val x = w * (0.08f + 0.84f * unit(seed, 70 + k)) + sin(q * 9f + k) * 3.dp.toPx()
        val y = h - q * (h + r * 2) + r
        val a = 0.75f * sin(q * PI.toFloat())
        drawCircle(Color(0xFFBDF4FF).copy(alpha = a), r, Offset(x, y), style = Stroke(1.5.dp.toPx()))
        drawRect(Color.White.copy(alpha = a), Offset(x - r * 0.45f, y - r * 0.55f), Size(r * 0.4f, r * 0.4f))
    }
}

/** Neve acumulada em cima, em degraus de pixel, e um brilho de gelo que pisca. */
private fun DrawScope.drawFrost(t: Float, seed: Int) {
    val w = size.width
    val px = 2.dp.toPx()
    var x = 0f
    var i = 0
    while (x < w) {
        val step = px * (2 + (unit(seed, 80 + i) * 4).toInt())
        val height = px * (1 + (unit(seed, 120 + i) * 2.4f).toInt())
        drawRect(Color(0xFFF2F8FF), Offset(x, 0f), Size(minOf(step, w - x), height))
        drawRect(Color(0xFFC9DDF5), Offset(x, height), Size(minOf(step, w - x), px * 0.5f))
        x += step
        i++
    }
    // O brilho: um pixel em cruz que acende e apaga num ponto do cartão.
    val period = 2.8f + unit(seed, 90)
    val q = ((t + unit(seed, 91) * period) / period) % 1f
    val a = (1f - abs(q * 2f - 1f)).let { it * it }
    val sx = w * (0.15f + 0.7f * unit(seed, (t / period).toInt() + 92))
    val sy = size.height * (0.3f + 0.5f * unit(seed, (t / period).toInt() + 93))
    val c = Color.White.copy(alpha = a * 0.9f)
    drawRect(c, Offset(sx, sy - px), Size(px, px * 3))
    drawRect(c, Offset(sx - px, sy), Size(px * 3, px))
}

/**
 * Teia no canto de cima à direita (spr_web, 24x24, um pixel da arte por 2 dp)
 * e uma aranha que desce e sobe pelo fio. Só em parte dos cartões: teia em
 * todos vira papel de parede.
 */
private fun DrawScope.drawWeb(t: Float, seed: Int, web: ImageBitmap) {
    val px = 2.dp.toPx()
    val w = web.width * px
    drawImage(
        web, srcOffset = IntOffset.Zero, srcSize = IntSize(web.width, web.height),
        dstOffset = IntOffset((size.width - w).toInt(), 0), dstSize = IntSize(w.toInt(), (web.height * px).toInt()),
        alpha = 0.55f, filterQuality = FilterQuality.None,
    )
    val x = size.width - w * 0.55f
    val drop = (0.5f + 0.5f * sin(t * 0.7f + unit(seed, 6) * 6.28f)) * minOf(size.height * 0.45f, 26.dp.toPx())
    val y = w * 0.45f + drop
    val thread = Color(0x8CE8E8F0)
    drawRect(thread, Offset(x, w * 0.45f), Size(px / 2, drop))
    // A aranha: corpo de 3x2 pixels e as pernas, pretas e roxas.
    val body = Color(0xFF14081C)
    drawRect(body, Offset(x - px, y), Size(px * 3, px * 2))
    drawRect(Color(0xFF5A2A6A), Offset(x, y), Size(px, px))
    for (side in listOf(-1f, 1f)) {
        val lx = if (side < 0) x - px * 2 else x + px * 2
        drawRect(body, Offset(lx, y - px / 2), Size(px, px / 2))
        drawRect(body, Offset(lx, y + px * 2), Size(px, px / 2))
    }
}

/** Esporos azuis subindo de dentro do cartão, como os do bioma. */
private fun DrawScope.drawCardSpores(t: Float, seed: Int) {
    val px = 2.dp.toPx()
    for (k in 0 until 4) {
        val period = 4f + 3f * unit(seed, 130 + k)
        val q = ((t + unit(seed, 140 + k) * period) / period) % 1f
        val x = size.width * (0.06f + 0.88f * unit(seed, 150 + k)) + sin(q * 7f + k) * 4.dp.toPx()
        val y = size.height * (1f - q)
        val a = 0.8f * sin(q * PI.toFloat())
        drawRect(Color(0xFF7FE0FF).copy(alpha = a * 0.3f), Offset(x - px, y - px), Size(px * 3, px * 3))
        drawRect(Color(0xFFBFF4FF).copy(alpha = a), Offset(x, y), Size(px, px))
    }
}

/** Grãos de areia correndo rente ao pé do cartão. */
private fun DrawScope.drawCardSand(t: Float, seed: Int) {
    val px = 2.dp.toPx()
    for (k in 0 until 6) {
        val speed = 0.25f + 0.25f * unit(seed, 160 + k)
        val q = (t * speed + unit(seed, 170 + k)) % 1f
        val x = q * (size.width + 20.dp.toPx()) - 10.dp.toPx()
        val y = size.height - px * (2 + (unit(seed, 180 + k) * 8).toInt())
        drawRect(Color(0xFFF0D8A0).copy(alpha = 0.55f * sin(q * PI.toFloat())), Offset(x, y), Size(px * 2, px / 2 + px / 2))
    }
}

/** Cruzinhas do Sagrado, cada uma acendendo e apagando no seu ritmo. */
private fun DrawScope.drawSparkles(t: Float, seed: Int) {
    val px = 2.dp.toPx()
    val colors = listOf(Color(0xFFFF8AD8), Color(0xFF8AF0FF), Color(0xFFFFF08A))
    for (k in 0 until 3) {
        val period = 2.2f + unit(seed, 190 + k) * 1.6f
        val cycle = ((t + unit(seed, 200 + k) * period) / period)
        val q = cycle % 1f
        val a = (1f - abs(q * 2f - 1f)).let { it * it }
        val n = cycle.toInt()
        val x = size.width * (0.08f + 0.84f * unit(seed, 210 + k * 7 + n))
        val y = size.height * (0.15f + 0.7f * unit(seed, 220 + k * 7 + n))
        val c = colors[k].copy(alpha = a * 0.9f)
        drawRect(c, Offset(x, y - px), Size(px, px * 3))
        drawRect(c, Offset(x - px, y), Size(px * 3, px))
    }
}
