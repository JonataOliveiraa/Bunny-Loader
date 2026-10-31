# NPC and item loot

Build the runtime and APK, install the APK on the test device, then build
the pack with `bash tools/tests/build-cs.sh csnpcloot`.

```bash
DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh csnpcloot
python tools/tests/csnpcloot/check-native.py
```

The pack checks the native NPC database, actual CommonDrop stacks, custom
conditions and rules, all three chain outcomes, LeadingConditionRule,
global additions and vanilla rule removal, live rule edits, PreKill vetoes,
OnKill order, a lethal native strike, boss potion and heart overrides,
native rate reporting, mode/options factories and mod/vanilla bag additions.
It also checks initialized per-entity globals, the one-parameter BossLoot
bridge, custom nested rules, rule/condition subclasses, custom chain
reporting, public CommonDrop inheritance, direct item attempts and ItemLoot
table mutations. Managed GC is forced before resolving the registered rules.

The NPCs are marked as town NPCs with AI style and contact damage zero so
csenv preserves them and they do not damage each other. The pack uses local
PNG fixtures. Drops are checked synchronously before pickup or updates can
change the world item list. Each run must end with `END pass=N fail=0`.

`check-native.py` verifies the six ARM64 instruction guards against the
mobile library and the appended C++/C# NativeApi table order. It requires
the local `refs/libil2cpp.so` dump.

Regressions: `csnpc`, `csnpcai`, `csnpchit`, `csnpcproj`, `csitems` and
`NOENV=1 DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh csnpccontact`.
Rebuild `csauto` when updating the state-guided runner. Dismiss the initial
touch-control setup prompt when the restored player save displays it.

Vanilla item contents remain in the game's hardcoded container handlers;
ItemLoot removal covers rules registered in the managed item table.
Bestiary refresh and two-device network validation are deferred to their
respective steps.
