# Wiki plan

Written 2026-11-13. Goal: every guide lives in the
[GitHub wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki), complete
and very explicit, with code cutouts and in-game cutouts; the README keeps only
the basics, for players.

## Status

| Phase | What | State |
|---|---|---|
| 1. Foundation | This plan, `tools/wiki/`, wiki skeleton, sample pages | done |
| 2. Port | Every current guide moved into the new page map, diagrams turned into prose, guide 09 split by subject, API reference generated straight into the wiki, README slimmed | done (2026-11-14), not published |
| 3. Getting started | Phone (BL Editor) and PC (Visual Studio) paths, tested end to end | all eight pages written and tested on the capture emulator (BL Editor's sword template and Test; Visual Studio 2026's MSBuild and zip import; export to `.bl`; the mod page with banner, icon, tabs and gallery; every C# example compiled against `BunnyLoader.dll`); three Visual Studio screenshots wait for computer use |
| 4. Basics | Items to Global classes, rewritten to the page anatomy | ported; Walls rewritten, Tiles revised, every "not available yet" checked against the code (2026-11-14) |
| 5. Intermediate and Advanced | The rest of the guides, rewritten | ported; World Generation rewritten, stale claims fixed, test-pack mentions moved out of the modder pages |
| 6. Playing | Player guides, FAQ, troubleshooting | all ten pages and the FAQ written (2026-11-14) |
| 7. Reference and Contributing | Manifest reference, log messages; the native core rewritten | API pages generated; Manifest Reference and Log Messages written from the code; Contributing ported |
| Go-live | Push the wiki, then the repository changes below | waiting for the go-ahead |

## Go-live checklist

Push the wiki first (`python tools/wiki/build.py` must pass), then, in one
commit of the main repository: the slim `README.md`; delete
`docs/mods-em-csharp/` and `docs/nucleo/` (move `nucleo/pesquisa-shaders.md`
to `docs/historico/`), `docs/README.md`, `docs/imagens/` once no history page
uses it, `tools/wiki.py` and `.github/workflows/wiki.yml`; point to the wiki
from `CONTRIBUTING.md` (also stale: it still says docs are in Portuguese and
mods go in the catalog), `THIRD_PARTY_NOTICES.md` (the reference path),
`samples/ExampleModCSharp/manifest.json` (its docs link), the BL Editor
README and the changelog links to `docs/`; rewrite `tools/docs/README.md`,
which still describes the removed JavaScript generator. Then, in the
repository settings, restrict wiki editing to collaborators.

The port pipeline (`port.py`, `demermaid.py`, `fixups.py`) lives in the
session scratchpad: until go-live, a change to a guide goes into `wiki/`, not
into `docs/`.

## Decisions

- **The wiki is the source.** It is cloned at `wiki/` inside this checkout
  (ignored by Git) and pushed on its own. The old copy machine
  (`tools/wiki.py` + `.github/workflows/wiki.yml`) goes away at go-live: it
  broke when the JavaScript guides moved (`abf48278`), so the wiki stayed on the
  2026-11-02 state.
- **What leaves the repository:** `docs/mods-em-csharp/` (guides and API
  reference), `docs/nucleo/` and the long parts of `tools/README.md` and of the
  README. **What stays:** `docs/historico/` (records, not guides),
  `changelogs/`, the legal files and short READMEs that point to the wiki.
- **English**, like the rest of the documentation.
- **No flowcharts or diagrams** (no Mermaid). Pictures are code cutouts and
  in-game or in-app cutouts; structure comes from steps, tables and callouts.
- **Visual Studio 2026** (18.0+) is the PC IDE of the guides: it is the first
  version that supports `net10.0` (Visual Studio 2022 builds it only with a
  warning, unsupported). VS Code and Rider get a short section.
- **Search engines.** GitHub only indexes wikis of repositories with 500+
  stars whose wiki editing is restricted to collaborators (75 stars today).
  Keep editing restricted. If search visibility matters later, the same pages
  can be published with GitHub Pages.

## What the research says

- **Diátaxis** ([diataxis.fr](https://diataxis.fr/)): tutorials (learn by
  doing), how-to guides (do a task), reference (look something up) and
  explanation (understand). A page does one of these; mixing them is what
  makes guides hard to follow.
- **tModLoader wiki**: getting started per audience (players, developers,
  contributors), one page per IDE, then Easy / Intermediate / Advanced / Expert
  guides, each tier opening with its prerequisites, then vanilla references.
- **Fabric docs**: getting started split into small steps (create the project,
  set up the IDE, launch, build), then topic groups (Items, Blocks,
  Entities...). Every snippet comes from a reference mod that builds.
- **GitHub wiki mechanics**: folders in the wiki repository are allowed but
  ignored in URLs, so page names must be unique; `_Sidebar.md` and
  `_Footer.md` at the root; images are referenced by their path from the root
  (`images/code/x.png`); soft limit of 5,000 files. Alerts (`> [!NOTE]`)
  render in wikis (checked on tModLoader's wiki), although GitHub's Markdown
  API does not render them (`preview.py` imitates them).

## Audiences and paths

| Reader | Starts at | Path |
|---|---|---|
| Player | Home → Installing Bunny Loader | Playing section |
| Modder with only a phone | Making Mods → Setup on Your Phone | Your First Mod → Basics → ... |
| Modder with a PC | Making Mods → Setup on PC | the same pages; steps that differ have two blocks |
| tModLoader veteran | Bunny Loader vs tModLoader | Reference |
| Artist | Making Texture Packs / Making Font Packs | BL Editor's texture editor |
| Contributor | Contributing | Building from Source → Native Core |

## Site map

Folders are only for us; the page name is the URL. `←` is the current source.

**Start**: `Home`, `FAQ`.

**Playing** (`playing/`)

| Page | Source |
|---|---|
| Installing-Bunny-Loader | README "Playing", signature notes of the README |
| Launcher-Tour | new: Home, Explore, Packages, Settings, themes |
| Installing-Mods | README, guide 01 step 5 |
| Mod-Menu | new (Mod Menu screens) |
| Mod-Settings | guide 16, player side |
| Touch-Controls | guide 19, player side |
| Multiplayer | `nucleo/multijogador.md`, player side |
| Texture-and-Font-Packs | guide 22, player side |
| Saves-and-Backups | new |
| Troubleshooting | guide 01 error table, Settings → Diagnostics |

**Getting started** (`getting-started/`)

| Page | Source |
|---|---|
| Making-Mods | `mods-em-csharp/README.md`, README "Making mods" |
| Setup-on-Your-Phone | BL Editor README, Mod Sources screen |
| Setup-on-PC | guide 01 steps 1–5, Visual Studio 2026 |
| Your-First-Mod | guide 01, the Mod Sources sword template |
| Mod-Structure | guide 01 manifest, package format 3, guide 09 "Textures and files" |
| Testing-and-Errors | guide 01 steps 6 and the error table |
| Sharing-Your-Mod | new: package, icon, description, license |
| CSharp-for-Modders | new: the C# a modder needs, with links to learn more |

**Basics** (`basics/`): split of guide 09 plus 10 and 15.

| Page | Source |
|---|---|
| Items | 09 "A new item", "Using an item", rarities, prefixes |
| Weapons | 09 combat, shooting and ammo, hitting an NPC, melee projectiles |
| Accessories-and-Armor | 09 armor, accessories and wings, equip drawing |
| Recipes | 09 recipes |
| Projectiles | 09 projectiles, grappling hooks, `GlobalProjectile` |
| NPCs | 09 NPCs, AI, loot, spawning, contact, buff immunity |
| Buffs | 09 buffs |
| Players | 09 `ModPlayer`, hurt, fishing, extra jumps, crafting materials |
| Tiles | 10 (blocks, objects, furniture, chests, tile entities, trees, pylons, banners) |
| Walls | 10 walls |
| Dust-and-Gore | 09 dusts, gores |
| Sounds | 15 |
| Localization | 09 translations |
| Global-Classes | 09 `GlobalItem`, `InstancePerEntity`, `GlobalNPC`, the other globals |

**Intermediate** (`intermediate/`)

| Page | Source |
|---|---|
| Content-Loading | 09: what gets loaded, the order, templates and instances, finding content by name |
| Systems | 11 |
| Saving-Data | 09 `SaveData`/`LoadData`, 11 world data |
| Mod-Configs | 16 |
| Town-NPCs | 09 town NPCs, shops, emotes |
| Bosses | 09 boss bars, loot, Bestiary; 13 music |
| Biomes | 13 |
| Mounts-Pets-and-Minions | 09 mounts, minions, sentries, pets |
| Damage-Classes | 09 damage classes |
| Chat-Commands | 17 |
| Achievements | 18 |
| Title-Menu-Themes | 14 |
| Touch-Buttons | 19, modder side |
| Networking | 09 `ModPacket`, `SendExtraAI` |
| Cross-Mod-Content | 09 other mods, `Call`, dependencies |

**Advanced** (`advanced/`)

| Page | Source |
|---|---|
| Hooks | 02 |
| Ref-Out-and-Nullable | 03 |
| Values-and-Objects | 04 |
| World-Generation | 12 |
| User-Interface | 20, `ModResourceOverlay` |
| Drawing | 09 NPC drawing, `PlayerDrawLayer`, hairstyles |
| Screen-Effects | 21 |
| Invasions | 23 |
| Dimensions | 24 |
| Performance | 06 |
| Under-the-Hood | 08 |

**Packs** (`packs/`): `Making-Texture-Packs`, `Making-Font-Packs` ← 22 and the
BL Editor texture and font tools.

**Reference** (`reference/`): `API-Reference` and its nine pages (generated by
`tools/docs/ApiReference`, which will write into the wiki),
`Bunny-Loader-vs-tModLoader` ← 05, `Manifest-Reference`, `Log-Messages`,
`Migrating-From-0.3` ← 07.

**Contributing** (`contributing/`): `Contributing`, `Building-From-Source`,
`Native-Core`, `Native-Hooks`, `Native-Content`, `Server-Join-Flow`,
`CSharp-Hook-Cost`, `Translating-the-App`, `Tools-and-Tests`,
`Releases-and-Signing` ← README, `nucleo/*`, `tools/README.md`, `CONTRIBUTING.md`.
`nucleo/pesquisa-shaders.md` moves to `docs/historico/`.

## Page anatomy

Pages read like the tModLoader wiki: a conversation in paragraphs, with the
code and the pictures woven into the text, not a stack of bullet points.

A tutorial opens with the section label (generated by `build.py`) and, when
there is one, the in-game cutout of what the page builds. Then comes a
paragraph that says what the reader will make and why it is worth it, followed
by a `> [!NOTE]` box with what to read first. The body is a series of sections
whose headings are the questions a reader would ask ("What is a projectile?",
"Why doesn't my item show up?") or the step being taken. Each code cutout is
followed by paragraphs that walk through it, pointing at the badges inline
("the class at **(1)**..."). The page ends with what to try in the game, what
usually goes wrong and why, what is different from tModLoader, and where to go
next; the previous/next links are generated.

When the phone and the PC differ in a step, the step has two collapsed blocks:
*On your phone (BL Editor)* and *On PC (Visual Studio)*.

## Writing style

Write in paragraphs. A list is only for things that really are a list: four
or more short, parallel items the reader will scan, such as the files of a
folder. A table is for reference data, such as fields and their meaning, and
belongs in the Reference section more than in a guide. Steps the reader
performs are sections with prose, like tModLoader's guides, not numbered lists
of fragments.

Use "you" for what the reader does and "we" or "let's" when a tutorial builds
something together. Explain every term the first time it shows up on a page,
with a link to the page that covers it, and connect the ideas: say why, not
only what. Avoid "simply", "just" and "obviously".

UI names go in **bold**, exactly as the English app shows them; files, paths
and code go in `code`. Callouts are rare and short: `[!NOTE]` for context,
`[!TIP]` for a shortcut, `[!IMPORTANT]` for a step that cannot be skipped,
`[!WARNING]` when something breaks and `[!CAUTION]` when data can be lost.
Code shown as an image is always also in text, ready to copy (`build.py` does
it), and complete files go through a compile check against `BunnyLoader.dll`
before a phase closes.

## Images

**Code cutouts** (`tools/wiki/codeshot.py`): the launcher's night palette, the
stepped corners of Terraria's panels, Visual Studio's dark colors for C#, the
file name on top, line numbers. `// (1)` at the end of a line becomes a yellow
badge. `hl="5-7"` highlights lines. PNG at twice the size, shown at 1x.

**Game and app cutouts** (`tools/wiki/gameshot.py`): screenshots straight from
the emulator (`adb:127.0.0.1:16384`) or a file, cropped to what matters, with
yellow numbered boxes that match the text, in the same stepped frame. WebP.

Rules for captures: the English UI; MuMu at 1280×720; one test character and
world for the whole wiki (daytime, no enemies, `csenv`); the Example Mod for
mod content; nothing personal (names, notifications).

Folders: `images/code/` (generated), `images/game/`, `images/app/` (launcher,
Mod Menu), `images/editor/` (BL Editor), `images/pc/` (Visual Studio).

## Capture environment

Set up on 2026-11-14 and kept apart from the test emulators.

**MuMu instance 3, "Wiki Captures"** (`127.0.0.1:16480`): 1600×900 at 240
dpi, 4 cores and 4 GB, root, Android 12, system language English, the MuMu
app store disabled (it opened ads over the apps). Installed: Terraria
1.4.5.8.6 (copied from the 16384), Bunny Loader Beta 0.5.5 from
`out/bunny-loader-beta.apk` and BL Editor Beta from
`../BL Editor/out/bl-editor-beta.apk`. Bunny Loader, BL Editor and the game
are in English, the Example Mod is installed from Explore and turned on, and
**Quick start** enters the character **Bunny** (Classic) in **Wiki World**
(small, Classic) straight away. Start it with
`MuMuManager.exe control -v 3 launch` and shut it down when done.

**Visual Studio Community 2026** 18.12 with the ".NET desktop development"
workload, next to Visual Studio 2022. It brought the .NET 11 SDK, which became
the default `dotnet` on this PC; the repository's `global.json` keeps its
builds on the .NET 10 SDK (10.0.300, rolling forward only within 10.0).

Raw captures are kept in `build/wiki-captures/` (ignored by Git) until they
are cropped into the wiki: the launcher (first-run language, Home, Explore,
the Example Mod page, Packages, Quick start), the game (title screen with the
Example Mod's theme, new player, new world, world generation, world list, the
first day) and the Mod Menu.

## Tools

All in `tools/wiki/`:

| Script | Does |
|---|---|
| `build.py [--strict] [wiki]` | code cutouts from `<!-- shot -->` blocks, section label and previous/next links (within a section) in sidebar order, link, anchor and image checks, pages missing from the sidebar; `--strict` also fails on links to pages not written yet |
| `codeshot.py` | one code cutout (also usable alone) |
| `gameshot.py` | one game or app cutout, with marks |
| `preview.py [pages]` | renders pages with GitHub's own Markdown renderer into a local folder, to check before pushing |

Writing a code cutout in a page:

````markdown
<!-- shot name="first-sword" title="Content/Items/FirstSword.cs" hl="17" -->
```csharp
...
```
````

`build.py` adds the image and wraps the code in a *Copy this code* block; edit
only the code.

## Found on the way

- The README's "whole mod" example (`DoubleDamage : GlobalItem` alone) does
  not load in Bunny Loader: `Host.LoadMod` requires a class that extends `Mod`
  ("no class extends Terraria.ModLoader.Mod"), while tModLoader creates one by
  itself. `Making-Mods` shows the version with the `Mod` class; the README
  gets fixed when it is slimmed, and the difference goes into
  `Bunny-Loader-vs-tModLoader`.
- The MuMu 16384 is a test bench (150 test packages, test helpers enabled),
  not a place for screenshots players will see.
- Things the English captures show that are still in Portuguese or stale: the
  Example Mod's manifest (category "Itens", authors joined with "e", links
  "Mods em C#" and "ExampleMod original"), the date on a catalog page ("10 de
  nov. de 2026" with the app in English), the **JS editor** setting, which
  still talks about JavaScript and QuickJS, and the log line "tiles de mod:
  mundo salvo sem tile de mod no .wld". Also the **Import package** dialog,
  which still says a package has "the content/ folder" (format 3 has none),
  and the **Itens** label of a mod's items tab in the Mod Menu.
- The repository guides were older than the code: blocks A to G (2026-11-06)
  added `GlobalWall`, wall conversion and drawing, `GlobalPylon` and the three
  pylon teleport hooks, `ModCactus`, smart interact outlines, mod accessory
  slots, recipe ordering, item PvP, global item saving and netcode, while the
  guides still said "not available yet". The wiki pages are fixed; the
  `docs/` copies go away at go-live.
- `ModWall.PlaceInWorld` is never called (only `GlobalWall.PlaceInWorld`,
  in `WallHooks.GlobalPlace`); the Walls page says so.
- In Bunny Loader `Color` is in `Microsoft.Xna.Framework.Graphics`; a sample
  that only has `using Microsoft.Xna.Framework;` does not compile. Two wiki
  samples were missing it.
- `ServerModSync.kt` still builds Portuguese text ("o pacote encontrado é",
  "você tem a", "a loja tem a", "o servidor não informou a versão").

## Open questions

- **Where players download the app and BL Editor.** GitHub has no Releases
  today, while the README says Releases are the only official builds and its
  Download link opens the Discord.
- **`BunnyLoader.dll` on the PC.** Today it is taken out of the APK by hand.
  A **Developer kit** button in Mod Sources (the `.dll`, a ready `.csproj` and
  a sample) would make the PC path much shorter.
