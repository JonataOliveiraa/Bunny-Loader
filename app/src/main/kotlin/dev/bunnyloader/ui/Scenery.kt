package dev.bunnyloader.ui

import androidx.compose.foundation.Canvas
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.lerp
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import dev.bunnyloader.R
import java.util.TimeZone
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.max
import kotlin.math.roundToInt
import kotlin.math.sin
import kotlin.random.Random

/**
 * O fundo do launcher: um cenário do Terraria em camadas, com céu, sol e lua
 * seguindo o relógio do celular.
 *
 * Cada camada anda numa fração do movimento da câmera (`parallax`): a montanha
 * lá no fundo quase não sai do lugar, a floresta da frente anda mais. A câmera
 * desliza devagar sozinha e dá um passo a cada troca de aba.
 *
 * O custo fica baixo por construção:
 *  - é um Canvas só, atrás da interface; o redesenho dele não recompõe nada e
 *    não repinta os cartões por cima;
 *  - o relógio compartilhado bate a 20 quadros por segundo — o cenário anda
 *    poucos pixels por segundo, 60 seria desperdício) e PARA quando o launcher
 *    sai da tela, então não disputa nada com o jogo;
 *  - as texturas são as do jogo em tamanho original, ampliadas por um fator
 *    inteiro na hora de desenhar: nenhum bitmap grande em memória, e o pixel
 *    continua quadrado.
 */
enum class Biome(val label: String, val layers: List<SceneLayer>) {
    FOREST("Floresta", listOf(
        SceneLayer(R.drawable.bg_mountains, 0.10f, 0.20f),
        SceneLayer(R.drawable.bg_hills, 0.20f, 0.27f),
        SceneLayer(R.drawable.bg_forest, 0.40f, 0.36f),
    )),
    OCEAN("Oceano", listOf(
        SceneLayer(R.drawable.bg_ocean, 0.15f, 0.30f),
    )),
    LAKE("Lago", listOf(
        SceneLayer(R.drawable.bg_mountains, 0.10f, 0.18f),
        SceneLayer(R.drawable.bg_lake, 0.35f, 0.33f),
    )),
    SNOW("Neve", listOf(
        SceneLayer(R.drawable.bg_snow_mountains, 0.10f, 0.17f),
        SceneLayer(R.drawable.bg_snow, 0.40f, 0.31f),
    )),
}

/** Uma camada: quanto ela anda com a câmera, e onde fica o topo dela (fração da altura). */
class SceneLayer(val res: Int, val parallax: Float, val top: Float)

/** Pixels da textura por segundo que a câmera anda sozinha. */
private const val DRIFT = 6f

/** Onde sol e lua nascem e se põem, em fração da altura: atrás das camadas. */
private const val HORIZON = 0.40f

// O dia do Terraria: o sol nasce 4:30 e se põe 19:30.
private const val SUNRISE = 270f
private const val SUNSET = 1170f

/**
 * O céu ao longo do dia, em minutos: topo, pé do céu, e a luz que tinge as
 * camadas e as nuvens (multiplicada; branco = cor original).
 */
private class SkyKey(val minute: Float, top: Long, bottom: Long, light: Long) {
    val top = Color(top or 0xFF000000)
    val bottom = Color(bottom or 0xFF000000)
    val light = Color(light or 0xFF000000)
}

private val SKY = listOf(
    SkyKey(0f, 0x070A1C, 0x1C2550, 0x4A5482),
    SkyKey(SUNRISE, 0x070A1C, 0x1C2550, 0x4A5482),
    SkyKey(330f, 0x34407E, 0xE89A6C, 0xC8A6A0),
    SkyKey(420f, 0x4A7FE0, 0x9CC8FF, 0xFFFFFF),
    SkyKey(1020f, 0x4A7FE0, 0x9CC8FF, 0xFFFFFF),
    SkyKey(1110f, 0x39397E, 0xEE7A50, 0xDDA08A),
    SkyKey(1185f, 0x070A1C, 0x1C2550, 0x4A5482),
    SkyKey(1440f, 0x070A1C, 0x1C2550, 0x4A5482),
)

private class Cloud(val sprite: Int, val x: Float, val y: Float, val speed: Float, val parallax: Float)

private class Star(val x: Float, val y: Float, val phase: Float, val big: Boolean)

private val CLOUD_SPRITES = listOf(
    R.drawable.bg_cloud_0, R.drawable.bg_cloud_1, R.drawable.bg_cloud_2, R.drawable.bg_cloud_3,
    R.drawable.bg_cloud_13, R.drawable.bg_cloud_21, R.drawable.bg_cloud_23,
)

