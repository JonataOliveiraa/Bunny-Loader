package dev.bunnyloader.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.InlineTextContent
import androidx.compose.foundation.text.appendInlineContent
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.LinkAnnotation
import androidx.compose.ui.text.Placeholder
import androidx.compose.ui.text.PlaceholderVerticalAlign
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextLinkStyles
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.text.withLink
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.em
import androidx.compose.ui.unit.sp
import dev.bunnyloader.mods.IconAnimation
import kotlinx.coroutines.withContext
import kotlin.math.floor

/**
 * Markdown dos pacotes: `description.md`, `changelog.md`, `license.md` e as
 * abas que o manifesto pede.
 *
 * Feito aqui, e não com uma biblioteca, porque o texto tem de sair do mesmo
 * material que o resto do launcher: fonte pixelada com o contorno preto do
 * Terraria, painéis com bisel, imagem sem filtro. Uma biblioteca de Markdown
 * traria a tipografia dela.
 *
 * O que entende (o guia docs/mods/13-pagina-do-pacote.md mostra cada um):
 * títulos `#`..`######`, parágrafo, `**negrito**`, `*itálico*`, `~~riscado~~`,
 * `==marcado==`, `` `código` ``, bloco de código, link, imagem do pacote
 * (sozinha na linha vira bloco; no meio do texto vira ícone; um `{width=64
 * align=right float=left}` logo depois dá tamanho e lugar), citação, os
 * avisos do GitHub (`> [!NOTE]`...), listas (com número, com marcador, de
 * tarefa e aninhadas), tabela, linha `---`, e os contêineres `::: center`,
 * `::: spoiler Título`, `::: panel` e `::: group`. Do Terraria: `[c/FF8800:texto]` pinta,
 * `[i:757]` desenha o item e `[b:3]` o buff.
 */

// ================================ estilo ================================

/** As cores com que o Markdown desenha. A ficha monta isto do tema do pacote. */
data class MdStyle(
    val text: Color = Bl.TextDim,
    val heading: Color = Bl.Text,
    val accent: Color = Bl.PressedText,
    val panel: Color = Bl.Panel,
)

/**
 * O que o texto referencia de fora dele. `image` recebe o caminho relativo à
 * raiz do pacote (e devolve os quadros: GIF anima); `sprite`, o tipo (`i`
 * item, `b` buff) e o id do jogo.
 */
class MdContext(
    val style: MdStyle,
    val image: (String) -> IconAnimation?,
    val sprite: (Char, Int) -> ImageBitmap?,
    val openUrl: (String) -> Unit,
)

private val LocalMdCenter = compositionLocalOf { false }

// ================================ blocos ================================

sealed interface MdBlock {
    data class Heading(val level: Int, val text: String) : MdBlock
    data class Paragraph(val text: String) : MdBlock
    data class Code(val lang: String, val text: String) : MdBlock
    data class Quote(val children: List<MdBlock>) : MdBlock
    data class Callout(val kind: String, val title: String, val children: List<MdBlock>) : MdBlock
    data class ListBlock(val ordered: Boolean, val start: Int, val items: List<ListItem>) : MdBlock
    data class Image(val alt: String, val src: String, val attrs: ImageAttrs = ImageAttrs()) : MdBlock
    data class Table(val header: List<String>, val aligns: List<TextAlign>, val rows: List<List<String>>) : MdBlock
    data class Container(val kind: String, val title: String, val children: List<MdBlock>) : MdBlock
    data object Rule : MdBlock
}

/** Uma medida de imagem: em dp ("pixels" do launcher) ou em % da largura do texto. */
data class ImageLength(val value: Float, val percent: Boolean = false)

/**
 * O `{...}` depois de uma imagem: `{width=128}`, `{h=24}`, `{64x32}`,
 * `{50% align=right}`, `{float=left width=40%}`. `align` posiciona a imagem
 * sozinha na linha; `float` a põe ao lado do bloco seguinte.
 */
data class ImageAttrs(
    val width: ImageLength? = null,
    val height: ImageLength? = null,
    /** "left", "center", "right" ou null (o padrão do lugar). */
    val align: String? = null,
    /** "left", "right" ou null. */
    val float: String? = null,
) {
    companion object {
        private val LENGTH = Regex("^(\\d+(?:\\.\\d+)?)(px|dp|%)?$")
        private val BOTH = Regex("^(\\d*)x(\\d*)$")
        private val SEPARATORS = Regex("[\\s,;]+")

        fun parse(source: String?): ImageAttrs {
            if (source.isNullOrBlank()) return ImageAttrs()
            var attrs = ImageAttrs()
            for (token in source.trim().split(SEPARATORS)) {
                val parts = token.split('=', ':', limit = 2)
                val key = if (parts.size == 2) parts[0].lowercase() else ""
                val value = parts.last().trim('"', '\'').lowercase()
                attrs = when (key) {
                    "width", "w", "largura" -> attrs.copy(width = length(value) ?: attrs.width)
                    "height", "h", "altura" -> attrs.copy(height = length(value) ?: attrs.height)
                    "align", "alinhar" -> attrs.copy(align = side(value, center = true) ?: attrs.align)
                    "float", "flutuar" -> attrs.copy(float = side(value, center = false) ?: attrs.float)
                    "" -> {
                        val both = BOTH.matchEntire(value)
                        val side = side(value, center = true)
                        when {
                            both != null -> attrs.copy(
                                width = both.groupValues[1].toFloatOrNull()?.takeIf { it > 0f }
                                    ?.let { ImageLength(it.coerceAtMost(MAX_DP)) } ?: attrs.width,
                                height = both.groupValues[2].toFloatOrNull()?.takeIf { it > 0f }
                                    ?.let { ImageLength(it.coerceAtMost(MAX_DP)) } ?: attrs.height,
                            )
                            side != null -> attrs.copy(align = side)
                            else -> attrs.copy(width = length(value) ?: attrs.width)
                        }
                    }
                    else -> attrs
                }
            }
            return attrs
        }

        private const val MAX_DP = 2048f

        private fun length(value: String): ImageLength? {
            val m = LENGTH.matchEntire(value) ?: return null
            val n = m.groupValues[1].toFloatOrNull()?.takeIf { it > 0f } ?: return null
            return if (m.groupValues[2] == "%") ImageLength(n.coerceAtMost(100f), percent = true)
            else ImageLength(n.coerceAtMost(MAX_DP))
        }

        private fun side(value: String, center: Boolean): String? = when (value) {
            "left", "esquerda" -> "left"
            "right", "direita" -> "right"
            "center", "centro" -> if (center) "center" else null
            else -> null
        }
    }
}

