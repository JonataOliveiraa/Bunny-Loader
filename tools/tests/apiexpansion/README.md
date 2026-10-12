# Drawing, CloneDefaults and banners regression

Validated on 2026-10-08 against tModLoader 1.4.5, commit
`39e7995fa519a94468ebaf4699508d25903e143e`, and Terraria Mobile
1.4.5.8/301720 ARM64 on MuMu. The analysis of the contracts and the
alternatives is in [compatibilidade-tmodloader-desenho-banners.md](../../../docs/nucleo/compatibilidade-tmodloader-desenho-banners.md).

## Results

| Check | Result |
|---|---|
| `check.mjs` | 27 behavioral cases; production JS, simulated native calls and signatures checked against the dump |
| `verify_native.py` | Four instruction blocks of the Pre/Post points checked in libil2cpp |
| In-game fixture, with ExampleMod loaded first | **26 checks, zero failures** |
| Later in-game round, with banner sway | **27 checks, zero failures**; 120 calls and 360 sprites, varying rotation |
| `npcaitype/check.mjs` regression | 39 cases |
| `globalprojectilehooks/check.mjs` regression | 59 cases and 22 native signatures |
| `moditemdata/check.mjs` regression | 67 cases, including drag/equipment and instance persistence |
| Suites migrated to CombatLoader | ModItem: 138, ModNPC: 46, ModPlayer: 55; zero failures |
| `pendingnetwork/check.mjs` | 8 cases: received PvP, relay, veto, exception, invalid origin and combat order |
| [PvP and banner across two processes](../pendingnetwork/README.md) | Host: 12, client: 12, reconnect: 2; zero failures |
| `:app:assembleDebug` | BUILD SUCCESSFUL |

Total of the JS suites above: **439 cases**. Those suites' native calls are
simulated. The in-game fixtures are counted separately.

The native fixture confirmed the order, arguments and colors in the
inventory, the sprite veto, Post after a veto, the returned scale, recursion
in the callback, rotation/scale refs reaching the world drawing, vanilla
defaults and those from another mod, Classic/Expert/Master difficulties,
preserving the instance, globals and the players' participation, shared
banner mapping, the 50th kill, the mobile claim list, serializing and
delivering the item through the native request with loopback, activating the
bonus through the tile and reloading the save.

Evidence of the order change: without ExampleMod, the representative was
**697**, with item **6197**. Loading the full ExampleMod first, it became
**700**, with item **6280**. The sidecar recovered 50 kills, one claimable
banner and the new-item notice. The `ExampleSlimeNPC`'s item and tile were
also checked. No load error was logged; the warning about disabled emotes is
an earlier ExampleMod limitation.

The vanilla save serialized **293** entries. The fixture's network packet with
ExampleMod serialized **702** entries. The payload's integer was read, instead
of using the initial `NetPacket.Length = 65535` capacity as proof.

## Running

```powershell
node tools/tests/apiexpansion/check.mjs
python tools/tests/apiexpansion/verify_native.py
```

For the native run, install the full APK with the updated code. `device.py`
uses MuMu `127.0.0.1:16384` and the local saves `sda.plr` and
`Pesadelo_Odioso.wld` as sources for `BL_ApiDiag` copies. Those names must
exist in that environment; the script does not generate a world from scratch.

```powershell
python tools/tests/apiexpansion/device.py prepare
python tools/tests/apiexpansion/device.py run
python tools/tests/apiexpansion/device.py collect
# Restarting tests reloading the sidecar saved by the first run.
python tools/tests/apiexpansion/device.py run
python tools/tests/apiexpansion/device.py collect
# Includes an isolated copy of ExampleMod and changes the fixture's IDs.
python tools/tests/apiexpansion/device.py example
python tools/tests/apiexpansion/device.py run
python tools/tests/apiexpansion/device.py collect
python tools/tests/apiexpansion/device.py restore
```

Wait for the `apiexpansion WIND FIM` line before collecting or restarting; it
comes after the banner's 120 measurement frames. `restore` compares the
original settings, mods and powers preferences byte by byte, removes the
save/package copies and relaunches the original session. The backup and the
logs live in `build/apiexpansion/` (ignored by Git). The originally installed
ExampleMod package is kept.

APK checked: `app/build/outputs/apk/debug/app-debug.apk`, 204975273 bytes.
SHA-256: `247e0085978390efcb42f3d51ce397f09b18c336294737bfe5bec023505fcf48`.

ExampleMod was packaged as **1.9.0** to identify the update with the new
banner. After that manifest change, the APK was rebuilt and the `.bl`
package's version and the presence of the new files were checked.

## Limits

- The sway test covers a 1x3 banner with a solid anchor, active wind and the
  matching option enabled. It does not cover every third-party
  texture/alternate nor is it a performance benchmark of many banners.
- Item PvP and banners passed across two processes on MuMu; the scenarios
  and limits are in the specific network report.
- There was no FPS measurement on the physical device from the EternalLaser
  report. The earlier MuMu data and the ExMod comparison are in
  `out/diagnostics/eternal-laser/analise.md`; the probe is available to
  capture the affected scenario without changing AIType or extraUpdates.

Banner textures: tModLoader's ExampleMod, at the cited commit, kept unedited.
The slime texture and the test item's come from this repository's existing
examples and fixtures.
