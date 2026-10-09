package dev.bunnyloader.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.IconAnimation
import dev.bunnyloader.mods.formatSize
import kotlinx.coroutines.withContext

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

/**
 * Pede arquivos da vitrine de um mod online (Shell.requestRemoteFiles). Quem
 * desenha ícone e capa pede o que falta na hora em que aparece na tela.
 */
val LocalRemoteFiles = androidx.compose.runtime.staticCompositionLocalOf<(String, List<String>) -> Unit> {
    { _, _ -> }
}

val LocalRemoteMedia = androidx.compose.runtime.staticCompositionLocalOf<(Catalog.Entry) -> Catalog.Entry> {
    { it }
}

@Composable
fun ModIcon(entry: Catalog.Entry, catalog: Catalog, size: androidx.compose.ui.unit.Dp) {
    val media = LocalRemoteMedia.current(entry)
    val remote = media.remote
    if (remote != null && media.iconAsset == null) {
        val request = LocalRemoteFiles.current
        LaunchedEffect(entry.uid, remote.download.sha256) { request(entry.uid, listOf(remote.iconFile)) }
    }
    val path = media.iconAsset
    val animated = path != null && path.endsWith(".gif", ignoreCase = true)
    val revision = "${remote?.download?.sha256 ?: entry.manifest.version}:${media.mediaRevision}"
    val gif = if (animated) rememberIconAnimation(catalog, path!!, revision) else null
    if (animated && gif?.done == true && gif.animation == null && remote != null && Catalog.ICON in remote.files) {
        val request = LocalRemoteFiles.current
        LaunchedEffect(entry.uid, remote.download.sha256) { request(entry.uid, listOf(Catalog.ICON)) }
    }
    // Lido uma vez por ícone, não a cada recomposição: agora pode ser arquivo.
    // Um icon.gif que não dá para animar (grande demais, quebrado) cede ao
    // icon.png ao lado; sem ele, o primeiro quadro, se o Android ler.
    val still = produceState<ImageBitmap?>(
        null,
        path, gif?.done, revision,
    ) {
        value = when {
            path == null || (animated && gif?.done != true) || gif?.animation != null -> null
            else -> withContext(MediaDispatcher) {
                if (animated) catalog.loadIconBitmap(path.dropLast(4) + ".png") ?: catalog.loadIconBitmap(path)
                else catalog.loadIconBitmap(path)
            }
        }
    }.value
    Box(Modifier.size(size).framePanel().padding(2.dp).clipToBounds(), contentAlignment = Alignment.Center) {
        when {
            gif?.animation != null -> PictureFrames(gif.animation, Modifier.fillMaxSize(), PictureFit.Crop)
            still != null -> Image(still, null, filterQuality = FilterQuality.None,
                contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
            // O GIF ainda montando: a moldura vazia, não o ícone da categoria
            // piscando antes do animado.
            animated && gif?.done != true -> Unit
            else -> PixelIcon(categoryIcon(entry.manifest.category), size - 8.dp)
        }
    }
}

private class IconLoad(val animation: IconAnimation?, val done: Boolean)

/** Os quadros do GIF: do cache na hora, ou montados fora da thread da interface. */
@Composable
private fun rememberIconAnimation(catalog: Catalog, path: String, version: String): IconLoad {
    val load by produceState(IconLoad(null, false), path, version) {
        value = IconLoad(null, false)
        value = IconLoad(withContext(MediaDispatcher) { catalog.loadAnimation(path) }, true)
    }
    return load
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
    val media = LocalRemoteMedia.current(entry)
    if (media.remote != null && media.bannerAsset == null) {
        val request = LocalRemoteFiles.current
        LaunchedEffect(entry.uid, media.remote.download.sha256) { request(entry.uid, listOf(media.remote.bannerFile)) }
    }
    val own = rememberPackagePicture(catalog, media.bannerAsset,
        "${media.remote?.download?.sha256 ?: media.manifest.version}:${media.mediaRevision}")
    val fallback = ImageBitmap.imageResource(
        FALLBACK_BANNERS[(entry.uid.hashCode() and 0x7fffffff) % FALLBACK_BANNERS.size]
    )
    Box(modifier.framePanel().padding(2.dp).clipToBounds()) {
        val first = own?.frames?.first()
        if (own == null || first == null) {
            Image(
                bitmap = fallback,
                contentDescription = null,
                filterQuality = FilterQuality.None,
                contentScale = ContentScale.Crop,
                alignment = Alignment.BottomCenter,
                modifier = Modifier.fillMaxSize(),
            )
        } else if (first.width < first.height * 1.8f) {
            // Arte estreita: inteira no meio, sobre ela mesma esmaecida.
            Box(Modifier.fillMaxSize().graphicsLayer { alpha = 0.22f }) {
                PictureFrames(own, Modifier.fillMaxSize(), PictureFit.Crop)
            }
            PictureFrames(own, Modifier.fillMaxSize(), PictureFit.Fit)
        } else {
            PictureFrames(own, Modifier.fillMaxSize(), PictureFit.Crop)
        }
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
    fill: Color = Bl.Panel,
    besideIcon: @Composable (() -> Unit)? = null,
    trailing: @Composable (() -> Unit)? = null,
) {
    PixelCard(modifier.fillMaxWidth(), fill = fill, onClick = onClick) {
        Row(
            Modifier.padding(10.dp).fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            besideIcon?.invoke()
            ModIcon(entry, catalog, 64.dp)
            Column(Modifier.weight(1f).padding(horizontal = 10.dp)) {
                PixelText(
                    entry.manifest.name,
                    size = Ts.Item, color = Bl.Text,
                    maxLines = 1, overflow = TextOverflow.Ellipsis,
                )
                PixelText(
                    "por ${entry.manifest.authorLine}",
                    size = Ts.Small, color = Bl.TextFaint,
                    maxLines = 1, overflow = TextOverflow.Ellipsis,
                )
                Row(
                    Modifier.padding(top = 5.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    PixelTag(entry.manifest.category, categoryColor(entry.manifest.category))
                    if (entry.manifest.isOutdated) {
                        PixelTag("Formato antigo", Bl.Bad, Modifier.padding(start = 8.dp))
                    }
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

@Composable
fun ModOrderButtons(name: String, canUp: Boolean, canDown: Boolean, onUp: () -> Unit, onDown: () -> Unit) {
    Column(Modifier.padding(end = 6.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        ModOrderArrow("Mover $name para cima", true, canUp, onUp)
        ModOrderArrow("Mover $name para baixo", false, canDown, onDown)
    }
}

@Composable
private fun ModOrderArrow(description: String, up: Boolean, enabled: Boolean, onClick: () -> Unit) {
    val interaction = remember { MutableInteractionSource() }
    Box(
        Modifier.size(30.dp).pixelPanel(fill = Bl.Select, raised = enabled)
            .semantics { contentDescription = description }
            .clickable(interaction, indication = null, enabled = enabled, role = Role.Button, onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        PixelIcon(R.drawable.ic_seta_esq, 18.dp,
            Modifier.rotate(if (up) 90f else 270f), alpha = if (enabled) 1f else 0.3f)
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

/**
 * Mods, Texturas e Fontes: as três listas do launcher, como as abas da ficha
 * de um mod. Cada aba mostra quantos pacotes tem; a vazia continua tocável
 * (a lista diz que não há nada) para o seletor não mudar de forma.
 */
@Composable
fun PackTypeTabs(
    selected: dev.bunnyloader.mods.PackType,
    counts: Map<dev.bunnyloader.mods.PackType, Int>,
    onSelect: (dev.bunnyloader.mods.PackType) -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        for (type in dev.bunnyloader.mods.PackType.entries) {
            val on = type == selected
            Box(
                Modifier.weight(1f)
                    .pixelPanel(fill = if (on) Bl.TitleFill else Bl.Select, raised = on)
                    .pixelClickable { onSelect(type) }
                    .padding(vertical = 8.dp),
                contentAlignment = Alignment.Center,
            ) {
                PixelText("${type.label} ${counts[type] ?: 0}", size = Ts.Small,
                    color = if (on) Bl.PressedText else Bl.TextDim, maxLines = 1)
            }
        }
    }
}
