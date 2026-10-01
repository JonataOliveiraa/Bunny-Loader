package dev.bunnyloader.ui

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.positionInParent
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.selected
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R
import dev.bunnyloader.mods.Author
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.formatDate
import dev.bunnyloader.mods.formatSize
import kotlin.math.roundToInt

/**
 * A ficha de um mod: capa, cabeçalho com as ações, e as abas que o PACOTE
 * escreve — `description.md`, `changelog.md`, `license.md`, os autores e as
 * páginas extras do manifesto. O launcher só desenha; o texto, as fotos e as
 * cores são do mod (docs/mods/13-pagina-do-pacote.md).
 *
 * As ações ficam em cima, antes das abas: com uma descrição longa, o botão de
 * instalar no pé da página ficava a vários dedos de rolagem.
 */
@Composable
fun ModDetail(entry: Catalog.Entry, shell: Shell, onBack: () -> Unit) {
    key(entry.uid) { ModDetailContent(entry, shell, onBack) }
}

@Composable
private fun ModDetailContent(entry: Catalog.Entry, shell: Shell, onBack: () -> Unit) {
    val catalog = shell.catalog
    val m = entry.manifest
    val uriHandler = LocalUriHandler.current
    val style = remember(m.theme) {
        MdStyle(
            text = parseHexColor(m.theme.text) ?: Bl.TextDim,
            heading = parseHexColor(m.theme.heading) ?: Bl.Text,
            accent = parseHexColor(m.theme.accent) ?: Bl.PressedText,
            panel = parseHexColor(m.theme.panel) ?: Bl.Panel,
        )
    }
    val ctx = remember(entry, style, catalog, uriHandler) {
        MdContext(
            style = style,
            image = { rel -> catalog.resolve(entry, rel)?.let(catalog::loadBitmap) },
            sprite = { kind, id ->
                catalog.loadBitmap("sprites/${if (kind == 'b') "buff" else "item"}/$id.png")
            },
            // Sem navegador instalado o openUri lança; o toque só não faz nada.
            openUrl = { url -> runCatching { uriHandler.openUri(url) } },
        )
    }
    val pages = rememberPages(entry, catalog)
    var tab by rememberSaveable(entry.uid) { mutableStateOf(0) }
    val selected = tab.coerceIn(0, pages.lastIndex)
    val scroll = rememberScrollState()
    var previousTab by remember { mutableIntStateOf(selected) }
    var tabsTop by remember { mutableIntStateOf(0) }
    LaunchedEffect(selected) {
        if (selected != previousTab) scroll.scrollTo(tabsTop)
        previousTab = selected
    }

    Box {
        Column(Modifier.fillMaxSize().verticalScroll(scroll)) {
            Cover(entry, shell, onBack)
            Header(entry, shell, ctx)

            if (pages.size > 1) {
                TabStrip(pages.map { it.title }, selected, style,
                    Modifier.onGloballyPositioned { tabsTop = it.positionInParent().y.roundToInt() },
                ) { tab = it }
            }
            val page = pages[selected]
            Column(
                Modifier.padding(horizontal = EdgePad)
                    .padding(top = if (pages.size > 1) 0.dp else 10.dp)
                    .pixelShadow().pixelPanel(fill = style.panel).padding(12.dp)
                    .fillMaxWidth(),
            ) {
                key(selected) {
                    when (page) {
                        is Page.Description -> {
                            MarkdownView(page.blocks, ctx)
                            Previews(entry, catalog)
                        }
                        is Page.Changelog -> ChangelogView(page.blocks, m.version, ctx)
                        is Page.License -> LicenseView(m.license, page.blocks, ctx)
                        is Page.Credits -> CreditsView(entry, catalog, ctx)
                        is Page.Custom -> MarkdownView(page.blocks, ctx)
                    }
                }
            }
            // O uid é a identidade real do pacote; aparece pequeno porque quem
            // precisa dele está depurando ou empacotando.
            PixelText(entry.uid, size = Ts.Tiny, color = Bl.TextMuted,
                modifier = Modifier.padding(horizontal = EdgePad + 4.dp, vertical = 10.dp))
            Spacer(Modifier.height(10.dp))
        }
        PixelScrollbar(scroll, Modifier.fillMaxSize())
    }
}

// ------------------------------- páginas -------------------------------

private sealed class Page(val title: String) {
    class Description(val blocks: List<MdBlock>) : Page("Descrição")
    class Changelog(val blocks: List<MdBlock>) : Page("Novidades")
    class License(val blocks: List<MdBlock>) : Page("Licença")
    class Credits : Page("Créditos")
    class Custom(title: String, val blocks: List<MdBlock>) : Page(title)
}

