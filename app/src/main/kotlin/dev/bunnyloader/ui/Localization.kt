package dev.bunnyloader.ui

import android.content.Context
import android.widget.Toast
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import bunny.MenuLocalization

/** A leitura observável recompõe os textos quando a escolha muda. */
object UiLocalization {
    var choice by mutableStateOf("")
        private set
    var language by mutableStateOf("pt-BR")
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
            Toast.makeText(context, tr("Não consegui salvar o idioma."), Toast.LENGTH_SHORT).show()
        }
    }
}

fun tr(source: String, vararg args: Any?): String {
    UiLocalization.language // Registra a dependência mesmo fora de uma função @Composable.
    return MenuLocalization.text(source, *args)
}
