package dev.bunnyloader.ui

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MarkdownTest {
    @Test fun numberedItemsRemainSeparate() {
        val list = Markdown.parse("1. Primeiro\n2. Segundo\n3. Terceiro").single() as MdBlock.ListBlock
        assertEquals(3, list.items.size)
        assertEquals(listOf("Primeiro", "Segundo", "Terceiro"), list.items.map {
            (it.children.single() as MdBlock.Paragraph).text
        })
    }

    @Test fun listsCanStartAfterOneAndContainNestedLists() {
        val list = Markdown.parse("7. Sete\n   - Filho\n8. Oito").single() as MdBlock.ListBlock
        assertEquals(7, list.start)
        assertEquals(2, list.items.size)
        assertTrue(list.items[0].children[1] is MdBlock.ListBlock)
    }

    @Test fun tasksAndCalloutsKeepTheirMeaning() {
        val blocks = Markdown.parse("- [x] Feita\n- [ ] Pendente\n\n> [!TIP] Dica\n> Texto")
        val list = blocks[0] as MdBlock.ListBlock
        assertEquals(listOf(true, false), list.items.map { it.checked })
        val tip = blocks[1] as MdBlock.Callout
        assertEquals("tip", tip.kind)
        assertEquals("Dica", tip.title)
    }

    @Test fun missingClosingFenceStillProducesCode() {
        val block = Markdown.parse("```js\nconst x = 1;").single() as MdBlock.Code
        assertEquals("const x = 1;", block.text)
    }

    @Test fun picturesAndItemIconsCanBeUsedInsideText() {
        val nodes = MdInlineParser.parse("Pegue ![](content/item.png) e [i:757].")
        assertEquals(listOf("img:content/item.png", "spr:i:757"),
            nodes.filterIsInstance<MdInline.Picture>().map { it.key })
    }

    @Test fun imageAttributesGiveSizeAndPlace() {
        val image = Markdown.parse("![Espada](a.gif){width=50% align=right}").single() as MdBlock.Image
        assertEquals("a.gif", image.src)
        assertEquals(ImageLength(50f, percent = true), image.attrs.width)
        assertEquals("right", image.attrs.align)

        val floating = Markdown.parse("![](a.png) {float=esquerda h=64px}\nTexto ao lado.")
        val attrs = (floating[0] as MdBlock.Image).attrs
        assertEquals("left", attrs.float)
        assertEquals(ImageLength(64f), attrs.height)
        assertTrue(floating[1] is MdBlock.Paragraph)

        assertEquals(ImageAttrs(ImageLength(64f), ImageLength(32f), align = "center"),
            ImageAttrs.parse("64x32 centro"))
        assertEquals(ImageAttrs(), ImageAttrs.parse("width=abc float=center"))
    }

    @Test fun inlinePicturesKeepTheirOwnSize() {
        val nodes = MdInlineParser.parse("A ![](m.png){h=24} e [i:757]{32} {texto}")
        val pictures = nodes.filterIsInstance<MdInline.Picture>()
        assertEquals(ImageLength(24f), pictures[0].attrs.height)
        assertEquals(ImageLength(32f), pictures[1].attrs.width)
        assertEquals("[i:757]", pictures[1].alt)
        assertTrue(pictures[0].slot != pictures[0].key)
        assertTrue((nodes.last() as MdInline.Text).text.endsWith("{texto}"))
    }

    @Test fun escapedFormattingAndEmptyTextAreLiteral() {
        assertEquals(emptyList<MdBlock>(), Markdown.parse(" \n\n"))
        assertEquals(listOf(MdInline.Text("*literal*")), MdInlineParser.parse("\\*literal\\*"))
    }
}
