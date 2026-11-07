package dev.bunnyloader.ui

import android.content.Context
import android.net.Uri
import android.provider.OpenableColumns
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.unit.dp
import dev.bunnyloader.R
import dev.bunnyloader.game.SaveFiles
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

// ========================= Mundos e personagens =========================

private fun displayName(context: Context, uri: Uri): String =
    runCatching {
        context.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { c ->
            if (c.moveToFirst()) c.getString(0) else null
        }
    }.getOrNull() ?: uri.lastPathSegment?.substringAfterLast('/') ?: "arquivo"

/**
 * Importar mundos e personagens de fora (outro aparelho, backup, o Terraria
 * oficial do PC). Cada botão abre o seletor com vários arquivos: o `.wld` ou o
 * `.plr` e, se houver, os arquivos do Bunny Loader do mesmo save
 * (`Mundo.wld.bl`, `Mundo.wld.tiles.bl`...), que guardam o que é de mod.
 */
@Composable
fun SavesCard(modifier: Modifier = Modifier) {
    val ctx = LocalContext.current
    var version by remember { mutableIntStateOf(0) }
    val players = remember(version) { SaveFiles.players(ctx).size }
    val worlds = remember(version) { SaveFiles.worlds(ctx).size }
    // O aviso antes de escolher, e o resultado depois: os dois em diálogo.
    var asking by remember { mutableStateOf<SaveFiles.Kind?>(null) }
    var report by remember { mutableStateOf<List<Pair<String, Boolean>>>(emptyList()) }

    fun pickerFor(kind: SaveFiles.Kind) = { uris: List<Uri> ->
        if (uris.isNotEmpty()) {
            val picked = uris.map { uri -> SaveFiles.Picked(displayName(ctx, uri)) { ctx.contentResolver.openInputStream(uri) } }
            report = SaveFiles.import(ctx, kind, picked).map { r ->
                r.fold({ tr("SaveImported", tr(kind.label).replaceFirstChar { it.uppercase() }, it) to true },
                    { tr("Failed", it.message) to false })
            }
            version++
        }
    }
    val worldPicker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenMultipleDocuments(), pickerFor(SaveFiles.Kind.WORLD))
    val playerPicker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenMultipleDocuments(), pickerFor(SaveFiles.Kind.PLAYER))

    PixelCard(modifier.fillMaxWidth().padding(bottom = 8.dp)) {
        Column(Modifier.padding(12.dp).fillMaxWidth()) {
            PixelText(tr("WorldsAndCharacters"), size = Ts.Item)
            PixelText(
                tr("ImportSavesDescription", worlds, players),
                size = Ts.Small, color = Bl.TextFaint,
            )
            Row(Modifier.padding(top = 10.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                PixelButton(tr("ImportWorld"), { asking = SaveFiles.Kind.WORLD },
                    Modifier.weight(1f), icon = R.drawable.ic_folder, fontSize = Ts.Small)
                PixelButton(tr("ImportCharacter"), { asking = SaveFiles.Kind.PLAYER },
                    Modifier.weight(1f), icon = R.drawable.ic_folder, fontSize = Ts.Small)
            }
        }
    }

    asking?.let { kind ->
        ImportWarning(kind, onCancel = { asking = null }) {
            asking = null
            (if (kind == SaveFiles.Kind.WORLD) worldPicker else playerPicker).launch(arrayOf("*/*"))
        }
    }
    if (report.isNotEmpty()) {
        val ok = report.all { it.second }
        PixelDialog(if (ok) tr("Imported") else tr("Import"), onDismiss = { report = emptyList() }) {
            for ((text, good) in report) {
                PixelText(text, size = Ts.Body, color = if (good) Bl.TextDim else Bl.Bad)
            }
            if (ok) {
                PixelText(tr("ImportedHint"), size = Ts.Small, color = Bl.TextFaint)
            }
            PixelButton("OK", { report = emptyList() }, Modifier.fillMaxWidth(), fontSize = Ts.Body)
        }
    }
}

