# Third-party native dependencies

## ShadowHook (hooking)

Comes through Gradle via **prefab**, nothing needed here:

```kotlin
// app/build.gradle.kts
buildFeatures { prefab = true }
dependencies { implementation("com.bytedance.android:shadowhook:1.0.10") }
```

CMake consumes it with `find_package(shadowhook REQUIRED CONFIG)`.

## MojoShader (compiled Direct3D effects)

`mojoshader/` holds the files of [MojoShader](https://github.com/icculus/mojoshader)
that the GLSL ES conversion of `.fxc` effects needs, copied unchanged from commit
`ad5dff84830c2863c841f4b1f4e3df78c705b383` (2026-09-03): `mojoshader.c`, `mojoshader.h`,
`mojoshader_internal.h`, `mojoshader_common.c`, `mojoshader_effects.c`, `LICENSE.txt` and,
in `profiles/`, `mojoshader_profile.h`, `mojoshader_profile_common.c` and
`mojoshader_profile_glsl.c`. zlib license (`mojoshader/LICENSE.txt`, keep it).

CMake builds it as the static `mojoshader` library with only the GLSL profiles
(`SUPPORT_PROFILE_*=0` for the others), `MOJOSHADER_EFFECT_SUPPORT` and
`MOJOSHADER_NO_VERSION_INCLUDE`. To update, copy the same files from a newer commit and
record the commit here.
