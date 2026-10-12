# Versions in mod sync

## Analysis

Requirement: with sync on, a host with a mod at 1.1.0 and a client with the
same uid at 1.0.0 cannot join together before syncing.

Facts verified in the code and reproduced by the tests before the fix:

- `ServerSyncLoader` already compared uid, order and version; adding just a
  version comparison would not fix the root of the problem.
- The launcher accepted `manifest.json` and `mod.json`, but the core only read
  the first. A mod with `mod.json` reached the handshake with an empty
  version.
- Two empty versions were accepted as equal.
- The Kotlin planning compared versions, but admitting the downloaded ZIP and
  the final write of the session did not check the version the host required.

Assumption: the manifest version is the compatibility identifier published by
the author. The protocol does not authenticate content by hash.

Edge cases: a missing or blank version, a newer client, a prerelease, the
store offline, a required version outside the store, a diverging index and
ZIP, an invalid/duplicate uid, an incompatible type/format, a mod changed
between planning and session, a different order, an empty list and sync
turned off by the host.

## Design

| Alternative | Time, memory and maintenance |
|---|---|
| Compare the version only in JS | O(n), O(1) extra in the comparison; it already existed and does not fix missing metadata nor a diverging ZIP |
| Align the metadata and validate each boundary | Chosen: O(n) comparison, O(n + m + k) planning with O(n + m + k) maps, keeping the existing protocol and formats |
| Replace the protocol with hashes of every file | Requires I/O proportional to the content, caching/invalidation and protocol compatibility; a larger scope than the version fix |

`n` is the host's list, `m` the installed ones and `k` the store's. The core
uses the same manifest names/priority as the launcher. The handshake refuses
a blank version. `ServerModSync` concentrates exact equality, planning,
admission and the session's final check. The importer's callback runs before
replacing the installation. The checks happen on joining/syncing, without
adding a per-frame update hook in QuickJS.

## Tests without the game

```powershell
node tools/tests/serversync/check.mjs
gradle :app:testDebugUnitTest :app:assembleDebug --console=plain
```

```bash
# Linux or WSL, at the repository root; requires g++ with C++20.
python3 tools/tests/serversync/native.py
```

- `check.mjs`: loads the production ServerSyncLoader in a VM with a simulated
  transport. It exercises the UUID introduction, the host's read, the
  decision, the offer and restoring the original UUID; it does not copy the
  private comparison function.
- `native.py` / `native.cpp`: build the production ModLoader.cpp with minimal
  engine/texture/log stand-ins, ASan and UBSan. They use real files to test
  both names, priority, a nested field, a directory instead of a manifest and
  a missing version.
- `ServerModSyncTest`: tests the production Kotlin policy for updating,
  returning to the host's version, failures/offline, an incompatible package
  and the session. Admission is tested as a policy; this suite does not run
  the HTTP transfer and installation of a ZIP on Android.

## Isolated Android test

`device.py` requires Python, ADB, an installed debug APK and emulators with
`su 0`. The game has to be closed before `prepare`. Preparation keeps the
original saves in a sibling folder, creates copies of a character/world, saves
the preferences/store cache and enables only this folder's fixture. Do not
replace `build/serversync/<serial>/prepared.json` or its backups while a
preparation is in use. Run `restore` after an interrupted round too: it checks
the saves' hashes and the bytes of the original preference/cache files.

Example with two instances already connected to ADB:

```powershell
python tools/tests/serversync/device.py prepare --device 127.0.0.1:16384 --version 1.1.0
python tools/tests/serversync/device.py prepare --device 127.0.0.1:16416 --version 1.0.0 --legacy
python tools/tests/serversync/device.py launch --device 127.0.0.1:16384
python tools/tests/serversync/device.py launch --device 127.0.0.1:16416
```

On the host, host the copied world with sync on. On the client, connect to the
host using the copied character. Set up network forwarding according to the
emulator: the two instances may be behind separate NATs. Collect
logs/screenshots with `collect --device SERIAL --label different`. Expect the
screen listing version 1.1.0 as missing, with the client not entering the
world.

```powershell
python tools/tests/serversync/device.py version --device 127.0.0.1:16416 --version 1.1.0 --legacy
python tools/tests/serversync/device.py launch --device 127.0.0.1:16416
```

Connect again, keeping the host open. Both should enter the same session;
collect with `--label equal`. The `serversync-probe WORLD` and `CONNECT` logs
let you check the version and the side. The fixture registers no game content.

```powershell
python tools/tests/serversync/device.py restore --device 127.0.0.1:16416
python tools/tests/serversync/device.py restore --device 127.0.0.1:16384
```

This run's results: [RESULTADOS.md](RESULTADOS.md).
