package dev.bunnyloader.game

import android.content.Context
import android.content.Intent
import android.os.Process
import android.util.Log
import dev.bunnyloader.GameActivity
import dev.bunnyloader.mods.Catalog
import dev.bunnyloader.mods.ModManifest
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.mods.PackType
import dev.bunnyloader.mods.RemoteCatalog
import dev.bunnyloader.mods.RemoteMod
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.io.File

/**
 * A sincronização de mods com um servidor (a opção da tela de Host). O
 * servidor recusa quem entra com mods diferentes dos dele e manda a lista
 * dele, na ordem de carga; o jogo chama [offer] com ela. Aqui cada mod vira
 * uma ação:
 *  - do servidor, já instalado na versão dele: entra (ligado, se estava desligado);
 *  - do servidor, na loja na versão dele: baixa;
 *  - do servidor, nem um nem outro: o jogador instala à mão (fica em `missing`);
 *  - do jogador, que o servidor não tem: fica de fora desta vez.
 *
 * A tela é do jogo (ServerSyncMenu, em JS): ela lê o andamento em [state] e
 * responde com [apply] ou [cancel]. Aceito, o jogo reinicia com exatamente os
 * mods do servidor, na ordem dele (a ordem dá os números de item e NPC), mais
 * os pacotes de textura do jogador, e volta a entrar no servidor sozinho. A
 * escolha vale uma abertura ([takeSession] apaga o arquivo): a próxima
 * abertura normal é a do jogador.
 */
object ServerSync {
    private const val TAG = "BunnyLoader"
    private const val SESSION = "server_session.json"

    /** Um mod do servidor, como o jogo manda (Mod.uuid/id/name/version). */
    @Serializable
    data class HostMod(val uid: String, val id: String = "", val name: String = "", val version: String = "")

    /** O que o jogo manda: os mods do servidor e como voltar a ele. */
    @Serializable
    data class Offer(
        val mods: List<HostMod>,
        val address: String,
        val port: Int = 7777,
        val password: String = "",
        /** O arquivo do personagem (`Bench.plr`). */
        val player: String,
        /** Os pacotes de textura ligados (uids), que seguem na volta. */
        val textures: List<String> = emptyList(),
    )

    /** A próxima abertura: os mods (`uid=entry`, na ordem) e o servidor. */
    @Serializable
    data class Session(
        val mods: List<String>,
        val address: String,
        val port: Int,
        val password: String,
        val player: String,
    )

    /**
     * O que a tela mostra. `phase`: idle, checking, ready (dá para
     * sincronizar), missing (falta o que a loja não tem), applying (baixando),
     * error (`message`). As listas são os nomes, prontos para a tela.
     */
    @Serializable
    data class State(
        val phase: String = "idle",
        val download: List<String> = emptyList(),
        val enable: List<String> = emptyList(),
        val disable: List<String> = emptyList(),
        val missing: List<String> = emptyList(),
        /** Baixando: qual (nome), o número dele, quantos e quanto dele já veio (0..1). */
        val current: String = "",
        val step: Int = 0,
        val steps: Int = 0,
        val progress: Float = 0f,
        val message: String = "",
    )

    private class Plan(
        val download: List<Pair<HostMod, RemoteMod>>,
        val enable: List<HostMod>,
        val disable: List<ModManifest>,
        val missing: List<String>,
    )

    private val json = Json { ignoreUnknownKeys = true; encodeDefaults = true }
    private val lock = Any()
    @Volatile private var state = State()
    private var offer: Offer? = null
    private var plan: Plan? = null

    /** Do jogo (JNI, thread do jogo): confere, numa thread à parte, o que muda. */
    @JvmStatic
    fun offer(text: String) {
        val offer = runCatching { json.decodeFromString<Offer>(text) }.getOrElse {
            Log.e(TAG, "sincronizar mods: oferta ilegível", it)
            return
        }
        val context = GameActivity.current?.applicationContext ?: run {
            Log.e(TAG, "sincronizar mods: sem a tela do jogo")
            return
        }
        synchronized(lock) {
            this.offer = offer
            plan = null
            state = State("checking")
        }
        Thread {
            val result = runCatching { plan(context, offer) }
            synchronized(lock) {
                if (this.offer !== offer) return@Thread
                result.onSuccess {
                    plan = it
                    state = State(
                        phase = if (it.missing.isNotEmpty()) "missing" else "ready",
                        download = it.download.map { (mod, _) -> label(mod) },
                        enable = it.enable.map(::label),
                        disable = it.disable.map { m -> m.name },
                        missing = it.missing,
                    )
                }.onFailure {
                    Log.e(TAG, "sincronizar mods: conferência falhou", it)
                    state = State("error", message = "Não consegui conferir os mods do servidor: ${it.message}")
                }
            }
        }.start()
    }

