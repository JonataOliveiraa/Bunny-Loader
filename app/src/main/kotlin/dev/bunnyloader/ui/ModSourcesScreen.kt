package dev.bunnyloader.ui

import android.content.ClipData
import android.content.Intent
import android.provider.DocumentsContract
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.bunnyloader.R

@Composable
fun ModSourcesScreen(shell: Shell, onBack: () -> Unit) {
    val context = LocalContext.current
    var creating by rememberSaveable { mutableStateOf(false) }
    var settings by rememberSaveable { mutableStateOf(false) }
    var query by rememberSaveable { mutableStateOf("") }
    var exportProject by rememberSaveable { mutableStateOf<String?>(null) }
    var localError by remember { mutableStateOf<String?>(null) }
    val busy = shell.sourceBusy
    BackHandler { onBack() }
    LaunchedEffect(Unit) { shell.refreshSources() }
    val exporter = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("application/zip")) { uri ->
        val key = exportProject
        exportProject = null
        if (uri != null && key != null) shell.buildSource(key, false, uri)
    }

    Column(Modifier.fillMaxSize().padding(horizontal = EdgePad)) {
        Row(Modifier.fillMaxWidth().padding(top = 10.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.semantics { contentDescription = tr("Voltar") }.pixelClickable(onBack).padding(8.dp)) {
                PixelIcon(R.drawable.ic_seta_esq, 18.dp)
            }
            PixelText(tr("Código-fonte dos mods"), size = Ts.Head, modifier = Modifier.padding(start = 8.dp))
        }
        if (busy) PixelText(tr("Processando..."), size = Ts.Small, color = Bl.TextFaint)
        (localError ?: shell.sourceMessage)?.takeIf { it.isNotBlank() }?.let {
            PixelText(it, size = Ts.Small, color = Bl.TextDim, modifier = Modifier.padding(bottom = 8.dp))
        }
        SourceSearchField(query, { query = it })
        Row(Modifier.fillMaxWidth().padding(vertical = 8.dp), horizontalArrangement = Arrangement.spacedBy(7.dp)) {
            Box(Modifier.semantics { contentDescription = tr("Atualizar lista") }
                .pixelClickable { if (!busy) shell.refreshSources() }.padding(10.dp)) {
                PixelIcon(R.drawable.ic_refresh, 20.dp)
            }
            PixelButton(tr("Locais de código-fonte"), { settings = true }, Modifier.weight(1f), fontSize = Ts.Tiny, shadow = false)
            PixelButton(tr("Criar mod"), { if (!busy) creating = true }, Modifier.weight(1f), fontSize = Ts.Tiny, shadow = false)
        }
        val filtered = filterSourceProjects(shell.sourceProjects, query, UiLocalization.language)
        if (filtered.isEmpty() && !busy) {
            PixelText(if (shell.sourceProjects.isEmpty()) tr("Nenhum projeto. Crie seu primeiro mod.")
                else tr("Nenhum projeto corresponde à busca."), size = Ts.Body)
        }
        LazyColumn(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(7.dp),
            contentPadding = PaddingValues(bottom = 12.dp)) {
            items(filtered, key = { "${it.sourceRoot}:${it.key}" }) { entry ->
                PixelCard(Modifier.fillMaxWidth(), shadow = false) {
                    Column(Modifier.fillMaxWidth().padding(10.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
                        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(7.dp)) {
                            Column(Modifier.weight(1f)) {
                                PixelText(entry.manifest?.nameFor(UiLocalization.language) ?: entry.name, size = Ts.Item)
                                PixelText(entry.manifest?.id ?: entry.name, size = Ts.Tiny, color = Bl.TextFaint)
                                PixelText(entry.sourceRoot.ifBlank { shell.modSources.root.absolutePath }, size = Ts.Tiny, color = Bl.TextFaint)
                                entry.error?.let { PixelText(it, size = Ts.Tiny, color = Bl.Bad) }
                            }
                            Box(Modifier.semantics { contentDescription = tr("Abrir pasta do projeto") }
                                .pixelClickable {
                                    if (!busy) {
                                        try {
                                            val uri = shell.sourceFolderUri(entry.key)
                                            val intent = Intent(Intent.ACTION_VIEW).apply {
                                                setDataAndType(uri, DocumentsContract.Document.MIME_TYPE_DIR)
                                                clipData = ClipData.newRawUri("Mod source", uri)
                                                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                                                    or Intent.FLAG_GRANT_PREFIX_URI_PERMISSION)
                                            }
                                            context.startActivity(intent)
                                            localError = null
                                        } catch (_: Exception) {
                                            localError = tr("Não foi possível abrir a pasta. Use um gerenciador de arquivos compatível com documentos do Android.")
                                        }
                                    }
                                }.padding(7.dp), contentAlignment = Alignment.Center) {
                                PixelIcon(R.drawable.ic_folder, 22.dp)
                            }
                        }
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(7.dp)) {
                            PixelButton(tr("Construir e instalar"), {
                                if (!busy && entry.error == null) shell.buildSource(entry.key, true)
                            }, Modifier.weight(1f), fontSize = Ts.Tiny, shadow = false)
                            PixelButton(tr("Exportar .bl"), {
                                if (!busy && entry.error == null) {
                                    exportProject = entry.key
                                    exporter.launch("${entry.manifest?.id ?: "mod"}.bl")
                                }
                            }, Modifier.weight(1f), fontSize = Ts.Tiny, shadow = false)
                        }
                    }
                }
            }
        }
    }
    var id by rememberSaveable { mutableStateOf("") }
    var name by rememberSaveable { mutableStateOf("") }
    var author by rememberSaveable { mutableStateOf("") }
    var sword by rememberSaveable { mutableStateOf("") }
    var location by rememberSaveable { mutableStateOf("") }
    if (creating) {
        PixelDialog(tr("Criar mod"), onDismiss = { creating = false }) {
            Column(Modifier.heightIn(max = 480.dp).verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SourceField(tr("Nome interno (letras, números e _)"), id) { id = it }
                SourceField(tr("Nome do mod"), name) { name = it }
                SourceField(tr("Autor"), author) { author = it }
                SourceField(tr("Nome da espada básica (opcional)"), sword) { sword = it }
                PixelText(tr("Local do projeto"), size = Ts.Small)
                for (folder in shell.sourceFolders) {
                    PixelButton((if (location == folder.id) "✓ " else "") + folder.label,
                        { location = folder.id }, Modifier.fillMaxWidth(), fontSize = Ts.Tiny, shadow = false)
                }
                PixelButton(tr("Criar"), {
                    if (!busy) {
                        creating = false
                        shell.createSource(id, name, author, sword, UiLocalization.language, location) {
                            query = ""
                            id = ""; name = ""; author = ""; sword = ""
                        }
                    }
                }, Modifier.fillMaxWidth(), fontSize = Ts.Small, shadow = false)
            }
        }
    }
    if (settings) PixelDialog(tr("Locais de código-fonte"), onDismiss = { settings = false }) {
        SourceLocationsSettings(shell)
    }
}

