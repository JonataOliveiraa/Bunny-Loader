# Fps stuck at 45 on a 90 Hz screen

Round of 2026-09-30, on a Galaxy A15 5G (SM-A156M, Android 16, One UI, 90/60 Hz
screen), Terraria 1.4.5.8.6. The game stayed at **a fixed 45 fps** in Bunny
Loader (30 to 35 with zoom) and at 60 in TL Pro, on the same device. Tools:
[`tools/tests/frametime`](../../tools/tests/frametime),
[`tools/bisect`](../../tools/bisect) and adb over ZeroTier.

## What it was not

- **The mods' JS.** Without ExampleMod it was 45 just the same, and with no
  mods no JS hook runs per frame (`bl.hookStats`).
- **A regression in the code.** Every version, even the ones from 09/27 and the
  1.4.5.6 ones, gave 45 on the device. On MuMu, with no mods, 09/28, 09/29 and
  09/30 stay between 5 and 6 ms of drawing. Two measurements of 8.9 ms were
  another MuMu instance running on the PC sharing the CPU.
- **MuMu.** It only has a 60 Hz screen; the problem does not show up there.

## What it was

Unity rounds `screen rate ÷ Application.targetFrameRate` to know how many
refreshes each frame takes. Terraria asks for 60 (`XNAUnityRunner.Awake`,
`SetAndroidSurfaceRate`, `Main.SetPlatform60FPS`). On a 90 Hz screen, 90/60 =
1.5 becomes **2**: one frame every two refreshes, 45 fps. Above 22.2 ms per
frame (zoom), three: 30.

TL Pro escapes because Samsung counts it as a **game** and, for games, unlocks
the screen's 60 Hz mode: Unity's `SwappyDisplayManager` picks 60 and 60/60 = 1.
For an app outside the list, with motion smoothness on "High", Samsung locks
the screen at 90 (`mDesiredDisplayModeSpecs` 90–90) and the app only sees that
mode (`Display.getSupportedModes()` returns one).

Samsung's game list (`dumpsys gamemanager`, "Package Data in SGM"):

- **ignores the manifest category** ("Game (Manifest excluded)"):
  `android:appCategory="game"` does not put the app there;
- counts the **installer**'s hint (`setApplicationCategoryHint`, which the Play
  Store gives; Android only accepts it from the installer of record), its own
  list and the user (adding it to Game Launcher did not carry over there);
- on 2026-09-28 21:39 GOS applied a new policy to the games on the list
  (`requestToRemoveRefreshRate`, `setPerformanceMode`): the "it was 60 the day
  before yesterday".

## The fix

The user found in the console that `targetFrameRate = 61` holds 60: 90/61 ≈
1.47 becomes **1**, and what caps it at 60 is XNA's fixed step. At 60 Hz
(60/61 → 1) and 120 Hz (120/61 ≈ 1.97 → 2) it gives the same as 60; battery
saver's 30 (90/30 = 3) stays. Implemented in
[`boot/FramePacing.cpp`](../../app/src/main/cpp/boot/FramePacing.cpp): a
native hook on `Application.set_targetFrameRate` (60 becomes 61) and, once on
the game thread, `Main.Setting_Target60FPS` is toggled back and forth so the
game redoes the choice through the hook (the setter returns early if the value
does not change).

## For next time

Fps locked at 45 or 30 on the phone: look at the screen before CPU time.

```bash
adb shell dumpsys display | grep -E 'mDesiredDisplayModeSpecs|mActiveModeId'
```

```bash
adb shell dumpsys gamemanager
```
