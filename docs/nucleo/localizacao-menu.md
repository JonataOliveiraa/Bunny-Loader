# Menu localization

The launcher and the Mod Menu share `app/src/main/assets/localization/menu.json`.
Each key is the original Portuguese text; its value holds the translations in
**English, Russian, Spanish, Simplified Chinese, Vietnamese and Thai**, in
that order. Portuguese remains available
and is the fallback for an unsupported language or a missing translation.

In **Settings → Appearance → Language**, the arrows cycle through Automatic,
Português (Brasil), English, Русский, Español, 简体中文, Tiếng Việt and ไทย.
Automatic follows the device's language, including variants like `en-GB`,
`es-MX` and `ru-RU`. The
choice is stored in `bunny_localization.xml`, separate from the game settings.
The launcher changes immediately; the Mod Menu reads the choice when the game
opens.

## Adding a text

In Kotlin, use `tr("Texto em português")`. In the menu's Java, use the same
function from `CheatBridge`. For variable values, keep the whole sentence and
pass the arguments separately:

```kotlin
tr("%1\$s de %2\$s", visibleCount, totalCount)
```

Translations must keep the numbered placeholders; for a formatted percentage,
write `%%`. Do not pass names, descriptions, code or mod texts to `tr`: that
content belongs to the package or to Terraria's localization. Labels kept in
arrays and enums must be translated at display time, so as not to keep a
translation from the first initialization.

`TranslationCatalog` holds language resolution, fallback and formatting,
without depending on Android. `MenuLocalization` loads the asset and saves the
choice; `UiLocalization` notifies Compose. The Mod Menu keeps a fallback font
for Cyrillic, Thai and Chinese characters that do not exist in the game's font.
Vietnamese switches the whole launcher and Mod Menu to the system font: the
pixel font lacks most of its letters (ă, đ, ơ, ư and the hook and dot-below
tones), and a per-letter fallback would change fonts in the middle of a word.

## Validating

Run `:app:testDebugUnitTest` to check regional variants, fallback, Unicode and
every placeholder in the catalog. After changing the menu's Java, run
`tools/build-cheatbridge-dex.sh` to regenerate `CheatBridgeDex.h` before
building the native library.
