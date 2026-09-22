# Bunny Loader

Launcher de mods para **Terraria Mobile**. Sobe o jogo já instalado dentro do
próprio processo, intercepta métodos nativos gerados pelo IL2CPP e executa mods.

- **v1:** mods escritos em **JavaScript** (motor **QuickJS**, sem JIT), no espírito
  do TL Pro. API `NativeClass` / `hook()`.
- **futuro:** motor próprio de bytecode (LRVM) para mods em C#.

> O Bunny Loader **não** inclui nem redistribui arquivos do Terraria. Ele usa a
> cópia instalada e comprada pelo usuário. Os dumps em `refs/` são gerados
> localmente e nunca publicados.

## Stack

| Camada | Tecnologia |
|---|---|
| App / UI | Kotlin + Jetpack Compose |
| Núcleo nativo | C++20 (NDK + CMake) → `libbunny.so` |
| Hooking | ShadowHook (ARM64 + ARM32, hook pendente por símbolo) |
| IL2CPP | `dlsym` das funções `il2cpp_*` |
| Motor de mod | QuickJS |
| Dump do jogo | Il2CppDumper / Cpp2IL |

## Estrutura

```
app/src/main/
  kotlin/dev/bunnyloader/   App Android (launcher + host da Unity)
  cpp/                      Núcleo nativo (libbunny.so)
  res/                      Recursos Android
refs/                       Dump local do jogo (NÃO versionado)
samples/HelloMod/           Mod de exemplo (JS)
docs/ARCHITECTURE.md        Arquitetura detalhada
tools/                      Ferramentas de PC (packer, etc. — futuro)
```

## Fases (ver docs/ARCHITECTURE.md §Roadmap)

0. Ambiente + dump do jogo
1. Launcher que sobe o Terraria (maior de-risk)
2. Núcleo que se anexa (`il2cpp_init` + `Il2CppApi.load()`)
3. Primeiro hook (`Projectile.AI`)
4. Motor QuickJS + primeiro mod real
5. Loader completo (importar `.bmod`, ativar, log)

## Build (resumo)

1. Abrir a pasta no Android Studio (deixe o Gradle Sync gerar o wrapper).
2. Instalar **NDK** e **CMake** (SDK Manager).
3. Adicionar QuickJS em `app/src/main/cpp/third_party/quickjs/` (ver README de lá).
4. Gerar o dump em `refs/` (ver `refs/README.md`).
5. `Run` no dispositivo/emulador.

> **Emulador:** MuMuPlayer serve para iterar a UI/launcher (Fase 1). A injeção
> nativa e os hooks (Fase 2+) devem ser validados em **dispositivo ARM real** ou
> emulador ARM — tradução ARM→x86 torna inline hooks instáveis.
