# Item PvP and banners across processes

Run on 2026-10-08, Terraria Mobile 1.4.5.8/301720 ARM64, on two separate MuMu
instances. Host: `127.0.0.1:16384`, player 0, `Main.netMode = 3`. Client:
`127.0.0.1:16416`, player 1, mode 1. `emulator-5554` is an alias of the first
device and was not counted as a third process.

## Fix and contracts

Reference: tModLoader 1.4.5, commit
`39e7995fa519a94468ebaf4699508d25903e143e`, files ModItem.cs,
Player.cs.patch, CombinedHooks.cs and PlayerLoader.cs.

CanHitPvp runs on the attacker, after native eligibility and before status,
Hurt and sending. ModifyHitPvp/OnHitPvp run on the victim. Mobile's
PlayerDeathReason carries type/prefix; PvpItemNetwork adds the identity and
the NetSend/NetReceive payload before message 117, on the same TCP stream.
The server relays the complement before the hit. The victim does not depend on
the slot currently selected by the attacker. The queues are bounded and
expire, and the receiving scope is restored in finally and cleared when
switching worlds.

The combat order was also aligned with the reference: ModifyIncomingHit,
attacker, target, general and specific ModPlayer. ModPlayer's general OnHit
comes before the specific one. The JS case composes multiplication and
addition to detect inversions in that order.

## Result

| Step | Checks | Failures |
|---|---:|---:|
| Host, initial session | 12 | 0 |
| Client, initial session | 12 | 0 |
| State received on reconnect, checked on the host | 2 | 0 |
| Total in game | 26 | 0 |
| Production JS, simulated native calls | 8 | 0 |

In both directions, CanHitPvp false prevented the native send and the remote
callbacks. With permission, there was one send and the victim took a single
hit of 7 damage. The weapon's default state is 3; the test changes it to 7
through NetSend and removes the weapon from the slot right after the attack.
ModifyHitPvp and OnHitPvp ran once on the victim and zero times on the
attacker.

The client received 49 kills and zero claims on joining. The 50th kill on the
host created a claim on the client. RequestBannerClaim delivered one unit and
zeroed the amount on both sides. Repeating the request neither duplicated the
item nor produced a negative counter. After closing the client process and
reconnecting, it received 51 kills and nothing pending. The two reconnect
checks are done on the host over the values read by the client; the client
only confirms completion, without being counted again.

Excerpts from the final round:

```text
22:52:05.369 pendingnetwork host FIM checks=12 falhas=0
22:52:06.143 pendingnetwork client FIM checks=12 falhas=0
22:52:30.136 pendingnetwork host REJOIN FIM checks=14 falhas=0
22:52:30.905 pendingnetwork client REJOIN FIM checks=0 falhas=0
```

Logs/screenshots: `build/pendingnetwork/127.0.0.1-16384/verified.*`,
`127.0.0.1-16416/verified.*` and `rejoin.*`. Both emulators' original saves,
preferences and cache were restored and checked by hash. The second instance,
started for the test, was shut down.

APK installed on both sides: `app/build/outputs/apk/debug/app-debug.apk`,
204975273 bytes, SHA-256
`247e0085978390efcb42f3d51ce397f09b18c336294737bfe5bec023505fcf48`.

## Reproducing

Requires ADB, an installed debug APK, Python and emulators with `su 0`. Close
the game before preparing. The runner keeps the original Players/Worlds
folders and creates copies of the first available .plr/.wld. Never replace
`prepared.json` or the backups of a preparation in progress.

```powershell
node tools/tests/pendingnetwork/check.mjs
adb -s 127.0.0.1:16384 shell am force-stop com.bunnyloader
adb -s 127.0.0.1:16416 shell am force-stop com.bunnyloader
python tools/tests/pendingnetwork/device.py prepare --device 127.0.0.1:16384 --role host
python tools/tests/pendingnetwork/device.py prepare --device 127.0.0.1:16416 --role client
python tools/tests/pendingnetwork/device.py launch --device 127.0.0.1:16384 --role host
adb -s 127.0.0.1:16384 forward tcp:7777 tcp:7777
# Wait for pendingnetwork HOST READY in the log; the fixture uses GUIMultiplayerHost.HostServer.
python tools/tests/pendingnetwork/device.py launch --device 127.0.0.1:16416 --role client
# Wait for FIM on both sides and collect before closing the client.
python tools/tests/pendingnetwork/device.py collect --device 127.0.0.1:16384 --label verified
python tools/tests/pendingnetwork/device.py collect --device 127.0.0.1:16416 --label verified
python tools/tests/pendingnetwork/device.py launch --device 127.0.0.1:16416 --role client
# Wait for REJOIN FIM and collect again with --label rejoin.
python tools/tests/pendingnetwork/device.py restore --device 127.0.0.1:16416
python tools/tests/pendingnetwork/device.py restore --device 127.0.0.1:16384
adb -s 127.0.0.1:16384 forward --remove tcp:7777
```

Run restore on both sides even after an interruption. The runner restores and
checks saves, preferences, cache and any previous server session.

## Limits

The scenario uses melee, zero defense and endurance, no crit and no dodge; it
tests one mod weapon with NetSend and one shared banner. It does not certify
every combination of buffs, crits, PvP projectiles, three or more players,
losing the connection during a claim or tampered clients. The relay to a third
player has a simulated JS test; the native test uses two processes. The tests
do not measure FPS.