@Composable
fun Scenery(biome: Biome, pan: () -> Float, time: () -> Float, modifier: Modifier = Modifier, effects: Boolean = false) {
    val layers = biome.layers.map { ImageBitmap.imageResource(it.res) }
    // O pé de cada camada, para cobrir o que sobra abaixo dela na tela.
    val floors = remember(biome) { layers.map { it.bottomColor() } }
    val cloudArt = CLOUD_SPRITES.map { ImageBitmap.imageResource(it) }
    val sun = ImageBitmap.imageResource(R.drawable.bg_sun)
    val moon = ImageBitmap.imageResource(R.drawable.bg_moon)
    val starArt = ImageBitmap.imageResource(R.drawable.bg_star)

    val clouds = remember {
        val r = Random(7)
        CLOUD_SPRITES.indices.map {
            Cloud(it, r.nextFloat(), 0.03f + r.nextFloat() * 0.25f,
                2f + r.nextFloat() * 5f, 0.04f + r.nextFloat() * 0.08f)
        }
    }
    val stars = remember {
        val r = Random(11)
        List(70) { Star(r.nextFloat(), r.nextFloat() * 0.45f, r.nextFloat() * 6.28f, it < 6) }
    }
    val moonFrame = remember { moonPhaseFrame(System.currentTimeMillis()) }
    Canvas(modifier) {
        val t = time()
        val cam = t * DRIFT + pan()
        val s = max(1, (size.height / 800f).roundToInt())
        val minute = minuteOfDay()
        val sky = skyAt(minute)
        val tint = if (sky.light.isWhite()) null else ColorFilter.tint(sky.light, BlendMode.Modulate)

        drawRect(Brush.verticalGradient(
            0f to sky.top, 0.5f to sky.bottom,
            startY = 0f, endY = size.height,
        ))

        // Estrelas: somem com o dia, piscam devagar.
        val night = ((0.75f - sky.light.red) / 0.45f).coerceIn(0f, 1f)
        if (night > 0f) {
            for (st in stars) {
                val a = night * (0.55f + 0.45f * sin(t * 1.3f + st.phase))
                val x = st.x * size.width
                val y = st.y * size.height
                if (st.big) {
                    drawSprite(starArt, x, y, s, alpha = a)
                } else {
                    val d = max(2, s).toFloat()
                    drawRect(Color.White.copy(alpha = a), Offset(x, y), Size(d, d))
                }
            }
        }

        // Sol de dia, lua de noite, num arco que termina atrás das camadas.
        val horizon = size.height * HORIZON
        val arc = horizon - size.height * 0.07f
        if (minute in SUNRISE..SUNSET) {
            val p = (minute - SUNRISE) / (SUNSET - SUNRISE)
            drawBody(sun, 0, sun.height, p, horizon, arc, s)
        } else {
            val p = ((minute - SUNSET + 1440f) % 1440f) / (1440f - SUNSET + SUNRISE)
            drawBody(moon, moonFrame * moon.width, moon.width, p, horizon, arc, s)
        }

        for (c in clouds) {
            val art = cloudArt[c.sprite]
            val w = art.width * s
            val span = size.width + w
            val pos = c.x * span + (t * c.speed - cam * c.parallax) * s
            drawSprite(art, pos.mod(span) - w, c.y * size.height, s, tint)
        }

        biome.layers.forEachIndexed { i, layer ->
            drawLayer(layers[i], floors[i], layer, cam, s, sky.light, tint)
        }

        if (effects) {
            // A noite escurece as partículas, mas não a ponto de sumirem; e o
            // pixel delas tem 2 de mínimo: com a tela deitada (s = 1), chuva
            // de 1 pixel não se vê.
            val light = sky.light.red.coerceIn(0.6f, 1f)
            val fx = max(2, s)
            when (biome) {
                Biome.FOREST -> drawLeaves(t, fx, light)
                Biome.OCEAN -> drawRain(t, fx, light)
                Biome.LAKE -> drawBubbles(t, fx, light, night)
                Biome.SNOW -> drawSnow(t, fx, light)
            }
        }
    }
}

// ------------------------------ efeitos do tema ------------------------------
//
// Cada partícula é uma função do tempo e de números fixos dela (sorteados uma
// vez): nada se guarda de um quadro para o outro, e o custo é o de desenhar.

