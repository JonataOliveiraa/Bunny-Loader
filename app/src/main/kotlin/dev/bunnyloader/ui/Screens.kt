package dev.bunnyloader.ui

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.text.style.TextAlign
import kotlinx.coroutines.delay
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.bunnyloader.R
import dev.bunnyloader.game.BootLog
import dev.bunnyloader.game.BundledRuntime
import dev.bunnyloader.game.SaveFiles
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.PackType
import dev.bunnyloader.mods.compareVersions
import dev.bunnyloader.mods.formatSize
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

internal val EdgePad = 14.dp

// =============================== Início ===============================

/**
 * A vitrine. Um mod pede destaque no próprio mod.json (`featured`); o resto,
 * embutido ou do catálogo online, entra em "Populares". Nenhum número
 * inventado aqui — não há contagem de download nem nota, porque não há
 * servidor para produzir isso.
 */
@Composable
fun InicioTab(shell: Shell, onHide: () -> Unit = {}, onOpen: (String) -> Unit) {
    val scroll = rememberScrollState()
    LaunchedEffect(Unit) { shell.refreshRemote() }
    // Arrastar para cima quando a tela não tem mais o que rolar (o caso comum:
    // o Início cabe na tela, e já está no topo) esconde a interface e deixa só
    // o cenário. Conta só o arraste com a rolagem no fim, e zera a cada gesto.
    // A conexão é uma só (rememberUpdatedState): recriá-la a cada recomposição
    // zerava a conta no meio do arraste.
    val threshold = with(androidx.compose.ui.platform.LocalDensity.current) { 90.dp.toPx() }
    val hide by androidx.compose.runtime.rememberUpdatedState(onHide)
    val hideOnPull = remember(scroll, threshold) {
        object : androidx.compose.ui.input.nestedscroll.NestedScrollConnection {
            var pulled = 0f
            override fun onPreScroll(
                available: androidx.compose.ui.geometry.Offset,
                source: androidx.compose.ui.input.nestedscroll.NestedScrollSource,
            ): androidx.compose.ui.geometry.Offset {
                // Puxar para cima (y negativo) com a rolagem já no fim: conta.
                // Qualquer movimento para baixo zera.
                if (available.y < 0f && !scroll.canScrollForward) {
                    pulled -= available.y
                    if (pulled > threshold) { pulled = 0f; hide() }
                } else if (available.y > 0f) {
                    pulled = 0f
                }
                return androidx.compose.ui.geometry.Offset.Zero
            }

            override suspend fun onPreFling(available: androidx.compose.ui.unit.Velocity): androidx.compose.ui.unit.Velocity {
                pulled = 0f
                return androidx.compose.ui.unit.Velocity.Zero
            }
        }
    }
    // Sem o efeito de esticar da borda: ele comia o arraste no fim da rolagem
    // antes de a conexão acima ver (available.y chegava 0).
    @OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
    androidx.compose.runtime.CompositionLocalProvider(
        androidx.compose.foundation.LocalOverscrollConfiguration provides null,
    ) {
    StorePullToRefresh(shell, Modifier.fillMaxSize()) {
        Column(Modifier.fillMaxSize()
            .nestedScroll(hideOnPull)
            .verticalScroll(scroll)) {
            // O título vai de ponta a ponta, FORA do padding da coluna.
            //
            // O `aspectRatio` é o que faz a coisa funcionar, e a falta dele era
            // o bug: com só `fillMaxWidth()`, a ALTURA colapsava para os 121 px
            // da arte e o ContentScale.Fit passava a caber por ela — a imagem
            // ficava em tamanho 1:1 no meio de uma faixa larga, e o `heightIn`
            // que parecia ser o limitador nunca chegava a valer. Travando a
            // proporção, a altura segue a largura e o título ocupa o que tem.
            Image(
                bitmap = ImageBitmap.imageResource(R.drawable.img_title),
                contentDescription = "Bunny Loader",
                filterQuality = FilterQuality.None,
                contentScale = ContentScale.Fit,
                modifier = Modifier.fillMaxWidth().aspectRatio(403f / 121f)
                    .padding(top = 10.dp, bottom = 6.dp),
            )
            Column(Modifier.padding(horizontal = EdgePad)) {
            RemoteStatusLine(shell, Modifier.padding(top = 6.dp))

            val featured = shell.entries.firstOrNull { it.manifest.featured }
                ?: shell.entries.firstOrNull()
            if (featured != null) {
                SectionTitle(tr("Em destaque"))
                FeaturedCard(featured, shell, onOpen)
            }

            val populares = shell.catalogEntries.filter { it.uid != featured?.uid }.take(3)
            if (populares.isNotEmpty()) {
                SectionTitle(tr("Populares"))
                for (e in populares) {
                    ModRow(e, shell.catalog, { onOpen(e.uid) }, Modifier.padding(bottom = 8.dp))
                }
            }
            Spacer(Modifier.height(16.dp))
            }
        }
        PixelScrollbar(scroll, Modifier.fillMaxSize())
    }
    }
}