/** `checked` é null num item comum, e true/false num `- [x]`/`- [ ]`. */
data class ListItem(val checked: Boolean?, val children: List<MdBlock>)

object Markdown {
    fun parse(source: String): List<MdBlock> =
        parseBlocks(stripComments(source).replace("\r\n", "\n").replace('\t', ' ').split('\n'))

    private fun stripComments(s: String) = s.replace(Regex("<!--[\\s\\S]*?-->"), "")

    private val HEADING = Regex("^ {0,3}(#{1,6})\\s+(.*?)\\s*#*\\s*$")
    private val RULE = Regex("^ {0,3}([-*_])(\\s*\\1){2,}\\s*$")
    private val FENCE = Regex("^ {0,3}(`{3,}|~{3,})\\s*([\\w+#.-]*).*$")
    private val LIST = Regex("^( {0,3})([-*+]|\\d{1,9}[.)])( +)(.*)$")
    private val EMPTY_LIST = Regex("^( {0,3})([-*+]|\\d{1,9}[.)])$")
    private val QUOTE = Regex("^ {0,3}> ?(.*)$")
    private val CONTAINER = Regex("^ {0,3}:::+\\s*(\\w+)\\s*(.*)$")
    private val CONTAINER_END = Regex("^ {0,3}:::+\\s*$")
    private val IMAGE_LINE = Regex("^\\s*!\\[([^\\]]*)]\\(\\s*([^)\\s]+)(?:\\s+\"[^\"]*\")?\\s*\\)(?:\\s*\\{([^}]*)\\})?\\s*$")
    private val TABLE_SEP = Regex("^\\s*\\|?\\s*:?-+:?\\s*(\\|\\s*:?-+:?\\s*)*\\|?\\s*$")
    private val CALLOUT = Regex("^\\[!(\\w+)]\\s*(.*)$")
    private val TASK = Regex("^\\[([ xX])]\\s+(.*)$", RegexOption.DOT_MATCHES_ALL)

    private fun isBlank(line: String) = line.isBlank()

    /** Linha que começa um bloco novo: interrompe um parágrafo. */
    private fun startsBlock(line: String) =
        HEADING.matches(line) || RULE.matches(line) || FENCE.matches(line) ||
            QUOTE.matches(line) || CONTAINER.matches(line) || IMAGE_LINE.matches(line) ||
            LIST.find(line)?.let { !it.groupValues[2].first().isDigit() || it.groupValues[2].startsWith("1") } == true

