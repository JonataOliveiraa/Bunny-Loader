package dev.bunnyloader.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import dev.bunnyloader.game.Eligibility

/** Os idiomas da interface, cada um com o nome na própria língua. */
val LanguageNames = listOf(
    "pt-BR" to "Português (Brasil)",
    "en" to "English",
    "ru" to "Русский",
    "es" to "Español",
    "zh" to "简体中文",
    "vi" to "Tiếng Việt",
    "th" to "ไทย",
)

/**
 * O JOGAR sem poder jogar: diz o porquê (o [status] traz as chaves do texto) e,
 * se o problema é o Terraria, leva à página dele na Play Store.
 */
@Composable
fun GameBlockedDialog(status: Eligibility.Status, onDismiss: () -> Unit) {
    val context = LocalContext.current
    PixelDialog(tr(status.titleKey), onDismiss = onDismiss) {
        PixelText(tr(status.messageKey), size = Ts.Body, color = Bl.TextFaint)
        Row(Modifier.fillMaxWidth().padding(top = 4.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            PixelButton(tr("Close"), onDismiss, Modifier.weight(1f))
            if (status == Eligibility.Status.NOT_INSTALLED) {
                PixelButton(tr("OpenPlayStore"), {
                    Eligibility.openStore(context)
                    onDismiss()
                }, Modifier.weight(1f))
            }
        }
    }
}

/**
 * A primeira abertura do app: a interface já está no idioma do aparelho (ou em
 * inglês, se ele não tiver arquivo) e o jogador confirma ou toca em outro. O
 * toque troca na hora, então o próprio diálogo mostra a língua escolhida.
 */
@Composable
fun LanguageConfirmDialog(onConfirm: () -> Unit) {
    val context = LocalContext.current
    val current = UiLocalization.language
    // Fechar por fora também confirma: a escolha fica gravada, sem o automático.
    val confirm = {
        UiLocalization.select(context, current)
        onConfirm()
    }
    PixelDialog(tr("Language"), onDismiss = confirm) {
        PixelText(tr("LanguageConfirmDescription"), size = Ts.Small, color = Bl.TextFaint)
        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
            for (pair in LanguageNames.chunked(2)) {
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    for ((code, name) in pair) {
                        val on = code == current
                        PixelButton(
                            name, { UiLocalization.select(context, code) }, Modifier.weight(1f),
                            fill = if (on) Bl.ButtonPressed else Bl.Button,
                            textColor = if (on) Bl.PressedText else Bl.Text,
                            fontSize = Ts.Body,
                        )
                    }
                    if (pair.size == 1) Spacer(Modifier.weight(1f))
                }
            }
        }
        Spacer(Modifier.height(2.dp))
        PixelButton(tr("Confirm"), confirm, Modifier.fillMaxWidth())
    }
}
