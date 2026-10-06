package dev.bunnyloader.ui

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
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
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
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
import dev.bunnyloader.mods.formatSize

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
    Box {
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
                SectionTitle("Em destaque")
                FeaturedCard(featured, shell, onOpen)
            }

            val populares = shell.catalogEntries.filter { it.uid != featured?.uid }.take(3)
            if (populares.isNotEmpty()) {
                SectionTitle("Populares")
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
 * O cartão grande. O ícone cavalga a borda de baixo do banner, como numa loja
 * de app: é o que amarra a capa ao texto em vez de empilhar duas caixas.
 */
@Composable
private fun FeaturedCard(entry: Catalog.Entry, shell: Shell, onOpen: (String) -> Unit) {
    PixelCard(Modifier.fillMaxWidth(), onClick = { onOpen(entry.uid) }) {
        Column(Modifier.padding(6.dp)) {
            // O ícone pende para FORA do banner, não empurra o layout: fica
            // como sobreposição, e o texto abaixo só reserva a margem dele.
            Box(Modifier.fillMaxWidth()) {
                ModBanner(entry, shell.catalog, Modifier.fillMaxWidth().height(146.dp))
                Box(Modifier.align(Alignment.BottomStart).offset(x = 8.dp, y = 22.dp)) {
                    ModIcon(entry, shell.catalog, 60.dp)
                }
            }
            Column(Modifier.padding(start = 80.dp, top = 6.dp, end = 4.dp)) {
                PixelText(entry.manifest.name, size = Ts.Head,
                    color = Bl.Text)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    PixelText("por ${entry.manifest.authorLine}", size = Ts.Small, color = Bl.TextFaint)
                    PixelTag(
                        entry.manifest.category,
                        categoryColor(entry.manifest.category),
                        Modifier.padding(start = 10.dp),
                    )
                }
            }
            PixelText(
                entry.manifest.summary,
                size = Ts.Body, color = Bl.TextDim,
                modifier = Modifier.padding(start = 8.dp, end = 8.dp, top = 10.dp, bottom = 4.dp),
            )
        }
    }
}

// =============================== Explorar ===============================

/**
 * O que vem dentro do app e o catálogo online. É daqui que um mod vai parar em
 * Pacotes. Ao voltar ao app a lista é atualizada; mudar de aba usa o cache
 * por alguns minutos. Sem rede, fica a última lista baixada.
 */
@Composable
fun ExplorarTab(shell: Shell, onOpen: (String) -> Unit) {
    var query by rememberSaveable { mutableStateOf("") }
    var category by rememberSaveable { mutableStateOf<String?>(null) }
    var type by rememberSaveable { mutableStateOf(PackType.MOD) }
    LaunchedEffect(Unit) { shell.refreshRemote() }
    // Montada e ordenada só quando a lista online muda (um ícone que chega a
    // troca), não a cada recomposição da aba.
    val all = shell.catalogEntries
    val search = rememberPackageSearchIndex(all)
    val counts = remember(all) { all.groupingBy { it.manifest.packType }.eachCount() }
    val categories = remember(all, type) { packageCategories(all, type) }
    val list = remember(all, type, query, category) {
        searchPackages(all, query, type, category, search)
    }
    val state = rememberLazyListState()
    LaunchedEffect(type, query, category) { state.scrollToItem(0) }

    Column(Modifier.fillMaxSize()) {
        SearchField(query, { query = it }, Modifier.padding(horizontal = EdgePad, vertical = 10.dp))
        PackTypeTabs(type, counts, { type = it; category = null }, Modifier.padding(start = EdgePad, end = EdgePad, bottom = 8.dp))
        CategoryFilter(categories, category, { category = it }, Modifier.padding(horizontal = EdgePad))
        FilterResultLine(list.size, query.isNotBlank() || category != null,
            { query = ""; category = null }, Modifier.padding(horizontal = EdgePad, vertical = 6.dp))
        if (shell.remoteStatus != RemoteStatus.Loading) {
            PixelButton("Atualizar loja", { shell.refreshRemote(force = true) },
                Modifier.padding(start = EdgePad, end = EdgePad, bottom = 8.dp),
                icon = R.drawable.ic_refresh, fontSize = Ts.Small, shadow = false)
        }
        RemoteStatusLine(shell, Modifier.padding(start = EdgePad, end = EdgePad, bottom = 8.dp))
        PauseListAnimations({ state.isScrollInProgress }) {
            Box(Modifier.weight(1f)) {
                LazyColumn(
                    state = state,
                    modifier = Modifier.fillMaxSize(),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(
                        start = EdgePad, end = EdgePad, bottom = 16.dp,
                    ),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    items(list, key = { it.uid }, contentType = { "package" }) { e ->
                        ModRow(e, shell.catalog, { onOpen(e.uid) }) {
                            val progress = shell.downloads[e.uid]
                            when {
                                progress != null -> PixelTag("${(progress * 100).toInt()}%")
                                shell.updateFor(e.uid) != null -> PixelTag("Atualizar", Bl.Good)
                                e.uid in shell.installed -> PixelTag("Instalado")
                            }
                        }
                    }
                    if (list.isEmpty() && shell.remoteStatus != RemoteStatus.Loading) {
                        item { Empty("Nenhum pacote corresponde aos filtros.") }
                    }
                }
                PixelScrollbar(state, Modifier.fillMaxSize())
            }
            }
    }
}

