# Hosting Terraria's UnityPlayer

Facts extracted from the `classes.dex` of Terraria **1.4.5.6.4** (Unity **2021.3.56f2**).
Reproduce with:

```bash
unzip -o -j refs/base.apk classes.dex -d /tmp/ph1
"$LOCALAPPDATA/Android/Sdk/build-tools/36.1.0/dexdump.exe" -d /tmp/ph1/classes.dex > /tmp/ph1/disasm.txt
```

## 1. The APK has PairIP — but in light mode

`application:name` is `com.pairip.application.Application`, and the dex
brings `com.pairip.{SignatureCheck, VMRunner, VmDecryptor, StartupLauncher,
InitContextProvider}` plus `com.pairip.licensecheck.*`. Injected obfuscated
packages show up inside *every* namespace (`com/unity3d/player/a/UuH/`,
`bitter/jnibridge/EiJ/`, `org/fmod/PS/`, `com/and/games505/TerrariaPaid/ZtK/`).

**But only 3 methods in the whole dex call `VMRunner.invoke`:**

| Class | Method | Relevance |
|---|---|---|
| `com.pairip.StartupLauncher` | `launch` | PairIP internal |
| `com.pairip.VMRunner$1` | `run` | PairIP internal |
| `com.unity3d.player.HFPStatus$1` | `onReceive` | Bluetooth headset receiver |

No `UnityPlayer` method is VM-protected. Since Bunny Loader never instantiates
the game's `Application` nor `InitContextProvider`,
`StartupLauncher.launch` — and therefore `SignatureCheck` and the license
check — **never runs**. The only casualty is `HFPStatus`, which detects a
Bluetooth headset.

`libil2cpp.so` is **not** encrypted: Il2CppDumper extracted the complete dump
straight from it.

> If a game update hardens PairIP (protects `UnityPlayer` or encrypts the
> `.so`), recheck the `VMRunner.invoke` count before investigating anything
> else.

## 2. Unity requires the Context to BE an Activity

`UnityPlayer.<init>(Context, IUnityPlayerLifecycleEvents)`:

```
0057: instance-of  v7, v6, Landroid/app/Activity;
005c: check-cast   v7, Landroid/app/Activity;
005e: iput-object  v7, ... UnityPlayer.mActivity
0060: sput-object  v7, ... UnityPlayer.currentActivity
0062: invoke-virtual {v7}, Activity.getRequestedOrientation()
```

It is a **plain** `instanceof`, without unwrapping
`ContextWrapper.getBaseContext()`. Passing a `ContextWrapper` leaves
`mActivity` null and Unity blows up right after.

That is why `GameActivity` **is** the Context: it overrides `getResources()`,
`getAssets()`, `getApplicationInfo()` and `getClassLoader()` returning the
game's, and passes `this` to `UnityPlayer`. While `GameEnvironment` is not
ready, the getters fall back to `super`, so `super.onCreate()` works normally.

## 3. UnityPlayer API checked

```
UnityPlayer extends android.widget.FrameLayout

<init>(Context)
<init>(Context, IUnityPlayerLifecycleEvents)

onStart() onResume() onPause() onStop() destroy() quit() unload() lowMemory()
windowFocusChanged(boolean)  configurationChanged(Configuration)
newIntent(Intent)  injectEvent(InputEvent):boolean  getView():View
UnitySendMessage(String,String,String)   [static]
currentActivity : Activity               [static field]
```

The stock `UnityPlayerActivity` uses **`onStart`/`onResume`/`onPause`/`onStop`**
— not the legacy `resume()`/`pause()`, which also exist. We follow the stock
one.

`IUnityPlayerLifecycleEvents` has exactly two methods:
`onUnityPlayerQuitted()` and `onUnityPlayerUnloaded()`.

## 4. What UnityPlayerActivity does (through PairIP's accessors)

The synthetic `$NNN` methods PairIP injects reveal each framework call. In
`onCreate`, in order:

