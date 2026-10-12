# ModItem: combat, use, ammo and healing

The [plan](PLANO.md) covers every requested method. This suite validates the 12 combat methods, the 11 use and ammo methods and the 4 healing methods. Drawing, pickup, stacks, state and reforging remain for the next steps.

## Automated tests

```powershell
node tools/tests/moditemhooks/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/modnpchooks/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
node tools/tests/localization/check.mjs
```

The ModItem test loads the real loaders in an environment that simulates native calls. It checks signatures and parameter names against `refs/dump.cs`, references, modifiers, order between classes, vetoes, inherited and late registrations, marks before entering JavaScript, exceptions and context restoration.

The nested context tests check the restoration of the JavaScript state. The native bridge keeps its reentry protection on the same hook. The suite does not remove that protection and the simulation is not an FPS measurement.

## Native run

The fixture uses temporary items, an NPC and a player in single player. It restores the changed fields and deactivates the NPC in `finally`. The test runs 60 updates after entering the world and writes `moditemhooks FIM checks=... falhas=...` to the log.

With the APK installed and a test Android instance connected, prepare the fixture:

```powershell
python tools/tests/moditemhooks/device.py prepare --device 127.0.0.1:16384 --run-id my-run
```

The script keeps the existing preferences, enables only its own UID and installs the content in a dedicated folder. Use `--world NAME.wld` only to pick an existing test world; without that option, it keeps the world configured in the launcher. Start the game from the launcher and read the log of the `com.bunnyloader:game` process.

After the run:

```powershell
python tools/tests/moditemhooks/device.py cleanup --device 127.0.0.1:16384 --run-id my-run
```

The cleanup restores only the fixture's preferences and confirms the absolute path before removing its folder. The local backups live in `build/moditemhooks/<run-id>`. Use a new identifier for a new session, keeping the same identifier for that session's retries and cleanup.

The frame counters tell the new ModItem hook apart from the existing armor hook. The ammo tests keep the inventory and the player's temporary fields. The contracts and the differences in mobile consumption are in [Use and ammo](../../../docs/referencia/moditem-uso-municao.md).

The healing tests run the real normal application helper, QuickHeal and QuickMana, checking life, mana, consumption, buffs, selection and special delays. Composition with ModPlayer is registered after measuring the filters without global observers. The fields, buffs and inventory are restored in finally. Contracts: [Healing](../../../docs/referencia/moditem-cura.md); results: [RESULTADOS-CURA.md](RESULTADOS-CURA.md).

The original native PvP test uses two Player objects in the same process.
The [2026-10-08 round](../pendingnetwork/README.md) fixed the weapon's origin
and the veto before sending, and validated the damage and the callbacks in
both directions across two processes. This folder's JS suite was migrated
from PlayerCombatHooks to CombatLoader and passes its 138 cases again.

Results and measurement: [RESULTADOS.md](RESULTADOS.md). Contracts: [ModItem combat](../../../docs/referencia/moditem-combate.md).

## Performance measurement

The [hookperf fixture](../hookperf/README.md) measures the current APK with 0, 1 and 8 ModPlayer classes, compares damage/frames of vanilla and mod items and checks the filters before JavaScript. The [results](../hookperf/RESULTADOS.md) include drawing and the limits of the comparisons; they are not an FPS measurement.