/** Shared by Settings and the sources list. Removes registrations, never source files. */
@Composable
fun SourceLocationsSettings(shell: Shell) {
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocumentTree()) { uri ->
        if (uri != null) shell.addSourceFolder(uri)
    }
    Column(Modifier.heightIn(max = 360.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(8.dp)) {
        PixelText(tr("A pasta padrão e todos os locais adicionados são carregados juntos."), size = Ts.Small, color = Bl.TextDim)
        for (folder in shell.sourceFolders) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    if (folder.id.isEmpty()) PixelText(tr("Pasta padrão"), size = Ts.Small)
                    PixelText(folder.label, size = Ts.Tiny, color = Bl.TextFaint)
                }
                if (folder.id.isNotEmpty()) PixelButton(tr("Remover local"), {
                    if (!shell.sourceBusy) shell.removeSourceFolder(folder.id)
                }, fontSize = Ts.Tiny, shadow = false)
            }
        }
        PixelButton(tr("Adicionar pasta"), { if (!shell.sourceBusy) picker.launch(null) },
            Modifier.fillMaxWidth(), fontSize = Ts.Small, shadow = false)
        shell.sourceMessage?.takeIf { it.isNotBlank() }?.let { PixelText(it, size = Ts.Tiny, color = Bl.TextDim) }
    }
}

internal fun filterSourceProjects(
    projects: List<dev.bunnyloader.mods.ModSources.Project>, query: String, language: String,
): List<dev.bunnyloader.mods.ModSources.Project> {
    val term = query.trim()
    if (term.isEmpty()) return projects
    return projects.filter { entry ->
        listOf(entry.manifest?.nameFor(language) ?: entry.name, entry.name, entry.manifest?.id.orEmpty(),
            entry.manifest?.author.orEmpty()).any { it.contains(term, ignoreCase = true) }
    }
}

@Composable
private fun SourceSearchField(value: String, onChange: (String) -> Unit) {
    Row(Modifier.fillMaxWidth().pixelPanel(fill = Bl.Select, raised = false).padding(9.dp),
        verticalAlignment = Alignment.CenterVertically) {
        PixelIcon(R.drawable.ic_lupa, 18.dp, Modifier.padding(end = 6.dp))
        Box(Modifier.weight(1f)) {
            if (value.isEmpty()) PixelText(tr("Buscar nome, ID ou autor"), size = Ts.Tiny, color = Bl.TextMuted)
            BasicTextField(value, onChange, singleLine = true,
                textStyle = TextStyle(color = Bl.Text, fontSize = Ts.Small.sp), cursorBrush = SolidColor(Bl.PressedText),
                modifier = Modifier.fillMaxWidth())
        }
        if (value.isNotEmpty()) PixelText("×", size = Ts.Body,
            modifier = Modifier.semantics { contentDescription = tr("Limpar busca") }.pixelClickable { onChange("") }.padding(5.dp))
    }
}

@Composable
private fun SourceField(label: String, value: String, onChange: (String) -> Unit) {
    PixelText(label, size = Ts.Small, color = Bl.TextFaint)
    BasicTextField(value, onChange, singleLine = true,
        textStyle = TextStyle(color = Bl.Text, fontSize = Ts.Body.sp), cursorBrush = SolidColor(Bl.PressedText),
        modifier = Modifier.fillMaxWidth().pixelPanel(fill = Bl.FrameFill, raised = false).padding(10.dp))
}
