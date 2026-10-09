package dev.bunnyloader.catalog

import android.annotation.SuppressLint
import android.content.ActivityNotFoundException
import android.content.Intent
import android.graphics.Color
import android.net.Uri
import android.os.Bundle
import android.webkit.ValueCallback
import android.webkit.WebChromeClient
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.webkit.WebViewAssetLoader
import kotlinx.serialization.json.JsonPrimitive
import java.io.File

/**
 * A página do gerenciador do PC (tools/mods/manager.html), servida dos assets
 * numa WebView. O que lá é o manager.py aqui é a [Bridge].
 */
class MainActivity : ComponentActivity() {
    private lateinit var web: WebView
    private var pending: ValueCallback<Array<Uri>>? = null

    private val pickFile = registerForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        pending?.onReceiveValue(uri?.let { arrayOf(it) })
        pending = null
    }

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val assets = WebViewAssetLoader.Builder()
            .addPathHandler("/", WebViewAssetLoader.AssetsPathHandler(this))
            .build()
        if (BuildConfig.DEBUG) WebView.setWebContentsDebuggingEnabled(true)
        val publisher = Publisher(EmbeddedToken.value(), File(cacheDir, "uploads"))

        web = WebView(this).apply {
            setBackgroundColor(Color.rgb(7, 10, 28))
            settings.javaScriptEnabled = true
            settings.domStorageEnabled = true
            settings.allowFileAccess = false
            settings.allowContentAccess = true
            webViewClient = object : WebViewClient() {
                override fun shouldInterceptRequest(view: WebView, request: WebResourceRequest): WebResourceResponse? =
                    assets.shouldInterceptRequest(request.url)

                override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
                    if (request.url.host == HOST) return false
                    try {
                        startActivity(Intent(Intent.ACTION_VIEW, request.url))
                    } catch (_: ActivityNotFoundException) {
                    }
                    return true
                }
            }
            webChromeClient = object : WebChromeClient() {
                override fun onShowFileChooser(view: WebView, callback: ValueCallback<Array<Uri>>,
                                               params: FileChooserParams): Boolean {
                    pending?.onReceiveValue(null)
                    pending = callback
                    pickFile.launch("*/*")
                    return true
                }
            }
            addJavascriptInterface(Bridge(publisher) { id, json ->
                post { evaluateJavascript("window.__blReply($id, ${JsonPrimitive(json)})", null) }
            }, "BLNative")
        }
        setContentView(web)
        if (savedInstanceState == null) web.loadUrl("https://$HOST/manager.html") else web.restoreState(savedInstanceState)
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        web.saveState(outState)
    }

    override fun onDestroy() {
        web.destroy()
        super.onDestroy()
    }

    companion object {
        private const val HOST = WebViewAssetLoader.DEFAULT_DOMAIN
    }
}
