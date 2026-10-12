# PlayerDrawLayer

A suite for custom JS layers in the mobile 301720 renderer.

## Automation

```powershell
node tools/tests/playerdrawlayers/check.mjs
node tools/tests/moditemhooks/check.mjs
node tools/tests/hairsettings/check.mjs
```

`check.mjs` reuses the ModPlayer harness, running its 55 checks before the 28
new ones. The 153 hook signatures and four internal steps are still checked
against `refs/dump.cs`.

The new scenarios check manual and automatic registration, content ready,
late registration, the positions cache, earlier/later children, names across
mods, cycles, exceptions, shaders, preserving the cache, changing the parent,
moving the native group and restoring filters. One scenario starts the loader
with only a custom layer, with no ModPlayer at all. Another involves nested
draws of two players with different shaders.

The `WingsGlowmask` example also runs: per-slot configuration, validating
frames and offsets, the texture frame, direction, transparency, shader and
excluding players that are dead, invisible or have no configured wings. The
test confirms the cache receives the built `DrawData`.

A layer only on the wings must trigger exactly one native layer hook in
JavaScript. Invisible layers do not trigger those hooks. Reordering a native
parent keeps its group and the data before/after the group.

## Android test

The fixture uses autoload for four exported layers and registers a fifth
while running. The normal renderer draws markers with `DrawData`, without a
direct call to `SpriteBatch`. The observer examines the active entries after
`DrawPlayer_RenderAllLayers`.

Steps: visible, native parent hidden, custom parent invisible, parent change,
native reordering, cycle, late registration and drawing with no changes. It
also checks that the Wings/Head filters are active in the visible draw and that
Skin stays out of JavaScript. The final state requires positive counts in the
eight steps, no failures, five initializations and nine position queries.

Prepare only on a test device. The helper saves the original values and
restores the changed preferences on cleanup:

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture playerdrawlayers --run-id layers --isolate
adb -s 127.0.0.1:16384 shell am start -n com.bunnyloader/dev.bunnyloader.LauncherActivity
```

Start the game from the launcher. After 470 updates of the local player, look
for `playerdrawlayers FIM` in the log of the `com.bunnyloader:game` process.
Confirm `passed: true` and the markers on screen. When done:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture playerdrawlayers --run-id layers
```

Audited results and limits: [RESULTADOS.md](RESULTADOS.md).
Public contract: [PlayerDrawLayer](../../../docs/referencia/playerdrawlayers.md).
