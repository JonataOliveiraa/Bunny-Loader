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
docs/UNITY-HOSTING.md       Como hospedar a UnityPlayer (análise do dex)
tools/                      Ferramentas de PC (packer, etc. — futuro)
```

## Fases (ver docs/ARCHITECTURE.md §Roadmap)

0. Ambiente + dump do jogo
1. Launcher que sobe o Terraria (maior de-risk)
2. Núcleo que se anexa (`il2cpp_init` + `Il2CppApi.load()`)
3. Primeiro hook (`Projectile.AI`)
4. Motor QuickJS + primeiro mod real
5. Loader completo (importar `.bmod`, ativar, log)

## Build

Abra a pasta no Android Studio e deixe o **Gradle Sync** rodar — ele gera o
wrapper, baixa o Gradle e oferece instalar a plataforma SDK que faltar.

### Só a Fase 1 (launcher, sem núcleo nativo)

Não precisa de NDK nem CMake. Ponha em `gradle.properties`:

```properties
bl.nativeBuild=false
```

e rode. O launcher sobe o Terraria sem carregar a `libbunny.so`.

### Fase 2 em diante (com o núcleo nativo)

1. SDK Manager → aba **SDK Tools** → instalar **NDK (Side by side)** e **CMake**.
2. Remover (ou pôr `true` em) `bl.nativeBuild`.
3. QuickJS só na Fase 4 — ver `app/src/main/cpp/third_party/README.md`.
   Sem ele o build passa e o motor de script fica em modo stub.

### Dump do jogo

```bash
tools/dump.sh
```

Ver `refs/README.md`.

> **Emulador:** MuMuPlayer serve para iterar a UI/launcher (Fase 1). A injeção
> nativa e os hooks (Fase 2+) devem ser validados em **dispositivo ARM real** ou
> emulador ARM — tradução ARM→x86 torna inline hooks instáveis.
