package dev.bunnyloader.mods

import java.io.File
import java.nio.file.Files
import java.util.zip.ZipFile
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Assume.assumeNoException
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder

class ModSourcesTest {
    @get:Rule val temporary = TemporaryFolder()
    private val json = Json { ignoreUnknownKeys = true; encodeDefaults = true }

    private fun sources() = ModSources(File(temporary.root, "sources"))
    private fun output(name: String = "Teste.bl") = File(temporary.root, name)

    @Test fun creationProducesFormatTwoSourcesAndKeepsIdentityAcrossReadsAndBuilds() {
        val sources = sources()
        val created = sources.create("Teste", "Meu mod", "Autora")
        val manifest = created.manifest!!
        assertEquals("Teste", created.key)
        assertNull(created.error)
        assertTrue(manifest.hasValidUid)
        assertEquals("Teste", manifest.id)
        assertEquals("Meu mod", manifest.name)
        assertEquals("Autora", manifest.author)
        assertEquals("1.0.0", manifest.version)
        assertEquals(3, manifest.blVersion)
        assertEquals("mod", manifest.type)
        assertEquals(listOf("Teste.dll", "Teste.cs"), manifest.entryNames)
        assertTrue(sources.readText("Teste", "Teste.cs")
            .contains("public class Teste : Mod"))
        for (folder in listOf("Assets", "Common", "Content", "Localization")) {
            assertTrue(File(sources.root, "Teste/$folder").isDirectory)
        }
        assertTrue(sources.readText("Teste", "description.md").contains("Meu mod"))
        assertFalse(sources.readText("Teste", "manifest.json").contains("\"entry\""))
        assertEquals(manifest.uid, sources.list().single().manifest!!.uid)
        assertEquals(manifest.uid, sources.build("Teste", output()).uid)
        assertEquals(manifest.uid, sources.build("Teste", output()).uid)
        assertNotEquals(manifest.uid, sources.create("Outro", "Outro", "Autor").manifest!!.uid)
    }

    @Test fun optionalSwordCreatesAutoloadedWeaponTextureLocalizationAndBuildArchive() {
        val sprite = File("src/main/assets/templates/basic-sword.png")
        assertTrue("A imagem da espada deve estar nos assets do aplicativo", sprite.isFile)
        val sources = ModSources(File(temporary.root, "sources")) { sprite.inputStream() }
        val manifest = sources.create("SwordMod", "Sword Mod", "Author", "  CopperSword  ", "zh").manifest!!
        val weapon = "Content/Items/CopperSword"
        val code = sources.readText("SwordMod", "$weapon.cs")
        assertTrue(code.contains("namespace SwordMod.Content.Items;"))
        assertTrue(code.contains("public class CopperSword : ModItem"))
        assertTrue(code.contains("Item.DamageType = DamageClass.Melee;"))
        assertTrue(code.contains("Item.useStyle = ItemUseStyleID.Swing;"))
        assertTrue(code.contains("CreateRecipe()"))
        assertTrue(code.contains(".AddIngredient(ItemID.DirtBlock, 10)"))
        for (culture in listOf("en-US", "zh-Hans")) {
            assertTrue(sources.readText("SwordMod", "Localization/${culture}_Mods.SwordMod.hjson")
                .contains("DisplayName: Copper Sword"))
        }
        assertEquals("Sword Mod", manifest.nameFor("zh"))
        assertEquals("Sword Mod", manifest.localizedNames["zh-Hans"])
        assertEquals("Sword Mod", json.decodeFromString<ModManifest>(sources.readText("SwordMod", "manifest.json")).nameFor("zh"))
        val readDescription: (String) -> String? = { path ->
            File(sources.root, "SwordMod/$path").takeIf(File::isFile)?.readText(Charsets.UTF_8)
        }
        assertTrue(readModDescription(manifest, "zh", readDescription)
            .contains("# Sword Mod\n\n这是 Sword Mod 的 Mod 源码项目。"))
        assertTrue(readModDescription(manifest, "en", readDescription)
            .contains("# Sword Mod\n\nThis is the source project for the Sword Mod mod."))
        assertTrue(File(sources.root, "SwordMod/$weapon.png").readBytes().contentEquals(sprite.readBytes()))
        assertEquals(manifest.uid, sources.build("SwordMod", output()).uid)
        ZipFile(output()).use { zip ->
            for (path in listOf("$weapon.cs", "$weapon.png", "Localization/en-US_Mods.SwordMod.hjson",
                "Localization/zh-Hans_Mods.SwordMod.hjson", "description.md", "description_zh-Hans.md", "SwordMod.cs")) {
                assertNotNull("Não empacotou $path", zip.getEntry(path))
            }
            assertTrue(zip.getInputStream(zip.getEntry("$weapon.png")).readBytes().contentEquals(sprite.readBytes()))
        }
        assertEquals(manifest.uid, sources.list().single().manifest!!.uid)
    }

