# refs/ — local game dump

**Nothing here is versioned** (only this README). Each developer generates
their own dump from their purchased copy of Terraria.

## Quick way

```bash
tools/dump.sh
```

Does everything: finds the device in adb, pulls `base.apk`, extracts the
targets, validates the metadata's magic and runs Il2CppDumper. Use
`--skip-pull` to reuse an already downloaded `base.apk`.

> The dump has to come from the SAME binaries that are integrated into the
> build (`app/src/main/jniLibs/arm64-v8a/libil2cpp.so` and the
> `global-metadata.dat` from `terraria1456_assets/`). The Google Play build
> from 1.4.5.8 on ships with PairIP and is not the one Bunny Loader runs: in
> that case, copy the two integrated files here and run only step 3 of the
> manual way.

Variables: `ADB_SERIAL`, `IL2CPPDUMPER`, `PKG`, `ABI`.

## Manual way

1. Find and pull the APK:
   ```bash
   adb shell pm path com.and.games505.TerrariaPaid
   adb pull <path>/base.apk refs/base.apk
   ```
2. Extract (the APK is a zip):
   ```bash
   cd refs
   unzip -o -j base.apk \
     "lib/arm64-v8a/libil2cpp.so" \
     "lib/arm64-v8a/libunity.so" \
     "assets/bin/Data/Managed/Metadata/global-metadata.dat" -d .
   ```
3. Run Il2CppDumper:
   ```bash
   Il2CppDumper.exe libil2cpp.so global-metadata.dat . < /dev/null
   ```
   > The `< /dev/null` avoids hanging on "Press any key to exit". The
   > `Cannot read keys...` exception at the end is **harmless** — it happens
   > after everything has been generated.

## Expected content

```
refs/
  base.apk                     ~204 MB  (also used for jadx in Phase 1)
  libil2cpp.so                  ~54 MB
  libunity.so                   ~13 MB
  global-metadata.dat          ~8.8 MB
  dump.cs                       ~16 MB  <- reading reference
  il2cpp.h                      ~31 MB
  script.json                   ~41 MB
  stringliteral.json           ~1.4 MB
  DummyDll/Assembly-CSharp.dll ~7.5 MB  <- signatures only, no bodies
```

## Validated build

| Item | Value |
|---|---|
| Package | `com.and.games505.TerrariaPaid` |
| versionName | **1.4.5.8.6** |
| versionCode | **301720** |
| minSdk | 23 |
| ABIs in the APK | `arm64-v8a`, `armeabi-v7a` |
| Installed `primaryCpuAbi` | `arm64-v8a` |
| Metadata | magic `AF1BB1FA`, **version 31** (Unity 2021.3.56f2) |
| `libunity.so` | no `libpairipcore.so` in `DT_NEEDED` |
| Il2CppDumper | v6.7.46 (win, self-contained) |

> The dumper warns `WARNING: find JNI_OnLoad` / `ERROR: This file may be
> protected` and then `Change il2cpp version to: 29`. That is normal for this
> build: it finds `CodeRegistration`/`MetadataRegistration` and the dump comes
> out complete.

The previous version (1.4.5.6.4, versionCode 301543) is in `refs/1.4.5.6.4/`,
with the same content, for comparison.

## Querying the dump

```bash
tools/dumpgrep.sh Entity Terraria           # the class block
tools/dumpgrep.sh Projectile Terraria | sed -n '/\/\/ Fields/,/\/\/ Properties/p'
tools/dumpgrep.sh ItemID Terraria.ID | grep -i minishark
```

## ⚠️ Signatures change between versions

Older community documentation describes Terraria **1.4.0.5.2.1**, and even
from one 1.4.5 to the next there are changes. Examples:

| Before | This build (1.4.5.8.6) |
|---|---|
| `Item.SetDefaults(int Type, bool noMatCheck)` (1.4.0.5) | `Item.SetDefaults(int Type, ItemVariant variant)` |
| `Main.NewText(string newText, byte R, byte G, byte B)` (1.4.5.6) | `+ bool onlyCurrentPlayer` |
| `NPC.StrikeNPC(...)` returned `double` and had `bool noEffect` (1.4.5.6) | returns `int`, no `noEffect` |
| `Projectile.identity` (1.4.5.6) | `Projectile.key` (`ProjectileKey`: Spawner, Index, Generation) |
| `Player.setBonus` (1.4.5.6) | does not exist: the set text comes from `ArmorSetBonuses` |

**Always check `dump.cs` before writing a hook.**