/**
 * O destaque reserva espaço para o ícone e separa autoria, resumo e ação.
 * A descrição completa continua na ficha do pacote.
 */
@Composable
private fun FeaturedCard(entry: Catalog.Entry, shell: Shell, onOpen: (String) -> Unit) {
    PixelCard(Modifier.fillMaxWidth(), onClick = { onOpen(entry.uid) }) {
        Column(Modifier.padding(8.dp)) {
            Box(Modifier.fillMaxWidth()) {
                ModBanner(entry, shell.catalog,
                    Modifier.fillMaxWidth().heightIn(min = 100.dp, max = 146.dp).aspectRatio(3.6f))

                Box(Modifier.align(Alignment.BottomStart).offset(x = 12.dp, y = 28.dp)) {
                    ModIcon(entry, shell.catalog, 64.dp)
                }
            }

            Column(
                Modifier.fillMaxWidth().heightIn(min = 64.dp)
                    .padding(start = 88.dp, top = 10.dp, end = 8.dp, bottom = 8.dp),
                verticalArrangement = Arrangement.spacedBy(4.dp),
            ) {
                PixelText(
                    entry.manifest.nameFor(UiLocalization.language), size = Ts.Head, color = Bl.Text,
                    maxLines = 2, overflow = TextOverflow.Ellipsis,
                )
                AuthorStrip(entry, shell.catalog)
            }

            PixelText(
                entry.manifest.summary,
                size = Ts.Body, color = Bl.TextDim,
                maxLines = 3, overflow = TextOverflow.Ellipsis,
                modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
            )

            Row(
                Modifier.fillMaxWidth().padding(start = 12.dp, end = 10.dp, top = 8.dp, bottom = 8.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                PixelTag(entry.manifest.category, categoryColor(entry.manifest.category))
                Spacer(Modifier.weight(1f))
                PixelText(tr("Ver detalhes"), size = Ts.Small, color = Bl.PressedText)
                PixelIcon(R.drawable.ic_seta_esq, 16.dp, Modifier.rotate(180f))
            }
        }
    }
}

// =============================== Explorar ===============================

/** Quantos cartões o Explorar acrescenta por vez. */
private const val EXPLORE_PAGE = 10

/**
 * O que vem dentro do app e o catálogo online. É daqui que um mod vai parar em
 * Pacotes. Ao voltar ao app a lista é atualizada; mudar de aba usa o cache
 * por alguns minutos. Sem rede, fica a última lista baixada.
 */
@Composable
fun ExplorarTab(shell: Shell, onOpen: (String) -> Unit) {
    var query by rememberSaveable { mutableStateOf("") }
    var type by rememberSaveable { mutableStateOf(PackType.MOD) }
    LaunchedEffect(Unit) { shell.refreshRemote() }
    // Montada e ordenada só quando a lista online muda (um ícone que chega a
    // troca), não a cada recomposição da aba.
    val all = shell.catalogEntries
    val search = rememberPackageSearchIndex(all)
    val counts = remember(all) { all.groupingBy { it.manifest.packType }.eachCount() }
    val list = remember(all, type, query, search) {
        searchPackages(all, query, type, index = search)
    }
    val state = rememberLazyListState()
    LaunchedEffect(type, query) { state.scrollToItem(0) }
    // De 10 em 10: o catálogo vai passar de mil pacotes, e cada cartão que
    // aparece pede o ícone pela rede. A próxima página entra quando a rolagem
    // chega perto do fim; trocar de busca ou de tipo volta à primeira.
    var shown by rememberSaveable(type, query) { mutableIntStateOf(EXPLORE_PAGE) }
    val page = remember(list, shown) { list.take(shown) }
    LaunchedEffect(state, list.size, type, query) {
        snapshotFlow { state.layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: 0 }
            .collect { last ->
                if (last >= shown - 3 && shown < list.size) shown = (shown + EXPLORE_PAGE).coerceAtMost(list.size)
            }
    }

    Column(Modifier.fillMaxSize()) {
        SearchField(query, { query = it }, Modifier.padding(horizontal = EdgePad, vertical = 10.dp))
        PackTypeTabs(type, counts, { type = it }, Modifier.padding(start = EdgePad, end = EdgePad, bottom = 8.dp))
        FilterResultLine(list.size, query.isNotBlank(),
            { query = "" }, Modifier.padding(horizontal = EdgePad, vertical = 6.dp))
        RemoteStatusLine(shell, Modifier.padding(start = EdgePad, end = EdgePad, bottom = 8.dp))

        PauseListAnimations({ state.isScrollInProgress }) {
            StorePullToRefresh(shell, Modifier.weight(1f)) {
                LazyColumn(
                    state = state,
                    modifier = Modifier.fillMaxSize(),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(
                        start = EdgePad, end = EdgePad, bottom = 16.dp,
                    ),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    items(page, key = { it.uid }, contentType = { "package" }) { e ->
                        ModRow(e, shell.catalog, { onOpen(e.uid) }) {
                            val progress = shell.downloads[e.uid]
                            when {
                                progress != null -> PixelTag("${(progress * 100).toInt()}%")
                                shell.updateFor(e.uid) != null -> PixelTag(tr("Atualizar"), Bl.Good)
                                e.uid in shell.installed -> PixelTag(tr("Instalado"))
                            }
                        }
                    }
                    if (page.size < list.size) {
                        item(key = "more", contentType = "more") {
                            PixelText(tr("Carregando mais... (%1\$s de %2\$s)", page.size, list.size), size = Ts.Small,
                                color = Bl.TextFaint, modifier = Modifier.fillMaxWidth().padding(vertical = 12.dp),
                                align = TextAlign.Center)
                        }
                    }
                    if (list.isEmpty() && shell.remoteStatus != RemoteStatus.Loading) {
                        item { Empty(tr("Nenhum pacote corresponde aos filtros.")) }
                    }
                }
                PixelScrollbar(state, Modifier.fillMaxSize())
            }
            }
    }
}

/**
 * Falha do catálogo online. O progresso fica no indicador de atualização.
 */
@Composable
private fun RemoteStatusLine(shell: Shell, modifier: Modifier = Modifier) {
    val status = shell.remoteStatus
    if (status !is RemoteStatus.Failed) return
    Row(modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        PixelText(
            tr("Catálogo online: %1\$s.", status.message) +
                if (shell.remote.isEmpty()) "" else tr(" Mostrando a última lista baixada."),
            size = Ts.Small, color = Bl.Bad, modifier = Modifier.weight(1f),
        )
        PixelButton(tr("Tentar de novo"), { shell.refreshRemote(force = true) },
            fontSize = Ts.Small, shadow = false)
    }
}

@Composable
private fun SearchField(value: String, onChange: (String) -> Unit, modifier: Modifier = Modifier) {
    Box(modifier.fillMaxWidth().pixelPanel(fill = Bl.Select, raised = false)
        .padding(horizontal = 12.dp, vertical = 10.dp)) {
        if (value.isEmpty()) {
            PixelText(tr("Nome, autor ou categoria..."), size = Ts.Body, color = Bl.TextMuted)
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            BasicTextField(
                value = value,
                onValueChange = onChange,
                singleLine = true,
                textStyle = TextStyle(
                    fontFamily = PixelFont, fontSize = Ts.Body.sp, color = Bl.Text,
                ),
                cursorBrush = SolidColor(Bl.PressedText),
                modifier = Modifier.weight(1f),
            )
            if (value.isNotEmpty()) {
                Box(Modifier.semantics { contentDescription = tr("Limpar busca") }
                    .pixelClickable { onChange("") }.padding(4.dp)) {
                    PixelIcon(R.drawable.ic_fechar, 18.dp)
                }
            }
        }
    }
}

// =============================== Pacotes ===============================

/**
 * O que está instalado em `Android/data/com.bunnyloader/bunny_packs` —
 * exatamente a pasta de onde o núcleo nativo carrega. O interruptor grava a
 * preferência que vai no NativeConfig no próximo boot do jogo.
 *
 * Importar traz um zip (`.bmod` ou `.zip`) de fora: o seletor do sistema
 * devolve uma Uri e o repositório desempacota. Um pacote que não é do catálogo
 * aparece aqui do mesmo jeito, com o `icon.png` e a `banner.png` dele.
 */
@Composable
fun PacotesTab(shell: Shell, onOpen: (String) -> Unit) {
    var query by rememberSaveable { mutableStateOf("") }
    var type by rememberSaveable { mutableStateOf(PackType.MOD) }
    val search = rememberPackageSearchIndex(shell.packages)
    val packages = remember(shell.packages, shell.enabled, type, query, search) {
        filterPackages(shell.packages, shell.enabled, PackageStatus.ALL, PackageSort.LOAD, type, query, index = search)
    }
    val counts = remember(shell.packages) { shell.packages.groupingBy { it.manifest.packType }.eachCount() }
    val manualOrder = query.isBlank()
    val state = rememberLazyListState()
    LaunchedEffect(type, query) { state.scrollToItem(0) }
    // Cada troca de lugar pedida por toque (interruptor, setas) anima a lista
    // por um instante; o arraste não (ver DraggablePackageList). A animação liga
    // no próprio toque, no mesmo quadro em que a ordem muda: ligada depois,
    // a lista já teria pulado para o lugar novo.
    var moves by remember { mutableIntStateOf(0) }
    var animateMoves by remember { mutableStateOf(false) }
    var movedUid by remember { mutableStateOf<String?>(null) }
    val animateNextMove = { uid: String -> animateMoves = true; movedUid = uid; moves++ }
    LaunchedEffect(moves) {
        if (moves == 0) return@LaunchedEffect
        delay(MOVE_MS + 250L)
        animateMoves = false
    }
    // texto + deu certo? Uma recusa em verde de sucesso se le como sucesso.
    var aviso by remember { mutableStateOf<Pair<String, Boolean>?>(null) }
    var asking by remember { mutableStateOf(false) }
    val importing = shell.importProgress != null || shell.pendingUpdate != null

    val picker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) {
            shell.importPackage(uri) { result ->
                aviso = result.fold(
                    onSuccess = { tr("%1\$s instalado", it.nameFor(UiLocalization.language)) to true },
                    onFailure = {
                        if (it is ImportCancelled) tr("Importação cancelada.") to true
                        else tr("Não deu: %1\$s", it.message) to false
                    },
                )
            }
        }
    }
    shell.importProgress?.let { ImportProgressDialog(it) }
    shell.pendingUpdate?.let { pkg ->
        UpdateImportDialog(pkg.manifest, pkg.installed ?: pkg.manifest, shell::answerUpdate)
    }

    Column(Modifier.fillMaxSize()) {
        Row(
            Modifier.fillMaxWidth().padding(start = EdgePad, end = EdgePad, top = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            PixelText(tr("Pacotes"), size = Ts.Big, modifier = Modifier.weight(1f))
            PixelText(tr("%1\$s/%2\$s ligados", shell.enabled.size, shell.installed.size),
                size = Ts.Body, color = Bl.TextFaint)
        }

        Box(Modifier.padding(start = EdgePad, end = EdgePad, top = 10.dp)) {
            PixelButton(
                if (importing) tr("Importando...") else tr("Importar pacote"),
                { if (!importing) asking = true },
                Modifier.fillMaxWidth(),
                icon = R.drawable.ic_folder,
            )
        }
        if (asking) {
            PixelDialog(tr("Importar pacote"), onDismiss = { asking = false }) {
                for (line in listOf(
                    tr("Um mod roda código dentro do jogo: só instale pacotes de quem você confia."),
                    tr("O pacote é um .bl, .bmod ou .zip com manifest.json e a pasta content/."),
                    tr("Se o mod já estiver instalado (o mesmo uid), o app pergunta antes de substituir."),
                )) {
                    Row {
                        PixelText("•", size = Ts.Body, color = Bl.PressedText, modifier = Modifier.padding(end = 8.dp))
                        PixelText(line, size = Ts.Small, color = Bl.TextDim)
                    }
                }
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    PixelButton(tr("Cancelar"), { asking = false }, Modifier.weight(1f), fontSize = Ts.Body, shadow = false)
                    PixelButton(tr("Escolher arquivo"), { asking = false; picker.launch(arrayOf("*/*")) },
                        Modifier.weight(1f), fill = Bl.TitleFill, fontSize = Ts.Body, shadow = false)
                }
            }
        }
        aviso?.let { (texto, ok) ->
            PixelText(texto, size = Ts.Small, color = if (ok) Bl.TextDim else Bl.Bad,
                modifier = Modifier.padding(start = EdgePad, end = EdgePad, top = 6.dp))
        }
        shell.packageError?.let {
            PixelText(it, size = Ts.Small, color = Bl.Bad, modifier = Modifier.padding(horizontal = EdgePad))
        }
        SearchField(query, { query = it }, Modifier.padding(start = EdgePad, end = EdgePad, top = 10.dp))
        PackTypeTabs(type, counts, { type = it }, Modifier.padding(start = EdgePad, end = EdgePad, top = 8.dp))
        FilterResultLine(packages.size, query.isNotBlank(), { query = "" },
            Modifier.padding(start = EdgePad, end = EdgePad, top = 6.dp))
        PixelText(if (manualOrder) tr("Segure e arraste dentro do mesmo grupo. Primeiro ligado carrega primeiro.")
                  else tr("Para mover, limpe a busca."), size = Ts.Small, color = Bl.TextFaint,
            modifier = Modifier.padding(start = EdgePad, end = EdgePad, top = 8.dp))

        DraggablePackageList(
            state = state,
            uids = packages.map { it.uid },
            enabledUids = shell.enabled,
            reorderEnabled = manualOrder,
            onDrop = shell::moveModTo,
            animateMoves = animateMoves,
            raisedUid = movedUid,
            modifier = Modifier.weight(1f).fillMaxWidth(),
            empty = { Empty(if (shell.refreshingPackages && shell.packages.isEmpty()) tr("Lendo pacotes...")
                           else if (shell.installed.isEmpty()) tr("Nenhum pacote. Pegue um em Explorar ou importe um .bmod.")
                           else tr("Nenhum pacote neste filtro.")) },
        ) { index, modifier, preview ->
            val e = packages[index]
            // Ligado: o cartão inteiro fica verde, e a lista se lê de longe.
            val fill = if (e.uid in shell.enabled) Bl.EnabledPanel else Bl.Panel
            // As setas movem em relação ao vizinho VISÍVEL: com o filtro de
            // tipo, o vizinho na ordem completa pode ser de outra lista.
            ModRow(e, shell.catalog, { if (!preview) onOpen(e.uid) }, modifier, fill = fill, besideIcon = if (manualOrder) ({
                val canMoveUp = index > 0 && (packages[index - 1].uid in shell.enabled) == (e.uid in shell.enabled)
                val canMoveDown = index < packages.lastIndex && (packages[index + 1].uid in shell.enabled) == (e.uid in shell.enabled)
                ModOrderButtons(e.manifest.nameFor(UiLocalization.language), canMoveUp, canMoveDown,
                    { if (!preview) { animateNextMove(e.uid); shell.moveModTo(e.uid, packages[index - 1].uid) } },
                    { if (!preview) { animateNextMove(e.uid); shell.moveModTo(e.uid, packages[index + 1].uid) } })
            }) else null) {
                SwitchSprite(e.uid in shell.enabled) {
                    if (!preview) {
                        // O LazyColumn ancora a rolagem na chave do primeiro item visível.
                        // No topo, a âncora é o item vazio da lista e o cartão desliza;
                        // rolada até este cartão, ela o seguiria e levaria a tela junto:
                        // aí a rolagem fica fixa, e o cartão troca de lugar sem animação.
                        if (state.anchoredAt(e.uid)) {
                            state.requestScrollToItem(state.firstVisibleItemIndex, state.firstVisibleItemScrollOffset)
                        }
                        animateNextMove(e.uid)
                        shell.setEnabled(e.uid, e.uid !in shell.enabled)
                    }
                }
            }
        }
    }
}

