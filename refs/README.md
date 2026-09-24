# refs/ — dump local do jogo

**Nada aqui é versionado** (só este README). Cada desenvolvedor gera o próprio
dump a partir da sua cópia comprada do Terraria.

## Jeito rápido

```bash
tools/dump.sh
```

Faz tudo: acha o device no adb, puxa o `base.apk`, extrai os alvos, valida o
magic do metadata e roda o Il2CppDumper. Use `--skip-pull` para reaproveitar um
`base.apk` já baixado.

Variáveis: `ADB_SERIAL`, `IL2CPPDUMPER`, `PKG`, `ABI`.

## Jeito manual

1. Localizar e puxar o APK:
   ```bash
   adb shell pm path com.and.games505.TerrariaPaid
   adb pull <caminho>/base.apk refs/base.apk
   ```
2. Extrair (o APK é um zip):
   ```bash
   cd refs
   unzip -o -j base.apk \
     "lib/arm64-v8a/libil2cpp.so" \
     "lib/arm64-v8a/libunity.so" \
     "assets/bin/Data/Managed/Metadata/global-metadata.dat" -d .
   ```
3. Rodar o Il2CppDumper:
   ```bash
   Il2CppDumper.exe libil2cpp.so global-metadata.dat . < /dev/null
   ```
   > O `< /dev/null` evita travar no "Press any key to exit". A exceção
   > `Cannot read keys...` no final é **inofensiva** — acontece depois de tudo
   > ter sido gerado.

## Conteúdo esperado

```
refs/
  base.apk                     ~204 MB  (serve também para o jadx na Fase 1)
  libil2cpp.so                  ~54 MB
  libunity.so                   ~13 MB
  global-metadata.dat          ~8,8 MB
  dump.cs                       ~16 MB  <- referência de leitura
  il2cpp.h                      ~31 MB
  script.json                   ~41 MB
  stringliteral.json           ~1,4 MB
  DummyDll/Assembly-CSharp.dll ~7,5 MB  <- só assinaturas, sem corpo
```

## Build validado

| Item | Valor |
|---|---|
| Pacote | `com.and.games505.TerrariaPaid` |
| versionName | **1.4.5.6.4** |
| versionCode | **301543** |
| minSdk / targetSdk | 23 / 35 |
| ABIs no APK | `arm64-v8a`, `armeabi-v7a` |
| `primaryCpuAbi` instalado | `arm64-v8a` |
| Metadata | magic `AF1BB1FA`, **versão 31** (Unity 2022.3.x) |
| Il2CppDumper | v6.7.46 (win, self-contained) |

> O dumper avisa `WARNING: find JNI_OnLoad` / `ERROR: This file may be
> protected` e depois `Change il2cpp version to: 29`. É normal neste build:
> ele acha `CodeRegistration`/`MetadataRegistration` e o dump sai completo.

## Consultar o dump

```bash
tools/dumpgrep.sh Entity Terraria           # bloco da classe
tools/dumpgrep.sh Projectile Terraria | sed -n '/\/\/ Fields/,/\/\/ Properties/p'
tools/dumpgrep.sh ItemID Terraria.ID | grep -i minishark
```

## ⚠️ Assinaturas mudam entre versões

Documentação antiga da comunidade descreve o Terraria **1.4.0.5.2.1**. Neste build (1.4.5.6.4)
várias assinaturas são diferentes. Exemplo já encontrado:

| 1.4.0.5.2.1 | Este build (1.4.5.6.4) |
|---|---|
| `Item.SetDefaults(int Type, bool noMatCheck)` | `Item.SetDefaults(int Type, ItemVariant variant)` |

**Sempre confira no `dump.cs` antes de escrever um hook.**