/**
 * As abas que este pacote tem. A Descrição existe sempre (sem
 * `description.md`, sai do `description` do manifesto, e sem ele do
 * `summary`); as outras só aparecem se o arquivo existir.
 */
@Composable
private fun rememberPages(entry: Catalog.Entry, catalog: Catalog): List<Page> = remember(entry, catalog) {
    val m = entry.manifest
    buildList {
        val description = catalog.readText(entry, Catalog.DESCRIPTION)
            ?: m.description.ifBlank { m.summary }
        add(Page.Description(Markdown.parse(description)))
        catalog.readText(entry, Catalog.CHANGELOG)?.let { add(Page.Changelog(Markdown.parse(it))) }
        val license = catalog.readText(entry, Catalog.LICENSE)
        if (license != null || m.license.isNotBlank()) {
            add(Page.License(license?.let(Markdown::parse).orEmpty()))
        }
        if (m.credits.isNotEmpty()) add(Page.Credits())
        for (p in m.pages) {
            if (p.title.isBlank()) continue
            catalog.readText(entry, p.file)?.let { add(Page.Custom(p.title, Markdown.parse(it))) }
        }
    }
}

// -------------------------------- topo --------------------------------

/** A capa, com voltar e favoritar flutuando sobre ela. */
@Composable
private fun Cover(entry: Catalog.Entry, shell: Shell, onBack: () -> Unit) {
    var favorite by remember { mutableStateOf(false) }
    Box(Modifier.fillMaxWidth()) {
        ModBanner(entry, shell.catalog, Modifier.fillMaxWidth().height(180.dp))
        Row(
            Modifier.fillMaxWidth().padding(10.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            // A seta de voltar do menu de ferramentas do jogo.
            RoundIcon(R.drawable.ic_seta_esq, onBack)
            RoundIcon(
                if (favorite) R.drawable.ic_fav_on else R.drawable.ic_fav_off,
                { favorite = !favorite },
            )
        }
    }
}

/**
 * Nome, autores, categoria, a ficha técnica e as ações. Um painel só: solto
 * sobre o cenário, o texto se perdia no fundo claro (neve, céu de dia).
 */
@Composable
private fun Header(entry: Catalog.Entry, shell: Shell, ctx: MdContext) {
    val m = entry.manifest
    val installed = entry.uid in shell.installed
    val on = entry.uid in shell.enabled
    var exportMessage by remember { mutableStateOf<Pair<String, Boolean>?>(null) }
    val exporter = rememberLauncherForActivityResult(
        ActivityResultContracts.CreateDocument("application/x-bunnyloader-mod")
    ) { uri ->
        if (uri != null) {
            exportMessage = shell.exportMod(entry.uid, uri).fold(
                onSuccess = { "${m.name} exportado" to true },
                onFailure = { "Não deu: ${it.message}" to false },
            )
        }
    }

    Column(
        Modifier.padding(horizontal = EdgePad).padding(top = 10.dp, bottom = 12.dp)
            .pixelShadow().pixelPanel(fill = ctx.style.panel).padding(12.dp),
    ) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            ModIcon(entry, shell.catalog, 60.dp)
            Column(Modifier.weight(1f).padding(horizontal = 10.dp)) {
                PixelText(m.name, size = Ts.Big, color = ctx.style.heading)
                AuthorStrip(entry, shell.catalog, Modifier.padding(top = 4.dp))
            }
        }
        Row(Modifier.padding(top = 10.dp), verticalAlignment = Alignment.CenterVertically) {
            PixelTag(m.category, categoryColor(m.category))
            if (m.isOutdated) PixelTag("Formato antigo", Bl.Bad, Modifier.padding(start = 8.dp))
        }

        // Ficha técnica. Nada de estrela nem contagem de download: precisariam
        // de um servidor que não existe, e número inventado é pior que nada.
        Row(
            Modifier.fillMaxWidth().padding(top = 12.dp)
                .pixelPanel(fill = ctx.style.panel.mix(Bl.Night, 0.35f), raised = false)
                .padding(horizontal = 8.dp, vertical = 8.dp),
        ) {
            InfoCell("Versão", "v${m.version}", Modifier.weight(1f))
            if (m.updated.isNotBlank()) InfoCell("Atualizado", formatDate(m.updated), Modifier.weight(1.4f))
            InfoCell("Tamanho", formatSize(entry.sizeBytes), Modifier.weight(1f))
            if (m.license.isNotBlank()) InfoCell("Licença", m.license, Modifier.weight(1f))
        }

        Spacer(Modifier.height(12.dp))
        if (!installed) {
            PixelButton("Baixar Mod", { shell.install(entry) },
                Modifier.fillMaxWidth(), icon = R.drawable.ic_start, shadow = false)
        } else {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Box(
                    Modifier.weight(1f).pixelPanel(fill = Bl.Select, raised = false)
                        .padding(start = 12.dp, top = 2.dp, bottom = 2.dp),
                    contentAlignment = Alignment.CenterStart,
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        PixelText(if (on) "Ligado" else "Desligado", size = Ts.Item,
                            color = if (on) Bl.PressedText else Bl.TextFaint,
                            modifier = Modifier.weight(1f))
                        SwitchSprite(on) { shell.setEnabled(entry.uid, !on) }
                    }
                }
                PixelButton("Remover", { shell.uninstall(entry.uid) },
                    icon = R.drawable.ic_trash, fontSize = Ts.Body, shadow = false)
            }
            PixelButton("Exportar .bl", {
                exporter.launch("${m.id.replace(Regex("[^A-Za-z0-9._-]"), "_")}.bl")
            }, Modifier.fillMaxWidth().padding(top = 8.dp), icon = R.drawable.ic_folder,
                fontSize = Ts.Body, shadow = false)
            exportMessage?.let { (message, ok) ->
                PixelText(message, size = Ts.Small, color = if (ok) Bl.TextDim else Bl.Bad,
                    modifier = Modifier.padding(top = 6.dp))
            }
        }

        // Links do pacote: só web, como os do Markdown.
        val links = m.links.filter { it.title.isNotBlank() && isWebUrl(it.url) }
        if (links.isNotEmpty()) {
            Row(
                Modifier.fillMaxWidth().padding(top = 10.dp).horizontalScroll(rememberScrollState()),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                for (link in links) {
                    PixelButton(link.title, { ctx.openUrl(link.url) },
                        fill = Bl.Select, textColor = ctx.style.accent,
                        fontSize = Ts.Small, shadow = false)
                }
            }
        }
    }
}