/** O aviso antes do seletor: o que acontece com o arquivo e o que levar junto. */
@Composable
private fun ImportWarning(kind: SaveFiles.Kind, onCancel: () -> Unit, onContinue: () -> Unit) {
    val world = kind == SaveFiles.Kind.WORLD
    PixelDialog(if (world) tr("ImportWorld") else tr("ImportCharacter"), onDismiss = onCancel) {
        val lines = buildList {
            add(tr("CloseGameBeforeImport"))
            add(tr("ImportNoOverwrite", tr(kind.label)))
            if (world) {
                add(tr("ImportWorldModsHint"))
                add(tr("JourneyWorldsNeedJourneyCharacters"))
            } else {
                add(tr("ImportCharacterModsHint"))
            }
            add(tr("ImportTrustWarning"))
        }
        for (line in lines) {
            Row {
                PixelText("•", size = Ts.Body, color = Bl.PressedText, modifier = Modifier.padding(end = 8.dp))
                PixelText(line, size = Ts.Small, color = Bl.TextDim)
            }
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            PixelButton(tr("Cancel"), onCancel, Modifier.weight(1f), fontSize = Ts.Body)
            PixelButton(tr("ChooseFiles"), onContinue, Modifier.weight(1f), fill = Bl.TitleFill,
                fontSize = Ts.Body)
        }
    }
}

/** Um diálogo no painel do launcher: título e o conteúdo empilhado. */
@Composable
fun PixelDialog(title: String, onDismiss: () -> Unit, content: @Composable () -> Unit) {
    androidx.compose.ui.window.Dialog(onDismissRequest = onDismiss) {
        Column(
            Modifier.widthIn(max = 480.dp).fillMaxWidth().pixelPanel(corner = PixelCorner.LARGE).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            PixelText(title, size = Ts.Head)
            content()
        }
    }
}

// =============================== Licenças ===============================

/** Os textos de assets/legal/: cópias de LICENSE.md e THIRD_PARTY_NOTICES.md (syncLegal no build). */
private enum class LegalText(val title: String, val file: String) {
    LICENSE("License", "LICENSE.md"),
    THIRD_PARTY("ThirdParties", "THIRD_PARTY_NOTICES.md"),
}

/**
 * A licença do app e os avisos das bibliotecas. MIT e BSD pedem que o aviso vá
 * junto com o binário; é aqui que ele vai.
 */
@Composable
fun LicensesCard(modifier: Modifier = Modifier) {
    val ctx = LocalContext.current
    val uriHandler = LocalUriHandler.current
    var open by remember { mutableStateOf<LegalText?>(null) }

    PixelCard(modifier.fillMaxWidth()) {
        Column(Modifier.padding(12.dp).fillMaxWidth()) {
            PixelText(tr("License"), size = Ts.Item)
            PixelText(
                tr("LicenseSummary"),
                size = Ts.Small, color = Bl.TextFaint,
            )
            Row(Modifier.padding(top = 10.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                for (text in LegalText.entries) {
                    PixelButton(tr(text.title), { open = text }, Modifier.weight(1f),
                        fontSize = Ts.Small)
                }
            }
        }
    }

    open?.let { text ->
        val blocks by produceState<List<MdBlock>?>(null, text) {
            value = withContext(Dispatchers.IO) {
                runCatching { ctx.assets.open("legal/${text.file}").bufferedReader().use { it.readText() } }
                    .map(Markdown::parse).getOrDefault(emptyList())
            }
        }
        val md = remember(uriHandler) {
            MdContext(
                style = MdStyle(),
                image = { null },
                sprite = { _, _ -> null },
                // Sem navegador instalado o openUri lança; o toque só não faz nada.
                openUrl = { url -> runCatching { uriHandler.openUri(url) } },
            )
        }
        PixelDialog(tr(text.title), onDismiss = { open = null }) {
            Box(Modifier.fillMaxWidth().heightIn(max = 460.dp).verticalScroll(rememberScrollState())) {
                when {
                    blocks == null -> Unit
                    blocks!!.isEmpty() -> PixelText(tr("TextNotInApk"), size = Ts.Small, color = Bl.Bad)
                    else -> MarkdownView(blocks!!, md)
                }
            }
            PixelButton(tr("Close"), { open = null }, Modifier.fillMaxWidth(), fontSize = Ts.Body)
        }
    }
}
