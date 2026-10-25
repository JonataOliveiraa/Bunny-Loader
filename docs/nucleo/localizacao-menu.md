# Menu localization

The launcher and the Mod Menu share `app/src/main/assets/localization/`, one
file per language, named by Terraria's culture (with a hyphen, as in the
game and in mod `Localization/` files):

| File | Language |
| --- | --- |
| `en-US.json` | English (source and fallback) |
| `pt-BR.json` | Portuguese (Brazil) |
| `ru-RU.json` | Russian |
| `es-ES.json` | Spanish |
| `zh-Hans.json` | Simplified Chinese |
| `vi-VN.json` | Vietnamese |
| `th-TH.json` | Thai |

Every file has the same keys: an English name in PascalCase that says what the
text is, mapped to the text in that language:

```json
{
  "Packages": "Pacotes",
  "DownloadingPercent": "Baixando %1$s%%",
  "PackageCountOne": "%1$s pacote",
  "PackageCountOther": "%1$s pacotes"
}
```

English is the fallback: a device language without a file uses English, and
a key missing or empty in one language shows its English text. A key missing
everywhere shows the key itself.

Key names follow a few patterns, so a translator knows what each one is:

| Pattern | Meaning |
| --- | --- |
| `...Description` | The explanation under a setting or power |
| `...CountOne` / `...CountOther` | Singular and plural of a count |
| `Filter...` / `Sort...` | Package list filters and sort orders |
| `Stat...` | The small label under a number |

In **Settings → Appearance → Language**, the arrows cycle through Automatic,
Português (Brasil), English, Русский, Español, 简体中文, Tiếng Việt and ไทย.
Automatic follows the device's language, including variants like `en-GB`,
`pt-PT`, `es-MX` and `ru-RU`. The choice is stored in `bunny_localization.xml`,
separate from the game settings. The launcher changes immediately; the Mod
Menu reads the choice when the game opens.

## Adding a text

Add the key to **every** language file (at least the English text; the others
fall back to it until translated), then use it in code. In Kotlin, use
`tr("PackageCountOther", count)`. In the menu's Java, use the same function
from `CheatBridge`. For variable values, keep the whole sentence and pass the
arguments separately:

```kotlin
tr("CountOfTotal", visibleCount, totalCount)
```

Translations must keep the numbered placeholders; for a formatted percentage,
write `%%`. Do not pass names, descriptions, code or mod texts to `tr`: that
content belongs to the package or to Terraria's localization. Labels kept in
arrays and enums hold the key and are translated at display time, so as not to
keep a translation from the first initialization.

`TranslationCatalog` holds the language list, the culture of each language,
resolution, fallback and formatting, without depending on Android.
`MenuLocalization` loads one file per language and saves the choice;
`UiLocalization` notifies Compose. The Mod Menu keeps a fallback font for
Cyrillic, Thai and Chinese characters that do not exist in the game's font.
Vietnamese switches the whole launcher and Mod Menu to the system font: the
pixel font lacks most of its letters (ă, đ, ơ, ư and the hook and dot-below
tones), and a per-letter fallback would change fonts in the middle of a word.

## Adding a language

Copy `en-US.json` to `<culture>.json` and translate the values, add the
language code to `TranslationCatalog.LANGUAGES` and its culture to
`TranslationCatalog.culture`, and add the option to `LanguagePicker` in
`Screens.kt`. Check the pixel font: a script it does not cover falls back to
the system font (see above).

## Validating

Run `:app:testDebugUnitTest`. It checks regional variants, the English
fallback, Unicode, every placeholder, that all language files have exactly the
same keys, and that every `tr("Key")` in the code exists in `en-US.json`. After
changing the menu's Java, run `tools/build-cheatbridge-dex.sh` to regenerate
`CheatBridgeDex.h` before building the native library.