@Composable
private fun InfoCell(label: String, value: String, modifier: Modifier = Modifier) {
    Column(modifier.padding(horizontal = 2.dp)) {
        PixelText(label, size = Ts.Tiny, color = Bl.TextMuted)
        PixelText(value, size = Ts.Small, color = Bl.Text, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}

/**
 * As abas, encostadas no painel de baixo como aba de pasta: a escolhida tem a
 * cor do painel e um fio de destaque em cima; as outras ficam no escuro.
 * Rolam de lado quando o pacote pede muitas.
 */
@Composable
private fun TabStrip(
    titles: List<String>, selected: Int, style: MdStyle,
    modifier: Modifier = Modifier, onSelect: (Int) -> Unit,
) {
    Row(
        modifier.fillMaxWidth().padding(horizontal = EdgePad).horizontalScroll(rememberScrollState())
            .selectableGroup(),
        horizontalArrangement = Arrangement.spacedBy(4.dp),
        verticalAlignment = Alignment.Bottom,
    ) {
        titles.forEachIndexed { i, title ->
            val on = i == selected
            Box(
                Modifier.then(if (on) Modifier else Modifier.padding(top = 4.dp))
                    .pixelPanel(fill = if (on) style.panel else style.panel.mix(Bl.Night, 0.45f))
                    // O fio de destaque, desenhado e não medido: dentro da
                    // rolagem lateral um fillMaxWidth não tem largura para encher.
                    .drawBehind {
                        if (on) {
                            val b = 2.dp.toPx()
                            drawRect(style.accent, Offset(b, b), Size(size.width - 2 * b, 3.dp.toPx()))
                        }
                    }
                    .semantics {
                        role = Role.Tab
                        this.selected = on
                    }
                    .pixelClickable { onSelect(i) },
            ) {
                PixelText(title, size = if (on) Ts.Item else Ts.Body,
                    color = if (on) style.accent else Bl.TextFaint,
                    modifier = Modifier.padding(start = 14.dp, end = 14.dp, top = 11.dp, bottom = 8.dp))
            }
        }
    }
}

@Composable
private fun Previews(entry: Catalog.Entry, catalog: Catalog) {
    if (entry.previews.isEmpty()) return
    SectionTitle("Imagens")
    Row(
        Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        for (p in entry.previews) {
            val bmp = remember(p) { catalog.loadBitmap(p) } ?: continue
            Image(bmp, null, filterQuality = FilterQuality.None,
                modifier = Modifier.height(96.dp).framePanel().padding(2.dp))
        }
    }
}

// ------------------------------ Novidades ------------------------------

private val VERSION_HEAD = Regex(
    "^\\[?v?(\\d+(?:\\.\\d+)+[\\w.+-]*)]?(?:\\s*[-–—(]?\\s*\\(?(\\d{4}-\\d{2}-\\d{2})\\)?)?\\s*[-–—:]?\\s*(.*)$",
    RegexOption.IGNORE_CASE,
)

/**
 * O changelog vira uma pilha de versões, cada uma fechável: o título `##` que
 * começa com um número de versão (`## 1.8.0 - 2026-09-27`, `## [1.8.0]`,
 * `## v1.8.0`) abre uma. A primeira vem aberta e a instalada ganha a etiqueta
 * "Atual". Um changelog sem títulos de versão sai como Markdown comum.
 */
@Composable
private fun ChangelogView(blocks: List<MdBlock>, current: String, ctx: MdContext) {
    val intro = mutableListOf<MdBlock>()
    val versions = mutableListOf<Triple<MatchResult, MutableList<MdBlock>, Int>>()
    for (b in blocks) {
        val head = (b as? MdBlock.Heading)?.takeIf { it.level <= 2 }?.let { VERSION_HEAD.matchEntire(it.text.trim()) }
        when {
            head != null -> versions += Triple(head, mutableListOf(), versions.size)
            versions.isEmpty() -> intro += b
            else -> versions.last().second += b
        }
    }
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        // O "# Changelog" do topo repete o nome da aba.
        val shownIntro = intro.filterNot { it is MdBlock.Heading && it.level == 1 && versions.isNotEmpty() }
        if (shownIntro.isNotEmpty()) MarkdownView(shownIntro, ctx)
        for ((head, body, index) in versions) {
            VersionCard(head.groupValues[1], head.groupValues[2], head.groupValues[3],
                body, current, index == 0, ctx)
        }
    }
}

@Composable
private fun VersionCard(
    version: String, date: String, title: String,
    body: List<MdBlock>, current: String, openFirst: Boolean, ctx: MdContext,
) {
    val isCurrent = version.removePrefix("v") == current.removePrefix("v")
    var open by rememberSaveable(version) { mutableStateOf(openFirst || isCurrent) }
    val st = ctx.style
    Column(
        Modifier.fillMaxWidth().pixelPanel(
            fill = st.panel.mix(Bl.Night, if (open) 0.15f else 0.3f), raised = !open,
        ),
    ) {
        Row(
            Modifier.fillMaxWidth().pixelClickable { open = !open }.padding(10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            PixelTag("v$version", if (isCurrent) st.accent.mix(Bl.Night, 0.45f) else Bl.TitleFill)
            Column(Modifier.weight(1f).padding(start = 10.dp)) {
                if (title.isNotBlank()) {
                    MdText(title, ctx, size = Ts.Body, color = st.heading)
                }
                if (date.isNotBlank()) {
                    PixelText(formatDate(date), size = Ts.Tiny, color = Bl.TextMuted)
                }
            }
            if (isCurrent) PixelTag("Atual", st.accent.mix(Bl.Night, 0.45f), Modifier.padding(end = 8.dp))
            PixelText(if (open) "-" else "+", size = Ts.Head, color = st.accent)
        }
        if (open && body.isNotEmpty()) {
            MarkdownView(body, ctx, Modifier.padding(start = 12.dp, end = 12.dp, bottom = 12.dp))
        }
    }
}

// ------------------------------- Licença -------------------------------

@Composable
private fun LicenseView(name: String, blocks: List<MdBlock>, ctx: MdContext) {
    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        if (name.isNotBlank()) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                PixelText("Licença", size = Ts.Body, color = Bl.TextFaint,
                    modifier = Modifier.padding(end = 10.dp))
                PixelTag(name, Bl.TitleFill)
            }
        }
        if (blocks.isEmpty()) {
            PixelText("O pacote não traz o texto da licença (license.md).",
                size = Ts.Small, color = Bl.TextMuted)
        } else {
            MarkdownView(blocks, ctx)
        }
    }
}

