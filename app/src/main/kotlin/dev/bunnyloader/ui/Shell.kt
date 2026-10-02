package dev.bunnyloader.ui

import android.content.Context
import android.net.Uri
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.mods.RemoteCatalog
import dev.bunnyloader.mods.compareVersions
import dev.bunnyloader.mods.networkMessage
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.MainScope
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

/**
 * O estado que as quatro abas compartilham.
 *
 * Instalado e ligado são conjuntos de id observáveis, recalculados a cada
 * mudança, em vez de cada tela perguntar ao disco quando bem entende: assim a
 * lista de Pacotes e o botão da ficha do mod nunca discordam.
 */
class Shell(context: Context) {
    val catalog = Catalog(context)
    private val repo = ModRepository(context)

    var installed by mutableStateOf(emptySet<String>())
        private set
    var enabled by mutableStateOf(emptySet<String>())
        private set

    /**
     * Mods que vieram de fora, lidos do disco (bunny_packs), com ícone, capa e
     * imagens do próprio pacote.
     *
     * O catálogo embutido é fixo; um pacote importado não está nele, e sem isto
     * ele instalaria e não apareceria em lugar nenhum.
     *
     * Declarado ANTES do init: Kotlin inicializa na ordem do arquivo, e o
     * `refresh()` do init escrevia num campo que ainda era nulo.
     */
    var imported by mutableStateOf(emptyList<Catalog.Entry>())
        private set
    var packages by mutableStateOf(emptyList<Catalog.Entry>())
        private set
    /** Versão no disco de cada mod instalado, pelo uid: é o que acusa atualização. */
    private var installedVersions by mutableStateOf(emptyMap<String, String>())

    private val remoteCatalog = RemoteCatalog(context, catalog)
    private val scope = MainScope()
    /** Um import por vez: todos desempacotam na mesma pasta temporária. */
    private val installLock = Mutex()

    /**
     * O catálogo online (RemoteCatalog). Começa com a última lista baixada,
     * para a aba não abrir vazia sem rede.
     */
    var remote by mutableStateOf(remoteCatalog.cached())
        private set
    var remoteStatus by mutableStateOf<RemoteStatus>(RemoteStatus.Idle)
        private set
    private var remoteFetchedAt = 0L

    /** Progresso (0..1) de cada download em andamento, pelo uid. */
    var downloads by mutableStateOf(emptyMap<String, Float>())
        private set
    /** O último erro de download de cada mod, para a ficha mostrar no lugar do botão. */
    var downloadErrors by mutableStateOf(emptyMap<String, String>())
        private set

    init {
        catalog.seedOnFirstRun()
        catalog.refreshInstalledOnUpdate()
        refresh()
    }

    val entries get() = catalog.entries

    /**
     * O que dá para instalar: os embutidos e o catálogo online, por nome. Um
     * mod que está nos dois aparece uma vez só, o embutido.
     */
    val catalogEntries: List<Catalog.Entry>
        get() {
            val builtIn = catalog.entries.map { it.uid }.toSet()
            return (catalog.entries + remote.filter { it.uid !in builtIn })
                .sortedBy { it.manifest.name.lowercase() }
        }
    fun entry(uid: String) = catalog.entries.firstOrNull { it.uid == uid }
        ?: imported.firstOrNull { it.uid == uid }
        ?: remote.firstOrNull { it.uid == uid }

    /** A versão online, se ela for mais nova que a instalada. */
    fun updateFor(uid: String): Catalog.Entry? {
        val local = installedVersions[uid] ?: return null
        return remote.firstOrNull { it.uid == uid }
            ?.takeIf { compareVersions(it.manifest.version, local) > 0 }
    }

    /**
     * Baixa a lista online. `force` ignora o intervalo: é o botão de tentar de
     * novo. Sem ele, voltar à aba não pede a lista outra vez a cada toque.
     */
    fun refreshRemote(force: Boolean = false) {
        if (remoteStatus == RemoteStatus.Loading) return
        if (!force && System.currentTimeMillis() - remoteFetchedAt < REMOTE_TTL_MS) return
        remoteStatus = RemoteStatus.Loading
        scope.launch {
            val result = withContext(Dispatchers.IO) { runCatching { remoteCatalog.refresh() } }
            result.onSuccess {
                remote = it
                remoteFetchedAt = System.currentTimeMillis()
                remoteStatus = RemoteStatus.Ready
            }.onFailure {
                remoteStatus = RemoteStatus.Failed(networkMessage(it))
            }
        }
    }

    /**
     * Baixa o pacote, confere o sha256 e instala pelo mesmo import do seletor
     * de arquivos. Roda no escopo do Shell, não da tela: sair da ficha no meio
     * não corta o download.
     */
    fun installRemote(entry: Catalog.Entry) {
        val mod = entry.remote ?: return
        if (mod.uid in downloads) return
        downloads = downloads + (mod.uid to 0f)
        downloadErrors = downloadErrors - mod.uid
        scope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching {
                    var shown = -1
                    val file = remoteCatalog.download(mod) { p ->
                        val pct = (p * 100).toInt()
                        if (pct != shown) {
                            shown = pct
                            scope.launch { downloads = downloads + (mod.uid to p) }
                        }
                    }
                    try {
                        installLock.withLock { repo.import(file).getOrThrow() }
                    } finally {
                        file.delete()
                    }
                }
            }
            downloads = downloads - mod.uid
            result.onFailure {
                downloadErrors = downloadErrors + (mod.uid to networkMessage(it))
            }
            refresh()
        }
    }

    fun install(entry: Catalog.Entry) {
        catalog.install(entry)
        refresh()
    }

    /** @return o nome do mod, ou a mensagem do que deu errado. */
    fun importPackage(uri: Uri): Result<ModManifest> =
        repo.import(uri).also { refresh() }

    fun exportMod(uid: String, uri: Uri): Result<Unit> = repo.export(uid, uri)

    fun uninstall(uid: String) {
        catalog.uninstall(uid)
        refresh()
    }

    fun setEnabled(uid: String, on: Boolean) {
        repo.setEnabled(uid, on)
        refresh()
    }

    fun moveMod(uid: String, direction: Int) {
        repo.move(uid, direction)
        refresh()
    }

    fun moveModTo(uid: String, targetUid: String) {
        repo.moveTo(uid, targetUid)
        refresh()
    }

    /**
     * Relê o disco. Chamado quando o launcher volta à frente: quem editou ou
     * colou um pacote em bunny_packs pelo gerenciador de arquivos vê na hora.
     */
    fun rescan() = refresh()

    private fun refresh() {
        val catalogIds = catalog.entries.map { it.uid }.toSet()
        val onDisk = repo.list()
        installed = onDisk.map { it.uid }.toSet()
        installedVersions = onDisk.associate { it.uid to it.version }
        packages = onDisk.mapNotNull { m ->
            repo.dirOf(m.uid)?.let { catalog.fromDisk(m, it) }
        }
        imported = packages.filter { it.uid !in catalogIds }
        // Pacote do formato antigo não carrega: aparece desligado.
        enabled = onDisk.filter { !it.isOutdated && repo.isEnabled(it.uid) }.map { it.uid }.toSet()
    }
}

sealed interface RemoteStatus {
    data object Idle : RemoteStatus
    data object Loading : RemoteStatus
    data object Ready : RemoteStatus
    data class Failed(val message: String) : RemoteStatus
}

/** Voltar à aba Explorar dentro disto não baixa a lista de novo. */
private const val REMOTE_TTL_MS = 10 * 60 * 1000L
