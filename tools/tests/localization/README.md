# Changing the language without restarting

## Analysis

The loader applied the JSONs after `LoadLanguage` and `SetLanguage`. In
mobile version 301720, `LoadLanguage` calls `LoadFilesForCulture`, fires
`OnLanguageChanging`, processes references and prepares aliases before
returning. The old hook arrived after those consumers. The references to the
game in the per-culture tables were resolved with the language active while
the mod loaded. Tooltips were not registered for the next change, and config
labels kept strings obtained only once.

Inputs: per-culture JSONs, name and description maps, the loaded culture and
native keys. Outputs: updated values in the same `LocalizedText`s and in the
processed tooltips. Cases covered: repeated changes, the English fallback,
relative and game references, shifted arguments, a missing or circular
reference, deferred initialization, a repeated language and literal text.

## Design

| Approach | Time | Space | Maintenance |
|---|---|---|---|
| Recompute texts on every read or tick | Repeats resolution and the trip through the bridge | Temporaries per read | Spreads the logic across consumers |
| Apply after the culture's files load | Resolution on load and on language change | Per-culture tables on load, a resolved table for the current language | A single update point before the native events |

The second approach uses a single hook on `LoadFilesForCulture`, after the
original. The JSONs are still read once per mod. The content templates resolve
references between the mod's texts and keep references to the game until the
matching culture loads. `ModifyTooltipLines` still receives the separate lines
and runs once per culture during registration.

Tooltips register the same key the C++ uses and invalidate their cache on the
change. The config labels, choices and button texts read
`LocalizedText.Value`; the choices array stays the same. There is no new
per-tick or per-frame hook.

A string obtained through `Translate` or `GetTextValue` and kept by the mod
itself is still a string. To follow the language, look it up when using it or
keep the `LocalizedText` returned by `GetText` and read `.Value`.

## Running

```powershell
node tools/tests/localization/check.mjs
```

The test loads the production code and simulates the order confirmed in the
game's disassembly. The first run reproduced 29 failures out of 89 checks. The
suite was extended to include registrations during the events; now 95 out of
95 pass. The mocks check the hooks' signatures against `refs/dump.cs`.

The package in `manifest.json` and `content/` uses UID
`c4c0f239-ad0e-4400-95f4-0354c884d12b`. Install only that package to repeat
the native proof. It creates item, NPC, buff, prefix and projectile types to
query the caches; it creates no entities in the world and hands out no items.
The test switches between Portuguese, English and French and restores the
initial culture. The log ends with `localization FIM: tudo ok` when every case
passes.

Circular references end at the full name of the key that closed the cycle.
The result does not keep `{$...}` commands for the native processor to change
later. The loader follows the files' culture even during `OnLanguageChanging`,
when `Language.ActiveCulture` still points to the previous one. Item and
projectile names with no entry in the JSON keep the class name.

## Sources

- `refs/dump.cs` and the disassembly of `SetLanguage`, `LoadLanguage`,
  `LoadFilesForCulture` and `ItemTooltip.ValidateTooltip` from version 301720.
- [tModLoader's LanguageManager.cs.patch](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Localization/LanguageManager.cs.patch),
  which loads mod translations at the end of `LoadFilesForCulture` and
  processes references afterwards.