@Composable
private fun rememberPackageSearchIndex(packages: List<Catalog.Entry>): PackageSearchIndex {
    // Ordem e imagens podem mudar sem alterar os campos pesquisáveis.
    val manifests = packages.map { it.manifest }.toSet()
    val language = UiLocalization.language
    return remember(manifests, language) { PackageSearchIndex(packages, language) }
}

@Composable
private fun FilterResultLine(count: Int, active: Boolean, onReset: () -> Unit, modifier: Modifier = Modifier) {
    Row(modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        PixelText(tr(if (count == 1) "%1\$s pacote" else "%1\$s pacotes", count), size = Ts.Small,
            color = Bl.TextFaint, modifier = Modifier.weight(1f))
        if (active) {
            Box(Modifier.pixelClickable(onReset).padding(horizontal = 8.dp, vertical = 4.dp)) {
                PixelText(tr("Limpar busca"), size = Ts.Small, color = Bl.PressedText)
            }
        }
    }
}

/** O interruptor OFF/ON do próprio Terraria, dois quadros de 43x20. */
@Composable
fun SwitchSprite(on: Boolean, onToggle: () -> Unit) {
    Box(Modifier.pixelClickable(onToggle).padding(4.dp)) {
        SpriteFrame(R.drawable.ic_switch, 43, 20, if (on) 1 else 0, 52.dp)
    }
}