// ------------------------------- Créditos -------------------------------

@Composable
private fun CreditsView(entry: Catalog.Entry, catalog: Catalog, ctx: MdContext) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        for (author in entry.manifest.credits) {
            val link = author.link.takeIf(::isWebUrl)
            Row(
                Modifier.fillMaxWidth()
                    .pixelPanel(fill = ctx.style.panel.mix(Bl.Night, 0.25f))
                    .then(if (link != null) Modifier.pixelClickable { ctx.openUrl(link) } else Modifier)
                    .padding(10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                AuthorAvatar(author, entry, catalog, 56.dp)
                Column(Modifier.weight(1f).padding(start = 12.dp)) {
                    PixelText(author.name, size = Ts.Item,
                        color = parseHexColor(author.color) ?: ctx.style.heading)
                    if (author.role.isNotBlank()) {
                        PixelText(author.role, size = Ts.Small, color = Bl.TextFaint)
                    }
                    if (link != null) {
                        PixelText(hostOf(link), size = Ts.Tiny, color = ctx.style.accent,
                            maxLines = 1, overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.padding(top = 2.dp))
                    }
                }
            }
        }
    }
}

/** "por A e B" com as fotos uma por cima da outra, como numa loja de app. */
@Composable
private fun AuthorStrip(entry: Catalog.Entry, catalog: Catalog, modifier: Modifier = Modifier) {
    val credits = entry.manifest.credits
    Row(modifier, verticalAlignment = Alignment.CenterVertically) {
        val shown = credits.take(4)
        if (shown.isNotEmpty()) {
            Box(Modifier.width(22.dp + 15.dp * (shown.size - 1)).height(22.dp)) {
                shown.forEachIndexed { i, a ->
                    Box(Modifier.padding(start = 15.dp * i)) { AuthorAvatar(a, entry, catalog, 22.dp) }
                }
            }
        }
        PixelText("por ${entry.manifest.authorLine}", size = Ts.Small, color = Bl.TextFaint,
            maxLines = 2, overflow = TextOverflow.Ellipsis,
            modifier = Modifier.padding(start = if (shown.isEmpty()) 0.dp else 6.dp))
    }
}