    private fun parseBlocks(lines: List<String>): List<MdBlock> {
        val out = mutableListOf<MdBlock>()
        var i = 0
        while (i < lines.size) {
            val line = lines[i]
            if (isBlank(line)) { i++; continue }

            val fenceMatch = FENCE.matchEntire(line)
            if (fenceMatch != null) {
                val fence = fenceMatch.groupValues[1]
                val body = mutableListOf<String>()
                i++
                while (i < lines.size && !lines[i].trimStart().startsWith(fence)) body += lines[i++]
                i++ // a cerca de fechamento (ou o fim)
                out += MdBlock.Code(fenceMatch.groupValues[2], body.joinToString("\n").trimEnd())
                continue
            }

            val containerMatch = CONTAINER.matchEntire(line)
            if (containerMatch != null) {
                val m = containerMatch
                val body = mutableListOf<String>()
                var depth = 1
                i++
                while (i < lines.size) {
                    val l = lines[i]
                    if (CONTAINER_END.matches(l)) { depth--; if (depth == 0) break }
                    else if (CONTAINER.matches(l)) depth++
                    body += l
                    i++
                }
                i++
                out += MdBlock.Container(m.groupValues[1].lowercase(), m.groupValues[2].trim(), parseBlocks(body))
                continue
            }

            val headingMatch = HEADING.matchEntire(line)
            if (headingMatch != null) {
                out += MdBlock.Heading(headingMatch.groupValues[1].length, headingMatch.groupValues[2])
                i++
                continue
            }

            if (RULE.matches(line)) { out += MdBlock.Rule; i++; continue }

            if (QUOTE.matches(line)) {
                val body = mutableListOf<String>()
                while (i < lines.size && QUOTE.matches(lines[i])) {
                    body += QUOTE.matchEntire(lines[i])!!.groupValues[1]
                    i++
                }
                val callout = body.firstOrNull()?.let { CALLOUT.matchEntire(it.trim()) }
                out += if (callout != null) {
                    MdBlock.Callout(callout.groupValues[1].lowercase(), callout.groupValues[2],
                        parseBlocks(body.drop(1)))
                } else {
                    MdBlock.Quote(parseBlocks(body))
                }
                continue
            }

            if (LIST.matches(line) || EMPTY_LIST.matches(line)) {
                i = parseList(lines, i, out)
                continue
            }

            if (i + 1 < lines.size && '|' in line && TABLE_SEP.matches(lines[i + 1]) && '-' in lines[i + 1]) {
                val header = cells(line)
                val aligns = cells(lines[i + 1]).map {
                    val l = it.startsWith(":"); val r = it.endsWith(":")
                    when { l && r -> TextAlign.Center; r -> TextAlign.End; else -> TextAlign.Start }
                }
                i += 2
                val rows = mutableListOf<List<String>>()
                while (i < lines.size && '|' in lines[i] && !isBlank(lines[i])) rows += cells(lines[i++])
                out += MdBlock.Table(header, aligns, rows)
                continue
            }

            val imageMatch = IMAGE_LINE.matchEntire(line)
            if (imageMatch != null) {
                out += MdBlock.Image(imageMatch.groupValues[1], imageMatch.groupValues[2],
                    ImageAttrs.parse(imageMatch.groupValues[3]))
                i++
                continue
            }

            // Parágrafo: até a linha em branco ou o começo de outro bloco. Dois
            // espaços ou `\` no fim da linha quebram a linha; o resto vira espaço.
            val text = StringBuilder()
            while (i < lines.size && !isBlank(lines[i]) && (text.isEmpty() || !startsBlock(lines[i]))) {
                val l = lines[i]
                val hard = l.endsWith("  ") || l.endsWith("\\")
                text.append(l.trim().removeSuffix("\\"))
                text.append(if (hard) "\n" else " ")
                i++
            }
            out += MdBlock.Paragraph(text.toString().trimEnd(' '))
        }
        return out
    }

    /**
     * Uma lista: os itens que começam no mesmo recuo e com o mesmo tipo de
     * marcador. O que vem recuado embaixo de um item é dele (outro parágrafo,
     * uma sublista), e é lido de novo como blocos.
     */
    private fun parseList(lines: List<String>, from: Int, out: MutableList<MdBlock>): Int {
        var i = from
        val first = LIST.matchEntire(lines[i]) ?: EMPTY_LIST.matchEntire(lines[i])!!
        val ordered = first.groupValues[2].first().isDigit()
        val start = if (ordered) first.groupValues[2].dropLast(1).toIntOrNull() ?: 1 else 1
        val indent = first.groupValues[1].length
        val items = mutableListOf<ListItem>()

        while (i < lines.size) {
            val m = LIST.matchEntire(lines[i]) ?: EMPTY_LIST.matchEntire(lines[i]) ?: break
            if (m.groupValues[1].length != indent) break
            if (m.groupValues[2].first().isDigit() != ordered) break
            val marker = m.groupValues[1].length + m.groupValues[2].length
            val contentIndent = marker + (m.groupValues.getOrNull(3)?.length?.coerceIn(1, 4) ?: 1)
            val body = mutableListOf(m.groupValues.getOrElse(4) { "" })
            i++
            var sawBlank = false
            while (i < lines.size) {
                val l = lines[i]
                if (isBlank(l)) { sawBlank = true; body += ""; i++; continue }
                val lead = l.length - l.trimStart().length
                // O próximo marcador no mesmo nível fecha este item, mesmo
                // quando é 2., 3. etc. (não interrompem parágrafos comuns).
                val next = LIST.matchEntire(l) ?: EMPTY_LIST.matchEntire(l)
                if (next != null && lead <= indent) break
                if (lead >= contentIndent) {
                    body += l.substring(contentIndent.coerceAtMost(lead)); sawBlank = false; i++
                } else if (!sawBlank && lead > indent && LIST.matches(l)) {
                    // Sublista recuada menos que o texto do item (`- a` / `  - b`).
                    body += l.substring(lead.coerceAtMost(indent + 2)); i++
                } else if (!sawBlank && !startsBlock(l)) {
                    body += l.trim(); i++ // continuação preguiçosa do parágrafo
                } else break
            }
            while (body.lastOrNull()?.isBlank() == true) body.removeAt(body.lastIndex)
            val task = TASK.matchEntire(body.firstOrNull().orEmpty())
            val checked = task?.let { it.groupValues[1] != " " }
            if (task != null) body[0] = task.groupValues[2]
            items += ListItem(checked, parseBlocks(body))
            // Uma linha em branco entre itens não fecha a lista; outra coisa, sim.
            if (sawBlank && i < lines.size && !LIST.matches(lines[i])) break
        }
        out += MdBlock.ListBlock(ordered, start, items)
        return i
    }

    private fun cells(line: String): List<String> {
        var s = line.trim()
        if (s.startsWith("|")) s = s.substring(1)
        if (s.endsWith("|") && !s.endsWith("\\|")) s = s.dropLast(1)
        return s.split(Regex("(?<!\\\\)\\|")).map { it.trim().replace("\\|", "|") }
    }
}

// ================================ inline ================================

