# Third-party native dependencies

## ShadowHook (hooking)

Comes through Gradle via **prefab**, nothing needed here:

```kotlin
// app/build.gradle.kts
buildFeatures { prefab = true }
dependencies { implementation("com.bytedance.android:shadowhook:1.0.10") }
```

CMake consumes it with `find_package(shadowhook REQUIRED CONFIG)`.
