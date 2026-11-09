# tools/: PC tools

None of them run by themselves in the build; they are all called by hand
(except `wiki.py`, which GitHub runs on every push to `main`).

## Development

| Script | What it does |
|---|---|
| [`ui.sh`](ui.sh) | A short UI cycle: builds without the game's assets and `.so` files, installs, opens and takes a screenshot (~6 s). |
| [`dump.sh`](dump.sh) | Regenerates `refs/` from the installed game (pull + extraction + Il2CppDumper). |
| [`dumpgrep.sh`](dumpgrep.sh) | Extracts a class, struct or enum block from `refs/dump.cs`. |
| [`gen-cheatdata.py`](gen-cheatdata.py) | Generates `cheatbridge/bunny/CheatData.java` (the hand-picked NPCs) from `refs/dump.cs`. Items do not go through here: the menu sorts them by the game's own fields. |
| [`build-cheatbridge-dex.sh`](build-cheatbridge-dex.sh) | Compiles the cheat menu to dex and embeds it as a C header. |
| [`bin2header.py`](bin2header.py) | Binary → C array. Used by the script above. |
| [`ui-sprites.py`](ui-sprites.py) | Crops the game's UI textures the menu uses (Journey hours, arrows) and the "?" for a missing mod item (`content/items/UnloadedIcon.h`). Receives the game's `Images` folder. |
| [`mod-art.py`](mod-art.py) | The example mods' covers and icons. |
| [`wiki.py`](wiki.py) | Generates the [wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki) from `README.md` and `docs/`. Runs by itself in the `.github/workflows/wiki.yml` workflow on every push to `main`; by hand, only to check. |

```bash
tools/ui.sh                                     # iterate on screen
tools/dump.sh                                   # full pipeline
tools/dump.sh --skip-pull                       # reuses refs/base.apk
tools/dumpgrep.sh Projectile Terraria           # inspect a class
```

There will be no `.bmod` packer: the package is assembled by hand by the
author, with the documentation as a reference.

## Play Store bundle

`tools/play/` builds and checks the `.aab` for Google Play. Each script takes
the channel, `beta` (default) or `stable`.

| Script | What it does |
|---|---|
| [`init-upload-key.sh`](play/init-upload-key.sh) | Creates the upload key once: `bunny-upload.keystore` and `keystore-play.properties`, both out of Git. Back them up outside the PC. |
| [`build-aab.sh`](play/build-aab.sh) | Runs `:app:bundleRelease -Pbl.play=true`, writes `out/bunny-loader-beta.aab` (+ `.sha256`) and runs the checks. |
| [`check-aab.sh`](play/check-aab.sh) | Checks what Play enforces: `bundletool validate`, target SDK, not debuggable, no debug certificate, 16 KB pages, base module download size. Also checks that Unity's files are stored in the base APK. |
| [`install-aab.sh`](play/install-aab.sh) | Installs the bundle on a connected device as Play would (splits and asset pack), signed with the official key. It installs `com.bunnyloader.potatolabs`, next to the APKs, and keeps its data between installs. |

```bash
tools/play/build-aab.sh
tools/play/install-aab.sh
```

What `-Pbl.play=true` changes, compared with the normal APK:

- **Package:** `com.bunnyloader.potatolabs` in both channels, the package of the app on the Play Console. Beta and stable are tracks of that one app there, so the bundle never gets the `.beta` suffix.
- **Key:** Play refuses a debug certificate, and the official key is one
  (`CN=Android Debug`), so the bundle is signed with the upload key.
- **Target SDK:** 36, which Play requires. The normal APK stays on 34.
- **Asset pack:** `dotnet/`, `mods/` and `sprites/` go to the install-time pack
  `play_assets`, because the base module has a 200 MB limit. The app reads them
  through `AssetManager`, which sees the pack. Unity's `bin/Data` has to stay in
  the base APK.

A bundle build is not debuggable, so `run-as` and the scripts that rely on it
do not work on an app installed from it.

## Downloading and extracting tModLoader mods

Double-click [`mods/baixar-tmod.bat`](mods/baixar-tmod.bat), paste a Workshop
link and wait. It also accepts IDs and several links on the command line:

```powershell
python tools/mods/tmod.py "https://steamcommunity.com/sharedfiles/filedetails/?id=3810452158"
python tools/mods/tmod.py 3810452158 2824688072
python tools/mods/tmod.py --local "C:/Mods/MyMod.tmod"
```

