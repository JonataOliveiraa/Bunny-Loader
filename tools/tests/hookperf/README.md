# ModPlayer, ModItem and drawing performance

The fixture measures the QuickJS runtime in the real APK. The [results](RESULTADOS.md) separate the bridge cost, queries through the bridge and `DoUpdate`/`DoDraw` times; none of these numbers is presented as FPS.

The [optimizations and their comparison between APKs](RESULTADOS-OTIMIZACOES.md) add block copies, stable lookups through direct loops and local hair reads, with cache regressions in the renderer.

## Scenarios

Run with 0, 1 and 8 **ModPlayer classes on the same single player player**. `--classes` does not represent connected players. Each class adds 15 simple update callbacks, a vetoing `PreItemCheck` and a neutral `CanStartExtraJump`. The fixture's three ModItem classes are present in every run so their filters can be compared.

Each phase warms up for 120 updates and measures 300 updates and the draws that happened in that interval. The queries use seven samples after 100 warm-up calls. Damage uses 1,000 calls per sample; frames 500; ammo 250; misc update and ItemCheck 4,000. The filtering counters include the warm-up, so the active cases have 7,100 damage callbacks and 3,600 frame ones.

With eight classes, the fixture registers one more drawing ModPlayer after the first phase and measures: empty order, helper, hiding HeldItem, reordering Skin after Head and empty order again. The repetition reveals variation in rendering time. The marker is created once and reused; the helper's measurement excludes creating the texture, vectors and DrawData. There is no direct call to `DrawData.Draw`.

`draw_manual` and `draw_helper` write into the same slot and restore the counter on each query. Both include that restoration. The ammo test in slot 53 measures that position of the native array and does not claim to be the last candidate in the search order nor a reproduction of placing it through the UI.

## Running

Use only one Android instance, the same APK and the same test character/world. The suite does not create a character or a world. Install the full APK and configure quick start for the test world, or enter it manually from the launcher.

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture hookperf --run-id my-round --isolate --classes 0
adb -s 127.0.0.1:16384 logcat -c
adb -s 127.0.0.1:16384 shell am start -n com.bunnyloader/dev.bunnyloader.LauncherActivity
```

Start the game and wait for the `hookperf` line with `kind=FIM`. Save the log of the `com.bunnyloader:game` PID before preparing the next run. Repeat with `--classes 1` and `--classes 8`, keeping the same run-id during that session. The files with the original preferences live in `build/hookperf/<run-id>/` and are not overwritten on retries.

```powershell
python tools/tests/hookperf/check.py log-0.txt log-1.txt log-8.txt
python tools/tests/moditemhooks/device.py cleanup --fixture hookperf --run-id my-round
```

To check a single eight-class round, use `python tools/tests/hookperf/check.py log-8.txt --classes 8`. That allows comparing two APKs without mixing records from different builds.

`--isolate` temporarily disables the other packages. The cleanup restores only the changed keys, keeps the other current preferences and removes this suite's UID and temporary folder after checking the paths. Pick another run-id for a new session after the cleanup. Use the cleanup after an interrupted run too.

## Measurement limits

- The comparison of 0, 1 and 8 classes includes installing hooks, dispatch and callbacks. ItemCheck's native body runs with zero classes and is vetoed in the other cases; its medians are not a comparison of the same native body.
- `bl.hookStats().jsMs` excludes `original()` and waiting for the lock before entering JavaScript. It does not measure the native path of rejected filters. Explicit nested calls can affect the sum of costs; the individual hooks are shown separately.
- `DoUpdate` and `DoDraw` measure the observed bodies of those methods, including work they trigger. They do not cover the whole frame presentation, Unity, Android scheduling or GPU latency. Warm-up and emulator load can change these times.
- The profile does not separately include ModPlayer's four internal stages nor every API option. FrameEffects/armor, network, multiple players, shadows, many projectiles and mods with heavy callbacks need their own measurements.
- The Node benchmark in [modplayerperf](../modplayerperf/README.md) measures V8 and remains only a comparison of dispatch in isolation.
