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
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.bunnyloader.R
import dev.bunnyloader.game.BootLog
import dev.bunnyloader.game.BundledRuntime
import dev.bunnyloader.game.Eligibility
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.formatSize

private val EdgePad = 14.dp

// =============================== Início ===============================

/**
 * A vitrine. Um mod pede destaque no próprio mod.json (`featured`); o resto
 * entra em "Populares". Nenhum número inventado aqui — não há contagem de
 * download nem nota, porque não há servidor para produzir isso.
 */
@Composable
fun InicioTab(shell: Shell, onOpen: (String) -> Unit) {
    val scroll = rememberScrollState()
    Box {
        Column(Modifier.fillMaxSize().verticalScroll(scroll)) {
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

            val featured = shell.entries.firstOrNull { it.manifest.featured }
                ?: shell.entries.firstOrNull()
            if (featured != null) {
                SectionTitle("Em destaque")
                FeaturedCard(featured, shell, onOpen)
            }

            val populares = shell.entries.filter { it.uid != featured?.uid }.take(3)
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
                    PixelText("por ${entry.manifest.author}",                         size = Ts.Small, color = Bl.TextFaint)
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

/** Tudo que vem dentro do app. É daqui que um mod vai parar em Pacotes. */
@Composable
fun ExplorarTab(shell: Shell, onOpen: (String) -> Unit) {
    var query by remember { mutableStateOf("") }
    val list = shell.entries.filter {
        query.isBlank() ||
            it.manifest.name.contains(query, true) ||
            it.manifest.category.contains(query, true) ||
            it.manifest.author.contains(query, true)
    }
    val state = rememberLazyListState()

    Column(Modifier.fillMaxSize()) {
        SearchField(query, { query = it }, Modifier.padding(horizontal = EdgePad, vertical = 10.dp))
        Box(Modifier.weight(1f)) {
            LazyColumn(
                state = state,
                modifier = Modifier.fillMaxSize(),
                contentPadding = androidx.compose.foundation.layout.PaddingValues(
                    start = EdgePad, end = EdgePad, bottom = 16.dp,
                ),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                items(list, key = { it.uid }) { e ->
                    ModRow(e, shell.catalog, { onOpen(e.uid) }) {
                        if (e.uid in shell.installed) PixelTag("Instalado")
                    }
                }
                if (list.isEmpty()) {
                    item { Empty("Nada com \"$query\".") }
                }
            }
            PixelScrollbar(state, Modifier.fillMaxSize())
        }
    }
}

@Composable
private fun SearchField(value: String, onChange: (String) -> Unit, modifier: Modifier = Modifier) {
    Box(modifier.fillMaxWidth().pixelPanel(fill = Bl.Select, raised = false)
        .padding(horizontal = 12.dp, vertical = 10.dp)) {
        if (value.isEmpty()) {
            PixelText("Procurar mod...", size = Ts.Body, color = Bl.TextMuted)
        }
        BasicTextField(
            value = value,
            onValueChange = onChange,
            singleLine = true,
            textStyle = TextStyle(
                fontFamily = PixelFont, fontSize = Ts.Body.sp, color = Bl.Text,
            ),
            cursorBrush = SolidColor(Bl.PressedText),
            modifier = Modifier.fillMaxWidth(),
        )
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
    val doCatalogo = shell.entries.filter { it.uid in shell.installed }
    val state = rememberLazyListState()
    // texto + deu certo? Uma recusa em verde de sucesso se le como sucesso.
    var aviso by remember { mutableStateOf<Pair<String, Boolean>?>(null) }

    val picker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) {
            aviso = shell.importPackage(uri).fold(
                onSuccess = { "${it.name} instalado" to true },
                onFailure = { "Não deu: ${it.message}" to false },
            )
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
                "Importar pacote",
                { picker.launch(arrayOf("*/*")) },
                Modifier.fillMaxWidth(),
                icon = R.drawable.ic_folder,
            )
        }
        aviso?.let { (texto, ok) ->
            PixelText(texto, size = Ts.Small, color = if (ok) Bl.TextDim else Bl.Bad,
                modifier = Modifier.padding(start = EdgePad, end = EdgePad, top = 6.dp))
        }

        Box(Modifier.weight(1f)) {
            LazyColumn(
                state = state,
                modifier = Modifier.fillMaxSize(),
                contentPadding = androidx.compose.foundation.layout.PaddingValues(
                    start = EdgePad, end = EdgePad, top = 10.dp, bottom = 16.dp,
                ),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                items(doCatalogo, key = { it.uid }) { e ->
                    ModRow(e, shell.catalog, { onOpen(e.uid) }) {
                        SwitchSprite(e.uid in shell.enabled) {
                            shell.setEnabled(e.uid, e.uid !in shell.enabled)
                        }
                    }
                }
                // Importado (ou colado em bunny_packs): a mesma linha e a mesma
                // ficha dos do catalogo, com o icone e a capa do proprio pacote.
                items(shell.imported, key = { it.uid }) { e ->
                    ModRow(e, shell.catalog, { onOpen(e.uid) }) {
                        SwitchSprite(e.uid in shell.enabled) {
                            shell.setEnabled(e.uid, e.uid !in shell.enabled)
                        }
                    }
                }
                if (shell.installed.isEmpty()) {
                    item { Empty("Nenhum pacote. Pegue um em Explorar ou importe um .bmod.") }
                }
            }
            PixelScrollbar(state, Modifier.fillMaxSize())
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
fun ConfigTab(shell: Shell, scenery: String, onScenery: (String) -> Unit) {
    val ctx = LocalContext.current
    val clipboard = LocalClipboardManager.current
    val scroll = rememberScrollState()
    var log by remember { mutableStateOf("") }
    val prefs = remember { Prefs(ctx) }
    var novosLigados by remember { mutableStateOf(prefs.enableOnInstall) }
    var canalDev by remember { mutableStateOf(prefs.devChannel) }
    var erroNoJogo by remember { mutableStateOf(prefs.errorPanel) }

    Box {
        Column(Modifier.fillMaxSize().verticalScroll(scroll).padding(horizontal = EdgePad)) {
            Row(
                Modifier.fillMaxWidth().padding(top = 14.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                // A arte ocupa os 104x96 inteiros dela, sem margem transparente:
                // encolher para caber dentro do quadro só deixava buraco.
                Box(Modifier.size(72.dp).framePanel().padding(3.dp)) {
                    Image(
                        bitmap = ImageBitmap.imageResource(R.drawable.ic_tab_config),
                        contentDescription = null,
                        filterQuality = FilterQuality.None,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.fillMaxSize(),
                    )
                }
                Column(Modifier.padding(start = 12.dp)) {
                    PixelText("Jogador", size = Ts.Big)
                    PixelText("Terraria ${BundledRuntime.VERSION_NAME}",
                        size = Ts.Body, color = Bl.TextFaint)
                }
            }

            SectionTitle("Coleção")
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Stat("${shell.installed.size}", "instalados", Modifier.weight(1f))
                Stat("${shell.enabled.size}", "ligados", Modifier.weight(1f))
                Stat("${shell.entries.size}", "no catálogo", Modifier.weight(1f))
            }

            SectionTitle("Aparência")
            SceneryPicker(scenery, onScenery)

            SectionTitle("Mods")
            Setting(
                "Ligar ao instalar",
                "Um pacote recém-instalado já entra valendo no próximo boot.",
                novosLigados,
            ) { novosLigados = it; prefs.enableOnInstall = it }
            Setting(
                "Mostrar erro dentro do jogo",
                "Quando um mod quebra, o log aparece na tela em vez de só no logcat.",
                erroNoJogo,
            ) { erroNoJogo = it; prefs.errorPanel = it }
            Setting(
                "Canal de comando por arquivo",
                "Aceita comandos via adb durante o jogo. Só para desenvolver.",
                canalDev,
            ) { canalDev = it; prefs.devChannel = it }

            SectionTitle("Diagnóstico")
            PixelCard(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp)) {
                    PixelText(remember { Eligibility.check(ctx).detail },
                        size = Ts.Small, color = Bl.TextDim)
                    Row(
                        Modifier.padding(top = 10.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        PixelButton("Ver log", { log = BootLog.read(ctx) }, fontSize = Ts.Small)
                        if (log.isNotEmpty()) {
                            PixelButton("Copiar", {
                                clipboard.setText(AnnotatedString(log))
                            }, fontSize = Ts.Small)
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

// ============================ ficha do mod ============================

@Composable
fun ModDetail(entry: Catalog.Entry, shell: Shell, onBack: () -> Unit) {
    val scroll = rememberScrollState()
    var favorite by remember { mutableStateOf(false) }
    val installed = entry.uid in shell.installed
    val m = entry.manifest

    Box {
        Column(Modifier.fillMaxSize().verticalScroll(scroll)) {
            // Topo: voltar e favoritar, flutuando sobre o banner.
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

            // Um painel para o texto da ficha: solto sobre o cenário, descrição e
            // campos se perdiam no fundo claro (neve, céu de dia).
            Column(
                Modifier.padding(horizontal = EdgePad).padding(top = 10.dp)
                    .pixelShadow().pixelPanel().padding(horizontal = 12.dp, vertical = 4.dp)
            ) {
                Row(
                    Modifier.fillMaxWidth().padding(top = 12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    ModIcon(entry, shell.catalog, 56.dp)
                    Column(Modifier.weight(1f).padding(horizontal = 10.dp)) {
                        PixelText(m.name, size = Ts.Big, color = Bl.Text)
                        PixelText("por ${m.author}", size = Ts.Body,
                            color = Bl.TextFaint)
                    }
                    PixelTag(m.category, categoryColor(m.category))
                }

                // Só o tamanho: estrela e contagem de download precisariam de um
                // servidor que não existe, e número inventado é pior que nada.
                Row(Modifier.padding(top = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                    PixelIcon(R.drawable.ic_folder, 16.dp)
                    PixelText(formatSize(entry.sizeBytes), size = Ts.Body,
                        color = Bl.TextDim, modifier = Modifier.padding(start = 6.dp))
                }

                SectionTitle("Descrição")
                PixelText(
                    m.description.ifBlank { m.summary },
                    size = Ts.Body, color = Bl.TextDim,
                )

                if (entry.previews.isNotEmpty()) {
                    SectionTitle("Imagens")
                    Row(
                        Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        for (p in entry.previews) {
                            shell.catalog.loadBitmap(p)?.let {
                                Image(it, null, filterQuality = FilterQuality.None,
                                    modifier = Modifier.height(96.dp).framePanel().padding(2.dp))
                            }
                        }
                    }
                }

                Row(Modifier.fillMaxWidth().padding(top = 18.dp)) {
                    Field("Versão", "v${m.version}", Modifier.weight(1f))
                    Field("Última atualização",
                        dev.bunnyloader.mods.formatDate(m.updated), Modifier.weight(1f))
                }
                // O uid é a identidade real do pacote; aparece pequeno porque
                // quem precisa dele está depurando ou empacotando.
                PixelText(entry.uid, size = Ts.Tiny, color = Bl.TextMuted,
                    modifier = Modifier.padding(top = 10.dp))

                Spacer(Modifier.height(18.dp))
                if (!installed) {
                    PixelButton("Baixar Mod", { shell.install(entry) },
                        Modifier.fillMaxWidth(), icon = R.drawable.ic_start)
                } else {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Box(Modifier.weight(1f).pixelPanel()
                            .padding(vertical = 10.dp), contentAlignment = Alignment.Center) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                PixelText(
                                    if (entry.uid in shell.enabled) "Ligado" else "Desligado",
                                    size = Ts.Item,
                                    color = if (entry.uid in shell.enabled) Bl.PressedText else Bl.TextFaint,
                                )
                                SwitchSprite(entry.uid in shell.enabled) {
                                    shell.setEnabled(entry.uid, entry.uid !in shell.enabled)
                                }
                            }
                        }
                        PixelButton("Remover", { shell.uninstall(entry.uid) },
                            icon = R.drawable.ic_trash, fontSize = Ts.Body)
                    }
                }
                Spacer(Modifier.height(20.dp))
            }
        }
        PixelScrollbar(scroll, Modifier.fillMaxSize())
    }
}

@Composable
private fun RoundIcon(res: Int, onClick: () -> Unit) {
    Box(
        Modifier.size(38.dp).framePanel().pixelClickable(onClick),
        contentAlignment = Alignment.Center,
    ) {
        PixelIcon(res, 20.dp)
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