// =========================== Configurações ===========================

@Composable
fun ConfigTab(
    shell: Shell, scenery: String, onScenery: (String) -> Unit,
    themeEffects: Boolean, onThemeEffects: (Boolean) -> Unit,
) {
    val ctx = LocalContext.current
    val clipboard = LocalClipboardManager.current
    val scroll = rememberScrollState()
    var log by remember { mutableStateOf("") }
    val prefs = remember { Prefs(ctx) }
    var enableNewMods by remember { mutableStateOf(prefs.enableOnInstall) }
    var devChannel by remember { mutableStateOf(prefs.devChannel) }
    var errorPanel by remember { mutableStateOf(prefs.errorPanel) }
    var verboseLog by remember { mutableStateOf(prefs.verboseLog) }
    var modMenu by remember { mutableStateOf(prefs.devModMenu) }
    var editor by remember { mutableStateOf(prefs.devEditor) }
    var restart by remember { mutableStateOf(prefs.devRestart) }
    var sourcesOpen by rememberSaveable { mutableStateOf(false) }
    if (sourcesOpen) {
        ModSourcesScreen(shell) { sourcesOpen = false }
        return
    }

    Box {
        Column(Modifier.fillMaxSize().verticalScroll(scroll).padding(horizontal = EdgePad)) {
            ProfileHeader(prefs, Modifier.padding(top = 14.dp))

            SectionTitle(tr("Coleção"))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Stat("${shell.installed.size}", tr("instalados"), Modifier.weight(1f))
                Stat("${shell.enabled.size}", tr("ligados"), Modifier.weight(1f))
                Stat("${shell.catalogEntries.size}", tr("no catálogo"), Modifier.weight(1f))
            }

            SectionTitle(tr("Mundos e personagens"))
            SavesCard()

            SectionTitle(tr("Aparência"))
            LanguagePicker()
            SceneryPicker(scenery, onScenery)
            Setting(
                tr("Efeitos do tema"),
                tr("Cada cenário tem o seu: folhas e cartões balançando na Floresta, chuva e brilho molhado no Oceano, bolhas no Lago, neve na Neve. Desligado, o fundo fica parado."),
                themeEffects,
                onThemeEffects,
            )

            SectionTitle(tr("Mods"))
            Setting(
                tr("Ligar ao instalar"),
                tr("Um pacote recém-instalado já entra valendo no próximo boot."),
                enableNewMods,
            ) { enableNewMods = it; prefs.enableOnInstall = it }
            Setting(
                tr("Mostrar erro dentro do jogo"),
                tr("Quando um mod quebra, o log aparece na tela em vez de só no logcat."),
                errorPanel,
            ) { errorPanel = it; prefs.errorPanel = it }
            Setting(
                tr("Log detalhado"),
                tr("O log de sessão (pasta logs/) ganha cada tabela, hook e tipo que o núcleo registra. Desligado, fica o resumo e as linhas dos mods."),
                verboseLog,
            ) { verboseLog = it; prefs.verboseLog = it }
            Setting(
                tr("Canal de comando por arquivo"),
                tr("Aceita comandos via adb durante o jogo. Só para desenvolver."),
                devChannel,
            ) { devChannel = it; prefs.devChannel = it }

            SectionTitle(tr("Desenvolvedor"))
            PixelButton(tr("Código-fonte dos mods"), { sourcesOpen = true },
                Modifier.fillMaxWidth().padding(bottom = 10.dp), icon = R.drawable.ic_folder)
            QuickStartCard(prefs)
            Setting(
                tr("Mod Menu"),
                tr("O botão do coelho dentro do jogo: poderes, itens, NPCs e buffs. Desligado, nenhum poder volta ligado da última partida."),
                modMenu,
            ) { modMenu = it; prefs.devModMenu = it }
            Setting(
                tr("Editor de JS"),
                tr("Roda JavaScript dentro do jogo. O ícone flutuante de JS fica disponível desde o menu inicial, com ou sem o Mod Menu."),
                editor,
            ) { editor = it; prefs.devEditor = it }
            Setting(
                tr("Reiniciar"),
                tr("O botão que fecha e abre o jogo lendo os mods da pasta de novo (no Mod Menu e no Editor)."),
                restart,
            ) { restart = it; prefs.devRestart = it }

            SectionTitle(tr("Diagnóstico"))
            PixelCard(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp)) {
                    PixelText(
                        tr("O jogo fechou sozinho, travou ou deu erro? Abra o relatório e mande o texto (Copiar) ou um print para quem cuida do Bunny Loader ou do mod."),
                        size = Ts.Small, color = Bl.TextDim,
                    )
                    Row(
                        Modifier.padding(top = 10.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        PixelButton(tr("Ver relatório"), { log = BootLog.read(ctx) },
                            fontSize = Ts.Small, shadow = false)
                        if (log.isNotEmpty()) {
                            PixelButton(tr("Copiar"), {
                                clipboard.setText(AnnotatedString(log))
                            }, fontSize = Ts.Small, shadow = false)
                        }
                    }
                    if (log.isNotEmpty()) {
                        Box(
                            Modifier.fillMaxWidth().padding(top = 10.dp)
                                .pixelPanel(fill = Bl.FrameFill, raised = false).padding(8.dp)
                        ) {
                            Text(log, fontSize = Ts.Tiny.sp, color = Bl.TextDim)
                        }
                    }
                }
            }

            SectionTitle(tr("Sobre"))
            PixelCard(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp)) {
                    Field("Bunny Loader", "v${dev.bunnyloader.BuildConfig.VERSION_NAME}")
                    Spacer(Modifier.height(8.dp))
                    Field(
                        tr("Terraria embutido"),
                        "${BundledRuntime.VERSION_NAME} (${BundledRuntime.VERSION_CODE})",
                    )
                }
            }
            LicensesCard(Modifier.padding(top = 8.dp))
            Spacer(Modifier.height(16.dp))
        }
        PixelScrollbar(scroll, Modifier.fillMaxSize())
    }
}

