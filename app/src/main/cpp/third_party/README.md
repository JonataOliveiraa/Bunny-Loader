# Third-party native dependencies

## ShadowHook (hooking)

Comes through Gradle via **prefab**, nothing needed here:

```kotlin
// app/build.gradle.kts
buildFeatures { prefab = true }
dependencies { implementation("com.bytedance.android:shadowhook:1.0.10") }
```

CMake consumes it with `find_package(shadowhook REQUIRED CONFIG)`.

## QuickJS (mod engine)

**Not versioned** — clone it locally into `third_party/quickjs/`. We use the
**quickjs-ng** fork (maintained, builds with the NDK):

```bash
git clone --depth 1 --branch v0.17.0   https://github.com/quickjs-ng/quickjs.git   app/src/main/cpp/third_party/quickjs
```

Our `CMakeLists.txt` builds the quickjs-ng v0.17 sources: `quickjs.c`,
`libregexp.c`, `libunicode.c`, `dtoa.c` (with `_GNU_SOURCE`). `quickjs.c` comes
in through our `script/bridge/QuickJsExt.c`, which includes it and adds access
to the runtime's frame stack (the engine shared between threads, see
`JsSuspend`); the clone stays untouched. There is no `cutils.c`/`libbf` anymore
as in classic QuickJS.

Without the folder, the build still works: `ScriptEngine` compiles in stub mode
(`BL_HAVE_QUICKJS` off) and the game boots without mods.