/**
 * A foto do autor, de `authors/` no pacote. Sem `avatar` no manifesto, vale o
 * arquivo com o nome dele (como está, ou em minúsculas com hífen). Sem foto,
 * a inicial na cor do autor.
 */
@Composable
fun AuthorAvatar(author: Author, entry: Catalog.Entry, catalog: Catalog, size: Dp) {
    val bmp = remember(entry, author) { findAvatar(author, entry, catalog) }
    val sizePx = with(LocalDensity.current) { size.toPx() }
    val tint = parseHexColor(author.color) ?: Bl.TitleFill
    Box(
        Modifier.size(size).framePanel(fill = if (bmp == null) tint.mix(Bl.Night, 0.5f) else Bl.FrameFill),
        contentAlignment = Alignment.Center,
    ) {
        if (bmp != null) {
            // Arte menor que o quadro amplia sem filtro (pixel quadrado); foto
            // maior encolhe com filtro, senão vira serrilhado.
            Image(bmp, author.name, contentScale = ContentScale.Crop,
                filterQuality = if (bmp.width < sizePx) FilterQuality.None else FilterQuality.Medium,
                modifier = Modifier.fillMaxSize().padding(2.dp))
        } else {
            PixelText(author.name.take(1).uppercase(), size = if (size >= 40.dp) Ts.Big else Ts.Tiny)
        }
    }
}

private fun findAvatar(author: Author, entry: Catalog.Entry, catalog: Catalog): ImageBitmap? {
    val candidates = if (author.avatar.isNotBlank()) {
        listOf(author.avatar)
    } else {
        val slug = author.name.lowercase().replace(Regex("[^a-z0-9]+"), "-").trim('-')
        listOf(author.name, slug).distinct().filter { it.isNotBlank() }
            .flatMap { base -> AVATAR_EXTENSIONS.map { "$base.$it" } }
    }
    return candidates.asSequence()
        .map { "${Catalog.AUTHORS}/$it" }
        .filter { catalog.exists(entry, it) }
        .mapNotNull { rel -> catalog.resolve(entry, rel)?.let(catalog::loadBitmap) }
        .firstOrNull()
}

private val AVATAR_EXTENSIONS = listOf("png", "jpg", "jpeg", "webp")

private fun isWebUrl(url: String) = url.startsWith("https://") || url.startsWith("http://")

private fun hostOf(url: String) =
    url.substringAfter("://").substringBefore('/').removePrefix("www.").ifBlank { url }

@Composable
private fun RoundIcon(res: Int, onClick: () -> Unit) {
    Box(
        Modifier.size(38.dp).framePanel().pixelClickable(onClick),
        contentAlignment = Alignment.Center,
    ) {
        PixelIcon(res, 20.dp)
    }
}