@Composable
private fun LanguagePicker() {
    val context = LocalContext.current
    val options = listOf("", "pt-BR", "en", "ru", "es", "zh")
    val names = listOf(tr("Automático"), "Português (Brasil)", "English", "Русский", "Español", "简体中文")
    val index = options.indexOf(UiLocalization.choice).coerceAtLeast(0)
    PixelCard(Modifier.fillMaxWidth().padding(bottom = 8.dp)) {
        Column(Modifier.padding(12.dp).fillMaxWidth()) {
            PixelText(tr("Idioma"), size = Ts.Item)
            PixelText(
                tr("Usar o idioma do aparelho. A escolha também vale para o Mod Menu na próxima abertura do jogo."),
                size = Ts.Small, color = Bl.TextFaint,
            )
            PixelSelect(
                names[index],
                label = tr("Idioma"),
                onPrev = { UiLocalization.select(context, options[(index - 1).mod(options.size)]) },
                onNext = { UiLocalization.select(context, options[(index + 1).mod(options.size)]) },
                modifier = Modifier.padding(top = 10.dp),
            )
        }
    }
}

/**
 * O cenário do fundo. Um seletor de setas em vez de lista suspensa: são cinco
 * opções, e a seta é o seletor que o próprio Terraria usa nos menus dele.
 */
