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

**Não versionado** — baixe localmente para `third_party/quickjs/`.

Fonte: <https://bellard.org/quickjs/> ou <https://github.com/quickjs-ng/quickjs>
(o fork `quickjs-ng` costuma ser mais fácil de compilar no NDK).

Arquivos esperados pelo `CMakeLists.txt`:

```
third_party/quickjs/
  quickjs.c   quickjs.h
  libregexp.c libregexp.h
  libunicode.c libunicode.h
  cutils.c    cutils.h
  quickjs-atom.h  quickjs-opcode.h  libunicode-table.h  list.h
```

Enquanto a pasta não existir, o build **continua funcionando**: o `ScriptEngine`
compila em modo stub (`BL_HAVE_QUICKJS` desligado) e o jogo sobe sem mods.
Isso é proposital — as Fases 1-3 não precisam do QuickJS.
