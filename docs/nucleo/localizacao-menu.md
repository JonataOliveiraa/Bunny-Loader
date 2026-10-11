# Localização dos menus

O launcher e o Mod Menu compartilham `app/src/main/assets/localization/menu.json`.
Cada chave é o texto original em português; seu valor contém as traduções em
**inglês, russo e espanhol**, nessa ordem. Português continua disponível e é o
retorno para um idioma não suportado ou uma tradução ausente.

Em **Config → Aparência → Idioma**, as setas alternam entre Automático,
Português (Brasil), English, Русский e Español. Automático acompanha o idioma
do aparelho, inclusive variantes como `en-GB`, `es-MX` e `ru-RU`. A escolha
fica em `bunny_localization.xml`, separada dos ajustes de jogo. O launcher muda
imediatamente; o Mod Menu lê a escolha quando o jogo abre.

## Adicionar um texto

No Kotlin, use `tr("Texto em português")`. No Java do menu, use a mesma função
de `CheatBridge`. Para valores variáveis, mantenha a frase inteira e passe os
argumentos separados:

```kotlin
tr("%1\$s de %2\$s", visibleCount, totalCount)
```

As traduções devem manter os placeholders numerados; para um percentual
formatado, escreva `%%`. Não passe nomes, descrições, código ou textos de mods
para `tr`: esses conteúdos pertencem ao pacote ou à localização do Terraria.
Rótulos guardados em arrays e enums precisam ser traduzidos na apresentação,
para não guardar uma tradução da primeira inicialização.

`TranslationCatalog` contém a resolução de idioma, o retorno e a formatação,
sem depender do Android. `MenuLocalization` carrega o asset e salva a escolha;
`UiLocalization` notifica o Compose. O Mod Menu mantém uma fonte de apoio para
caracteres cirílicos que não existem na fonte do jogo.

## Validar

Execute `:app:testDebugUnitTest` para verificar variantes regionais, retorno,
Unicode e todos os placeholders do catálogo. Depois de alterar Java do menu,
execute `tools/build-cheatbridge-dex.sh` para regenerar `CheatBridgeDex.h` antes
de compilar a biblioteca nativa.
