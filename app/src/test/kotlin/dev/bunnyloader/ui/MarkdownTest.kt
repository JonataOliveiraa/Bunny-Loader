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

    @Test fun escapedFormattingAndEmptyTextAreLiteral() {
        assertEquals(emptyList<MdBlock>(), Markdown.parse(" \n\n"))
        assertEquals(listOf(MdInline.Text("*literal*")), MdInlineParser.parse("\\*literal\\*"))
    }
}