sealed interface MdInline {
    data class Text(val text: String) : MdInline
    data class Bold(val children: List<MdInline>) : MdInline
    data class Italic(val children: List<MdInline>) : MdInline
    data class Strike(val children: List<MdInline>) : MdInline
    data class Mark(val children: List<MdInline>) : MdInline
    data class Code(val text: String) : MdInline
    data class Link(val url: String, val children: List<MdInline>) : MdInline
    data class Tint(val color: Color, val children: List<MdInline>) : MdInline
    /**
     * Imagem do pacote (`img:<caminho>`) ou sprite do jogo (`spr:<tipo>:<id>`),
     * com o tamanho do `{...}` que vier colado nela.
     */
    data class Picture(val key: String, val alt: String, val attrs: ImageAttrs = ImageAttrs()) : MdInline {
        /** Um por tamanho: a mesma imagem pode aparecer pequena e grande na frase. */
        val slot: String get() = if (attrs.width == null && attrs.height == null) key
            else "$key#${attrs.width?.value}x${attrs.height?.value}"
    }
}

object MdInlineParser {
    private val TINT = Regex("^\\[c/([0-9a-fA-F]{6}):")
    private val SPRITE = Regex("^\\[([ib])(?:/[^:\\]]*)?:(\\d{1,5})](?:\\{([^}]*)\\})?")
    private val LINK = Regex("^\\[((?:[^\\[\\]]|\\[[^\\]]*])*)]\\(\\s*([^)\\s]+)(?:\\s+\"[^\"]*\")?\\s*\\)")
    private val IMAGE = Regex("^!\\[([^\\]]*)]\\(\\s*([^)\\s]+)(?:\\s+\"[^\"]*\")?\\s*\\)(?:\\{([^}]*)\\})?")
    private val AUTOLINK = Regex("^<((?:https?://|mailto:)[^>\\s]+)>")
    private const val ESCAPABLE = "\\`*_{}[]()#+-.!|~=<>:"

    fun parse(s: String): List<MdInline> {
        val out = mutableListOf<MdInline>()
        val text = StringBuilder()
        fun flush() { if (text.isNotEmpty()) { out += MdInline.Text(text.toString()); text.clear() } }
        var i = 0
        while (i < s.length) {
            val c = s[i]
            val rest = s.substring(i)
            if (c == '\\' && i + 1 < s.length && s[i + 1] in ESCAPABLE) {
                text.append(s[i + 1]); i += 2; continue
            }
            if (c == '`') {
                val run = rest.takeWhile { it == '`' }.length
                val end = s.indexOf("`".repeat(run), i + run)
                if (end > 0) {
                    flush(); out += MdInline.Code(s.substring(i + run, end).trim()); i = end + run; continue
                }
            }
            if (c == '!') {
                val m = IMAGE.find(rest)
                if (m != null) {
                    flush()
                    out += MdInline.Picture("img:" + m.groupValues[2], m.groupValues[1],
                        ImageAttrs.parse(m.groupValues[3]))
                    i += m.value.length
                    continue
                }
            }
            if (c == '[') {
                val tint = TINT.find(rest)
                val close = if (tint != null) matchingBracket(s, i) else -1
                if (tint != null && close > 0) {
                    flush()
                    val inner = s.substring(i + tint.value.length, close)
                    out += MdInline.Tint(Color(0xFF000000 or tint.groupValues[1].toLong(16)), parse(inner))
                    i = close + 1
                    continue
                }
                val sprite = SPRITE.find(rest)
                if (sprite != null) {
                    flush()
                    out += MdInline.Picture("spr:${sprite.groupValues[1]}:${sprite.groupValues[2]}",
                        sprite.value.substringBefore('{'), ImageAttrs.parse(sprite.groupValues[3]))
                    i += sprite.value.length
                    continue
                }
                val link = LINK.find(rest)
                if (link != null) {
                    flush(); out += MdInline.Link(link.groupValues[2], parse(link.groupValues[1]))
                    i += link.value.length
                    continue
                }
            }
            if (c == '<') {
                val m = AUTOLINK.find(rest)
                if (m != null) {
                    flush(); out += MdInline.Link(m.groupValues[1], listOf(MdInline.Text(m.groupValues[1])))
                    i += m.value.length
                    continue
                }
            }
            // Link solto (https://...) também vira link: é como todo mundo escreve.
            if ((c == 'h') && (rest.startsWith("https://") || rest.startsWith("http://")) &&
                (i == 0 || !s[i - 1].isLetterOrDigit())) {
                val url = rest.takeWhile { !it.isWhitespace() && it != ')' && it != ']' }
                    .trimEnd('.', ',', ';', ':', '!', '?')
                flush(); out += MdInline.Link(url, listOf(MdInline.Text(url))); i += url.length; continue
            }
            val pair = when {
                rest.startsWith("**") -> "**"
                rest.startsWith("__") -> "__"
                rest.startsWith("~~") -> "~~"
                rest.startsWith("==") -> "=="
                c == '*' -> "*"
                c == '_' && (i == 0 || !s[i - 1].isLetterOrDigit()) -> "_"
                else -> null
            }
            if (pair != null) {
                val close = findClose(s, i + pair.length, pair)
                if (close > 0) {
                    flush()
                    val inner = parse(s.substring(i + pair.length, close))
                    out += when (pair) {
                        "**", "__" -> MdInline.Bold(inner)
                        "~~" -> MdInline.Strike(inner)
                        "==" -> MdInline.Mark(inner)
                        else -> MdInline.Italic(inner)
                    }
                    i = close + pair.length
                    continue
                }
            }
            text.append(c)
            i++
        }
        flush()
        return out
    }

