package dev.bunnyloader.game

import android.media.AudioAttributes
import android.media.MediaMetadataRetriever
import android.media.MediaPlayer
import android.media.SoundPool
import android.util.Log
import java.util.concurrent.ConcurrentHashMap

/**
 * Sons e musicas de mod, tocados pelo Android.
 *
 * A Unity deste build nao cria AudioClip novo: o "Strip Engine Code" tirou o
 * AudioClip.Create do C# e, na libunity, o registro e a implementacao das
 * icalls por baixo dele (Construct_Internal, CreateUserSound, SetData). Nao ha
 * AssetBundle nem FMOD exportado. Entao o audio de mod toca fora da Unity:
 *
 *  - efeito: SoundPool, que decodifica (OGG, MP3, WAV...), mistura e aplica
 *    volume por lado e velocidade (o tom);
 *  - musica: MediaPlayer, em streaming, em laco.
 *
 * O nativo chama por JNI (content/sounds/AndroidAudio.cpp). Quanto e quando
 * tocar vem do jogo, por script/js/mod/audio.js: volume de efeitos, distancia ate o
 * centro da tela, volume de musica e a musica que o jogo escolheria.
 */
object ModAudio {
    private const val TAG = "BunnyLoader"

    // ------------------------------- efeitos -------------------------------

    private val loaded: MutableSet<Int> = ConcurrentHashMap.newKeySet()
    private val failed: MutableSet<Int> = ConcurrentHashMap.newKeySet()
    private val durations = ConcurrentHashMap<Int, Int>()

    private val effects by lazy {
        SfxQueue(object : SfxQueue.Backend {
            override fun play(id: Int, left: Float, right: Float, rate: Float) = pool.play(id, left, right, 1, 0, rate)
            override fun stop(stream: Int) = pool.stop(stream)
            override fun pause() { if (poolCreated) pool.autoPause() }
            override fun resume() { if (poolCreated) pool.autoResume() }
        })
    }

    @Volatile private var poolCreated = false

    private val pool: SoundPool by lazy {
        poolCreated = true
        SoundPool.Builder()
            .setMaxStreams(32)
            .setAudioAttributes(attributes(AudioAttributes.CONTENT_TYPE_SONIFICATION))
            .build()
            .apply {
                setOnLoadCompleteListener { _, id, status ->
                    if (status == 0) loaded += id else failed += id
                }
            }
    }