    /** O andamento, em JSON (State). Qualquer thread. */
    @JvmStatic
    fun state(): String = json.encodeToString(state)

    /** O jogador aceitou: baixa, grava a sessão e reinicia. Só com a fase `ready`. */
    @JvmStatic
    fun apply() {
        val context = GameActivity.current?.applicationContext ?: return
        val (offer, plan) = synchronized(lock) {
            val o = offer ?: return
            val p = plan ?: return
            if (state.phase != "ready") return
            state = state.copy(phase = "applying", steps = p.download.size)
            o to p
        }
        Thread {
            val result = runCatching {
                val repo = ModRepository(context)
                val store = RemoteCatalog(context, Catalog(context))
                plan.download.forEachIndexed { i, (mod, remote) ->
                    update { it.copy(current = label(mod), step = i + 1, progress = 0f) }
                    val file = store.download(remote) { p -> update { it.copy(progress = p) } }
                    try {
                        repo.import(file).getOrThrow()
                    } finally {
                        file.delete()
                    }
                }
                writeSession(context, repo, offer)
            }
            result.onSuccess { relaunch(context) }.onFailure { e ->
                Log.e(TAG, "sincronizar mods: parou", e)
                update { State("error", message = "A sincronização parou: ${e.message}") }
            }
        }.start()
    }

    /** O jogador desistiu (ou a tela fechou). */
    @JvmStatic
    fun cancel() {
        synchronized(lock) {
            offer = null
            plan = null
            state = State()
        }
    }

    /** Na abertura do jogo: a sessão pedida pela sincronização, uma vez só. */
    fun takeSession(context: Context): Session? {
        val file = File(context.filesDir, SESSION)
        if (!file.isFile) return null
        val session = runCatching { json.decodeFromString<Session>(file.readText()) }.getOrNull()
        file.delete()
        return session
    }

    private fun update(change: (State) -> State) {
        synchronized(lock) { state = change(state) }
    }

    private fun plan(context: Context, offer: Offer): Plan {
        val repo = ModRepository(context)
        val store = RemoteCatalog(context, Catalog(context))
        // A loja na hora: sem rede, a última lista baixada.
        val shop = runCatching { runBlocking { store.refresh() } }.getOrElse { store.cached() }
            .mapNotNull { it.remote }.associateBy { it.uid }
        val installed = repo.list().associateBy { it.uid }
        val hostUids = offer.mods.map { it.uid }.toSet()

        val download = mutableListOf<Pair<HostMod, RemoteMod>>()
        val enable = mutableListOf<HostMod>()
        val missing = mutableListOf<String>()
        for (mod in offer.mods) {
            val have = installed[mod.uid]
            val remote = shop[mod.uid]
            when {
                have != null && have.version == mod.version && !have.isOutdated -> {
                    if (!repo.isEnabled(mod.uid)) enable += mod
                }
                remote != null && remote.version == mod.version -> download += mod to remote
                else -> missing += label(mod) + when {
                    remote != null -> " (a loja tem a ${remote.version})"
                    have != null -> " (você tem a ${have.version})"
                    else -> ""
                }
            }
        }
        val disable = repo.list().filter {
            it.packType == PackType.MOD && it.uid !in hostUids && repo.isEnabled(it.uid) && !it.isOutdated
        }
        return Plan(download, enable, disable, missing)
    }

    private fun label(mod: HostMod) =
        (mod.name.ifEmpty { mod.id.ifEmpty { mod.uid } }) + (if (mod.version.isNotEmpty()) " v${mod.version}" else "")

    /** Os mods do servidor na ordem dele, como o launcher os passa ao jogo. */
    private fun writeSession(context: Context, repo: ModRepository, offer: Offer) {
        val installed = repo.list().associateBy { it.uid }
        val specs = offer.mods.map { mod ->
            val manifest = installed[mod.uid] ?: error("${label(mod)} não ficou instalado")
            "${mod.uid}=${manifest.entry}"
        } + offer.textures.filter { installed[it]?.packType == PackType.TEXTURE }.map { "$it=@texture" }
        val session = Session(specs, offer.address, offer.port, offer.password, offer.player)
        File(context.filesDir, SESSION).writeText(json.encodeToString(session))
        Log.i(TAG, "sincronizar mods: sessão gravada (${specs.size} pacotes, ${offer.address}:${offer.port})")
    }

    /** Como o Reiniciar do Mod Menu: a RestartActivity do launcher e o fim deste processo. */
    private fun relaunch(context: Context) {
        val intent = Intent().apply {
            setClassName(context.packageName, "dev.bunnyloader.RestartActivity")
            putExtra("pid", Process.myPid())
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_NO_ANIMATION)
        }
        context.startActivity(intent)
        Process.killProcess(Process.myPid())
    }
}