    /** O fecho de um `**`/`*`/`~~`: não vale logo depois de espaço, nem vazio. */
    private fun findClose(s: String, from: Int, pair: String): Int {
        if (from >= s.length || s[from].isWhitespace()) return -1
        var j = from
        while (true) {
            j = s.indexOf(pair, j)
            if (j < 0) return -1
            if (j > from && !s[j - 1].isWhitespace() && s[j - 1] != '\\') {
                // `*` único não fecha no meio de um `**`.
                if (pair.length == 1 && j + 1 < s.length && s[j + 1] == pair[0]) { j += 2; continue }
                if (pair == "_" && j + 1 < s.length && s[j + 1].isLetterOrDigit()) { j++; continue }
                return j
            }
            j++
        }
    }

    private fun matchingBracket(s: String, open: Int): Int {
        var depth = 0
        for (k in open until s.length) {
            when (s[k]) {
                '[' -> depth++
                ']' -> { depth--; if (depth == 0) return k }
            }
        }
        return -1
    }
}

/** `#RGB`, `#RRGGBB` ou `#AARRGGBB`; null para o resto. */
fun parseHexColor(s: String?): Color? {
    val hex = s?.trim()?.removePrefix("#") ?: return null
    val v = hex.toLongOrNull(16) ?: return null
    return when (hex.length) {
        3 -> {
            val r = (v shr 8) and 0xF; val g = (v shr 4) and 0xF; val b = v and 0xF
            Color(0xFF000000 or (r * 17 shl 16) or (g * 17 shl 8) or (b * 17))
        }
        6 -> Color(0xFF000000 or v)
        8 -> Color(v)
        else -> null
    }
}

// =============================== desenho ===============================

/**
 * O documento inteiro, bloco a bloco. Uma imagem com `float` vai ao lado do
 * bloco seguinte (o parágrafo, a lista, um `::: group` com vários).
 */
@Composable
fun MarkdownView(blocks: List<MdBlock>, ctx: MdContext, modifier: Modifier = Modifier) {
    Column(modifier, verticalArrangement = Arrangement.spacedBy(10.dp)) {
        var i = 0
        while (i < blocks.size) {
            val b = blocks[i]
            val next = blocks.getOrNull(i + 1)
            if (b is MdBlock.Image && b.attrs.float != null && next != null) {
                FloatingImage(b, next, ctx)
                i += 2
            } else {
                MdBlockView(b, ctx)
                i++
            }
        }
    }
}

/**
 * A imagem num lado e o bloco no outro. A largura da imagem é a do `{...}`
 * (até 60% do texto); sem ela, 40%.
 */
@Composable
private fun FloatingImage(image: MdBlock.Image, beside: MdBlock, ctx: MdContext) {
    BoxWithConstraints(Modifier.fillMaxWidth()) {
        val full = maxWidth
        val asked = image.attrs.width?.let { if (it.percent) full * (it.value / 100f) else it.value.dp + 4.dp }
        val cell = (asked ?: full * 0.4f).coerceAtMost(full * 0.6f)
        // Dentro da célula, a imagem enche a largura; só com a altura pedida,
        // ela segue a altura (e cabe na célula).
        val inner = if (image.attrs.width == null && image.attrs.height != null) image
            else image.copy(attrs = image.attrs.copy(width = ImageLength(100f, percent = true), align = null))
        val left = image.attrs.float == "left"
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            if (left) MdImage(inner, ctx, Modifier.width(cell))
            Box(Modifier.weight(1f)) { MdBlockView(beside, ctx) }
            if (!left) MdImage(inner, ctx, Modifier.width(cell))
        }
    }
}

@Composable
fun MdBlockView(block: MdBlock, ctx: MdContext) {
    val st = ctx.style
    when (block) {
        is MdBlock.Heading -> {
            val size = when (block.level) { 1 -> Ts.Big; 2 -> Ts.Head; 3 -> Ts.Item; else -> Ts.Body }
            Column(Modifier.fillMaxWidth().padding(top = if (block.level <= 2) 6.dp else 2.dp)) {
                MdText(block.text, ctx, size = size, color = st.heading)
                // O título grande ganha um fio no tom de destaque, como a faixa
                // dos títulos de seção do launcher, só que fina.
                if (block.level <= 2) {
                    Box(Modifier.padding(top = 4.dp).fillMaxWidth().height(2.dp)
                        .pixelPanel(fill = st.accent.copy(alpha = if (block.level == 1) 0.9f else 0.45f),
                            outline = Color.Transparent))
                }
            }
        }
        is MdBlock.Paragraph -> MdText(block.text, ctx)
        is MdBlock.Code -> CodeBlock(block)
        is MdBlock.Rule -> Box(Modifier.fillMaxWidth().padding(vertical = 4.dp).height(2.dp)
            .framePanel(border = Bl.FrameBorder, fill = Bl.FrameBorder))
        is MdBlock.Image -> MdImage(block, ctx)
        is MdBlock.Quote -> Row(Modifier.fillMaxWidth().height(IntrinsicSize.Min)) {
            Box(Modifier.width(4.dp).fillMaxHeight().framePanel(border = st.accent.mix(Bl.Night, 0.4f),
                fill = st.accent.mix(Bl.Night, 0.4f)))
            MarkdownView(block.children, ctx.copyStyle(st.copy(text = st.text.mix(Bl.TextMuted, 0.35f))),
                Modifier.padding(start = 10.dp).weight(1f))
        }
        is MdBlock.Callout -> Callout(block, ctx)
        is MdBlock.ListBlock -> MdList(block, ctx)
        is MdBlock.Table -> MdTable(block, ctx)
        is MdBlock.Container -> when (block.kind) {
            "center", "centro" -> CompositionLocalProvider(LocalMdCenter provides true) {
                MarkdownView(block.children, ctx, Modifier.fillMaxWidth())
            }
            "spoiler", "details", "detalhes" -> Spoiler(block, ctx)
            // Só junta blocos, sem painel: para pôr vários ao lado de uma imagem.
            "group", "grupo" -> MarkdownView(block.children, ctx, Modifier.fillMaxWidth())
            else -> Column(
                Modifier.fillMaxWidth().pixelPanel(fill = st.panel.mix(Bl.Night, 0.3f), raised = false)
                    .padding(10.dp),
            ) {
                if (block.title.isNotEmpty()) {
                    MdText(block.title, ctx, size = Ts.Item, color = st.heading,
                        modifier = Modifier.padding(bottom = 6.dp))
                }
                MarkdownView(block.children, ctx)
            }
        }
    }
}

