<p align="center"><img src="_icons/launcher/titulo.png" alt="Bunny Loader"></p>

<p align="center">
  <b>Play Terraria Mobile with mods.</b>
</p>

<p align="center">
  <a href="https://play.google.com/store/apps/details?id=com.and.games505.TerrariaPaid"><img alt="Terraria Mobile 1.4.5.8.6" src="https://img.shields.io/badge/Terraria%20Mobile-1.4.5.8.6-6aa84f"></a>
  <img alt="Android 7.0+" src="https://img.shields.io/badge/Android-7.0%2B-3DDC84">
  <a href="docs/mods-em-csharp/README.md"><img alt="Mods in C#" src="https://img.shields.io/badge/mods-C%23%20%C2%B7%20.NET%2010-512BD4?logo=dotnet&logoColor=white"></a>
  <a href="docs/mods-em-csharp/05-comparacao.md"><img alt="tModLoader-style API" src="https://img.shields.io/badge/API-tModLoader--style-8a5cf6"></a>
  <a href="LICENSE.md"><img alt="License: PolyForm Noncommercial" src="https://img.shields.io/badge/license-PolyForm%20Noncommercial-orange"></a>
  <a href="https://github.com/JonataOliveiraa/Bunny-Loader/commits/main"><img alt="Last commit" src="https://img.shields.io/github/last-commit/JonataOliveiraa/Bunny-Loader/main"></a>
</p>

<p align="center">
  <a href="https://discord.gg/HgH3CfC8yV"><img alt="Discord" src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fdiscord.com%2Fapi%2Fv9%2Finvites%2FHgH3CfC8yV%3Fwith_counts%3Dtrue&query=%24.approximate_member_count&label=Discord&suffix=%20members&logo=discord&logoColor=white&color=5865F2"></a>
  <a href="https://www.tiktok.com/@bunny_loader"><img alt="TikTok" src="https://img.shields.io/badge/TikTok-%40bunny__loader-000000?logo=tiktok&logoColor=white"></a>
</p>

<p align="center">
  <a href="https://discord.gg/eunzRDSvuj">Download</a> ·
  <a href="docs/mods-em-csharp/README.md">Make a mod</a> ·
  <a href="docs/README.md">Documentation</a> ·
  <a href="docs/mods-em-csharp/referencia/README.md">API reference</a> ·
  <a href="https://discord.gg/HgH3CfC8yV">Community</a>
</p>

Bunny Loader is a free app that brings mods to **Terraria on Android**. New
weapons, bosses, biomes, pets, furniture, texture packs and more: the kind of
mods PC players enjoy with tModLoader, now on your phone. Install the app,
pick your mods and play, alone or with friends.

It is also a place to **make mods**. Bunny Loader follows tModLoader's way of
writing them, so what you learn here works there, and the other way around.

## Contents

