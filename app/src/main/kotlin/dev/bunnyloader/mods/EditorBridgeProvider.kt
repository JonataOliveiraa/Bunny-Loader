package dev.bunnyloader.mods

import android.content.ContentProvider
import android.content.ContentValues
import android.database.Cursor
import android.net.Uri
import android.os.Bundle
import android.util.Log

/**
 * O "salvar instala" do BL Editor: `call("install", projeto)` empacota,
 * instala e liga o mod sem abrir tela nenhuma (uma Activity a cada salvar
 * roubaria o foco do editor). Protegido pela mesma permissão de assinatura da
 * EditorBridgeActivity. Volta `ok` e, se falhou, `error`.
 */
class EditorBridgeProvider : ContentProvider() {
    override fun onCreate(): Boolean = true

    override fun call(method: String, arg: String?, extras: Bundle?): Bundle {
        val context = requireNotNull(context)
        if (method != METHOD_INSTALL) return Bundle().apply {
            putBoolean(KEY_OK, false)
            putString(KEY_ERROR, "Unknown method: $method")
        }
        return try {
            val manifest = EditorBridge.install(context, arg.orEmpty())
            Bundle().apply {
                putBoolean(KEY_OK, true)
                putString(KEY_VERSION, manifest.version)
            }
        } catch (e: Exception) {
            Log.w("BunnyLoader", "Editor install of '$arg' failed", e)
            Bundle().apply {
                putBoolean(KEY_OK, false)
                putString(KEY_ERROR, e.message ?: e.javaClass.simpleName)
            }
        }
    }

    override fun query(uri: Uri, projection: Array<out String>?, selection: String?,
                       selectionArgs: Array<out String>?, sortOrder: String?): Cursor? = null
    override fun getType(uri: Uri): String? = null
    override fun insert(uri: Uri, values: ContentValues?): Uri? = null
    override fun delete(uri: Uri, selection: String?, selectionArgs: Array<out String>?): Int = 0
    override fun update(uri: Uri, values: ContentValues?, selection: String?, selectionArgs: Array<out String>?): Int = 0

    companion object {
        const val METHOD_INSTALL = "install"
        const val KEY_OK = "ok"
        const val KEY_ERROR = "error"
        const val KEY_VERSION = "version"
    }
}
