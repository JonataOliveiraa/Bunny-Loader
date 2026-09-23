package dev.bunnyloader.ui

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.drawscope.clipRect
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.bunnyloader.R
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.formatSize

/** A categoria é do pacote; o app só escolhe a cor e um ícone de reserva. */
fun categoryColor(category: String): Color = when (category) {
    "Textura" -> Bl.Dirt2
    "Armas" -> Bl.Stone2
    "Jogabilidade" -> Bl.Grass2
    "Cheat" -> Bl.Dirt0
    "Utilidade" -> Bl.Stone1
    else -> Bl.Stone1
}

fun categoryIcon(category: String): Int = when (category) {
    "Textura", "Jogabilidade" -> R.drawable.ic_tab_inicio
    "Armas" -> R.drawable.ic_tab_pacotes
    "Cheat" -> R.drawable.ic_bunny_head
    else -> R.drawable.ic_config
}

@Composable
fun ModIcon(entry: Catalog.Entry, catalog: Catalog, size: androidx.compose.ui.unit.Dp) {
    val own = entry.iconAsset?.let { catalog.loadBitmap(it) }
    Box(
        Modifier.size(size).pixelPanel(
            fill = Bl.Stone0,
            light = categoryColor(entry.manifest.category),
            dark = Bl.Night,
        ),
        contentAlignment = Alignment.Center,
    ) {
        if (own != null) {
            Image(own, null, filterQuality = FilterQuality.None,
                modifier = Modifier.size(size * 0.7f))
        } else {
            PixelIcon(categoryIcon(entry.manifest.category), size * 0.6f)
        }
    }
}


/**
 * Banner do mod: uma cena de Terraria montada na hora.
 *
 * Nenhum mod traz arte de capa, e um retângulo liso no topo da ficha fica
 * morto. Então desenhamos o mundo com os tiles do próprio jogo: céu estrelado,
 * um morro de grama ao fundo, o chão na frente, plantas, uma árvore — e o
 * coelho atravessando. O sorteio sai do id do mod, então a capa de um mod é
 * sempre a mesma, reconhecível, sem precisar de arquivo nenhum.
 */
