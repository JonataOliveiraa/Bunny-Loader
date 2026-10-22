package dev.bunnyloader.catalog

/**
 * O idioma do registro e dos erros, o mesmo que a página escolheu: ela manda
 * `lang` em cada pedido e a [Bridge] acerta aqui antes de atender. Os pedidos
 * correm numa thread só, então um valor global basta.
 */
object I18n {
    @Volatile
    var english = false

    fun tr(pt: String, en: String) = if (english) en else pt
}