1. `requestWindowFeature(FEATURE_NO_TITLE)`
2. `getIntent()` → `getStringExtra("unity")` → `updateUnityCommandLineArguments()` → `putExtra()`
3. `new UnityPlayer(this, this)`
4. `setContentView(mUnityPlayer)`
5. `mUnityPlayer.requestFocus()`
6. `getWindow().getAttributes()` / `setAttributes()` — display cutout layout
7. `getDisplay().getSupportedModes()` — the surface's frame rate
8. `getPackageManager().hasSystemFeature(...)`
9. `setOnTouchListener` / `setOnGenericMotionListener` / `setPointerIcon`

All input is forwarded through `injectEvent`: `dispatchKeyEvent`,
`onKeyDown`, `onKeyUp`, `onTouchEvent`, `onGenericMotionEvent`.

## 5. Manifest config copied from the game

| Attribute | The game's value |
|---|---|
| `launchMode` | `2` = `singleTask` |
| `screenOrientation` | `11` = `sensorLandscape` |
| `hardwareAccelerated` | `false` |
| `resizeableActivity` | `false` |
| `configChanges` | `0x40003fff` (practically everything) |

Unity reads `unity.splash-mode`, `unity.splash-enable`,
`unity.launch-fullscreen`, `unity.auto-report-fully-drawn`, `notch.config`
and `android.notch_support` from the **host** package via PackageManager —
that is, from *our* manifest. That is why they are replicated there.

---

# IMPORTANT CORRECTION (next session)

Section 1 above was **right about the VM protection and wrong about what
mattered**. Counting calls to `VMRunner.invoke` measures only one of the two
things PairIP does. The other one kills the approach.

## PairIP encrypts the whole app's string constants

They live in injected storage classes that have **no methods**. A real
example, `uk.co.drstudios.lvl.yh.sHQPfQqKk`: 13 `public static String` fields,
zero methods, zero `<clinit>`. Someone from outside fills them in at runtime.

Without that someone, trivial code breaks in unlikely places:

```
UnityPlayer.getNaturalOrientation():
    iget-object   v0, v3, UnityPlayer.mContext
    sget-object   v1, sHQPfQqKk.pPb              <- should be "window", is null
    invoke-virtual Context.getSystemService(v1)  -> null
    invoke-interface WindowManager.getDefaultDisplay()  -> NullPointerException
```

## Calling StartupLauncher directly does NOT solve it

The idea was to trigger only the decryption, without going through
`attachBaseContext` (where `SignatureCheck` lives). Implemented in
`PairipBootstrap`. Result measured on the device:

```
PairIP: antes do bootstrap, sHQPfQqKk.pPb = null
PairIP: VMRunner carregada (libpairipcore ok)
PairIP: VMRunner.setContext() ok
PairIP: StartupLauncher.launch() ok
PairIP: depois do bootstrap, sHQPfQqKk.pPb = null
```

Everything "ok", nothing populated, and in the same millisecond. The reason is
in the dex:

```
com.pairip.VMRunner.invoke:(Ljava/lang/String;[Ljava/lang/Object;)Ljava/lang/Object;
    0000: const/4       v0, #int 0
    0001: return-object v0      <- returns null on the first instruction
    0002: sget-object   v0, VMRunner.context   <- the real body, unreachable
```

The `invoke` stored in the dex is a **stub**. The real body starts at offset
0002 and never runs. `libpairipcore.so` rewrites that method in memory when
initialized through the app's legitimate path. Loading the lib (which happens
in VMRunner's `<clinit>`) is not enough.

## Consequence for the architecture

Hosting `UnityPlayer` inside our process requires making the process pass as
legitimate to `libpairipcore.so`. That is fragile and breaks with every game
update.

The alternative is to invert the design: instead of **hosting** the game,
**launch** the game in its own process (where PairIP initializes normally and
the strings get populated) and inject `libbunny.so` into that process. The
cost is that injecting into someone else's process requires root.

Decision pending.
