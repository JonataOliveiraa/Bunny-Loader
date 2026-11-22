# Plan: compiled mod packages

Status: proposal (2026-11-15). Nothing implemented yet. Follows the tModLoader `.tmod` model
on purpose: real protection is not possible (a .NET DLL decompiles in ILSpy and any key would
live in the APK), so the goal is the same as tML's: no sources in the package, not a zip, and
no way inside the app to turn a compiled mod back into an editable one.

## Goal

Today every package is a plain zip (`ModRepository.stageFrom` unzips anything), the catalog's
Releases carry the `.cs` files next to `bin/<id>.dll`, and the installed folder in
`Android/data/<pkg>/bunny_packs/<uid>/` holds the sources ready to edit.

After this plan:

- A mod from the catalog is a compiled package: a binary file with the `.dll` and the assets,
  no `.cs`. Renaming it to `.zip` opens nothing.
- The app never extracts a compiled package to a folder the user edits, and never exports it as
  source.
- Importing a zipped `.bl`/`.bmod`/`.zip` installs a **source** package, as today. It is never
  turned into a compiled one, even if it only carries `<id>.dll`.
- The BL Editor and Mod Sources keep producing zips (`ModSources.build` → `ModRepository.import`).
- Local compilation (`:compile`, `Host.FromSources`, the `bin/` seal) keeps working for source
  packages.

## How tModLoader does it

Read in `exmod_tmod/tModLoader/patches/tModLoader/Terraria/ModLoader/Core/`:

- `TmodFile.Save`: binary file. `"TMOD"` + tML version + SHA-1 of the data + 256-byte signature
  slot (unused) + data length; data = name, version, file table (name, length, stored length),
  then each file, deflated when it pays off (`ShouldCompress`, not for png/ogg/etc.).
- `ModCompile`: the `.tmod` gets `<Name>.dll` (+ `.pdb`), `Info`, `lib/*.dll` and resources.
  `.cs`/`.csproj`/`.sln` are left out unless `includeSource = true` in build.txt.
- The `.tmod` stays as one file in the Mods folder. tML reads entries straight from it.
- `AssemblyManager`: the `.dll` is loaded from memory (`LoadFromStream`), never written to disk.
- The hash is checked before loading; a modified file is refused ("File has been modified").
- `hideCode`/`hideResources` only grey out the in-game "Extract" button (`UIExtractMod`).

What we take: binary container, no sources, hash check, DLL from memory, the file is the
install. What we do not take: the Extract button (the user's rule is stricter: a compiled mod
never goes back to source inside the app), the signature slot (unused in tML too) and `.pdb`.

## Terms

| Term | What it is |
|---|---|
| Source package | Today's zip: `manifest.json`, `<id>.cs` (or `<id>.dll`), assets. Installed as a folder. |
| Compiled package | New binary file (`BLPK`), no `.cs`. Installed as the file itself. |
| Store package | A compiled package that came from the catalog (sha256 matches the index). |

The extension stays `.bl`. The importer tells them apart by the first 4 bytes: `PK\x03\x04` =
zip, `BLPK` = compiled. `.bmod` and `.zip` are only ever zips.

## Container format: BLPK v1

Same layout idea as `TmodFile`:

```
"BLPK"                 4 bytes
format version         u16 = 1
blVersion              u16 (manifest blVersion, 3 today)
api                    string (BunnyLoader.dll AssemblyVersion, 2.0.0.0 today)
sha256                 32 bytes, of everything after the data length field
data length            u64
data:
  manifest.json        the manifest as is (uid, id, name, version, dependencies...)
  file count           u32
  file table           name, length, stored length (stored < length = deflated)
  files                back to back
```

- Files: `manifest.json`, `icon.png`, `description.md`, `changelog.md`, `license.md`, `authors/`,
  `Assets/`, `Content/` images and sounds, `Localization/`, `Images/`/`Fonts/` for packs,
  `<id>.dll`, `ref/<id>.dll`.