/**
 * Uma linha sobre o catálogo online: buscando, ou por que não veio. Quando deu
 * certo, não diz nada — a lista é a resposta.
 */
@Composable
private fun RemoteStatusLine(shell: Shell, modifier: Modifier = Modifier) {
    val status = shell.remoteStatus
    if (status == RemoteStatus.Idle || status == RemoteStatus.Ready) return
    Row(modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        if (status is RemoteStatus.Failed) {
            PixelText(
                "Catálogo online: ${status.message}." +
                    if (shell.remote.isEmpty()) "" else " Mostrando a última lista baixada.",
                size = Ts.Small, color = Bl.Bad, modifier = Modifier.weight(1f),
            )
            PixelButton("Tentar de novo", { shell.refreshRemote(force = true) },
                fontSize = Ts.Small, shadow = false)
        } else {
            StoreLoadingIcon(Modifier.padding(end = 8.dp))
            val progress = shell.remoteProgress
            PixelText(if (progress == null || progress.total == 0) "Buscando mods online..."
                else "Carregando mods: ${progress.completed}/${progress.total}",
                size = Ts.Small, color = Bl.TextFaint)
        }
    }
}

@Composable
private fun SearchField(value: String, onChange: (String) -> Unit, modifier: Modifier = Modifier) {
    Box(modifier.fillMaxWidth().pixelPanel(fill = Bl.Select, raised = false)
        .padding(horizontal = 12.dp, vertical = 10.dp)) {
        if (value.isEmpty()) {
            PixelText("Nome, autor ou categoria...", size = Ts.Body, color = Bl.TextMuted)
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
                Box(Modifier.semantics { contentDescription = "Limpar busca" }
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
    var category by rememberSaveable { mutableStateOf<String?>(null) }
    var status by rememberSaveable { mutableStateOf(PackageStatus.ALL) }
    var sort by rememberSaveable { mutableStateOf(PackageSort.LOAD) }
    var type by rememberSaveable { mutableStateOf(PackType.MOD) }
    var showFilters by rememberSaveable { mutableStateOf(false) }
    val search = rememberPackageSearchIndex(shell.packages)
    val categories = remember(shell.packages, type) { packageCategories(shell.packages, type) }
    val packages = remember(shell.packages, shell.enabled, status, sort, type, query, category) {
        filterPackages(shell.packages, shell.enabled, status, sort, type, query, category, search)
    }
    val counts = remember(shell.packages) { shell.packages.groupingBy { it.manifest.packType }.eachCount() }
    val manualOrder = status == PackageStatus.ALL && sort == PackageSort.LOAD && query.isBlank() && category == null
    val state = rememberLazyListState()
    LaunchedEffect(status, sort, type, query, category) { state.scrollToItem(0) }
    // texto + deu certo? Uma recusa em verde de sucesso se le como sucesso.
    var aviso by remember { mutableStateOf<Pair<String, Boolean>?>(null) }
    var asking by remember { mutableStateOf(false) }
    var importing by remember { mutableStateOf(false) }

    val picker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) {
            importing = true
            shell.importPackage(uri) { result ->
                importing = false
                aviso = result.fold(
                    onSuccess = { "${it.name} instalado" to true },
                    onFailure = { "Não deu: ${it.message}" to false },
                )
            }
        }
    }

    Column(Modifier.fillMaxSize()) {
        Row(
            Modifier.fillMaxWidth().padding(start = EdgePad, end = EdgePad, top = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            PixelText("Pacotes", size = Ts.Big, modifier = Modifier.weight(1f))
            PixelText("${shell.enabled.size}/${shell.installed.size} ligados",
                size = Ts.Body, color = Bl.TextFaint)
        }

        Box(Modifier.padding(start = EdgePad, end = EdgePad, top = 10.dp)) {
            PixelButton(
                if (importing) "Importando..." else "Importar pacote",
                { if (!importing) asking = true },
                Modifier.fillMaxWidth(),
                icon = R.drawable.ic_folder,
            )
        }
        if (asking) {
            PixelDialog("Importar pacote", onDismiss = { asking = false }) {
                for (line in listOf(
                    "Um mod roda código dentro do jogo: só instale pacotes de quem você confia.",
                    "O pacote é um .bl, .bmod ou .zip com manifest.json e a pasta content/.",
                    "Se o mod já estiver instalado (o mesmo uid), a versão do arquivo substitui a atual.",
                )) {
                    Row {
                        PixelText("•", size = Ts.Body, color = Bl.PressedText, modifier = Modifier.padding(end = 8.dp))
                        PixelText(line, size = Ts.Small, color = Bl.TextDim)
                    }
                }
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    PixelButton("Cancelar", { asking = false }, Modifier.weight(1f), fontSize = Ts.Body, shadow = false)
                    PixelButton("Escolher arquivo", { asking = false; picker.launch(arrayOf("*/*")) },
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
        PackTypeTabs(type, counts, { type = it; category = null }, Modifier.padding(start = EdgePad, end = EdgePad, top = 8.dp))
        FilterResultLine(packages.size, query.isNotBlank() || category != null || status != PackageStatus.ALL || sort != PackageSort.LOAD,
            { query = ""; category = null; status = PackageStatus.ALL; sort = PackageSort.LOAD },
            Modifier.padding(start = EdgePad, end = EdgePad, top = 6.dp),
            onToggle = { showFilters = !showFilters }, expanded = showFilters)
        if (showFilters) {
            CategoryFilter(categories, category, { category = it }, Modifier.padding(horizontal = EdgePad))
            PackageFilterControls(status, { status = it }, sort, { sort = it },
                Modifier.padding(start = EdgePad, end = EdgePad, top = 8.dp))
        } else {
            val selectedFilters = listOfNotNull(category, status.takeIf { it != PackageStatus.ALL }?.label,
                sort.takeIf { it != PackageSort.LOAD }?.label)
            if (selectedFilters.isNotEmpty()) PixelText(selectedFilters.joinToString(" · "),
                size = Ts.Small, color = Bl.TextDim, modifier = Modifier.padding(horizontal = EdgePad))
        }
        PixelText(if (manualOrder) "Segure e arraste dentro do mesmo grupo. Primeiro ligado carrega primeiro."
                  else "Para mover, limpe a busca e escolha Todos e Ordem de carga.", size = Ts.Small, color = Bl.TextFaint,
            modifier = Modifier.padding(start = EdgePad, end = EdgePad, top = 8.dp))

        DraggablePackageList(
            state = state,
            uids = packages.map { it.uid },
            enabledUids = shell.enabled,
            reorderEnabled = manualOrder,
            onDrop = shell::moveModTo,
            modifier = Modifier.weight(1f).fillMaxWidth(),
            empty = { Empty(if (shell.refreshingPackages && shell.packages.isEmpty()) "Lendo pacotes..."
                           else if (shell.installed.isEmpty()) "Nenhum pacote. Pegue um em Explorar ou importe um .bmod."
                           else "Nenhum pacote neste filtro.") },
        ) { index, modifier, preview ->
            val e = packages[index]
            // Ligado: o cartão inteiro fica verde, e a lista se lê de longe.
            val fill = if (e.uid in shell.enabled) Bl.EnabledPanel else Bl.Panel
            // As setas movem em relação ao vizinho VISÍVEL: com o filtro de
            // tipo, o vizinho na ordem completa pode ser de outra lista.
            ModRow(e, shell.catalog, { if (!preview) onOpen(e.uid) }, modifier, fill = fill, besideIcon = if (manualOrder) ({
                val canMoveUp = index > 0 && (packages[index - 1].uid in shell.enabled) == (e.uid in shell.enabled)
                val canMoveDown = index < packages.lastIndex && (packages[index + 1].uid in shell.enabled) == (e.uid in shell.enabled)
                ModOrderButtons(e.manifest.name, canMoveUp, canMoveDown,
                    { if (!preview) shell.moveModTo(e.uid, packages[index - 1].uid) },
                    { if (!preview) shell.moveModTo(e.uid, packages[index + 1].uid) })
            }) else null) {
                SwitchSprite(e.uid in shell.enabled) {
                    if (!preview) shell.setEnabled(e.uid, e.uid !in shell.enabled)
                }
            }
        }
    }
}

private fun packageCategories(packages: List<Catalog.Entry>, type: PackType): List<String> =
    packages.filter { it.manifest.packType == type }.map { it.manifest.category }
        .filter { it.isNotBlank() }.distinct().sorted()

@Composable
private fun rememberPackageSearchIndex(packages: List<Catalog.Entry>): PackageSearchIndex {
    // Ordem e imagens podem mudar sem alterar os campos pesquisáveis.
    val manifests = remember(packages) { packages.map { it.manifest }.toSet() }
    return remember(manifests) { PackageSearchIndex(packages) }
}

@Composable
private fun CategoryFilter(categories: List<String>, selected: String?, onSelect: (String?) -> Unit,
                           modifier: Modifier = Modifier) {
    if (categories.size < 2 && selected == null) return
    Row(modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        for (category in listOf<String?>(null) + categories) {
            Box(Modifier.pixelPanel(fill = if (category == selected) Bl.TitleFill else Bl.Select)
                .pixelClickable { onSelect(category) }.padding(horizontal = 10.dp, vertical = 6.dp)) {
                PixelText(category ?: "Todas as categorias", size = Ts.Small,
                    color = if (category == selected) Bl.PressedText else Bl.TextDim)
            }
        }
    }
}

@Composable
private fun FilterResultLine(count: Int, active: Boolean, onReset: () -> Unit, modifier: Modifier = Modifier,
                             onToggle: (() -> Unit)? = null, expanded: Boolean = false) {
    Row(modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        PixelText("$count ${if (count == 1) "pacote" else "pacotes"}", size = Ts.Small,
            color = Bl.TextFaint, modifier = Modifier.weight(1f))
        if (onToggle != null) {
            Box(Modifier.pixelClickable(onToggle).padding(horizontal = 8.dp, vertical = 4.dp)) {
                PixelText(if (expanded) "Ocultar filtros" else "Filtros", size = Ts.Small, color = Bl.PressedText)
            }
        }
        if (active) {
            Box(Modifier.pixelClickable(onReset).padding(horizontal = 8.dp, vertical = 4.dp)) {
                PixelText("Limpar filtros", size = Ts.Small, color = Bl.PressedText)
            }
        }
    }
}

@Composable
private fun PackageFilterControls(
    status: PackageStatus, onStatus: (PackageStatus) -> Unit,
    sort: PackageSort, onSort: (PackageSort) -> Unit, modifier: Modifier = Modifier,
) {
    val show: @Composable (Modifier) -> Unit = { m ->
        Column(m) {
            PixelText("Mostrar", size = Ts.Small, color = Bl.TextFaint, modifier = Modifier.padding(bottom = 4.dp))
            PixelSelect(status.label,
                { onStatus(PackageStatus.entries[(status.ordinal - 1).mod(PackageStatus.entries.size)]) },
                { onStatus(PackageStatus.entries[(status.ordinal + 1).mod(PackageStatus.entries.size)]) },
                label = "Mostrar mods")
        }
    }
    val order: @Composable (Modifier) -> Unit = { m ->
        Column(m) {
            PixelText("Ordenar", size = Ts.Small, color = Bl.TextFaint, modifier = Modifier.padding(bottom = 4.dp))
            PixelSelect(sort.label,
                { onSort(PackageSort.entries[(sort.ordinal - 1).mod(PackageSort.entries.size)]) },
                { onSort(PackageSort.entries[(sort.ordinal + 1).mod(PackageSort.entries.size)]) },
                label = "Ordenar mods")
        }
    }
    BoxWithConstraints(modifier.fillMaxWidth()) {
        if (maxWidth >= 320.dp) {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                show(Modifier.weight(1f))
                order(Modifier.weight(1f))
            }
        } else {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                show(Modifier.fillMaxWidth())
                order(Modifier.fillMaxWidth())
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

    Box {
        Column(Modifier.fillMaxSize().verticalScroll(scroll).padding(horizontal = EdgePad)) {
            ProfileHeader(prefs, Modifier.padding(top = 14.dp))

            SectionTitle("Coleção")
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Stat("${shell.installed.size}", "instalados", Modifier.weight(1f))
                Stat("${shell.enabled.size}", "ligados", Modifier.weight(1f))
                Stat("${shell.catalogEntries.size}", "no catálogo", Modifier.weight(1f))
            }

            SectionTitle("Mundos e personagens")
            SavesCard()

            SectionTitle("Aparência")
            SceneryPicker(scenery, onScenery)
            Setting(
                "Efeitos do tema",
                "Cada cenário tem o seu: folhas e cartões balançando na Floresta, chuva e brilho " +
                    "molhado no Oceano, bolhas no Lago, neve na Neve. Desligado, o fundo fica parado.",
                themeEffects,
                onThemeEffects,
            )

            SectionTitle("Mods")
            Setting(
                "Ligar ao instalar",
                "Um pacote recém-instalado já entra valendo no próximo boot.",
                enableNewMods,
            ) { enableNewMods = it; prefs.enableOnInstall = it }
            Setting(
                "Mostrar erro dentro do jogo",
                "Quando um mod quebra, o log aparece na tela em vez de só no logcat.",
                errorPanel,
            ) { errorPanel = it; prefs.errorPanel = it }
            Setting(
                "Log detalhado",
                "O log de sessão (pasta logs/) ganha cada tabela, hook e tipo que o núcleo " +
                    "registra. Desligado, fica o resumo e as linhas dos mods.",
                verboseLog,
            ) { verboseLog = it; prefs.verboseLog = it }
            Setting(
                "Canal de comando por arquivo",
                "Aceita comandos via adb durante o jogo. Só para desenvolver.",
                devChannel,
            ) { devChannel = it; prefs.devChannel = it }

            SectionTitle("Desenvolvedor")
            QuickStartCard(prefs)
            Setting(
                "Mod Menu",
                "O botão do coelho dentro do jogo: poderes, itens, NPCs e buffs. Desligado, " +
                    "nenhum poder volta ligado da última partida.",
                modMenu,
            ) { modMenu = it; prefs.devModMenu = it }
            Setting(
                "Editor de JS",
                "Roda JavaScript dentro do jogo. Sem o Mod Menu, o botão flutuante vira o " +
                    "de JS e abre o Editor.",
                editor,
            ) { editor = it; prefs.devEditor = it }
            Setting(
                "Reiniciar",
                "O botão que fecha e abre o jogo lendo os mods da pasta de novo (no Mod Menu " +
                    "e no Editor).",
                restart,
            ) { restart = it; prefs.devRestart = it }

            SectionTitle("Diagnóstico")
            PixelCard(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp)) {
                    PixelText(
                        "O jogo fechou sozinho, travou ou deu erro? Abra o relatório e mande o " +
                            "texto (Copiar) ou um print para quem cuida do Bunny Loader ou do mod.",
                        size = Ts.Small, color = Bl.TextDim,
                    )
                    Row(
                        Modifier.padding(top = 10.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        PixelButton("Ver relatório", { log = BootLog.read(ctx) },
                            fontSize = Ts.Small, shadow = false)
                        if (log.isNotEmpty()) {
                            PixelButton("Copiar", {
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

            SectionTitle("Sobre")
            PixelCard(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp)) {
                    Field("Bunny Loader", "v${dev.bunnyloader.BuildConfig.VERSION_NAME}")
                    Spacer(Modifier.height(8.dp))
                    Field(
                        "Terraria embutido",
                        "${BundledRuntime.VERSION_NAME} (${BundledRuntime.VERSION_CODE})",
                    )
                }
            }
            Spacer(Modifier.height(16.dp))
        }
        PixelScrollbar(scroll, Modifier.fillMaxSize())
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
    val label = Biome.entries.firstOrNull { it.name == choice }?.label ?: "Automático"
    PixelCard(Modifier.fillMaxWidth().padding(bottom = 8.dp)) {
        Column(Modifier.padding(12.dp).fillMaxWidth()) {
            PixelText("Cenário", size = Ts.Item)
            PixelText("O fundo do launcher. O céu segue o relógio do celular; no automático, o cenário troca a cada abertura.",
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
    val players = remember { SaveFiles.players(ctx) }
    val worlds = remember { SaveFiles.worlds(ctx) }
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

    androidx.compose.runtime.LaunchedEffect(on, player?.file, world?.file) {
        if (!on) return@LaunchedEffect
        prefs.quickPlayer = player?.file ?: ""
        prefs.quickWorld = world?.file ?: Prefs.TITLE_ONLY
    }

    PixelCard(Modifier.fillMaxWidth().padding(bottom = 8.dp)) {
        Column(Modifier.padding(12.dp).fillMaxWidth()) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f).padding(end = 8.dp)) {
                    PixelText("Início rápido", size = Ts.Item)
                    PixelText(
                        "A logo da Re-Logic some assim que o jogo termina de carregar, e o jogo " +
                            "entra direto no mundo abaixo. Vale também para o Reiniciar do Mod Menu.",
                        size = Ts.Small, color = Bl.TextFaint,
                    )
                }
                SwitchSprite(on) { on = !on; prefs.quickStart = on }
            }
            if (on) {
                if (player == null) {
                    PixelText(
                        "Nenhum personagem salvo ainda. Crie um no jogo; até lá, o jogo para no título.",
                        size = Ts.Small, color = Bl.TextDim, modifier = Modifier.padding(top = 10.dp),
                    )
                } else {
                    PixelText("Personagem", size = Ts.Small, color = Bl.TextFaint,
                        modifier = Modifier.padding(top = 10.dp))
                    val pi = players.indexOf(player)
                    PixelSelect(
                        saveLabel(player),
                        onPrev = { playerFile = players[(pi - 1).mod(players.size)].file },
                        onNext = { playerFile = players[(pi + 1).mod(players.size)].file },
                        modifier = Modifier.padding(top = 4.dp),
                    )
                    PixelText("Mundo", size = Ts.Small, color = Bl.TextFaint,
                        modifier = Modifier.padding(top = 8.dp))
                    val wi = worldOptions.indexOf(world)
                    val pickWorld = { i: Int ->
                        worldFile = worldOptions[i.mod(worldOptions.size)]?.file ?: Prefs.TITLE_ONLY
                    }
                    PixelSelect(
                        world?.let { saveLabel(it) } ?: "Nenhum: parar no título",
                        onPrev = { pickWorld(wi - 1) },
                        onNext = { pickWorld(wi + 1) },
                        modifier = Modifier.padding(top = 4.dp),
                    )
                    if (fitting.isEmpty()) {
                        PixelText(
                            if (player.journey) "Nenhum mundo de Jornada salvo." else "Nenhum mundo salvo.",
                            size = Ts.Small, color = Bl.TextDim, modifier = Modifier.padding(top = 6.dp),
                        )
                    }
                }
            }
        }
    }
}

private fun saveLabel(save: SaveFiles.Save) = if (save.journey) "${save.name} (Jornada)" else save.name

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
