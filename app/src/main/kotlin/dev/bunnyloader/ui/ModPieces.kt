package dev.bunnyloader.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.formatSize

/**
 * A categoria é do pacote; o app só escolhe a cor e um ícone de reserva. As
 * cores ficam na família azul da paleta, só variando o tom: a etiqueta separa
 * uma categoria da outra sem trazer uma segunda paleta para a tela.
 */
fun categoryColor(category: String): Color = when (category) {
    "Textura" -> Bl.TitleFill
    "Armas" -> Color(0xFF5B4FA8)
    "Jogabilidade" -> Color(0xFF3F7A9E)
    "Cheat" -> Color(0xFF7A4A9E)
    else -> Bl.Select
}

fun categoryIcon(category: String): Int = when (category) {
    "Textura", "Jogabilidade" -> R.drawable.ic_tab_inicio
    "Armas" -> R.drawable.ic_tab_pacotes
    "Cheat" -> R.drawable.ic_bunny_head
    else -> R.drawable.ic_config
}

@Composable
fun ModIcon(entry: Catalog.Entry, catalog: Catalog, size: androidx.compose.ui.unit.Dp) {
    // Lido uma vez por ícone, não a cada recomposição: agora pode ser arquivo.
    val own = remember(entry.iconAsset) { entry.iconAsset?.let { catalog.loadBitmap(it) } }
    Box(Modifier.size(size).framePanel(), contentAlignment = Alignment.Center) {
        if (own != null) {
            Image(own, null, filterQuality = FilterQuality.None,
                modifier = Modifier.size(size * 0.7f))
        } else {
            PixelIcon(categoryIcon(entry.manifest.category), size * 0.6f)
        }
    }
}


/**
 * Capa do mod: a `banner.png` do pacote, montada com texturas do jogo
 * (tools/mod-art.py para os nossos). Cortada para caber, nunca esticada, e sem
 * filtro — o pixel da arte continua quadrado.
 *
 * Pacote sem capa (um importado, por exemplo) mostra um trecho do cenário do
 * launcher, escolhido pelo id: a ficha de um mod é sempre a mesma, e é
 * imagem do jogo, não um retângulo liso.
 */
@Composable
fun ModBanner(entry: Catalog.Entry, catalog: Catalog, modifier: Modifier = Modifier) {
    val own = remember(entry.bannerAsset) { entry.bannerAsset?.let { catalog.loadBitmap(it) } }
    val fallback = ImageBitmap.imageResource(
        FALLBACK_BANNERS[(entry.uid.hashCode() and 0x7fffffff) % FALLBACK_BANNERS.size]
    )
    Box(modifier.framePanel().padding(2.dp).clipToBounds()) {
        Image(
            bitmap = own ?: fallback,
            contentDescription = null,
            filterQuality = FilterQuality.None,
            contentScale = ContentScale.Crop,
            alignment = if (own != null) Alignment.Center else Alignment.BottomCenter,
            modifier = Modifier.fillMaxSize(),
        )
    }
}

private val FALLBACK_BANNERS = listOf(
    R.drawable.bg_forest, R.drawable.bg_ocean, R.drawable.bg_lake, R.drawable.bg_snow,
)

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

/**
 * Cabeçalho de seção: "Em destaque", "Populares". Uma placa do tamanho do
 * texto, não uma faixa de ponta a ponta — com o cenário atrás, uma faixa
 * inteira cortaria a tela em fatias.
 */
@Composable
fun SectionTitle(text: String, modifier: Modifier = Modifier) {
    Box(modifier.fillMaxWidth().padding(top = 18.dp, bottom = 8.dp)) {
        Box(
            Modifier.pixelShadow(3.dp, 4.dp).pixelPanel(fill = Bl.TitleFill)
                .padding(horizontal = 12.dp, vertical = 5.dp),
        ) {
            PixelText(text, size = Ts.Head, color = Bl.Text)
        }
    }
}