- [Features](#features)
- [Playing](#playing)
- [Making mods](#making-mods)
- [Documentation](#documentation)
- [Building from source](#building-from-source)
- [Community](#community)
- [Acknowledgments](#acknowledgments)
- [License](#license)

## Features

- **New content**: items, weapons, enemies, bosses with their own music,
  town NPCs with shops, pets, biomes, blocks and furniture.
- **Your saves stay safe**: mod content is kept next to your character and
  world, which still open without the mod.
- **Mod Menu in game**: each mod's items and NPCs at a tap, superpowers, and
  each mod's options in the pause menu.
- **Texture and font packs**, including resource packs made for the PC.
- **Multiplayer** with friends who also use Bunny Loader: whoever joins gets
  the host's mods offered to download.
- **Made for touch**: mod buttons on screen, menus that work with your
  fingers.
- **For mod makers**: mods in C#, with tModLoader's classes and the whole game
  available to your code ([Making mods](#making-mods)).

## Playing

1. Have **Terraria from Google Play** installed: the game only opens with it.
2. Install the APK from [Releases](https://github.com/JonataOliveiraa/Bunny-Loader/releases),
   the only official builds.
3. Import mods in the *Packages* tab, or put them in
   `Android/data/com.bunnyloader/bunny_packs/<uid>/`.

## Making mods

Bunny Loader copies tModLoader's API, so **tModLoader's material is your
learning material too**:

- [tModLoader](https://github.com/tModLoader/tModLoader) and its
  [wiki](https://github.com/tModLoader/tModLoader/wiki): start with the
  [Basic Modding Guide](https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide).
- tModLoader's [ExampleMod](https://github.com/tModLoader/tModLoader/tree/1.4.5/ExampleMod)
  and the [API reference](https://docs.tmodloader.net/).
- Then [Bunny Loader × tModLoader](docs/mods-em-csharp/05-comparacao.md): the
  short list of what the phone does differently (no IL editing, `Item.New()`,
  touch instead of mouse...).
- The [Bunny Loader API reference](docs/mods-em-csharp/referencia/README.md):
  every class and member, side by side with tModLoader.

A whole mod, which compiles the same in tModLoader:

```csharp
using Terraria;
using Terraria.ModLoader;

public class DoubleDamage : GlobalItem
{
    public override void SetDefaults(Item item)
    {
        if (item.damage > 0) item.damage *= 2;
    }
}
```

[Your first C# mod](docs/mods-em-csharp/01-primeiro-mod.md) shows the project,
the package and how to install it. [`ExampleModCSharp`](samples/ExampleModCSharp)
is the port of tModLoader's ExampleMod, with working examples of every class.

## Documentation

Everything lives in [`docs/`](docs/README.md), also on the
[wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki). What changed in
each version is in [`changelogs/`](changelogs/).

| | |
|---|---|
| [Making mods in C#](docs/mods-em-csharp/README.md) | The guides, from the first mod to screen effects: hooks, `ref`/`out`, values and objects, content classes, tiles, systems, world generation, biomes, menus, sounds, configs, commands, achievements, touch buttons, interface, fonts and packs. |
| [API reference](docs/mods-em-csharp/referencia/README.md) | Every class and member, explained and compared with tModLoader: what is the same, what differs, what exists only here and what is missing. |
| [Bunny Loader × tModLoader](docs/mods-em-csharp/05-comparacao.md) | What is equal and what differs. |
| [Performance](docs/mods-em-csharp/06-desempenho.md) | Measured on a real phone, and how to write a fast mod. |
| [Migration to C#](docs/mods-em-csharp/07-migracao-para-csharp.md) | For mods made before 0.4.0. |
| [The native core](docs/nucleo/README.md) | For people working on Bunny Loader: the boot, [hooks](docs/nucleo/hooks.md), [new content](docs/nucleo/conteudo.md), [the cost of C# hooks](docs/nucleo/custo-csharp.md), [multiplayer](docs/nucleo/multijogador.md). |
| [History](docs/historico/README.md) | The decisions, investigations and measurements behind the design. |

## Building from source

> This repository does **not** include Terraria's binaries or assets
> (`libil2cpp.so`, `libunity.so`, the game data), Unity's Java classes or the
> Andy font: they stay out of Git and are integrated locally. The dumps in
> `refs/` are generated locally and never published. Some UI textures
> (backgrounds, menu icons, example mod covers) are crops of Terraria's art,
> generated by `tools/ui-sprites.py` and `tools/mod-art.py`.

| Layer | Technology |
|---|---|
| App and launcher | Kotlin + Jetpack Compose |
| Native core | C++20 (NDK + CMake) → `libbunny.so` |
| Hooks | [ShadowHook](https://github.com/bytedance/android-inline-hook) (inline, ARM64), with our own chain |
| IL2CPP | the embedding API (`il2cpp_*`), via `dlsym`; everything resolved by name |
| Mod runtime | .NET 10 on Mono, with generated proxies of the game's types |
| C# compiler | Roslyn, for mods that ship `.cs` files |
| Game dump | Il2CppDumper |

```
app/src/main/
  kotlin/dev/bunnyloader/   Android app: the launcher and the GameActivity that boots Unity
  cpp/                      Native core (libbunny.so); map in docs/nucleo/README.md
  csharp/                   C# API (BunnyLoader.dll), its proxy generator and the mod compiler
  res/                      Android resources
samples/                    Example mods
docs/                       Documentation (guides, core, history)
tools/                      PC tools: fast build, dump, tests
refs/                       Local game dump (NOT versioned)
```

Open the folder in Android Studio and let **Gradle Sync** run: it downloads
Gradle and offers to install any missing SDK platform.

1. SDK Manager → **SDK Tools** tab → install **NDK (Side by side)** and
   **CMake**.
2. The game runtime (the `.so` files in `app/src/main/jniLibs/arm64-v8a/` and
   the data in `terraria1456_assets/`) is not in the repository; see the
   [folder's README](app/src/main/jniLibs/arm64-v8a/README.md). Without it the
   app builds and installs, and `GameActivity` reports that the runtime is
   missing.
3. Unity's classes (`app/libs/unity-classes.jar`) and the Andy font
   (`app/src/main/res/font/bunny.ttf`) also stay out of Git, since they belong
   to third parties. Without the jar the game does not boot ([`app/libs/README.md`](app/libs/README.md));
   without the font the app uses Comic Neue ([`app/src/fontFallback`](app/src/fontFallback/README.md)).
4. Mods need the **.NET 10 SDK** on the `PATH` (`dotnet`): the build
   compiles `BunnyLoader.dll` and downloads the Mono runtime from nuget.org
   once. Without the SDK, build with `-Pbl.csharp=false` (no mods load).

| Shortcut | |
|---|---|
| `-Pbl.nativeBuild=false` | Launcher only, no NDK or CMake. |
| `-Pbl.uiOnly=true` | Without the game's assets and `.so` files: the UI cycle drops to ~6 s ([`tools/ui.sh`](tools/ui.sh)). |
| `-Pbl.csharp=false` | Without mods: no .NET SDK needed, and the APK loses the Mono runtime and the compiler. |

**Game dump.** `tools/dump.sh` generates `refs/` (the `dump.cs` with every
class, field and method of the game) from the installed Terraria. See
[`refs/README.md`](refs/README.md).

**Tests.** The tests are mods in [`tools/tests/`](tools/tests), run on the
emulator by [`tools/tests/run-cs.sh`](tools/tests/run-cs.sh) (and
[`run-mp.sh`](tools/tests/run-mp.sh) on two devices); see
[`tools/README.md`](tools/README.md). Development happens on MuMu Player (which
runs the game's ARM code through translation, and where hooks work) and is
checked on a real ARM device.

<details>
<summary><b>Release APK and signing</b></summary>

`assembleRelease` signs with the key from `keystore.properties` at the root
(copy [`keystore.properties.example`](keystore.properties.example), which also
has the `keytool` command to create the key). The variables
`BL_RELEASE_STORE_FILE`, `BL_RELEASE_STORE_PASSWORD`, `BL_RELEASE_KEY_ALIAS` and
`BL_RELEASE_KEY_PASSWORD` work too. The key and the file stay out of Git.
Without them, the release APK comes out unsigned.

When that key is set, the debug APK is signed with it too. Without it, Gradle
falls back to the debug key of the machine running the build.

Keep the key safe: Android only updates the app in place with the same
signature. The official APKs on the Releases page are signed with this
certificate:

```
SHA-256: DB:68:1B:9C:95:91:58:E8:AF:08:65:EC:2F:F1:A0:A1:B5:EB:6C:41:41:2D:86:FD:29:E6:AA:91:9B:E3:A6:90
```

An APK built on another machine has a different signature: installing it over
the official one (or the other way around) fails with "App not installed".
Back up `Android/data/com.bunnyloader` (players, worlds, mods) and uninstall
first. `apksigner verify --print-certs bunny-loader.apk` shows which key an APK
carries.

`versionCode` is the number of commits on the branch, so each new build can
update the previous one.

</details>

## Community

- [Discord](https://discord.gg/HgH3CfC8yV): help, mods, news and bug reports.
- [TikTok](https://www.tiktok.com/@bunny_loader): videos of new features and
  mods.
- [CONTRIBUTING.md](CONTRIBUTING.md) to send changes, and
  [SECURITY.md](SECURITY.md) to report a flaw privately.

## Acknowledgments

- The **tModLoader Team**, for the design of the mod classes and the original
  ExampleMod that our [`ExampleModCSharp`](samples/ExampleModCSharp) ports.
- **GST378** and **Animatak_**, authors of ExMod, the community port of
  tModLoader's ExampleMod to mobile, which helped shape our first examples.
- **TL Pro**, whose publicly documented scripting API (`NativeClass`,
  signature strings, `hook((original, self, ...args) => ...)`, the global `tl`
  object, later renamed `bl`) Bunny Loader's first bridge, removed since, was
  modeled on. Inspecting the manifest, class names and file layout of TL Pro's
  Android package (version 1.64.0) informed our choice of hosting Unity in our
  own process. No TL Pro code or assets are part of Bunny Loader, and Bunny
  Loader is not affiliated with or endorsed by TL Pro.

## License

Bunny Loader is **source-available for noncommercial use** under the
[PolyForm Noncommercial License 1.0.0](LICENSE.md): use it, study it, modify it
and share it for free, but not to make money. Selling it, putting ads in it, or
locking features behind payment needs written permission.

- [LICENSING.md](LICENSING.md): what applies to each part, what counts as
  commercial use, and the clause that keeps mods under their authors' own
  licenses.
- [TRADEMARKS.md](TRADEMARKS.md): forks need their own name, icon and
  `applicationId`.
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md): ShadowHook, the .NET
  runtime (Mono), Roslyn, tModLoader, AndroidX and the parts this license does
  not cover (Terraria, Unity, the Andy font).
