package dev.bunnyloader.game

import android.content.Context
import android.util.Log
import dev.bunnyloader.BuildConfig
import dev.bunnyloader.mods.ModRepository
import dev.bunnyloader.mods.RemoteCatalog
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.io.File
import java.io.IOException
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import java.util.concurrent.atomic.AtomicBoolean
import java.util.zip.GZIPInputStream

object AotImages {
    private const val TAG = "BunnyLoader"
    private const val FOLDER = "aot"
    private const val STATE = "state.json"
    private const val MAX_MANIFEST_BYTES = 2L shl 20
    private const val MAX_IMAGE_BYTES = 120L shl 20
    private const val RELEASES =
        "https://github.com/JonataOliveiraa/Bunny-Loader-CSharp-Mods/releases/download/"

    private val json = Json { ignoreUnknownKeys = true }
    private val running = AtomicBoolean(false)
    private val again = AtomicBoolean(false)
    private val keyPattern = Regex("^[0-9a-f]{16}-[0-9A-Za-z.]+$")
    private val namePattern = Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$")
    private val shaPattern = Regex("^[0-9a-f]{64}$")

    @Serializable
    data class Manifest(val format: Int = 1, val key: String = "", val images: List<Image> = emptyList())

    @Serializable
    data class Image(val assembly: String, val dll: String, val url: String, val sha256: String, val size: Long = 0)

    @Serializable
    private data class State(val images: Map<String, String> = emptyMap())

    fun requestSync(context: Context) {
        val app = context.applicationContext
        again.set(true)
        if (!running.compareAndSet(false, true)) return
        Thread({
            try {
                while (again.getAndSet(false)) {
                    runCatching { sync(app) }.onFailure { Log.w(TAG, "aot: sync failed: ${it.message}") }
                }
            } finally {
                running.set(false)
            }
        }, "bl-aot-sync").apply { priority = Thread.MIN_PRIORITY }.start()
    }

    fun key(context: Context): String? = runCatching {
        context.assets.open("dotnet/aot-key.txt").bufferedReader().use { it.readLine()?.trim() }
    }.getOrNull()?.takeIf { keyPattern.matches(it) }

    private fun sync(context: Context) {
        val key = key(context) ?: return
        val root = File(context.filesDir, FOLDER)
        root.listFiles()?.filter { it.name != key }?.forEach { it.deleteRecursively() }
        val dir = File(root, key).apply { mkdirs() }

        val assemblies = ModRepository(context).modAssemblies()
            .associate { it.nameWithoutExtension to it }
            .filterKeys { namePattern.matches(it) }
        val stateFile = File(dir, STATE)
        val state = runCatching { json.decodeFromString<State>(stateFile.readText()).images }.getOrDefault(emptyMap())
            .toMutableMap()

        dir.listFiles()?.filter { it.name.endsWith(".dll.so") }?.forEach { file ->
            val name = file.name.removeSuffix(".dll.so")
            if (name !in assemblies) {
                file.delete()
                state.remove(name)
            }
        }
        if (assemblies.isEmpty()) {
            stateFile.writeText(json.encodeToString(State.serializer(), State(state)))
            return
        }

        val manifest = readManifest(context, key) ?: return
        if (manifest.key != key) return
        val wanted = manifest.images.filter {
            namePattern.matches(it.assembly) && shaPattern.matches(it.dll) && shaPattern.matches(it.sha256)
        }.associateBy { it.dll }

        var fetched = 0
        for ((name, dll) in assemblies) {
            val dllSha = sha256(dll)
            val target = File(dir, "$name.dll.so")
            if (state[name] == dllSha && target.isFile) continue
            val image = wanted[dllSha]?.takeIf { it.assembly == name }
            if (image == null) {
                if (target.delete()) Log.i(TAG, "aot: $name changed; its old image is gone")
                state.remove(name)
                continue
            }
            try {
                download(context, image, target)
                state[name] = dllSha
                fetched++
            } catch (e: Exception) {
                Log.w(TAG, "aot: $name: ${e.message}")
            }
        }
        stateFile.writeText(json.encodeToString(State.serializer(), State(state)))
        if (fetched > 0) Log.i(TAG, "aot: $fetched image(s) ready for $key")
    }

    private fun localStore(context: Context): File? {
        if (!BuildConfig.DEBUG) return null
        return context.getExternalFilesDir(null)?.let { File(it, "aotstore") }?.takeIf { it.isDirectory }
    }

    private fun readManifest(context: Context, key: String): Manifest? {
        val local = localStore(context)
        val text = if (local != null) {
            File(local, "$key.json").takeIf { it.isFile }?.readText() ?: return null
        } else {
            val conn = connect(RemoteCatalog.BASE_URL + "aot/$key.json")
            try {
                when (conn.responseCode) {
                    HttpURLConnection.HTTP_OK -> String(readLimited(conn.inputStream, MAX_MANIFEST_BYTES), Charsets.UTF_8)
                    HttpURLConnection.HTTP_NOT_FOUND -> return null
                    else -> throw IOException("the server answered ${conn.responseCode}")
                }
            } finally {
                conn.disconnect()
            }
        }
        return json.decodeFromString<Manifest>(text)
    }

    private fun download(context: Context, image: Image, target: File) {
        val local = localStore(context)
        val input: InputStream = if (local != null) {
            File(local, image.url.substringAfterLast('/')).inputStream()
        } else {
            require(image.url.startsWith(RELEASES)) { "image outside the catalog: ${image.url}" }
            val conn = connect(image.url)
            if (conn.responseCode != HttpURLConnection.HTTP_OK) {
                conn.disconnect()
                throw IOException("the server answered ${conn.responseCode}")
            }
            conn.inputStream
        }
        val packed = input.use { readLimited(it, MAX_IMAGE_BYTES) }
        if (image.size > 0 && packed.size.toLong() != image.size) throw IOException("size differs from the manifest")
        if (hex(MessageDigest.getInstance("SHA-256").digest(packed)) != image.sha256) {
            throw IOException("sha256 differs from the manifest")
        }
        val temp = File(target.parentFile, target.name + ".part")
        GZIPInputStream(packed.inputStream()).use { gz -> temp.outputStream().use { gz.copyTo(it) } }
        if (!temp.renameTo(target)) {
            temp.delete()
            throw IOException("could not move the image into place")
        }
    }

    private fun connect(url: String): HttpURLConnection = (URL(url).openConnection() as HttpURLConnection).apply {
        connectTimeout = 15_000
        readTimeout = 30_000
        instanceFollowRedirects = true
        setRequestProperty("User-Agent", "BunnyLoader")
    }

    private fun readLimited(input: InputStream, limit: Long): ByteArray {
        val out = java.io.ByteArrayOutputStream()
        val buffer = ByteArray(64 * 1024)
        var total = 0L
        while (true) {
            val n = input.read(buffer)
            if (n < 0) break
            total += n
            if (total > limit) throw IOException("file too large")
            out.write(buffer, 0, n)
        }
        return out.toByteArray()
    }

    private fun sha256(file: File): String {
        val digest = MessageDigest.getInstance("SHA-256")
        file.inputStream().use { input ->
            val buffer = ByteArray(64 * 1024)
            while (true) {
                val n = input.read(buffer)
                if (n < 0) break
                digest.update(buffer, 0, n)
            }
        }
        return hex(digest.digest())
    }

    private fun hex(bytes: ByteArray) = bytes.joinToString("") { "%02x".format(it) }
}