    private fun attributes(content: Int): AudioAttributes =
        AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_GAME)
            .setContentType(content)
            .build()

    /** Comeca a carregar (o SoundPool decodifica noutra thread). O id, ou 0. */
    @JvmStatic
    fun load(path: String): Int {
        val id = pool.load(path, 1)
        if (id > 0) durations[id] = durationOf(path)
        return id
    }

    /** 1 pronto, 0 carregando, -1 falhou. */
    @JvmStatic
    fun state(id: Int): Int = when (id) {
        in loaded -> 1
        in failed -> -1
        else -> 0
    }

    /** Duracao em ms (0 se o arquivo nao disse). */
    @JvmStatic
    fun duration(id: Int): Int = durations[id] ?: 0

    /** Volume por lado 0..1, velocidade 0,5..2 (2 = uma oitava acima). O stream, ou 0. */
    @JvmStatic
    fun play(id: Int, left: Float, right: Float, rate: Float): Int =
        if (id in loaded) pool.play(id, left, right, 1, 0, rate) else 0

    @JvmStatic
    fun stop(stream: Int) {
        if (poolCreated) pool.stop(stream)
    }

    /** Accept a short effect without waiting for SoundPool. Handle != native stream. */
    @JvmStatic
    fun enqueue(group: Int, id: Int, left: Float, right: Float, rate: Float, max: Int, ignore: Boolean): Int {
        if (id !in loaded) return 0
        return effects.enqueue(group, id, left, right, rate, duration(id), max, ignore)
    }

    @JvmStatic
    fun cancel(handle: Int) = effects.stop(handle)

    @JvmStatic
    fun playbackState(handle: Int): Int = effects.state(handle)

    @JvmStatic
    fun activeSound(group: Int): Int = effects.active(group)

    @JvmStatic
    fun queueStats(): String = effects.stats()

    private fun durationOf(path: String): Int {
        val r = MediaMetadataRetriever()
        return try {
            r.setDataSource(path)
            r.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION)?.toIntOrNull() ?: 0
        } catch (e: Exception) {
            0
        } finally {
            r.release()
        }
    }

    // ------------------------------- musica -------------------------------

    private class Track(val path: String) {
        var player: MediaPlayer? = null
        var prepared = false
        var failed = false
        var volume = 0f            // o que o jogo pediu por ultimo
        var pausedByApp = false    // tocava quando o app foi para o fundo
    }

    private val tracks = ConcurrentHashMap<Int, Track>()
    @Volatile private var nextTrack = 1

    /** Registra uma musica (nada e lido ainda). O id. */
    @JvmStatic
    @Synchronized
    fun musicRegister(path: String): Int {
        val id = nextTrack++
        tracks[id] = Track(path)
        return id
    }

    /**
     * O volume da musica agora (0..1), chamado a cada quadro pelo jogo com o
     * fade ja aplicado. Acima de 0 ela toca (prepara na primeira vez, sem
     * travar o jogo); em 0 ela para e volta ao comeco, como a faixa do jogo.
     */
    @JvmStatic
    @Synchronized
    fun musicVolume(id: Int, volume: Float) {
        val t = tracks[id] ?: return
        if (t.failed) return
        t.volume = volume
        val p = t.player ?: if (volume > 0f) prepare(id, t) else return
        if (!t.prepared) return
        if (volume > 0f) {
            p.setVolume(volume, volume)
            if (!p.isPlaying && !t.pausedByApp) p.start()
        } else if (p.isPlaying) {
            p.pause()
            p.seekTo(0)
        }
    }

    /** -1 falhou, 0 parada, 1 preparando, 2 tocando. */
    @JvmStatic
    @Synchronized
    fun musicState(id: Int): Int {
        val t = tracks[id] ?: return -1
        return when {
            t.failed -> -1
            t.player == null -> 0
            !t.prepared -> 1
            t.player!!.isPlaying -> 2
            else -> 0
        }
    }

    private fun prepare(id: Int, t: Track): MediaPlayer {
        val p = MediaPlayer()
        t.player = p
        try {
            p.setAudioAttributes(attributes(AudioAttributes.CONTENT_TYPE_MUSIC))
            p.setDataSource(t.path)
            p.isLooping = true
            p.setOnPreparedListener { onPrepared(id) }
            p.setOnErrorListener { _, what, extra ->
                Log.e(TAG, "musica de mod ${t.path}: erro $what/$extra")
                synchronized(this) { t.failed = true }
                true
            }
            p.prepareAsync()
        } catch (e: Exception) {
            Log.e(TAG, "musica de mod ${t.path}: ${e.message}")
            t.failed = true
        }
        return p
    }

    @Synchronized
    private fun onPrepared(id: Int) {
        val t = tracks[id] ?: return
        t.prepared = true
        musicVolume(id, t.volume)
    }

    // ------------------------------- app -------------------------------

    /** O jogo foi para o fundo: nada de mod soando por cima de outro app. */
    @JvmStatic
    @Synchronized
    fun pauseAll() {
        effects.pause()
        for (t in tracks.values) {
            val p = t.player ?: continue
            if (t.prepared && p.isPlaying) {
                p.pause()
                t.pausedByApp = true
            }
        }
    }

    @JvmStatic
    @Synchronized
    fun resumeAll() {
        effects.resume()
        for (t in tracks.values) {
            if (!t.pausedByApp) continue
            t.pausedByApp = false
            if (t.volume > 0f) t.player?.start()
        }
    }
}