private class Particle(val x: Float, val y: Float, val speed: Float, val phase: Float, val size: Float)

private fun particles(count: Int, seed: Int) = Random(seed).let { r ->
    List(count) { Particle(r.nextFloat(), r.nextFloat(), r.nextFloat(), r.nextFloat() * 6.28f, r.nextFloat()) }
}

private val RAIN = particles(150, 21)
private val SNOW = particles(110, 22)
private val LEAVES = particles(22, 23)
private val BUBBLES = particles(36, 24)
private val FIREFLIES = particles(26, 25)

/** Chuva do Terraria: riscos claros e inclinados, caindo rápido. */
private fun DrawScope.drawRain(t: Float, s: Int, light: Float) {
    val len = 10f * s
    val dx = 3f * s
    val color = Color(0xFFB9D2F0).copy(alpha = 0.7f * light)
    for (p in RAIN) {
        val speed = size.height * (1.1f + p.speed * 0.6f)
        val y = (p.y * (size.height + len) + t * speed).mod(size.height + len) - len
        val x = (p.x * (size.width + 80f) - (y / size.height) * 60f).mod(size.width + 80f) - 40f
        drawLine(color, Offset(x, y), Offset(x - dx, y + len), strokeWidth = s.toFloat())
    }
    // Um véu cinza: o céu de chuva é mais fechado.
    drawRect(Color(0xFF26324A).copy(alpha = 0.18f))
}

/** Flocos de dois tamanhos, balançando enquanto caem. */
private fun DrawScope.drawSnow(t: Float, s: Int, light: Float) {
    val color = Color.White.copy(alpha = 0.85f * light)
    for (p in SNOW) {
        val d = (if (p.size > 0.7f) 3 else if (p.size > 0.3f) 2 else 1) * s.toFloat()
        val speed = 22f * s * (0.6f + p.speed)
        val y = (p.y * size.height + t * speed).mod(size.height + d) - d
        val x = (p.x * size.width + sin(t * 0.9f + p.phase) * 14f * s + t * 4f * s).mod(size.width)
        drawRect(color, Offset(x, y), Size(d, d))
    }
}

/**
 * Folhas caindo com o vento: um losango de pixel que, ao girar, fica de lado
 * (uma linha fina) e volta. Verdes, e umas poucas já secas.
 */
private fun DrawScope.drawLeaves(t: Float, s: Int, light: Float) {
    val colors = listOf(Color(0xFF4F9B3A), Color(0xFF6DB847), Color(0xFF3C7A2E), Color(0xFFC98A2E))
    for ((i, p) in LEAVES.withIndex()) {
        val px = (if (p.size > 0.5f) 3 else 2) * s / 2f
        val fall = 16f * s * (0.5f + p.speed)
        val y = (p.y * size.height + t * fall).mod(size.height + 8 * px) - 4 * px
        val wind = t * 10f * s * (0.6f + p.speed)
        val x = (p.x * size.width + wind + sin(t * 1.4f + p.phase) * 18f * s).mod(size.width + 8 * px) - 4 * px
        val base = colors[i % colors.size]
        val c = Color(base.red * light, base.green * light, base.blue * light)
        // De frente: losango (1, 3, 1 pixels). De lado: a coluna do meio só.
        val flat = abs(sin(t * 2.2f + p.phase)) > 0.35f
        drawRect(c, Offset(x + px, y), Size(px, px * 3))
        if (flat) drawRect(c, Offset(x, y + px), Size(px * 3, px))
    }
}

/** Bolhas subindo do lago e, à noite, vaga-lumes piscando sobre ele. */
private fun DrawScope.drawBubbles(t: Float, s: Int, light: Float, night: Float) {
    val top = size.height * 0.45f
    for (p in BUBBLES) {
        val r = (2f + p.size * 3f) * s
        val span = size.height - top
        val q = ((p.y * span + t * 18f * s * (0.5f + p.speed)) % span) / span
        val y = size.height - q * span
        val x = p.x * size.width + sin(t * 1.3f + p.phase) * 5f * s
        val a = 0.7f * sin(q * PI.toFloat()) * light
        drawCircle(Color(0xFFBDF4FF).copy(alpha = a), r, Offset(x, y), style = Stroke(s.toFloat()))
        drawRect(Color.White.copy(alpha = a), Offset(x - r * 0.5f, y - r * 0.5f), Size(s.toFloat(), s.toFloat()))
    }
    if (night <= 0f) return
    for (p in FIREFLIES) {
        val x = (p.x * size.width + sin(t * 0.5f + p.phase) * 30f * s).mod(size.width)
        val y = size.height * (0.35f + 0.45f * p.y) + sin(t * 0.7f + p.phase * 2) * 12f * s
        val a = night * (0.5f + 0.5f * sin(t * (1.5f + p.speed) + p.phase)).coerceIn(0f, 1f)
        drawRect(Color(0xFFD9F27A).copy(alpha = a), Offset(x, y), Size(2f * s, 2f * s))
        drawRect(Color(0xFFD9F27A).copy(alpha = a * 0.25f), Offset(x - s, y - s), Size(4f * s, 4f * s))
    }
}

