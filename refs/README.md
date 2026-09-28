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

> O dump tem de ser dos MESMOS binários que vão integrados na build
> (`app/src/main/jniLibs/arm64-v8a/libil2cpp.so` e o `global-metadata.dat` de
> `terraria1456_assets/`). A build da Play a partir da 1.4.5.8 vem com PairIP e
> não é a que o Bunny Loader roda: nesse caso, copie os dois arquivos
> integrados para cá e rode só o passo 3 do jeito manual.

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
| versionName | **1.4.5.8.6** |
| versionCode | **301720** |
| minSdk | 23 |
| ABIs no APK | `arm64-v8a`, `armeabi-v7a` |
| `primaryCpuAbi` instalado | `arm64-v8a` |
| Metadata | magic `AF1BB1FA`, **versão 31** (Unity 2021.3.56f2) |
| `libunity.so` | sem `libpairipcore.so` no `DT_NEEDED` |
| Il2CppDumper | v6.7.46 (win, self-contained) |

> O dumper avisa `WARNING: find JNI_OnLoad` / `ERROR: This file may be
> protected` e depois `Change il2cpp version to: 29`. É normal neste build:
> ele acha `CodeRegistration`/`MetadataRegistration` e o dump sai completo.

A versão anterior (1.4.5.6.4, versionCode 301543) fica em `refs/1.4.5.6.4/`,
com o mesmo conteúdo, para comparar.

## Consultar o dump

```bash
tools/dumpgrep.sh Entity Terraria           # bloco da classe
tools/dumpgrep.sh Projectile Terraria | sed -n '/\/\/ Fields/,/\/\/ Properties/p'
tools/dumpgrep.sh ItemID Terraria.ID | grep -i minishark
```

## ⚠️ Assinaturas mudam entre versões

Documentação antiga da comunidade descreve o Terraria **1.4.0.5.2.1**, e até
de uma 1.4.5 para a seguinte há mudanças. Exemplos:

| Antes | Este build (1.4.5.8.6) |
|---|---|
| `Item.SetDefaults(int Type, bool noMatCheck)` (1.4.0.5) | `Item.SetDefaults(int Type, ItemVariant variant)` |
| `Main.NewText(string newText, byte R, byte G, byte B)` (1.4.5.6) | `+ bool onlyCurrentPlayer` |
| `NPC.StrikeNPC(...)` devolvia `double` e tinha `bool noEffect` (1.4.5.6) | devolve `int`, sem `noEffect` |
| `Projectile.identity` (1.4.5.6) | `Projectile.key` (`ProjectileKey`: Spawner, Index, Generation) |
| `Player.setBonus` (1.4.5.6) | não existe: o texto do conjunto vem do `ArmorSetBonuses` |

**Sempre confira no `dump.cs` antes de escrever um hook.**