@Composable
fun ModBanner(entry: Catalog.Entry, modifier: Modifier = Modifier, animate: Boolean = true) {
    val grassTiles = ImageBitmap.imageResource(R.drawable.tile_grass)
    val stoneTiles = ImageBitmap.imageResource(R.drawable.tile_stone)
    val plants = ImageBitmap.imageResource(R.drawable.spr_grass_tuft)
    val bunny = ImageBitmap.imageResource(R.drawable.spr_bunny)
    val tree = ImageBitmap.imageResource(R.drawable.ic_tab_inicio)

    // O coelho atravessa a cena e volta; `walk` anda de 0 a 2 (ida e volta).
    val clock = rememberInfiniteTransition(label = "cena")
    val walk by if (animate) {
        clock.animateFloat(
            initialValue = 0f, targetValue = 2f,
            animationSpec = infiniteRepeatable(tween(14000, easing = LinearEasing)),
            label = "andar",
        )
    } else {
        remember { mutableFloatStateOf(0.35f) }
    }

    Box(modifier.pixelPanel(fill = Bl.Outline, gradient = false)) {
        Canvas(Modifier.fillMaxSize().padding(2.dp).clipToBounds()) {
            val px = 16.dp.toPx()          // um tile na tela
            val groundTop = size.height - px * 2
            val hillTop = groundTop - px
            var seed = (entry.uid.hashCode().toLong() and 0x7fffffff) or 1L
            fun rnd(n: Int): Int {
                seed = (seed * 1103515245 + 12345) and 0x7fffffff
                return (seed % n).toInt()
            }

            // Céu: o azul do Terraria clareando na direção do chão.
            drawRect(
                Brush.verticalGradient(0f to Bl.Night, 0.45f to Bl.Outline, 1f to Bl.GamePanel),
                size = Size(size.width, hillTop),
            )
            repeat(30) {
                val sx = rnd(size.width.toInt().coerceAtLeast(1)).toFloat()
                val sy = rnd((hillTop * 0.75f).toInt().coerceAtLeast(1)).toFloat()
                val s = 2.dp.toPx()
                drawRect(Color.White.copy(alpha = 0.2f + rnd(55) / 100f), Offset(sx, sy), Size(s, s))
            }

            // Morro ao fundo, escurecido: dá profundidade sem arte nova.
            clipRect(top = hillTop, bottom = groundTop + px) {
                drawTileGround(grassTiles, hillTop, px, seed = 1)
                drawRect(Bl.Night.copy(alpha = 0.45f), Offset(0f, hillTop),
                    Size(size.width, size.height - hillTop))
            }

            // Chão da frente: grama por cima, pedra no subsolo.
            drawTileGround(grassTiles, groundTop, px)
            drawTileGround(stoneTiles, groundTop + px * 2, px)

            // Uma árvore, do lado que o id escolher.
            val treeH = px * 3.2f
            val treeW = treeH * tree.width / tree.height
            val treeX = if (rnd(2) == 0) size.width * 0.1f else size.width * 0.76f
            drawImage(
                tree,
                dstOffset = IntOffset(treeX.toInt(), (groundTop - treeH + 2).toInt()),
                dstSize = IntSize(treeW.toInt(), treeH.toInt()),
                filterQuality = FilterQuality.None,
            )

            // Plantas na linha da grama.
            var x = 2.dp.toPx()
            while (x < size.width - 8.dp.toPx()) {
                val f = rnd(plants.width / 18)
                val w = 18 * 2f
                val h = 22 * 2f
                drawImage(
                    image = plants,
                    srcOffset = IntOffset(f * 18, 0), srcSize = IntSize(18, 22),
                    dstOffset = IntOffset(x.toInt(), (groundTop - h + 4).toInt()),
                    dstSize = IntSize(w.toInt(), h.toInt()),
                    filterQuality = FilterQuality.None,
                )
                x += (20 + rnd(30)).dp.toPx()
            }

            // O coelho atravessa e volta. O quadro do pulo vem do próprio
            // deslocamento, então ele mexe as patas na medida em que anda.
            val scale = 1.6f
            val bw = 48 * scale
            val bh = (bunny.height / 7) * scale
            val span = size.width - bw
            val goingBack = walk > 1f
            val t = if (goingBack) 2f - walk else walk
            val bx = t * span
            val frame = 1 + ((walk * 40).toInt() % 6)
            drawBunny(bunny, frame, bx, groundTop - bh + 4, scale, facingRight = !goingBack)
        }
    }
}

/** Linha da lista: ícone, nome, autor, categoria e tamanho. */
@Composable
fun ModRow(
    entry: Catalog.Entry,
    catalog: Catalog,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    trailing: @Composable (() -> Unit)? = null,
) {
    PixelCard(modifier.fillMaxWidth(), onClick = onClick) {
        Row(
            Modifier.padding(10.dp).fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            ModIcon(entry, catalog, 44.dp)
            Column(Modifier.weight(1f).padding(horizontal = 10.dp)) {
                PixelText(
                    entry.manifest.name,
                    size = Ts.Item, color = Bl.Text,
                    maxLines = 1, overflow = TextOverflow.Ellipsis,
                )
                PixelText(
                    "por ${entry.manifest.author}",
                    size = Ts.Small, color = Bl.TextFaint,
                )
                Row(
                    Modifier.padding(top = 5.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    PixelTag(entry.manifest.category, categoryColor(entry.manifest.category))
                    PixelText(
                        formatSize(entry.sizeBytes),
                        size = Ts.Small, color = Bl.TextFaint,
                        modifier = Modifier.padding(start = 12.dp),
                    )
                }
            }
            trailing?.invoke()
        }
    }
}

/** Cabeçalho de seção: "Em destaque", "Populares". */
@Composable
fun SectionTitle(text: String, modifier: Modifier = Modifier) {
    Row(
        modifier.fillMaxWidth().padding(top = 18.dp, bottom = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.Start,
    ) {
        Box(Modifier.width(3.dp).height(16.dp).pixelPanel(
            fill = Bl.Grass2, light = Bl.Grass4, dark = Bl.Grass0,
        ))
        PixelText(
            text,
            size = Ts.Head, color = Bl.Text,
            modifier = Modifier.padding(start = 8.dp),
        )
    }
}