@Composable
private fun SceneryPicker(choice: String, onChoice: (String) -> Unit) {
    val options = listOf("") + Biome.entries.map { it.name }
    val i = options.indexOf(choice).coerceAtLeast(0)
    val label = Biome.entries.firstOrNull { it.name == choice }?.label?.let { tr(it) } ?: tr("Automático")
    PixelCard(Modifier.fillMaxWidth().padding(bottom = 8.dp)) {
        Column(Modifier.padding(12.dp).fillMaxWidth()) {
            PixelText(tr("Cenário"), size = Ts.Item)
            PixelText(tr("O fundo do launcher. O céu segue o relógio do celular; no automático, o cenário troca a cada abertura."),
                size = Ts.Small, color = Bl.TextFaint)
            PixelSelect(
                label,
                onPrev = { onChoice(options[(i - 1).mod(options.size)]) },
                onNext = { onChoice(options[(i + 1).mod(options.size)]) },
                modifier = Modifier.padding(top = 10.dp),
            )
        }
    }
}

/**
 * O início rápido: o interruptor e, ligado, o personagem e o mundo.
 *
 * Os saves são lidos do disco quando a aba abre (o jogo pode ter criado um
 * personagem desde a última vez). Guarda-se o NOME DO ARQUIVO, que é como o
 * núcleo acha o save na lista do jogo. A escolha que vale é sempre a que
 * aparece na tela: se o save guardado sumiu, fica o primeiro da lista, e isso
 * vai para as preferências também.
 */