private fun DrawScope.drawLayer(img: ImageBitmap, floor: Color, layer: SceneLayer, cam: Float,
                                s: Int, light: Color, tint: ColorFilter?) {
    val w = img.width * s
    val h = img.height * s
    val y = (layer.top * size.height).roundToInt()
    var x = -(cam * layer.parallax * s).mod(w.toFloat()).toInt()
    while (x < size.width) {
        drawImage(
            img, srcOffset = IntOffset.Zero, srcSize = IntSize(img.width, img.height),
            dstOffset = IntOffset(x, y), dstSize = IntSize(w, h),
            filterQuality = FilterQuality.None, colorFilter = tint,
        )
        x += w
    }
    if (y + h < size.height) {
        drawRect(floor.times(light), Offset(0f, (y + h).toFloat()),
            Size(size.width, size.height - y - h))
    }
}

/** Sol ou lua: `p` de 0 (nascendo, à esquerda) a 1 (se pondo, à direita). */
private fun DrawScope.drawBody(img: ImageBitmap, srcY: Int, frame: Int, p: Float, horizon: Float,
                               arc: Float, s: Int) {
    val w = img.width * s
    val h = frame * s
    val cx = size.width * (-0.08f + 1.16f * p)
    val cy = horizon - sin(PI.toFloat() * p) * arc
    drawImage(
        img, srcOffset = IntOffset(0, srcY), srcSize = IntSize(img.width, frame),
        dstOffset = IntOffset((cx - w / 2).toInt(), (cy - h / 2).toInt()), dstSize = IntSize(w, h),
        filterQuality = FilterQuality.None,
    )
}

private fun DrawScope.drawSprite(img: ImageBitmap, x: Float, y: Float, s: Int,
                                 tint: ColorFilter? = null, alpha: Float = 1f) {
    drawImage(
        img, srcOffset = IntOffset.Zero, srcSize = IntSize(img.width, img.height),
        dstOffset = IntOffset(x.toInt(), y.toInt()), dstSize = IntSize(img.width * s, img.height * s),
        alpha = alpha, filterQuality = FilterQuality.None, colorFilter = tint,
    )
}

private class Sky(val top: Color, val bottom: Color, val light: Color)

private fun skyAt(minute: Float): Sky {
    val i = SKY.indexOfLast { it.minute <= minute }.coerceIn(0, SKY.size - 2)
    val a = SKY[i]
    val b = SKY[i + 1]
    val f = ((minute - a.minute) / (b.minute - a.minute)).coerceIn(0f, 1f)
    return Sky(lerp(a.top, b.top, f), lerp(a.bottom, b.bottom, f), lerp(a.light, b.light, f))
}

/** Minuto do dia na hora local, com fração: o sol anda sem degrau. */
private fun minuteOfDay(): Float {
    val now = System.currentTimeMillis()
    val local = now + TimeZone.getDefault().getOffset(now)
    return (local.mod(86_400_000L)) / 60_000f
}

/**
 * A fase da lua de verdade, no quadro de Moon_0 (0 = cheia, 4 = nova, na
 * ordem em que o jogo avança). Conta ciclos sinódicos desde a lua nova de
 * 6/1/2000 18:14 UTC.
 */
private fun moonPhaseFrame(nowMs: Long): Int {
    val synodic = 29.530588853
    val days = (nowMs - 947_182_440_000L) / 86_400_000.0
    val age = (days.mod(synodic)) / synodic
    return ((age * 8).roundToInt() + 4) % 8
}

private fun ImageBitmap.bottomColor(): Color =
    Color(asAndroidBitmap().getPixel(0, height - 1))

private fun Color.isWhite() = red > 0.99f && green > 0.99f && blue > 0.99f

private fun Color.times(o: Color) = Color(red * o.red, green * o.green, blue * o.blue, alpha)
