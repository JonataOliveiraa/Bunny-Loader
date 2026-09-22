# Dependências nativas de terceiros

## ShadowHook (hooking)

Vem pelo Gradle via **prefab**, não precisa de nada aqui:

```kotlin
// app/build.gradle.kts
buildFeatures { prefab = true }
dependencies { implementation("com.bytedance.android:shadowhook:1.0.10") }
```

O CMake consome com `find_package(shadowhook REQUIRED CONFIG)`.

## QuickJS (motor de mod)

**Não versionado** — clone localmente para `third_party/quickjs/`. Usamos o fork
**quickjs-ng** (mantido, compila no NDK):

```bash
git clone --depth 1 --branch v0.17.0   https://github.com/quickjs-ng/quickjs.git   app/src/main/cpp/third_party/quickjs
```

Nosso `CMakeLists.txt` compila os fontes do quickjs-ng v0.17:
`quickjs.c`, `libregexp.c`, `libunicode.c`, `dtoa.c` (com `_GNU_SOURCE`).
Não há mais `cutils.c`/`libbf` como no QuickJS clássico.

Sem a pasta, o build ainda funciona: o `ScriptEngine` compila em modo stub
(`BL_HAVE_QUICKJS` desligado) e o jogo sobe sem mods.
