<p align="center"><img src="_icons/launcher/titulo.png" alt="Bunny Loader"></p>

<p align="center">
  <b>Play Terraria Mobile with mods, and make your own.</b>
</p>

<p align="center">
  <a href="https://play.google.com/store/apps/details?id=com.and.games505.TerrariaPaid"><img alt="Terraria Mobile 1.4.5.8.6" src="https://img.shields.io/badge/Terraria%20Mobile-1.4.5.8.6-6aa84f"></a>
  <img alt="Android 7.0+" src="https://img.shields.io/badge/Android-7.0%2B-3DDC84">
  <a href="https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Making-Mods"><img alt="Mods in C#" src="https://img.shields.io/badge/mods-C%23%20%C2%B7%20.NET%2010-512BD4?logo=dotnet&logoColor=white"></a>
  <a href="https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Bunny-Loader-vs-tModLoader"><img alt="tModLoader-style API" src="https://img.shields.io/badge/API-tModLoader--style-8a5cf6"></a>
  <a href="LICENSE.md"><img alt="License: PolyForm Noncommercial" src="https://img.shields.io/badge/license-PolyForm%20Noncommercial-orange"></a>
  <a href="https://github.com/JonataOliveiraa/Bunny-Loader/commits/main"><img alt="Last commit" src="https://img.shields.io/github/last-commit/JonataOliveiraa/Bunny-Loader/main"></a>
</p>

<p align="center">
  <a href="https://discord.gg/HgH3CfC8yV"><img alt="Discord" src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fdiscord.com%2Fapi%2Fv9%2Finvites%2FHgH3CfC8yV%3Fwith_counts%3Dtrue&query=%24.approximate_member_count&label=Discord&suffix=%20members&logo=discord&logoColor=white&color=5865F2"></a>
  <a href="https://www.tiktok.com/@bunny_loader"><img alt="TikTok" src="https://img.shields.io/badge/TikTok-%40bunny__loader-000000?logo=tiktok&logoColor=white"></a>
</p>

<p align="center">
  <a href="https://discord.gg/eunzRDSvuj">Download</a> ·
  <a href="https://github.com/JonataOliveiraa/Bunny-Loader/wiki">Wiki</a> ·
  <a href="https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Making-Mods">Make a mod</a> ·
  <a href="https://discord.gg/HgH3CfC8yV">Community</a>
</p>

Bunny Loader is a free app that brings mods to **Terraria on Android**. New
weapons, bosses, biomes, pets, furniture, texture packs and more: the kind of
mods PC players enjoy with tModLoader, now on your phone. Install the app,
pick your mods and play, alone or with friends.

It is also a place to **make mods**, on the phone itself or on a PC. Bunny
Loader follows tModLoader's way of writing them, so what you learn here works
there, and the other way around.

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
- **Make mods anywhere**: on the phone with BL Editor, or on a PC with Visual
  Studio, with tModLoader's classes and the whole game available to your code.

## Getting started

To play, you need **Terraria from Google Play** on your phone, because Bunny
Loader opens the copy of the game you own. Get the app from the **Download**
link above, open it, and import mods in the **Packages** tab.
[Installing Bunny Loader](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Installing-Bunny-Loader)
walks you through every step, with pictures.

To make mods, start at
[Making mods](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Making-Mods).
A whole mod can be this small:

<p align="center"><img src="https://raw.githubusercontent.com/wiki/JonataOliveiraa/Bunny-Loader/images/code/double-damage.png" alt="DoubleDamage.cs: a mod class and a GlobalItem that doubles the damage of every weapon" width="538"></p>

## Documentation

Everything else lives in the **[wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki)**:
playing with mods, making them on the phone or on a PC, every class of the
API side by side with tModLoader, and how Bunny Loader works inside, from
[building it from source](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Building-From-Source)
to its native core. What changed in each version is in
[`changelogs/`](changelogs/).

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