    @Test fun optionalSwordValidationRejectsInvalidNamesBeforeCreatingDirectory() {
        val sources = ModSources(File(temporary.root, "sources")) { byteArrayOf(137.toByte(), 80, 78, 71).inputStream() }
        for (name in listOf("../Sword", "Sword-1", "1Sword", "Mágica", "class", "s".repeat(65))) {
            rejects("Nome da espada inválido") { sources.create("SwordMod", "Sword Mod", "Author", name) }
        }
        assertFalse(sources.root.exists())
        rejects("imagem") { sources().create("SwordMod", "Sword Mod", "Author", "Sword") }
        assertFalse(sources.root.exists())
        sources.create("Empty", "No weapon", "Author", "  ")
        assertFalse(File(sources.root, "Empty/Content/Items").exists())
        assertFalse(File(sources.root, "Empty/Localization/en-US_Mods.Empty.hjson").exists())
        assertEquals("No weapon", sources.list().single().manifest!!.nameFor("pt-BR"))
        assertTrue(sources.readText("Empty", "description_pt-BR.md")
            .contains("# No weapon\n\nEste é o projeto-fonte do mod No weapon."))
        assertTrue(sources.files("Empty").none { it.contains("Sword") })
    }

    @Test fun csharpKeywordsStillProduceValidClassNames() {
        val sources = sources()
        sources.create("class", "Classe", "Autor")
        val code = sources.readText("class", "class.cs")
        assertTrue(code.contains("namespace @class;"))
        assertTrue(code.contains("public class classMod : Mod"))
    }

    @Test fun duplicateIdsAreRejectedIgnoringCaseWithoutChangingOriginal() {
        val sources = sources()
        val original = sources.create("Teste", "Original", "Autor")
        rejects("Já existe") { sources.create("tEsTe", "Outro", "Autor") }
        assertEquals(listOf(original), sources.list())
        File(sources.root, "OUTRO").writeText("arquivo que ocupa o nome")
        rejects("Já existe") { sources.create("outro", "Outro", "Autor") }
    }

    @Test fun identifiersAndRequiredCreationFieldsAreValidatedBeforeCreatingDirectories() {
        val sources = sources()
        for (id in listOf("", "1Teste", "Teste-mod", "../Teste", "a".repeat(65), "Módulo")) {
            rejects("ID inválido") { sources.create(id, "Teste", "Autor") }
        }
        rejects("nome") { sources.create("Teste", " ", "Autor") }
        rejects("autor") { sources.create("Teste", "Teste", " ") }
        assertFalse(sources.root.exists())
        sources.create("A" + "_".repeat(63), "Teste", "Autor")
    }

