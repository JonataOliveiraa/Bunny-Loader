package dev.bunnyloader.ui

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.provider.OpenableColumns
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.bunnyloader.R
import dev.bunnyloader.game.BundledRuntime
import dev.bunnyloader.game.SaveFiles
import java.io.File
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

// ================================ Perfil ================================

/** A foto do perfil: um PNG quadrado na pasta interna do app. */
private fun profileFile(context: Context) = File(context.filesDir, "profile.png")

/**
 * Lê a imagem escolhida, recorta o quadrado do meio e reduz para
 * [PROFILE_PX]: uma foto de câmera inteira na memória não é para um ícone.
 */
private fun saveProfilePhoto(context: Context, uri: Uri): Boolean = runCatching {
    val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
    context.contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, bounds) }
    var sample = 1
    while (minOf(bounds.outWidth, bounds.outHeight) / (sample * 2) >= PROFILE_PX) sample *= 2
    val full = context.contentResolver.openInputStream(uri)?.use {
        BitmapFactory.decodeStream(it, null, BitmapFactory.Options().apply { inSampleSize = sample })
    } ?: return false
    val side = minOf(full.width, full.height)
    val square = Bitmap.createBitmap(full, (full.width - side) / 2, (full.height - side) / 2, side, side)
    val scaled = Bitmap.createScaledBitmap(square, PROFILE_PX, PROFILE_PX, true)
    profileFile(context).outputStream().use { scaled.compress(Bitmap.CompressFormat.PNG, 100, it) }
    true
}.getOrDefault(false)

private const val PROFILE_PX = 192

/**
 * O topo da aba Config: a foto e o nome de quem usa o launcher. Tocar na foto
 * escolhe outra; tocar no nome edita. Sem foto, a arte da aba.
 */
@Composable
fun ProfileHeader(prefs: Prefs, modifier: Modifier = Modifier) {
    val ctx = LocalContext.current
    var name by remember { mutableStateOf(prefs.profileName) }
    var editing by remember { mutableStateOf(false) }
    var draft by remember { mutableStateOf(name) }
    var photoVersion by remember { mutableIntStateOf(0) }
    val scope = rememberCoroutineScope()
    val photo by produceState<ImageBitmap?>(null, photoVersion) {
        value = withContext(Dispatchers.IO) {
            profileFile(ctx).takeIf { it.isFile }?.let { BitmapFactory.decodeFile(it.path)?.asImageBitmap() }
        }
    }
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) scope.launch {
            if (withContext(Dispatchers.IO) { saveProfilePhoto(ctx, uri) }) photoVersion++
        }
    }
    val save = {
        name = draft.trim().take(24)
        prefs.profileName = name
        editing = false
    }

    Row(modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        // A arte ocupa os 104x96 inteiros dela, sem margem transparente:
        // encolher para caber dentro do quadro só deixava buraco.
        Box(Modifier.size(72.dp).framePanel().padding(3.dp).pixelClickable { picker.launch("image/*") }) {
            Image(
                bitmap = photo ?: ImageBitmap.imageResource(R.drawable.ic_tab_config),
                contentDescription = "Foto do perfil",
                filterQuality = FilterQuality.None,
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize(),
            )
        }
        Column(Modifier.weight(1f).padding(start = 12.dp)) {
            if (editing) {
                // O campo já abre com o cursor e o teclado.
                val focus = remember { androidx.compose.ui.focus.FocusRequester() }
                androidx.compose.runtime.LaunchedEffect(Unit) { focus.requestFocus() }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.weight(1f).pixelPanel(fill = Bl.Select, raised = false)
                        .padding(horizontal = 10.dp, vertical = 8.dp)) {
                        BasicTextField(
                            value = draft,
                            onValueChange = { draft = it.take(24) },
                            singleLine = true,
                            textStyle = TextStyle(fontFamily = PixelFont, fontSize = Ts.Item.sp, color = Bl.Text),
                            cursorBrush = SolidColor(Bl.PressedText),
                            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
                            keyboardActions = KeyboardActions(onDone = { save() }),
                            modifier = Modifier.fillMaxWidth()
                                .focusRequester(focus),
                        )
                    }
                    PixelButton("OK", save, Modifier.padding(start = 8.dp), fontSize = Ts.Small, shadow = false)
                }
            } else {
                Box(Modifier.pixelClickable { draft = name; editing = true }) {
                    PixelText(name.ifBlank { "Jogador" }, size = Ts.Big)
                }
            }
            PixelText("Terraria ${BundledRuntime.VERSION_NAME}", size = Ts.Body, color = Bl.TextFaint)
            PixelText("Toque na foto ou no nome para mudar", size = Ts.Tiny, color = Bl.TextMuted)
        }
    }
}

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
                r.fold({ "${kind.label.replaceFirstChar { it.uppercase() }} \"$it\" importado" to true },
                    { "Não deu: ${it.message}" to false })
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
            PixelText("Mundos e personagens", size = Ts.Item)
            PixelText(
                "$worlds mundo(s) e $players personagem(ns) salvos. Escolha o .wld ou o .plr; " +
                    "os arquivos do Bunny Loader do mesmo save (.wld.bl, .wld.tiles.bl...) podem ir junto.",
                size = Ts.Small, color = Bl.TextFaint,
            )
            Row(Modifier.padding(top = 10.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                PixelButton("Importar mundo", { asking = SaveFiles.Kind.WORLD },
                    Modifier.weight(1f), icon = R.drawable.ic_folder, fontSize = Ts.Small, shadow = false)
                PixelButton("Importar personagem", { asking = SaveFiles.Kind.PLAYER },
                    Modifier.weight(1f), icon = R.drawable.ic_folder, fontSize = Ts.Small, shadow = false)
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
        PixelDialog(if (ok) "Importado" else "Importação", onDismiss = { report = emptyList() }) {
            for ((text, good) in report) {
                PixelText(text, size = Ts.Body, color = if (good) Bl.TextDim else Bl.Bad)
            }
            if (ok) {
                PixelText("Ele já aparece na lista do jogo e no Início rápido.", size = Ts.Small, color = Bl.TextFaint)
            }
            PixelButton("OK", { report = emptyList() }, Modifier.fillMaxWidth(), fontSize = Ts.Body, shadow = false)
        }
    }
}