private fun MdContext.copyStyle(style: MdStyle) = MdContext(style, image, sprite, openUrl)

/**
 * Texto com marcação, com o contorno preto do PixelText.
 *
 * As cinco cópias precisam do MESMO layout, então todas levam as mesmas
 * marcas que mudam medida (negrito, itálico, código, os espaços das imagens);
 * só a de cima leva cor, fundo, imagem e link.
 */
@Composable
fun MdText(
    source: String,
    ctx: MdContext,
    modifier: Modifier = Modifier,
    size: Int = Ts.Body,
    color: Color = ctx.style.text,
) {
    val nodes = remember(source) { MdInlineParser.parse(source) }
    val slots = remember(nodes) {
        buildMap {
            fun walk(list: List<MdInline>) {
                for (n in list) when (n) {
                    is MdInline.Picture -> putIfAbsent(n.slot, n)
                    is MdInline.Bold -> walk(n.children)
                    is MdInline.Italic -> walk(n.children)
                    is MdInline.Strike -> walk(n.children)
                    is MdInline.Mark -> walk(n.children)
                    is MdInline.Link -> walk(n.children)
                    is MdInline.Tint -> walk(n.children)
                    else -> Unit
                }
            }
            walk(nodes)
        }
    }
    val pictures = if (slots.isEmpty()) emptyMap() else {
        produceState<Map<String, IconAnimation>>(emptyMap(), slots, ctx) {
            value = withContext(MediaDispatcher) {
                buildMap {
                    for (key in slots.values.map { it.key }.distinct()) {
                        val picture = if (key.startsWith("img:")) ctx.image(key.removePrefix("img:"))
                        else key.split(':').let { (_, kind, id) -> ctx.sprite(kind[0], id.toInt()) }
                            ?.let { IconAnimation(listOf(it), intArrayOf(0)) }
                        if (picture != null) put(key, picture)
                    }
                }
            }
        }.value
    }
    val st = ctx.style
    val painted = remember(nodes, pictures, color, st, ctx) { render(nodes, pictures, st, color, true, ctx) }
    val outline = remember(nodes, pictures, color, st, ctx) { render(nodes, pictures, st, color, false, ctx) }
    val density = LocalDensity.current
    val inline = remember(slots, pictures, density) { inlineContent(slots, pictures, density, draw = true) }
    val inlineBlank = remember(slots, pictures, density) { inlineContent(slots, pictures, density, draw = false) }
    val align = if (LocalMdCenter.current) TextAlign.Center else TextAlign.Start
    val fill = if (LocalMdCenter.current) Modifier.fillMaxWidth() else Modifier

    Box(modifier.then(fill)) {
        for ((dx, dy) in MD_OUTLINE) {
            Text(outline, color = Bl.Ink, fontFamily = PixelFont, fontSize = size.sp,
                inlineContent = inlineBlank, textAlign = align,
                modifier = fill.offset(dx.dp, dy.dp))
        }
        Text(painted, color = color, fontFamily = PixelFont, fontSize = size.sp,
            inlineContent = inline, textAlign = align, modifier = fill)
    }
}

private val MD_OUTLINE = listOf(-1 to 0, 1 to 0, 0 to -1, 0 to 1)

/**
 * O espaço de cada imagem na linha. Sem `{...}`, a altura de uma linha e pouco
 * e a largura na proporção da arte; com `{width=..}`/`{height=..}`, o tamanho
 * em dp (o `%` não vale no meio da frase).
 */
private fun inlineContent(
    slots: Map<String, MdInline.Picture>,
    pictures: Map<String, IconAnimation>,
    density: Density,
    draw: Boolean,
) = slots.mapNotNull { (slot, node) ->
    val picture = pictures[node.key] ?: return@mapNotNull null
    val first = picture.frames.first()
    val ratio = first.width.toFloat() / first.height.coerceAtLeast(1)
    val w = node.attrs.width?.takeUnless { it.percent }?.value
    val h = node.attrs.height?.takeUnless { it.percent }?.value
    val placeholder = if (w == null && h == null) {
        Placeholder((1.3f * ratio).em, 1.3f.em, PlaceholderVerticalAlign.TextCenter)
    } else with(density) {
        val width = w ?: (h!! * ratio)
        val height = h ?: (w!! / ratio)
        Placeholder(width.dp.toSp(), height.dp.toSp(), PlaceholderVerticalAlign.TextCenter)
    }
    slot to InlineTextContent(placeholder) {
        if (draw) PictureFrames(picture, Modifier.fillMaxSize())
    }
}.toMap()