    @Test fun editedAndNewFilesRoundTripThroughStandardArchiveWithoutExcludedFiles() {
        val sources = sources()
        sources.create("Teste", "Teste", "Autor")
        val edited = "public class Teste : Mod { public override void Load() => Logger.Info(\"olá\"); }\n"
        sources.writeText("Teste", "Teste.cs", edited)
        sources.writeText("Teste", "Localization/pt-BR.hjson", "{ saudacao: Olá }")
        sources.writeText("Teste", "docs/novo.txt", "Novo arquivo 🐰")
        val project = File(sources.root, "Teste")
        File(project, ".git").mkdir()
        File(project, ".git/config").writeText("segredo")
        File(project, "Content/.cache").mkdir()
        File(project, "Content/.cache/gerado.js").writeText("ignorar")
        File(project, "bin").mkdir()
        File(project, "bin/Teste.dll").writeText("ignorar")
        File(project, "obj").mkdir()
        File(project, "obj/gerado.cs").writeText("ignorar")
        File(project, "Teste.csproj").writeText("ignorar")
        File(project, "build").mkdir()
        File(project, "build/antigo.bl").writeText("ignorar")
        File(project, "Assets/imagem.png").writeBytes(byteArrayOf(1, 2, 3))
        assertEquals(edited, sources.readText("Teste", "Teste.cs"))
        assertTrue("docs/novo.txt" in sources.files("Teste"))
        assertFalse(sources.files("Teste").any { it.endsWith(".png") || it.contains(".git") })
        sources.build("Teste", output())
        ZipFile(output()).use { zip ->
            val paths = zip.entries().asSequence().map { it.name }.toList()
            assertTrue("manifest.json" in paths)
            assertTrue("description.md" in paths)
            assertTrue("Assets/imagem.png" in paths)
            assertFalse(paths.any { it.startsWith("bin/") || it.startsWith("obj/") || it.endsWith(".csproj") })
            assertFalse(paths.any { it.startsWith("build/") || it.contains("/.") || it.startsWith(".") })
            assertEquals(edited, zip.getInputStream(zip.getEntry("Teste.cs")).reader(Charsets.UTF_8).readText())
            assertEquals("Novo arquivo 🐰", zip.getInputStream(zip.getEntry("docs/novo.txt")).reader(Charsets.UTF_8).readText())
            assertEquals(sources.list().single().manifest,
                json.decodeFromString<ModManifest>(zip.getInputStream(zip.getEntry("manifest.json")).reader().readText()))
        }
    }

    @Test fun malformedProjectFolderCanStillBeOpenedThroughTheSafeDirectoryLookup() {
        val sources = sources()
        File(sources.root, "Bad-id").mkdirs()
        assertEquals(File(sources.root, "Bad-id").canonicalFile, sources.projectDirectoryPath("Bad-id").canonicalFile)
        rejects("inválida") { sources.build("Bad-id", output()) }
        rejects("inválida") { sources.projectDirectoryPath("../outside") }
    }

    @Test fun listingKeepsMalformedAndMissingManifestProjectsVisibleWithErrors() {
        val sources = sources()
        assertTrue(sources.list().isEmpty())
        sources.create("Bom", "Mod válido", "Autor")
        File(sources.root, "SemManifesto").mkdir()
        File(sources.root, "Quebrado").mkdir()
        File(sources.root, "Quebrado/manifest.json").writeText("{ não é json }")
        File(sources.root, "nome-invalido").mkdir()
        File(sources.root, ".oculto").mkdir()
        val listed = sources.list().associateBy { it.key }
        assertEquals(setOf("Bom", "SemManifesto", "Quebrado", "nome-invalido"), listed.keys)
        assertNull(listed.getValue("Bom").error)
        assertNotNull(listed.getValue("SemManifesto").error)
        assertNotNull(listed.getValue("Quebrado").error)
        assertNotNull(listed.getValue("nome-invalido").error)
        assertNull(listed.getValue("Quebrado").manifest)
        rejects("manifest.json") { sources.build("Quebrado", output()) }
    }