Requires **Windows and Python 3.10+**. The script sets up portable versions of
the [official SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD),
[tModUnpacker 1.3](https://github.com/IVogel/tModUnpacker/releases/tag/1.3) and
[standalone 7-Zip](https://www.7-zip.org/download.html) in `build/tmod-tools/`.
The unpacker uses Windows' .NET Framework 4.6.1 or later; it does not need the
.NET SDK. The unpacker release has its SHA-256 pinned in the script. On first
use, SteamCMD's own update can take a few minutes; the script continues
automatically when the bootstrap finishes updating.

Each run creates a new folder in `out/tmod/<ID>/<date-time>-<suffix>/`:

- `tmod/`: the original `.tmod` files, keeping the Workshop's version folders.
- `extraido/`: one folder per package/version; inside it, the unpacker creates
  the folder with the mod's internal name, resources and DLLs. `.rawimg`
  becomes `.png`.
- `steamcmd.log` and `logs/`: download and extraction diagnostics.

Every `.tmod` of the item is extracted. Earlier results are kept. `--local`
only extracts; `--out FOLDER` changes the output; `--cache FOLDER` changes the
cache; `--steamcmd PATH.exe` and `--unpacker PATH.exe` use already installed
tools. `--timeout 900` is the default limit, in seconds, per call. In batches,
a failure is logged and the other items continue; any failure results in exit
code 1.

The download uses anonymous access, like
[tModLoader's server script](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/release_extras/DedicatedServerUtils/manage-tModLoaderServer.sh).
Private, removed, other-game or restricted items may not download. The script
checks SteamCMD's confirmation, the `.tmod`'s structure and hash, and refuses
internal names that would escape the extraction folder.

Extracting does not decompile the C# DLLs and does not convert the mod to
Bunny Loader. The extracted content depends on what the author put in the
package.

Validation tests, without network:

```powershell
python -m unittest discover -s tools/mods -p test_tmod.py -v
```

## Online catalog

The mods the Explore tab downloads live in a separate repository,
[Bunny-Loader-Mods](https://github.com/JonataOliveiraa/Bunny-Loader-Mods), with
no server. Separate from the app's code on purpose: a takedown request against
a mod only takes down the catalog, and the publishing token cannot write to the
code.

| Where | What |
|---|---|
| `main` | `index.json` (the list), `mods/<uid>/` (each mod's showcase: manifest, icon, cover, `description.md`, the authors' photos) and `ports/` (the sources of the ported texture packs). |
| Releases `mod-<id>-v<version>` | The `.bl` of each version. Never marked as *Latest*. |

The app reads everything without logging in (`raw.githubusercontent.com` and
the Release download link) and checks the index's sha256 before installing
([`RemoteCatalog.kt`](../app/src/main/kotlin/dev/bunnyloader/mods/RemoteCatalog.kt)).
There is no token in the APK. Only publishing needs one, on the PC.

When the launcher opens or comes back, the app checks the catalog. During the
fetch, Home and Explore show the spinning icon and the showcases' progress;
the last downloaded list remains available. On Home and Explore, pull down with
the scroll at the top and release to refresh the store. The showcases are
synced with up to four simultaneous operations.

A newer online version of an installed mod opens a popup with **Update**,
**Update all** and **Not now**. Each UID/version is announced once, when the
popup becomes visible; dismissing it keeps the update button on the page. A
later version produces another notice. Downloads continue after closing the
popup, with up to three simultaneous packages and serialized import. Offline,
new versions already known to the cache can also be announced.

**The easy way:** double-click [`mods/gerenciar.bat`](mods/gerenciar.bat) (or
`python tools/mods/manager.py`). It opens a page in the browser
(`http://127.0.0.1:8770`) that shows the mods in `samples/` with each one's
status (up to date, new version ready, changed without bumping the version),
publishes with one click, bumps the version, checks and publishes a dragged
`.bl`, and removes mods from the catalog. The GitHub token comes from a local
file or environment variable and is never exposed to the page. The page only
serves this computer and requires a key randomized on every run.

Underneath, it calls the same command-line scripts.
[`mods/publish.py`](mods/publish.py) checks the package with the app's import
rules, creates the Release, builds the showcase in `build/mods-repo/` (a clone
of the mods repository) and pushes the index:

Only ExampleMod goes inside the APK (`BUNDLED_SAMPLES` in
`app/build.gradle.kts`). The others in `samples/` are published here.
[`mods/pack.py`](mods/pack.py) builds a folder's `.bl`, always with the same
bytes, in `build/mods-packs/`.

[`mods/port_textures.py`](mods/port_textures.py) copies the images of resource
packs from the local Workshop to `build/mods-repo/ports/textures/`, creates
`manifest.json` with authorship and builds the `.bl` files. Example: `python tools/mods/port_textures.py 2439853873 2796830227`.
Each port has a README in `ports/textures/<name>/` of the mods repository; the
finished `.bl` files sit in `ports/textures/in bl/`.

The token can come from `BL_GITHUB_TOKEN`; without it, the script asks for it
on the spot, without showing it on screen.

```bash
python tools/mods/pack.py samples/VidaCheia      # -> build/mods-packs/vidacheia-1.0.0.bl
python tools/mods/publish.py --dry-run Mod.bl    # checks and builds, no network
python tools/mods/publish.py Mod.bl Other.bl     # publishes
python tools/mods/publish.py --remove <uid>      # removes from the catalog (the Release stays)
python tools/mods/publish.py --feature consolaria # Home's "Featured" (uid or id; several = carousel)
python tools/mods/publish.py --feature           # clears it (back to the built-in mod.json `featured`)
```

The manager page has the same switch (the star on each catalog card).

### Download counts

GitHub counts every download of a Release asset. The `Download stats` Action
in the mods repository ([`mods/download-stats.yml`](mods/download-stats.yml),
copied to `.github/` by `publish.py`) runs every 6 hours, sums the `.bl`
downloads of all Releases of each mod ([`mods/download_stats.py`](mods/download_stats.py))
and commits `stats.json` when a number changed. The app reads it to rank the
Home's "Popular" list and shows the count on cards and on the mod page; without
the file, nothing is shown. Each update counts as a download, as on GitHub.
Run it by hand with `python tools/mods/download_stats.py build/mods-repo`, or
from the repository's Actions tab ("Run workflow").

Updating a mod means publishing the `.bl` with a higher `version` in
`manifest.json`: the app compares it with the installed one and shows
"Update". Republishing the same version with different content is refused.
`raw.githubusercontent.com` caches for a few minutes, so the change may take a
moment to show up in the app.

### Discord catalog updates

The PC manager and `publish.py` can announce catalog changes through a Discord
webhook. Set `BL_DISCORD_WEBHOOK_URL`, or put the webhook URL in
`bunnyloaderdiscordwebhook.txt` at the project root. That file is ignored by
Git. The URL is read only by the local Python process; it is not returned to
the browser, stored in packages or embedded in the Android apps.

After a successful GitHub push, each affected package gets one English embed:
**New package published**, **Package updated**, **Catalog listing updated** or
**Package removed**. Embeds contain the package's name, current and previous
versions where relevant, authors, package type, size, icon and release/download
links. Generated text is English; package and author names retain their names.
Emojis are stripped and mentions are disabled. Package descriptions and release
notes remain available through the links rather than being copied in their
original language.

Dry runs, local version bumps, uploads for validation, synchronization without
publication and unchanged republishes do not create announcements. A failed
GitHub push does not announce success; retrying its pending commit announces
the changes after confirmation. Reindexing announces changed listings only.

Discord failures do not turn a completed publication into an error. Pending
embeds and delivery history are saved locally in `build/discord-updates.json`.
They are retried on the next publication, manager synchronization/startup, or
with **Reenviar pendentes**. Discord's rate-limit cooldown is respected, and
each attempt sends at most ten queued embeds. Delivery requires Discord's
message confirmation (`wait=true`). As with other webhook clients, an ambiguous
network failure after Discord accepted a message can result in a duplicate on
retry.

The manager shows connection status and pending deliveries under
**Atualizações no Discord**. **Enviar teste** sends a clearly labeled connection
test without changing the catalog. Restart an already running manager and
refresh its browser tab after installing this feature. The Android catalog
publisher is independent and does not use this local webhook configuration.

The payload format and limits follow the official [Discord webhook
API](https://docs.discord.com/developers/resources/webhook#execute-webhook)
and [embed limits](https://docs.discord.com/developers/resources/message#embed-limits).

### Android catalog app

`catalog/` builds **BL Catálogo**, the same manager page on the phone (the
Kotlin [`Bridge`](../catalog/src/main/kotlin/dev/bunnyloader/catalog/Bridge.kt)
answers in place of `manager.py`). The APK carries no token. Publishing signs in
through a GitHub App with the device flow: the app shows a short code, you
approve it at `github.com/login/device`, and GitHub returns a user token limited
to what the App may do where it is installed.

- **Access:** the GitHub App has only *Contents: Read and write* (plus the
  mandatory *Metadata*) and is installed only on `Bunny-Loader-Mods`. The app
  checks that installation right after signing in.
- **Lifetimes:** the access token lasts 8 hours and lives only in memory. The
  refresh token (6 months, replaced on every use) is encrypted with an Android
  Keystore key that works only for 30 seconds after the fingerprint or the
  device PIN, in `noBackupFilesDir`. A new fingerprint or lock screen deletes
  it, so you sign in again.
- **Confirmation:** each publication and removal asks for the fingerprint or
  PIN, with the mod's name in the dialog. Opening the app asks once when the
  8-hour token has expired; cancelling shows the public catalog.
- **Signing out:** tap your account in the header. To cut access everywhere,
  revoke the App in GitHub (Settings → Applications → Authorized GitHub Apps).
- The phone needs a PIN, password or pattern (Android 11 or newer).

Creating the GitHub App (once): **Settings → Developer settings → GitHub Apps →
New GitHub App**, any homepage URL, no callback URL, *Expire user authorization
tokens* on, *Enable Device Flow* on, webhook off, the permissions above, *Only
on this account*. Then **Install App → Only select repositories →
Bunny-Loader-Mods**. No client secret or private key is needed. Put the
**Client ID** (public, starts with `Iv`) in `local.properties` and build:

```
bl.catalogClientId=Iv23li...
```

```bash
gradle :catalog:testDebugUnitTest :catalog:assembleDebug
```

## Tests

The tests are **C# mods**: each folder in [`tests/`](tests) installs into the
game, does what it tests for real (uses the item, summons the boss, saves and
reloads the world) and writes one log line per check, ending in
`<test> END pass=N fail=M`.

[`tests/run-cs.sh`](tests/run-cs.sh) builds nothing: it installs the test (with
`csenv` and `csauto`), opens the game, enters the test world, waits for `END`
and prints the result lines. [`tests/run-mp.sh`](tests/run-mp.sh) does the same
on two emulators, host and client. [`tests/build-cs.sh`](tests/build-cs.sh)
compiles the test packages.

```bash
bash tools/tests/build-cs.sh csconfig
bash tools/tests/run-cs.sh csconfig
CLIENT=127.0.0.1:16417 bash tools/tests/run-mp.sh csconfigmp
```

| Test | What it checks | Needs the Example Mod |
|---|---|---|
| **The bridge** | | |
| `tilesettled` | Mod tile readiness, including zero registered tiles: keeps quick start from waiting forever without the Example Mod. A standalone binary (`tilesettled/run.sh`), without opening the game or touching saves. | no |
| **Content** | | |
| `loadordersort` | Sorting by `dependencies` (`mods/LoadOrder.cpp`) and reading the field from the manifest (`mods/ManifestText.cpp`): a dependency before whoever asks for it, the launcher's order for the rest, missing/cycle/ambiguous id disables the mod, an already sorted list does not change (the sync's). Runs on the PC: `wsl bash tools/tests/loadordersort/run.sh`. | no |
| `soundasync` | A bounded SFX queue off the game thread: states/failures, cancellation, concurrent pause, retention and an Android A/B with normal and extreme load and bursts. Shares soundfilter's runner with `--suite soundasync`; see the [README](tests/soundasync/README.md). | save copies only |
| `launcherperf` | Launcher loading: cache/concurrency, GIFs and per-uid invalidation, with an A/B comparison of Explore, Settings and the page on Android. See the [README](tests/launcherperf/README.md) and the [results](tests/launcherperf/RESULTADOS.md). | restores preferences/cache; does not start the game |
| **Multiplayer** | | |

[`mp-session.sh`](mp-session.sh) starts a multiplayer session between two
MuMu instances: one hosts, the other joins, with no new enemies and during the
day.

## Measurement and investigation

| Folder | What it is |
|---|---|
| [`bisect/`](bisect) | `buildat.sh <commit>` builds an old commit's APK in a worktree outside the repository (third-party sources, assets and the game's `.so` files linked through junctions and undone before deleting) and leaves it at `out/bisect/app-<commit>.apk`: the binary search for a regression. One build at a time. Careful: MuMu only has a 60 Hz screen, and another instance running on the PC distorts the measurement. |
| [`crash-trials/`](crash-trials) | Creates worlds in a row on MuMu, restarting the app on each attempt, and counts crashes ([history](../docs/historico/AVALIACAO-PONTE-E-CRASH.md)). |
| [`disasm/`](disasm) | Annotated disassembly of `libil2cpp.so` (`prep.sh` generates the caches from `refs/`). Compiled limits: `scan_limits.py` (`cmp #N`), `scan_loops.py` (end of a loop in bytes), `scan_movs.py` (`new T[N]`) and `scan_halved.py` (the drop's `0 < x < N` done by halves). See [content under the hood](../docs/nucleo/conteudo.md#the-compiled-limits). |