@Composable
private fun QuickStartCard(prefs: Prefs) {
    val ctx = LocalContext.current
    val saves by produceState<Pair<List<SaveFiles.Save>, List<SaveFiles.Save>>?>(null, ctx) {
        value = withContext(Dispatchers.IO) { SaveFiles.players(ctx) to SaveFiles.worlds(ctx) }
    }
    val players = saves?.first.orEmpty()
    val worlds = saves?.second.orEmpty()
    var on by remember { mutableStateOf(prefs.quickStart) }
    var playerFile by remember { mutableStateOf(prefs.quickPlayer) }
    var worldFile by remember { mutableStateOf(prefs.quickWorld) }

    val player = players.firstOrNull { it.file == playerFile } ?: players.firstOrNull()
    // A regra da lista de mundos do jogo: personagem de Jornada só entra em
    // mundo de Jornada, e vice-versa. O que não serve nem aparece.
    val fitting = worlds.filter { player == null || it.journey == player.journey }
    // null = parar no título (só a abertura rápida), a última opção.
    val worldOptions: List<SaveFiles.Save?> = if (player == null) listOf(null) else fitting + null
    val world = when (worldFile) {
        Prefs.TITLE_ONLY -> null
        else -> fitting.firstOrNull { it.file == worldFile } ?: fitting.firstOrNull()
    }

    androidx.compose.runtime.LaunchedEffect(on, saves, player?.file, world?.file) {
        if (!on || saves == null) return@LaunchedEffect
        prefs.quickPlayer = player?.file ?: ""
        prefs.quickWorld = world?.file ?: Prefs.TITLE_ONLY
    }

    PixelCard(Modifier.fillMaxWidth().padding(bottom = 8.dp)) {
        Column(Modifier.padding(12.dp).fillMaxWidth()) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f).padding(end = 8.dp)) {
                    PixelText(tr("Início rápido"), size = Ts.Item)
                    PixelText(
                        tr("A logo da Re-Logic some assim que o jogo termina de carregar, e o jogo entra direto no mundo abaixo. Vale também para o Reiniciar do Mod Menu."),
                        size = Ts.Small, color = Bl.TextFaint,
                    )
                }
                SwitchSprite(on) { on = !on; prefs.quickStart = on }
            }
            if (on) {
                if (saves == null) {
                    PixelText(tr("Carregando personagens e mundos..."), size = Ts.Small,
                        modifier = Modifier.padding(top = 10.dp))
                } else if (player == null) {
                    PixelText(
                        tr("Nenhum personagem salvo ainda. Crie um no jogo; até lá, o jogo para no título."),
                        size = Ts.Small, color = Bl.TextDim, modifier = Modifier.padding(top = 10.dp),
                    )
                } else {
                    PixelText(tr("Personagem"), size = Ts.Small, color = Bl.TextFaint,
                        modifier = Modifier.padding(top = 10.dp))
                    val pi = players.indexOf(player)
                    PixelSelect(
                        saveLabel(player),
                        onPrev = { playerFile = players[(pi - 1).mod(players.size)].file },
                        onNext = { playerFile = players[(pi + 1).mod(players.size)].file },
                        modifier = Modifier.padding(top = 4.dp),
                    )
                    PixelText(tr("Mundo"), size = Ts.Small, color = Bl.TextFaint,
                        modifier = Modifier.padding(top = 8.dp))
                    val wi = worldOptions.indexOf(world)
                    val pickWorld = { i: Int ->
                        worldFile = worldOptions[i.mod(worldOptions.size)]?.file ?: Prefs.TITLE_ONLY
                    }
                    PixelSelect(
                        world?.let { saveLabel(it) } ?: tr("Nenhum: parar no título"),
                        onPrev = { pickWorld(wi - 1) },
                        onNext = { pickWorld(wi + 1) },
                        modifier = Modifier.padding(top = 4.dp),
                    )
                    if (fitting.isEmpty()) {
                        PixelText(
                            if (player.journey) tr("Nenhum mundo de Jornada salvo.") else tr("Nenhum mundo salvo."),
                            size = Ts.Small, color = Bl.TextDim, modifier = Modifier.padding(top = 6.dp),
                        )
                    }
                }
            }
        }
    }
}