/** O aviso antes do seletor: o que acontece com o arquivo e o que levar junto. */
@Composable
private fun ImportWarning(kind: SaveFiles.Kind, onCancel: () -> Unit, onContinue: () -> Unit) {
    val world = kind == SaveFiles.Kind.WORLD
    PixelDialog(if (world) "Importar mundo" else "Importar personagem", onDismiss = onCancel) {
        val lines = buildList {
            add("Feche o jogo antes: com ele aberto, o save pode ser gravado por cima.")
            add("Nada é sobrescrito: se já existir um ${kind.label} com o mesmo nome de arquivo, o importado ganha um _2.")
            if (world) {
                add("Blocos, paredes e NPCs de mod só aparecem com os mesmos mods ligados. Escolha também " +
                    "os arquivos .wld.bl, .wld.tiles.bl e .wld.walls.bl do mesmo mundo, se ele tiver.")
                add("Mundo de Jornada só abre com personagem de Jornada.")
            } else {
                add("Itens de mod só aparecem com os mesmos mods ligados. Escolha também o .plr.bl do " +
                    "mesmo personagem, se ele tiver.")
            }
            add("Só abra saves de quem você confia.")
        }
        for (line in lines) {
            Row {
                PixelText("•", size = Ts.Body, color = Bl.PressedText, modifier = Modifier.padding(end = 8.dp))
                PixelText(line, size = Ts.Small, color = Bl.TextDim)
            }
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            PixelButton("Cancelar", onCancel, Modifier.weight(1f), fontSize = Ts.Body, shadow = false)
            PixelButton("Escolher arquivos", onContinue, Modifier.weight(1f), fill = Bl.TitleFill,
                fontSize = Ts.Body, shadow = false)
        }
    }
}

/** Um diálogo no painel do launcher: título e o conteúdo empilhado. */
@Composable
fun PixelDialog(title: String, onDismiss: () -> Unit, content: @Composable () -> Unit) {
    androidx.compose.ui.window.Dialog(onDismissRequest = onDismiss) {
        Column(
            Modifier.widthIn(max = 480.dp).fillMaxWidth().pixelPanel().padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            PixelText(title, size = Ts.Head)
            content()
        }
    }
}