    @Test fun buildValidatesIdentityNameVersionFormatAndTypeAndPreservesOldOutput() {
        val sources = sources()
        val valid = sources.create("Teste", "Teste", "Autor").manifest!!
        val oldOutput = "pacote anterior".toByteArray()
        output().writeBytes(oldOutput)
        val invalid = listOf(
            valid.copy(uid = "../fora") to "UID",
            valid.copy(id = "9invalido") to "ID",
            valid.copy(name = " ") to "nome",
            valid.copy(version = "") to "versão",
            valid.copy(version = "1\n2") to "versão",
            valid.copy(blVersion = 2) to "blVersion",
            valid.copy(blVersion = 4) to "blVersion",
            valid.copy(type = "banana") to "tipo",
        )
        for ((manifest, error) in invalid) {
            sources.writeText("Teste", "manifest.json", json.encodeToString(manifest))
            rejects(error) { sources.build("Teste", output()) }
            assertTrue(output().readBytes().contentEquals(oldOutput))
            assertNotNull(sources.list().single().error)
        }
        assertFalse(temporary.root.listFiles()!!.any { it.name.startsWith(".bunny-mod-") })
    }

    private fun pack(type: String): ModManifest {
        val manifest = ModManifest(uid = "19e3900c-83a2-4e9f-9724-1a38f82dcae9", id = "meupacote", name = "Meu pacote",
            version = "1.0.0", author = "Autor", type = type, blVersion = ModRepository.BL_VERSION)
        val folder = File(temporary.root, "sources/meupacote").apply { mkdirs() }
        File(folder, "manifest.json").writeText(json.encodeToString(manifest))
        return manifest
    }

    @Test fun texturePackBuildsWithoutModClassOnceItHasImages() {
        val sources = sources()
        val manifest = pack("texture")
        rejects("Images") { sources.build("meupacote", output()) }
        assertNotNull(sources.list().single().error)
        val image = File(sources.root, "meupacote/Images/UI/Item_1.png").apply { parentFile!!.mkdirs() }
        image.writeBytes(byteArrayOf(1, 2, 3))
        assertNull(sources.list().single().error)
        assertEquals(manifest.uid, sources.build("meupacote", output()).uid)
        ZipFile(output()).use { zip ->
            assertNotNull(zip.getEntry("manifest.json"))
            assertTrue(zip.getInputStream(zip.getEntry("Images/UI/Item_1.png")).readBytes().contentEquals(byteArrayOf(1, 2, 3)))
        }
    }

    @Test fun fontPackBuildsWithoutModClassOnceItHasFonts() {
        val sources = sources()
        val manifest = pack("font")
        rejects("Fonts") { sources.build("meupacote", output()) }
        val font = File(sources.root, "meupacote/Fonts/Mouse_Text.json").apply { parentFile!!.mkdirs() }
        font.writeText("{}")
        File(font.parentFile, "Mouse_Text.0.png").writeBytes(byteArrayOf(1))
        assertEquals(manifest.uid, sources.build("meupacote", output()).uid)
        ZipFile(output()).use { zip ->
            assertNotNull(zip.getEntry("Fonts/Mouse_Text.json"))
            assertNotNull(zip.getEntry("Fonts/Mouse_Text.0.png"))
        }
    }

    @Test fun entryIsTheModClassFileAtTheRoot() {
        val sources = sources()
        val valid = sources.create("Teste", "Teste", "Autor").manifest!!
        File(sources.root, "Teste/Teste.cs").delete()
        sources.writeText("Teste", "Common/Teste.cs", "public class Teste : Mod { }")
        rejects("Teste.cs") { sources.build("Teste", output()) }
        assertNotNull(sources.list().single().error)
        assertFalse(output().exists())
        sources.writeText("Teste", "Teste.cs", "public class Teste : Mod { }")
        assertEquals(valid.uid, sources.build("Teste", output()).uid)
    }

    @Test fun unknownManifestFieldsAndOriginalManifestBytesArePreservedInArchive() {
        val sources = sources()
        val manifest = sources.create("Teste", "Teste", "Autor").manifest!!
        sources.writeText("Teste", "manifest.json", """
            {"uid":"${manifest.uid}","id":"Teste","name":"Teste","version":"1.0",
             "type":"mod","blVersion":3,"futureOption":{"value":true}}
        """.trimIndent())
        sources.build("Teste", output())
        ZipFile(output()).use { zip ->
            val text = zip.getInputStream(zip.getEntry("manifest.json")).reader().readText()
            assertTrue(text.contains("\"futureOption\""))
            assertEquals(sources.readText("Teste", "manifest.json"), text)
            assertEquals("Teste", json.decodeFromString<ModManifest>(text).id)
        }
    }

