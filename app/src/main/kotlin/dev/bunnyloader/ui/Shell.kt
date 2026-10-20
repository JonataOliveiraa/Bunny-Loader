package dev.bunnyloader.ui

import android.content.Context
import android.net.Uri
import androidx.compose.runtime.getValue
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.CatalogProgress
import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.ModOrder
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.mods.RemoteCatalog
import dev.bunnyloader.mods.compareVersions
import dev.bunnyloader.mods.networkMessage
import dev.bunnyloader.mods.unseenModUpdates
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.MainScope
import kotlinx.coroutines.cancel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.sync.withPermit
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
    private val repo by lazy { ModRepository(context) }
    val modSources = dev.bunnyloader.mods.ModSources(
        java.io.File(context.getExternalFilesDir(null) ?: context.filesDir, "ModSources"),
        { context.assets.open("templates/basic-sword.png") },
    )
    private val sourceBuilds = java.io.File(context.cacheDir, "ModBuilds")
    private val sourceContext = context.applicationContext
    val sourceLocations = dev.bunnyloader.mods.SourceLocations(sourceContext, modSources).apply {
        trees = Prefs(sourceContext).sourceTrees
    }
    var sourceFolders by mutableStateOf(sourceLocations.locations)
        private set
    var sourceProjects by mutableStateOf(emptyList<dev.bunnyloader.mods.ModSources.Project>())
        private set
    var sourceBusy by mutableStateOf(false)
        private set
    var sourceMessage by mutableStateOf<String?>(null)
        private set

    fun sourceTask(operation: () -> String) {
        if (sourceBusy) return
        sourceBusy = true
        sourceMessage = null
        scope.launch {
            try {
                sourceMessage = withContext(Dispatchers.IO) { operation() }
                sourceProjects = withContext(Dispatchers.IO) { sourceLocations.list() }
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                sourceMessage = tr("Não deu: %1\$s", e.message)
            } finally {
                sourceBusy = false
            }
        }
    }

    fun refreshSources() = sourceTask { "" }

    fun addSourceFolder(uri: Uri) {
        sourceTask {
            try {
                sourceContext.contentResolver.takePersistableUriPermission(uri,
                    android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION or android.content.Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
            } catch (e: Exception) {
                throw IllegalArgumentException(tr("Não foi possível guardar a permissão desta pasta."), e)
            }
            sourceLocations.validateTree(uri)
            val updated = Prefs(sourceContext).sourceTrees + uri.toString()
            Prefs(sourceContext).sourceTrees = updated
            sourceLocations.trees = updated
            val folders = sourceLocations.locations
            scope.launch { sourceFolders = folders }
            tr("Local adicionado.")
        }
    }

    fun removeSourceFolder(id: String) {
        if (id.isEmpty()) return
        sourceTask {
            val updated = Prefs(sourceContext).sourceTrees - id
            Prefs(sourceContext).sourceTrees = updated
            sourceLocations.trees = updated
            val folders = sourceLocations.locations
            scope.launch { sourceFolders = folders }
            // Removing a scan root does not delete its files.
            runCatching {
                sourceContext.contentResolver.releasePersistableUriPermission(Uri.parse(id),
                    android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION or android.content.Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
            }
            tr("Local removido. Os arquivos foram mantidos.")
        }
    }

    fun sourceFolderUri(key: String): Uri = sourceLocations.folderUri(key)

    fun createSource(
        id: String,
        name: String,
        author: String,
        basicSword: String = "",
        language: String = UiLocalization.language,
        location: String = "",
        onCreated: (String) -> Unit,
    ) {
        sourceTask {
            val project = sourceLocations.create(id.trim(), name.trim(), author.trim(), basicSword.trim(), language, location)
            scope.launch { onCreated(project.key) }
            tr("Projeto criado.")
        }
    }

    fun buildSource(key: String, install: Boolean, destination: Uri? = null) {
        if (sourceBusy) return
        sourceBusy = true
        sourceMessage = null
        scope.launch {
            try {
                val (result, outputPath) = installLock.withLock {
                    require(!install || (pendingUpdate == null && importProgress == null)) {
                        tr("Aguarde a importação terminar antes de instalar o projeto.")
                    }
                    withContext(Dispatchers.IO) {
                        val output = java.io.File(sourceBuilds, "source-${java.util.UUID.randomUUID()}.bl")
                        try {
                            val manifest = sourceLocations.build(key, output)
                            if (destination != null) {
                                requireNotNull(sourceContext.contentResolver.openOutputStream(destination, "wt")) {
                                    "não consegui criar o arquivo"
                                }.use { target -> output.inputStream().use { it.copyTo(target) } }
                            }
                            if (install) {
                                repo.import(output).getOrThrow()
                                if (Prefs(sourceContext).enableOnInstall) repo.setEnabled(manifest.uid, true)
                            }
                            manifest to output.absolutePath
                        } finally {
                            if (install || destination != null) output.delete()
                        }
                    }
                }
                sourceProjects = withContext(Dispatchers.IO) { sourceLocations.list() }
                sourceMessage = if (install) tr("%1\$s construído e instalado. Reabra o jogo para carregar.", result.name)
                    else if (destination != null) tr("%1\$s exportado", result.name)
                    else tr("Pacote criado: %1\$s", outputPath)
                if (install) rescan()
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                sourceMessage = tr("Não deu: %1\$s", e.message)
            } finally {
                sourceBusy = false
            }
        }
    }

    var installed by mutableStateOf(emptySet<String>())
        private set
    var enabled by mutableStateOf(emptySet<String>())
        private set
    var entries by mutableStateOf(emptyList<Catalog.Entry>())
        private set
    var refreshingPackages by mutableStateOf(true)
        private set
    var packageError by mutableStateOf<String?>(null)
        private set
    private var refreshRequested = false
    private var refreshRunning = false

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
    private val downloadSlots = Semaphore(3)
    /** Arquivos da vitrine online (ícone, capa, ficha): poucos de cada vez. */
    private val fileSlots = Semaphore(2)
    private val filesInFlight = mutableSetOf<String>()
    private val filesLoaded = mutableSetOf<String>()
    private val remoteMedia = StoreMedia()
    private val noticePrefs = context.getSharedPreferences("mod_update_notices", Context.MODE_PRIVATE)
    private var announcedVersions = noticePrefs.all.mapNotNull { (uid, value) ->
        (value as? String)?.let { uid to it }
    }.toMap()

    var updateNotices by mutableStateOf(emptyList<Catalog.Entry>())
        private set

    /**
     * O catálogo online (RemoteCatalog). O cache é lido fora da thread da UI,
     * antes da busca na rede, para abrir mesmo com muitas vitrines no disco.
     */
    var remote by mutableStateOf(emptyList<Catalog.Entry>())
        private set
    var remoteStatus by mutableStateOf<RemoteStatus>(RemoteStatus.Idle)
        private set
    var remoteProgress by mutableStateOf<CatalogProgress?>(null)
        private set
    private var remoteFetchedAt = 0L
    private var remoteCacheLoaded = false

    /** Progresso (0..1) de cada download em andamento, pelo uid. */
    var downloads by mutableStateOf(emptyMap<String, Float>())
        private set
    /** O último erro de download de cada mod, para a ficha mostrar no lugar do botão. */
    var downloadErrors by mutableStateOf(emptyMap<String, String>())
        private set

    init {
        scope.launch {
            try {
                entries = installLock.withLock {
                    withContext(Dispatchers.IO) {
                        catalog.seedOnFirstRun()
                        catalog.refreshInstalledOnUpdate()
                        catalog.entries
                    }
                }
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                packageError = e.message ?: tr("Não consegui preparar os pacotes.")
            }
            rescan()
        }
    }

    fun close() = scope.cancel()

    /**
     * O que dá para instalar: os embutidos e o catálogo online, por nome. Um
     * mod que está nos dois aparece uma vez só, o embutido.
     */
    private val mergedCatalog = derivedStateOf {
        val builtIn = entries.map { it.uid }.toSet()
        (entries + remote.filter { it.uid !in builtIn }).sortedBy { it.manifest.nameFor(UiLocalization.language).lowercase() }
    }
    val catalogEntries: List<Catalog.Entry> get() = mergedCatalog.value
    fun entry(uid: String) = entries.firstOrNull { it.uid == uid }
        ?: imported.firstOrNull { it.uid == uid }
        ?: remote.firstOrNull { it.uid == uid }?.let(::mediaEntry)

    /** File arrival updates only consumers of this uid, not the search/catalog index. */
    fun mediaEntry(entry: Catalog.Entry): Catalog.Entry = remoteMedia.entryFor(entry)

    /** A versão online, se ela for mais nova que a instalada. */
    fun updateFor(uid: String): Catalog.Entry? {
        val local = installedVersions[uid] ?: return null
        return remote.firstOrNull { it.uid == uid }
            ?.takeIf { compareVersions(it.manifest.version, local) > 0 }
    }

    fun installedVersion(uid: String): String? = installedVersions[uid]

    /** Chamado quando o popup fica visível, não ao terminar a busca em segundo plano. */
    fun markUpdateNoticesShown() {
        val notices = updateNotices
        if (notices.isEmpty()) return
        noticePrefs.edit().apply {
            for (entry in notices) putString(entry.uid, entry.manifest.version)
        }.apply()
        announcedVersions = announcedVersions + notices.associate { it.uid to it.manifest.version }
    }

    fun dismissUpdateNotices() {
        updateNotices = emptyList()
    }

    fun updateAllNotices() {
        updateNotices.forEach { entry -> updateFor(entry.uid)?.let(::installRemote) }
    }

    private fun offerUpdateNotices(available: List<Catalog.Entry>) {
        if (updateNotices.isNotEmpty()) return
        val unseen = unseenModUpdates(installedVersions,
            available.associate { it.uid to it.manifest.version }, announcedVersions)
        updateNotices = available.filter { it.uid in unseen }.sortedBy { it.manifest.nameFor(UiLocalization.language).lowercase() }
    }

    /**
     * Baixa a lista online. `force` ignora o intervalo: é o botão de tentar de
     * novo. Sem ele, voltar à aba não pede a lista outra vez a cada toque.
     */
    fun refreshRemote(force: Boolean = false) {
        if (remoteStatus == RemoteStatus.Loading) return
        if (!force && System.currentTimeMillis() - remoteFetchedAt < REMOTE_TTL_MS) return
        remoteStatus = RemoteStatus.Loading
        remoteProgress = null
        scope.launch {
            if (!remoteCacheLoaded) {
                remote = withContext(Dispatchers.IO) { remoteCatalog.cached() }
                remoteCacheLoaded = true
            }
            val result = withContext(Dispatchers.IO) {
                runCatching {
                    remoteCatalog.refresh { progress ->
                        scope.launch {
                            if (remoteStatus == RemoteStatus.Loading &&
                                progress.completed >= (remoteProgress?.completed ?: 0)) {
                                remoteProgress = progress
                            }
                        }
                    }
                }
            }
            result.onSuccess {
                remote = it
                remoteFetchedAt = System.currentTimeMillis()
                remoteStatus = RemoteStatus.Ready
                offerUpdateNotices(it)
            }.onFailure {
                remoteStatus = RemoteStatus.Failed(networkMessage(it))
                offerUpdateNotices(remote)
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
                    downloadSlots.withPermit {
                        var shown = -1
                        val file = remoteCatalog.download(mod) { p ->
                            val pct = (p * 100).toInt()
                            if (pct != shown) {
                                shown = pct
                                scope.launch {
                                    if (mod.uid in downloads) downloads = downloads + (mod.uid to p)
                                }
                            }
                        }
                        try {
                            installLock.withLock { repo.import(file).getOrThrow() }
                        } finally {
                            file.delete()
                        }
                    }
                }
            }
            downloads = downloads - mod.uid
            result.onFailure {
                downloadErrors = downloadErrors + (mod.uid to networkMessage(it))
            }
            rescan()
        }
    }

    /**
     * Baixa arquivos da vitrine de um mod online na hora em que eles aparecem:
     * o ícone quando o cartão entra na tela, a capa no destaque, tudo quando a
     * ficha abre. O que já está no cache, ou já está a caminho, não pede de novo.
     */
    fun requestRemoteFiles(uid: String, rels: List<String>) {
        val entry = remote.firstOrNull { it.uid == uid } ?: return
        val mod = entry.remote ?: return
        val prefix = "$uid|${mod.download.sha256}|"
        val missing = rels.distinct().filter {
            it in mod.files && prefix + it !in filesLoaded && prefix + it !in filesInFlight
        }
        if (missing.isEmpty()) return
        val keys = missing.map { prefix + it }
        filesInFlight.addAll(keys)
        scope.launch {
            try {
                val fresh = withContext(Dispatchers.IO) {
                    fileSlots.withPermit { remoteCatalog.ensureFiles(mod, missing) }
                }
                if (fresh != null) {
                    remoteMedia.publish(fresh)
                    filesLoaded.addAll(keys)
                }
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                // A later visit can retry a failed storefront request.
            } finally {
                filesInFlight.removeAll(keys.toSet())
            }
        }
    }

    fun install(entry: Catalog.Entry) {
        mutatePackages { catalog.install(entry) }
    }

    /** O import do seletor em andamento: de 0 a 1; null fora dele (a janela de progresso). */
    var importProgress by mutableStateOf<Float?>(null)
        private set

    /** Um pacote de um mod já instalado, esperando a resposta "atualizar?". */
    var pendingUpdate by mutableStateOf<ModRepository.StagedPackage?>(null)
        private set
    private var pendingResult: ((Result<ModManifest>) -> Unit)? = null

    /**
     * Instala fora da UI e entrega o nome do mod ou a mensagem de erro. Se o
     * mod já está instalado, para em pendingUpdate até answerUpdate.
     */
    fun importPackage(uri: Uri, onResult: (Result<ModManifest>) -> Unit) {
        if (importProgress != null || pendingUpdate != null) return
        importProgress = 0f
        scope.launch {
            var shown = -1
            val staged = installLock.withLock {
                withContext(Dispatchers.IO) {
                    repo.stage(uri) { p ->
                        val pct = (p * 100).toInt()
                        if (pct != shown) {
                            shown = pct
                            scope.launch { if (importProgress != null) importProgress = p }
                        }
                    }
                }
            }
            staged.fold(
                onSuccess = { pkg ->
                    if (pkg.installed != null) {
                        importProgress = null
                        pendingResult = onResult
                        pendingUpdate = pkg
                    } else {
                        finishImport(pkg, onResult)
                    }
                },
                onFailure = {
                    importProgress = null
                    onResult(Result.failure(it))
                },
            )
        }
    }

    /** A resposta da pergunta "atualizar?": instala por cima, ou joga o pacote fora. */
    fun answerUpdate(update: Boolean) {
        val pkg = pendingUpdate ?: return
        val onResult = pendingResult
        pendingUpdate = null
        pendingResult = null
        if (update) {
            importProgress = 0.95f
            finishImport(pkg, onResult ?: {})
        } else {
            scope.launch(Dispatchers.IO) { repo.discard(pkg) }
            onResult?.invoke(Result.failure(ImportCancelled()))
        }
    }

    private fun finishImport(pkg: ModRepository.StagedPackage, onResult: (Result<ModManifest>) -> Unit) {
        scope.launch {
            val result = installLock.withLock { withContext(Dispatchers.IO) { repo.commit(pkg) } }
            importProgress = null
            onResult(result)
            rescan()
        }
    }

    fun exportMod(uid: String, uri: Uri): Result<Unit> = repo.export(uid, uri)

    fun uninstall(uid: String) {
        mutatePackages { catalog.uninstall(uid) }
    }

    fun setEnabled(uid: String, on: Boolean) {
        val entry = packages.firstOrNull { it.uid == uid } ?: return
        if (entry.manifest.isOutdated || (uid in enabled) == on) return
        val order = packages.map { it.uid }
        val changed = if (on) ModOrder.activate(order, uid, enabled) else order
        repo.setEnabled(uid, on)
        enabled = if (on) enabled + uid else enabled - uid
        publishOrder(ModOrder.enabledFirst(changed, enabled))
    }

    fun moveMod(uid: String, direction: Int) {
        publishOrder(ModOrder.move(packages.map { it.uid }, uid, direction, enabled))
    }

    fun moveModTo(uid: String, targetUid: String) {
        publishOrder(ModOrder.moveTo(packages.map { it.uid }, uid, targetUid, enabled))
    }

    private fun publishOrder(order: List<String>) {
        if (order == packages.map { it.uid }) return
        repo.saveOrder(order)
        val byUid = packages.associateBy { it.uid }
        packages = order.map { byUid.getValue(it) }
    }

    private fun mutatePackages(operation: () -> Unit) {
        scope.launch {
            try {
                installLock.withLock { withContext(Dispatchers.IO) { operation() } }
                packageError = null
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                packageError = e.message ?: tr("Não consegui alterar os pacotes.")
            }
            rescan()
        }
    }

    /**
     * Relê o disco. Chamado quando o launcher volta à frente: quem editou ou
     * colou um pacote em bunny_packs pelo gerenciador de arquivos vê na hora.
     */
    fun rescan() {
        refreshRequested = true
        if (refreshRunning) return
        refreshRunning = true
        scope.launch {
            refreshingPackages = true
            try {
                while (refreshRequested) {
                    refreshRequested = false
                    val scanned = installLock.withLock {
                        withContext(Dispatchers.IO) {
                            repo.list().mapNotNull { m -> repo.dirOf(m.uid)?.let { catalog.fromDisk(m, it) } }
                        }
                    }
                    // Uma ativação pode ocorrer durante a leitura: aplica a preferência atual.
                    enabled = scanned.filter { !it.manifest.isOutdated && repo.isEnabled(it.uid) }
                        .map { it.uid }.toSet()
                    val byUid = scanned.associateBy { it.uid }
                    packages = ModOrder.resolve(scanned.map { it.uid }, repo.savedOrder(), enabled)
                        .map { byUid.getValue(it) }
                    installed = byUid.keys
                    installedVersions = scanned.associate { it.uid to it.manifest.version }
                    val catalogIds = entries.map { it.uid }.toSet()
                    imported = packages.filter { it.uid !in catalogIds }
                    updateNotices = updateNotices.filter { entry ->
                        installedVersions[entry.uid]?.let { compareVersions(entry.manifest.version, it) > 0 } == true
                    }
                    if (remoteStatus == RemoteStatus.Ready || remoteStatus is RemoteStatus.Failed) {
                        offerUpdateNotices(remote)
                    }
                }
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                packageError = e.message ?: tr("Não consegui ler os pacotes.")
            } finally {
                refreshingPackages = false
                refreshRunning = false
            }
        }
    }
}

/** O usuário respondeu "não" ao "atualizar?": não é erro. */
class ImportCancelled : Exception("import cancelled")

sealed interface RemoteStatus {
    data object Idle : RemoteStatus
    data object Loading : RemoteStatus
    data object Ready : RemoteStatus
    data class Failed(val message: String) : RemoteStatus
}

/** Voltar à aba Explorar dentro disto não baixa a lista de novo. */
private const val REMOTE_TTL_MS = 10 * 60 * 1000L
