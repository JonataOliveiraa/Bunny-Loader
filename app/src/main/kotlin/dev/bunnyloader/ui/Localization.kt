package dev.bunnyloader.ui

import android.content.Context
import android.widget.Toast
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import bunny.MenuLocalization
import bunny.TranslationCatalog

/** A leitura observável recompõe os textos quando a escolha muda. */
object UiLocalization {
    var choice by mutableStateOf("")
        private set
    var language by mutableStateOf(TranslationCatalog.FALLBACK)
        private set

    fun initialize(context: Context) {
        MenuLocalization.initialize(context)
        choice = MenuLocalization.choice()
        language = MenuLocalization.language()
    }

    fun select(context: Context, selected: String) {
        if (MenuLocalization.select(context, selected)) {
            choice = MenuLocalization.choice()
            language = MenuLocalization.language()
        } else {
            Toast.makeText(context, tr("LanguageSaveFailed"), Toast.LENGTH_SHORT).show()
        }
    }
}

/** O texto da chave (nome em inglês, em assets/localization/<cultura>.json) no idioma ativo. */
fun tr(key: String, vararg args: Any?): String {
    UiLocalization.language // Registra a dependência mesmo fora de uma função @Composable.
    return MenuLocalization.text(key, *args)
}
