# Game runtime (arm64-v8a)

These four `.so` files **are not in the repository** and are not added by me:

```
libil2cpp.so
libunity.so
libmain.so
libc++_shared.so
```

Putting them here means redistributing the game's binaries. It is a step for
the project owner, covered by the license they declare to have — not a build
step.

Gradle packages whatever is in this folder into the AAB's ABI split, and at
runtime they show up in `ApplicationInfo.nativeLibraryDir`, which is where
`BundledRuntime` loads them from through `System.loadLibrary`.

Without them, the app builds and installs normally; `BundledRuntime.isPresent()`
returns false and `GameActivity` refuses to boot with an explicit message,
instead of blowing up inside Unity.

## Content requirements

- It has to be a build whose `libunity.so` does **not** have
  `libpairipcore.so` in `DT_NEEDED`. The check runs at boot
  (`BundledRuntime.pairipCheck`), because the symptom of getting this wrong is
  an anti-tamper SIGSEGV in the middle of Unity's boot.
- `libil2cpp.so` and the asset pack's `global-metadata.dat` have to come from
  the SAME build. They are a pair; mixing versions does not give a clear
  error, it gives corruption.

## What else is missing, besides these libs

- **Asset pack** with `assets/bin/Data/` (`data.unity3d`, `resources.resource`,
  `global-metadata.dat`, `boot.config`, `globalgamemanagers`, ...).
- **Java classes the IL2CPP code calls through JNI.** They have to go along:
  without them the game breaks on a class lookup:
  - `com.and.games505.TerrariaPaid.BuildConfig`
  - `uk.co.drstudios.lvl.BuildConfig`
  - `uk.co.drstudios.lvl.LicensingCheck`
  - `com.google.android.vending.licensing.*` (Google's LVL)
- **Checking that Terraria is installed.** Today `Eligibility` only detects the
  package and warns if it is missing; it does not check origin or signature
  and does not block.