    @Test fun pathsCannotTraverseProjectsOrAccessHiddenFiles() {
        val sources = sources()
        sources.create("Teste", "Teste", "Autor")
        val forbidden = listOf("../fora.txt", "content/../../fora.txt", "/fora.txt", "C:/fora.txt",
            "\\fora.txt", "content\\..\\..\\fora.txt", "Content/.oculto/a.js", ".git/config.txt", "a\u0000.js", "", ".")
        for (path in forbidden) {
            rejects { sources.readText("Teste", path) }
            rejects { sources.writeText("Teste", path, "texto") }
        }
        rejects { sources.files("../Teste") }
        rejects { sources.readText("../Teste", "manifest.json") }
        rejects { sources.writeText("../Teste", "a.js", "texto") }
        rejects { sources.build("../Teste", output()) }
        assertFalse(File(sources.root, "fora.txt").exists())
        assertFalse(File(temporary.root, "fora.txt").exists())
    }

    @Test fun editorEnforcesUtf8ByteLimitAndTextExtensions() {
        val sources = sources()
        sources.create("Teste", "Teste", "Autor")
        val limit = "é".repeat(512 * 1024)
        sources.writeText("Teste", "limite.TXT", limit)
        assertEquals(limit, sources.readText("Teste", "limite.TXT"))
        rejects("1 MB") { sources.writeText("Teste", "limite.TXT", limit + "é") }
        assertEquals(limit, sources.readText("Teste", "limite.TXT"))
        rejects("Extensão") { sources.writeText("Teste", "imagem.png", "texto") }
        File(sources.root, "Teste/grande.cs").writeText("a".repeat(1024 * 1024 + 1))
        rejects("1 MB") { sources.readText("Teste", "grande.cs") }
        assertFalse("grande.cs" in sources.files("Teste"))
        File(sources.root, "Teste/invalido.txt").writeBytes(byteArrayOf(0xc3.toByte(), 0x28))
        rejects("UTF-8") { sources.readText("Teste", "invalido.txt") }
    }

    @Test fun outputCannotBeAnywhereUnderSourceRoot() {
        val sources = sources()
        sources.create("Teste", "Teste", "Autor")
        for (file in listOf(File(sources.root, "saida.bl"), File(sources.root, "Teste/saida.bl"),
            File(sources.root, "Teste/Content/saida.bl"))) {
            rejects("fora") { sources.build("Teste", file) }
            assertFalse(file.exists())
        }
        rejects(".bl") { sources.build("Teste", output("saida.zip")) }
    }

    @Test fun symlinksAreRejectedEvenWhenTheyPointInsideTheSameProject() {
        val sources = sources()
        sources.create("Teste", "Teste", "Autor")
        val project = File(sources.root, "Teste")
        val link = File(project, "atalho")
        try { Files.createSymbolicLink(link.toPath(), File(project, "Content").toPath()) }
        catch (e: Exception) { assumeNoException("O ambiente não permite criar links simbólicos", e); return }
        rejects("simbólicos") { sources.readText("Teste", "atalho/Teste.cs") }
        rejects("simbólicos") { sources.writeText("Teste", "atalho/Teste.cs", "não sobrescrever") }
        output().writeText("anterior")
        rejects("simbólicos") { sources.build("Teste", output()) }
        assertEquals("anterior", output().readText())
        assertTrue(sources.readText("Teste", "Teste.cs").contains(": Mod"))
    }

    private fun rejects(fragment: String? = null, action: () -> Unit) {
        try { action() }
        catch (e: IllegalArgumentException) {
            assertTrue("Mensagem ausente", !e.message.isNullOrBlank())
            if (fragment != null) assertTrue("Esperado '$fragment' em '${e.message}'", e.message!!.contains(fragment))
            return
        }
        fail("A operação deveria ter sido recusada")
    }
}
