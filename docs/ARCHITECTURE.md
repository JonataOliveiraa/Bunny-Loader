# Bunny Loader — Arquitetura (v1, motor QuickJS)

## 1. Visão geral

Três blocos:

```
┌───────────── App Kotlin (processo :game) ─────────────┐
│  LauncherActivity  ── "Jogar" ──►  GameActivity        │
│  GameActivity:                                          │
│    1. GameInstall.locate()   acha APK+libs do Terraria │
│    2. NativeBridge.init()    carrega libbunny.so       │
│    3. GameContext            recursos do jogo          │
│    4. DexClassLoader(APK)    código do jogo            │
│    5. UnityHost.create()     cria UnityPlayer          │
│         └─ Unity carrega libil2cpp.so                  │
│               └─ LibWatcher (hook pendente)            │
│                     └─ hkInit espera il2cpp_init       │
│                           ├─ Il2CppApi.load()          │
│                           ├─ GameRefs.resolve()        │
│                           ├─ ScriptEngine.init(QuickJS)│
│                           ├─ ModLoader.loadAll()       │
│                           └─ runtime hooks instalados  │
└────────────────────────────────────────────────────────┘
```

O ponto central é **esperar o `il2cpp_init` terminar**: antes disso a
`libil2cpp.so` está carregada, mas domínio, classes e metadados não existem.

## 2. Contratos

1. **JNI** (`NativeBridge`): Kotlin → C++, chamado UMA vez antes da Unity subir.
2. **API de mod JS**: `NativeClass`, `NativeObject`, `NativeMethod` (com `hook()`),
   `NativeArray`, `bl.*`, `require`.
3. **IL2CPP embedding API**: funções `il2cpp_*` resolvidas via `dlsym`.

## 3. Núcleo nativo (libbunny.so)

```
cpp/
  boot/            Entrada e sequência de boot
    Entry.cpp        construtor da libbunny
    jni_entry.cpp    ponto JNI (NativeBridge.init)
    LibWatcher.*     hook pendente em il2cpp_init (ShadowHook)
    Boot.*, Probe.*  o que roda depois do il2cpp_init: ponte, mods, loaders
  core/            Config global, log (logcat + arquivo)
  il2cpp/          Api (il2cpp_* por dlsym), Resolver (nome → classe/método/campo),
                   Signature, Types
  hook/            HookManager (inline hooks encadeados) e CodePatch (os limites
                   de tipo compilados no código do jogo)
  mods/            ModLoader: acha os pacotes e roda o main.js de cada um
  content/         O loader de conteúdo de mod: tipos novos nas tabelas do jogo
    common/          ModContent (bl.onContentReady), TypeTables (crescer as
                     tabelas por tipo), ContentAssets (textura, nome), GameRefs
    items/  npcs/  tiles/  projectiles/  buffs/
                     um por tipo de conteúdo, com o save dele ao lado
  menu/            Mod Menu dentro do jogo: CheatButton (a ponte com o Java),
                   Cheats, Powers, MapReveal, MenuCatalog, ModMenu, NetRequests
  script/
    bridge/          A ponte JS ↔ IL2CPP: ScriptEngine (QuickJS), Bindings,
                     JsHook, Invoke/Abi, Marshal/Value/Ref, Members, Roots
    api/             O bl.* dos mods: itens, NPCs, projéteis, buffs, tiles,
                     texturas, arquivos; ModClasses.cpp embute o JS
    js/              ModClasses.js (ModItem, ModNPC... e os hooks das classes)
                     e ModHelpers.js (Vector2, Color...)
  third_party/     QuickJS
```

## 4. Motor de mod (QuickJS)

- **Por que QuickJS:** minúsculo, embutível, ES2020, **sem JIT** (roda no iOS).
- Um `JSRuntime` + um `JSContext`, na thread principal da Unity (a mesma que
  chamou `il2cpp_init`).
- `NativeClass('Ns','Class')` → resolve `Il2CppClass*` e expõe campos/métodos.
- `metodo.hook(cb)` → instala inline hook via HookManager; o trampolim nativo
  entra no QuickJS, chama o callback do mod, e `original(...)` chama a função real.
- Conversão de tipos primitivos C# ↔ JS automática; `NativeObject.wrap/unwrap`
  para boxing explícito.

## 5. Regras de robustez

1. Nada é buscado por nome em runtime: tudo resolvido no carregamento.
2. Falha de mod é logada e **não** derruba o jogo; mod que falha demais é
   desativado em runtime.
3. Referências ao jogo guardadas em variáveis JS de vida longa passam por
   `il2cpp_gchandle_new`; temporárias usam o ponteiro direto.
4. Na inicialização, compara `gameVersion` com a faixa suportada; fora dela, o
   jogo abre sem mods e o launcher avisa.

## 6. Roadmap

| Fase | Entrega | Teste de aceite |
|---|---|---|
| 0 | Ambiente + dump | `refs/` com Assembly-CSharp.dll, libil2cpp.so, global-metadata.dat |
| 1 | Launcher sobe o jogo | Terraria abre dentro do Bunny Loader |
| 2 | Núcleo se anexa | logcat: "il2cpp_init concluído" + assemblies listadas |
| 3 | Primeiro hook | log do `type` de cada Projectile.AI, sem crash |
| 4 | QuickJS + 1 mod | mod JS altera a Minishark (firerate/velocidade) |
| 5 | Loader completo | importar `.bmod`, ativar, ver log; falha isolada |
| 6+ | LRVM (C#) | track paralelo — reaproveita toda a base |

## 7. Distribuição / legal

- Não inclui nem redistribui arquivos do Terraria.
- Dumps (`refs/`) gerados localmente, nunca publicados.
- Cada mod declara faixa de versão do jogo no `mod.json`.