private fun render(
    nodes: List<MdInline>,
    pictures: Map<String, IconAnimation>,
    st: MdStyle,
    base: Color,
    paint: Boolean,
    ctx: MdContext,
): AnnotatedString = buildAnnotatedString {
    fun AnnotatedString.Builder.emit(list: List<MdInline>) {
        for (n in list) when (n) {
            is MdInline.Text -> append(n.text)
            is MdInline.Bold -> withStyle(SpanStyle(fontWeight = FontWeight.Bold,
                color = if (paint && base == st.text) st.heading else Color.Unspecified)) { emit(n.children) }
            is MdInline.Italic -> withStyle(SpanStyle(fontStyle = FontStyle.Italic)) { emit(n.children) }
            is MdInline.Strike -> withStyle(SpanStyle(textDecoration = TextDecoration.LineThrough,
                color = if (paint) Bl.TextMuted else Color.Unspecified)) { emit(n.children) }
            is MdInline.Mark -> withStyle(SpanStyle(
                background = if (paint) st.accent.copy(alpha = 0.35f) else Color.Unspecified,
                color = if (paint) Bl.Text else Color.Unspecified)) { emit(n.children) }
            is MdInline.Code -> withStyle(SpanStyle(
                fontFamily = FontFamily.Monospace,
                fontSize = 0.85.em,
                background = if (paint) Bl.FrameFill else Color.Unspecified,
                color = if (paint) Bl.TextDim else Color.Unspecified,
            )) { append(" ${n.text} ") }
            is MdInline.Tint -> withStyle(SpanStyle(color = if (paint) n.color else Color.Unspecified)) {
                emit(n.children)
            }
            is MdInline.Link -> {
                val url = n.url
                // Só web e e-mail: o texto é de terceiros, e um `intent:` ou
                // `file:` abriria qualquer coisa no aparelho.
                val safe = url.startsWith("https://") || url.startsWith("http://") || url.startsWith("mailto:")
                if (paint && safe) {
                    withLink(LinkAnnotation.Clickable(url,
                        TextLinkStyles(SpanStyle(color = st.accent, textDecoration = TextDecoration.Underline)),
                    ) { ctx.openUrl(url) }) { emit(n.children) }
                } else if (paint) {
                    withStyle(SpanStyle(color = st.accent)) { emit(n.children) }
                } else {
                    emit(n.children)
                }
            }
            is MdInline.Picture -> {
                // O Compose recusa texto alternativo vazio, e `![](x.png)` é o
                // jeito comum de pôr um ícone na frase.
                if (n.key in pictures) appendInlineContent(n.slot, n.alt.ifEmpty { "￼" })
                else append(n.alt) // imagem que não existe: o texto alternativo
            }
        }
    }
    emit(nodes)
}

@Composable
private fun CodeBlock(block: MdBlock.Code) {
    Box(Modifier.fillMaxWidth().pixelPanel(fill = Bl.FrameFill, raised = false)) {
        Text(
            block.text,
            fontFamily = FontFamily.Monospace, fontSize = Ts.Small.sp, color = Bl.TextDim,
            softWrap = false,
            modifier = Modifier.horizontalScroll(rememberScrollState()).padding(10.dp),
        )
        if (block.lang.isNotEmpty()) {
            PixelText(block.lang, size = Ts.Tiny, color = Bl.TextMuted,
                modifier = Modifier.align(Alignment.TopEnd).padding(4.dp))
        }
    }
}

/**
 * Imagem do pacote, sem filtro e ampliada por número inteiro, para o pixel
 * continuar quadrado. A que não cabe encolhe para a largura, aí com filtro (o
 * contrário viraria um mosaico). GIF anima.
 *
 * Com `{width=..}`/`{height=..}`, o tamanho pedido (dp ou % da largura do
 * texto; só um dos dois mantém a proporção). `align` põe à esquerda, no
 * centro ou à direita; sem ele, vale o `::: center` em volta.
 */
@Composable
private fun MdImage(block: MdBlock.Image, ctx: MdContext, modifier: Modifier = Modifier.fillMaxWidth()) {
    val picture = produceState<IconAnimation?>(null, block.src, ctx) {
        value = withContext(MediaDispatcher) { ctx.image(block.src) }
    }.value
    if (picture == null) {
        if (block.alt.isNotEmpty()) MdText("[${block.alt}]", ctx, color = Bl.TextMuted, modifier = modifier)
        return
    }
    val first = picture.frames.first()
    val density = LocalDensity.current
    val horizontal = when (block.attrs.align ?: if (LocalMdCenter.current) "center" else "left") {
        "center" -> Alignment.CenterHorizontally
        "right" -> Alignment.End
        else -> Alignment.Start
    }
    BoxWithConstraints(modifier) {
        val maxPx = with(density) { (maxWidth - 4.dp).toPx() }.coerceAtLeast(1f)
        fun px(length: ImageLength) =
            if (length.percent) maxPx * length.value / 100f else with(density) { length.value.dp.toPx() }
        val ratio = first.height.toFloat() / first.width.coerceAtLeast(1)
        val attrs = block.attrs
        var wPx: Float
        var hPx: Float
        when {
            attrs.width != null && attrs.height != null -> { wPx = px(attrs.width); hPx = px(attrs.height) }
            attrs.width != null -> { wPx = px(attrs.width); hPx = wPx * ratio }
            attrs.height != null -> { hPx = px(attrs.height); wPx = hPx / ratio }
            else -> {
                val scale = (maxPx / first.width).let { if (it >= 1f) floor(it).coerceAtMost(4f) else it }
                wPx = first.width * scale
                hPx = first.height * scale
            }
        }
        if (wPx > maxPx) { hPx *= maxPx / wPx; wPx = maxPx }
        val w = with(density) { wPx.toDp() }
        val h = with(density) { hPx.toDp() }
        Column(Modifier.fillMaxWidth(), horizontalAlignment = horizontal) {
            PictureFrames(picture, Modifier.size(w + 4.dp, h + 4.dp).framePanel().padding(2.dp),
                filter = if (wPx >= first.width) FilterQuality.None else FilterQuality.Medium)
            if (block.alt.isNotEmpty()) {
                PixelText(block.alt, size = Ts.Tiny, color = Bl.TextMuted,
                    modifier = Modifier.padding(top = 4.dp))
            }
        }
    }
}