private fun saveLabel(save: SaveFiles.Save) = if (save.journey) tr("%1\$s (Jornada)", save.name) else save.name

/** Uma linha de ajuste: nome, o que ela faz, e o interruptor. */
@Composable
private fun Setting(title: String, help: String, on: Boolean, onChange: (Boolean) -> Unit) {
    PixelCard(Modifier.fillMaxWidth().padding(bottom = 8.dp)) {
        Row(
            Modifier.padding(12.dp).fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(Modifier.weight(1f).padding(end = 8.dp)) {
                PixelText(title, size = Ts.Item)
                PixelText(help, size = Ts.Small, color = Bl.TextFaint)
            }
            SwitchSprite(on, { onChange(!on) })
        }
    }
}

@Composable
private fun Stat(value: String, label: String, modifier: Modifier = Modifier) {
    PixelCard(modifier) {
        Column(
            Modifier.fillMaxWidth().padding(vertical = 12.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            PixelText(value, size = Ts.Big, color = Bl.PressedText)
            PixelText(label, size = Ts.Small, color = Bl.TextFaint)
        }
    }
}

@Composable
private fun Field(label: String, value: String, modifier: Modifier = Modifier) {
    Column(modifier) {
        PixelText(label, size = Ts.Small, color = Bl.TextFaint)
        PixelText(value, size = Ts.Body, color = Bl.Text)
    }
}

@Composable
private fun Empty(text: String) {
    Box(Modifier.fillMaxWidth().padding(vertical = 40.dp), contentAlignment = Alignment.Center) {
        PixelText(text, size = Ts.Body, color = Bl.TextFaint)
    }
}

/** A janela do import: a barra e quanto já foi. Não fecha pelo toque fora. */
@Composable
private fun ImportProgressDialog(progress: Float) {
    val pct = (progress.coerceIn(0f, 1f) * 100).toInt()
    PixelDialog(tr("Importando pacote"), onDismiss = {}) {
        Box(Modifier.fillMaxWidth().height(22.dp).pixelPanel(Bl.FrameFill, raised = false)) {
            Box(
                Modifier.fillMaxWidth(progress.coerceIn(0f, 1f)).height(22.dp).padding(4.dp)
                    .background(Bl.Good),
            )
        }
        PixelText(tr("%1\$s%% concluído", pct), size = Ts.Body, color = Bl.TextDim)
    }
}

/** Um pacote de um mod que já está instalado: atualizar ou deixar como está. */
@Composable
private fun UpdateImportDialog(file: ModManifest, installed: ModManifest, onAnswer: (Boolean) -> Unit) {
    PixelDialog(tr("Atualizar mod?"), onDismiss = { onAnswer(false) }) {
        PixelText(tr("%1\$s já está instalado.", installed.nameFor(UiLocalization.language)), size = Ts.Body)
        PixelText(tr("Instalado: %1\$s · Arquivo: %2\$s", installed.version, file.version),
            size = Ts.Small, color = Bl.TextDim)
        val order = compareVersions(file.version, installed.version)
        if (order == 0) {
            PixelText(tr("É a mesma versão: atualizar reinstala o mod."), size = Ts.Small, color = Bl.TextFaint)
        } else if (order < 0) {
            PixelText(tr("O arquivo é mais antigo que o mod instalado."), size = Ts.Small, color = Bl.Bad)
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            PixelButton(tr("Cancelar"), { onAnswer(false) }, Modifier.weight(1f), fontSize = Ts.Body, shadow = false)
            PixelButton(tr("Atualizar"), { onAnswer(true) }, Modifier.weight(1f), fill = Bl.TitleFill,
                fontSize = Ts.Body, shadow = false)
        }
    }
}
