# Runtime do jogo (arm64-v8a)

Estas quatro `.so` **não estão no repositório** e não são adicionadas por mim:

```
libil2cpp.so
libunity.so
libmain.so
libc++_shared.so
```

Colocá-las aqui é redistribuir os binários do jogo. É um passo do responsável
pelo projeto, coberto pela licença que ele declara ter — não uma etapa de build.

O Gradle empacota o que estiver nesta pasta no split de ABI do AAB, e em runtime
elas aparecem em `ApplicationInfo.nativeLibraryDir`, que é de onde o
`BundledRuntime` as carrega por `System.loadLibrary`.

Sem elas, o app compila e instala normalmente; o `BundledRuntime.isPresent()`
devolve false e a `GameActivity` recusa subir com uma mensagem explícita, em vez
de estourar dentro da Unity.

## Requisitos do conteúdo

- Precisa ser uma build cuja `libunity.so` **não** tenha `libpairipcore.so` em
  `DT_NEEDED`. A verificação roda no boot (`BundledRuntime.pairipCheck`), porque
  o sintoma de errar isso é um SIGSEGV do anti-tamper no meio do boot da Unity.
- `libil2cpp.so` e o `global-metadata.dat` do asset pack têm de ser do MESMO
  build. Eles são um par; misturar versões não dá erro claro, dá corrupção.

## O que mais falta, além destas libs

- **Asset pack** com `assets/bin/Data/` (`data.unity3d`, `resources.resource`,
  `global-metadata.dat`, `boot.config`, `globalgamemanagers`, ...).
- **Classes Java que o código IL2CPP chama por JNI.** Precisam ir junto:
  sem elas o jogo quebra em lookup de classe:
  - `com.and.games505.TerrariaPaid.BuildConfig`
  - `uk.co.drstudios.lvl.BuildConfig`
  - `uk.co.drstudios.lvl.LicensingCheck`
  - `com.google.android.vending.licensing.*` (a LVL do Google)
- **`Eligibility.OFFICIAL_CERT_SHA256`**, com o digest de um install genuíno da
  Play. Enquanto estiver vazio o gate aceita qualquer assinatura e diz isso.