- Never inside: `*.cs`, `*.csproj`, `*.sln`, `bin/`, `obj/`, `*.pdb`.
- `ref/<id>.dll` is a reference assembly (Roslyn `EmitOptions(metadataOnly: true)`: signatures,
  no method bodies), so another mod can compile against a compiled dependency. tML extracts the
  dependency's `.dll` to a temp folder for the same reason (`ModCompile`, `refMod`).
- Compression like tML: deflate only text, json and dll; png/ogg/wav/xnb stored as is.
- One writer/reader in Kotlin (`BlpkFile.kt`) shared by the app and the catalog, plus a small C#
  reader in `BunnyLoader` for the `.dll` entry. JVM unit test for write → read → verify.

## Install

The compiled package is installed as **one file**: `bunny_packs/<uid>.bl`, next to the source
folders (like `.tmod` files in tML's Mods folder). Nothing is extracted there.

- `stageFrom` reads the magic first. Zip → today's path. BLPK → read the header, check
  `blVersion`, uid, id, sha256, then copy the file in place.
- The native side loads textures, sounds and fonts by path, so the assets need to be on disk:
  the app extracts them (not the `.dll`) to a private cache, `filesDir/compiled_cache/<uid>/`,
  keyed by the package sha256, and rebuilds the cache when it is missing or stale. The cache is
  an implementation detail, not an install: it is never listed, exported or edited.
- `list()` reads both: folders (source) and `<uid>.bl` files (compiled). One uid lives in only
  one of them.
- `ModManifest` gains `origin` (`local`, `compiled`, `store`), kept in the app's preferences by
  uid. `RemoteCatalog` passes `store` when the download sha256 matches the index.
- Before each JOGAR, a compiled package whose sha256 no longer matches is refused with a clear
  message on the mod card (tML's "File has been modified").

## Replacement rules (same uid)

| Installed | Imported | Result |
|---|---|---|
| none | zip | source folder `bunny_packs/<uid>/` (today) |
| none | BLPK | file `bunny_packs/<uid>.bl` |
| compiled | zip | **the zip replaces the compiled one**: `<uid>.bl` and its cache are deleted, the source folder is installed |
| compiled | BLPK | normal update of the file |
| source | BLPK | the compiled one replaces the source folder (Mod Sources is never touched) |
| source | zip | today's update |

- Nothing turns a compiled install back into source: no Extract, no export as zip, no "copy to
  Mod Sources". A modder who owns a store mod and wants to change it imports their own zip,
  which replaces the store copy. That needs the source the modder already has.
- The confirm dialog that `StagedPackage.installed` already feeds says what changes:
  "This replaces the catalog version with your copy that has code." / the reverse.
- A folder pasted by hand into `bunny_packs/` with the uid of a compiled install counts as an
  import of a zip: source wins, `<uid>.bl` is deleted on the next `list()`.

## Load

- `enabledSpecs()` sends `uid=entry` today and the native side builds `modsDir + "/" + uid`
  (`cpp/mods/ModLoader.cpp:73`). Change the spec to carry the asset folder and the entry, e.g.
  `uid=<folder>|<entry>`, where the folder is `bunny_packs/<uid>` for source and
  `compiled_cache/<uid>` for compiled. Use it in `loadAll`, `configureTexturePacks`,
  `configureFontPacks` and `Host.LoadMod`, and check every place that rebuilds a mod path from
  `modsDir` (`ModAssets.cs`, sounds, localization, save side files).
- Entry `<uid>.bl` → `Host.LoadMod` reads the `<id>.dll` entry from the package with the C#
  reader → `AssemblyLoadContext.Default.LoadFromStream`. The `.dll` never touches the disk.
- `Assembly.Location` is empty for stream-loaded assemblies: `DependencyAssemblies` must return
  `compiled_cache/<uid>/ref/<id>.dll` for those, and `ResolveFromMods` must find already loaded
  mod assemblies by name before looking at folders.
- `enabledSourceBuilds()` skips compiled installs; a source mod that depends on a compiled one
  references its `ref/<id>.dll`.
- API: a compiled mod can't be recompiled on the phone (tML has the same limit: mods are rebuilt
  by their authors per tML version). Log in English
  (`mod <uid>: compiled for API X, this is Y`), try to load anyway, and show "needs an update
  from the author" on the mod card when it throws `MissingMethodException`/`TypeLoadException`.

## Export

`Shell.exportMod` → `ModRepository.export` today zips the installed folder.

- Compiled/store install: export copies `<uid>.bl` as is. No toggle.
- Source install: the export sheet gets a switch **"Export compiled"**, **off by default**.
  - Off: today's zip.
  - On: run the same build as JOGAR (`CompileActivity` in `:compile`, `NativeEntry.Build`),
    also emit `ref/<id>.dll`, then write a BLPK without sources. Compile errors show in the
    sheet, as the BL Editor's Test does.
  - This is the file a modder sends to the owner for the catalog. Importing it installs it as
    compiled, like any BLPK.
- Texture and font packs have no code: the switch does not show for them.

## Catalog

Both the BL Catálogo app (`catalog/`) and the PC tools (`publish.py`, `manager.py`,
`csharp_build.py`) change; they share the rules.

- Prerequisite: `CSharpBuild.prepare` still reads `content/$entry` (format 2). Move it to
  format 3 (`<id>.cs` at the root) first.
- Upload of a zip with sources: compile (as today), then publish a **BLPK**. The zip is never
  published.
- Upload of a BLPK (exported compiled by a modder): check header, sha256, `blVersion`, `api`,
  and that the `.dll` loads its metadata (no recompile), then publish it.
- `index.json` entries gain `"format": "blpk"`.
- Old Releases (`mod-<id>-v<version>`) in Bunny-Loader-Mods still hold `.cs`. After the
  republish, deleting them is the owner's call.

## Phases

1. **Format**: `BlpkFile.kt` + C# reader + JVM tests.
2. **Install**: magic detection, `<uid>.bl` installs, private asset cache, two-kind `list()`,
   replacement matrix, origin, hash check before JOGAR; unit tests for the matrix.
3. **Load**: spec with folder, native paths, `LoadFromStream`, ref assemblies, API message.
4. **Launcher UI**: "Compiled"/"Catalog" badge on the card, replace dialogs, hide folder/edit
   actions for compiled installs, update notice rules (decision D2).
5. **Export**: the "Export compiled" switch (off by default), copy for compiled installs.
6. **Catalog**: format 3 fix, BLPK output, BLPK upload, private source repo (D1), index fields
   (`format`, `minAppVersion`), republish, Release cleanup.
7. **Tests**: `tools/tests/` case on the MuMu (with `BL_PKG`): ExampleMod as BLPK loads, no `.cs`
   on the device, zip with the same uid replaces it and back, edited `<uid>.bl` is refused, a
   source mod depending on a compiled one, MP with a compiled mod on host and client. Then the
   phone.
8. **Docs**: wiki page for modders ("Compiled packages": what it is, how to export compiled,
   how to replace a store copy with your own), changelog entry.

## Decisions (owner, 2026-11-15)

- **D1 – Source custody: approved.** On upload of a zip with sources, the catalog app (and the
  PC tools) also upload that zip to a **private** repo (e.g. `Bunny-Loader-Mods-src`, with its
  own token that only reaches it), so the owner can rebuild every store mod when the API
  breaks. Mods that arrive already compiled from a modder have no source there; those depend on
  the modder sending a new build.
- **D2 – Store update over a local copy: approved.** When the installed copy is a zip (origin
  `local`) with the uid of a store mod and the store has a newer version, there is no automatic
  update notice; the store page shows "Installed (local copy)" with a "Use the catalog version"
  button.
- **D3 – Older Bunny Loader versions: they must update.** No zip is kept next to the BLPK. An
  older app that downloads a BLPK fails at unzip with its "not a package" message; the release
  notes and the Discord notice say to update. From this version on, `index.json` gets
  `minAppVersion` per entry, so a newer format later shows "update the Bunny Loader" instead of
  an install error.
- **No protection beyond tML's.** The repos are public and a .NET DLL decompiles anyway; the
  debuggable APK question is dropped.