/** Os avisos do GitHub, com os nomes em português também. */
@Composable
private fun Callout(block: MdBlock.Callout, ctx: MdContext) {
    val (label, tint) = when (block.kind) {
        "tip", "dica" -> "Dica" to Color(0xFF5FB86A)
        "important", "importante" -> "Importante" to Color(0xFFA77BE0)
        "warning", "aviso" -> "Aviso" to Color(0xFFE8B23F)
        "caution", "cuidado", "danger", "perigo" -> "Cuidado" to Bl.Bad
        else -> "Nota" to Color(0xFF6FA3E8)
    }
    Column(
        Modifier.fillMaxWidth().pixelPanel(fill = tint.mix(ctx.style.panel, 0.78f)).padding(10.dp),
    ) {
        PixelText(block.title.ifEmpty { label }, size = Ts.Item, color = tint.mix(Color.White, 0.35f),
            modifier = Modifier.padding(bottom = if (block.children.isEmpty()) 0.dp else 6.dp))
        MarkdownView(block.children, ctx)
    }
}

@Composable
private fun Spoiler(block: MdBlock.Container, ctx: MdContext) {
    var open by remember { mutableStateOf(false) }
    Column(Modifier.fillMaxWidth().pixelPanel(fill = ctx.style.panel.mix(Bl.Night, 0.25f))) {
        Row(
            Modifier.fillMaxWidth().pixelClickable { open = !open }.padding(10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            PixelText(if (open) "-" else "+", size = Ts.Item, color = ctx.style.accent,
                modifier = Modifier.width(18.dp))
            MdText(block.title.ifEmpty { "Mostrar" }, ctx, size = Ts.Item, color = ctx.style.heading,
                modifier = Modifier.weight(1f))
        }
        if (open) MarkdownView(block.children, ctx, Modifier.padding(start = 10.dp, end = 10.dp, bottom = 10.dp))
    }
}

@Composable
private fun MdList(block: MdBlock.ListBlock, ctx: MdContext) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        block.items.forEachIndexed { n, item ->
            Row {
                Box(Modifier.width(if (block.ordered) 30.dp else 20.dp).padding(top = 2.dp)) {
                    when {
                        item.checked != null -> CheckBox(item.checked, ctx.style.accent)
                        block.ordered -> PixelText("${block.start + n}.", size = Ts.Body, color = ctx.style.accent)
                        else -> Canvas(Modifier.padding(top = 6.dp, start = 4.dp).size(6.dp)) {
                            drawRect(Bl.Ink)
                            val b = 1.dp.toPx()
                            drawRect(ctx.style.accent, Offset(b, b), Size(size.width - 2 * b, size.height - 2 * b))
                        }
                    }
                }
                MarkdownView(item.children, ctx, Modifier.weight(1f))
            }
        }
    }
}

@Composable
private fun CheckBox(on: Boolean, accent: Color) {
    Canvas(Modifier.padding(top = 1.dp).size(14.dp)) {
        val b = 2.dp.toPx()
        drawRect(Bl.FrameBorder)
        drawRect(Bl.FrameFill, Offset(b, b), Size(size.width - 2 * b, size.height - 2 * b))
        if (on) drawRect(accent, Offset(2 * b, 2 * b), Size(size.width - 4 * b, size.height - 4 * b))
    }
}

@Composable
private fun MdTable(block: MdBlock.Table, ctx: MdContext) {
    val cols = block.header.size.coerceAtLeast(1)
    Column(Modifier.fillMaxWidth().framePanel(border = Bl.FrameBorder, fill = ctx.style.panel.mix(Bl.Night, 0.3f))
        .padding(2.dp)) {
        val rows = listOf(block.header) + block.rows
        rows.forEachIndexed { r, row ->
            Row(
                Modifier.fillMaxWidth().then(
                    if (r == 0) Modifier.pixelPanel(fill = ctx.style.accent.mix(Bl.Panel, 0.75f), outline = Color.Transparent)
                    else if (r % 2 == 0) Modifier.framePanel(border = Color.Transparent, fill = Color(0x14FFFFFF))
                    else Modifier
                ),
            ) {
                for (c in 0 until cols) {
                    val align = block.aligns.getOrElse(c) { TextAlign.Start }
                    Box(Modifier.weight(1f).padding(horizontal = 6.dp, vertical = 5.dp),
                        contentAlignment = when (align) {
                            TextAlign.Center -> Alignment.TopCenter
                            TextAlign.End -> Alignment.TopEnd
                            else -> Alignment.TopStart
                        }) {
                        MdText(row.getOrElse(c) { "" }, ctx, size = Ts.Small,
                            color = if (r == 0) ctx.style.heading else ctx.style.text)
                    }
                }
            }
        }
    }
}
