# Hospedando a UnityPlayer do Terraria

Fatos extraídos do `classes.dex` do Terraria **1.4.5.6.4** (Unity **2021.3.56f2**).
Reproduzir com:

```bash
unzip -o -j refs/base.apk classes.dex -d /tmp/ph1
"$LOCALAPPDATA/Android/Sdk/build-tools/36.1.0/dexdump.exe" -d /tmp/ph1/classes.dex > /tmp/ph1/disasm.txt
```

## 1. O APK tem PairIP — mas em modo leve

O `application:name` é `com.pairip.application.Application`, e o dex traz
`com.pairip.{SignatureCheck, VMRunner, VmDecryptor, StartupLauncher,
InitContextProvider}` mais `com.pairip.licensecheck.*`. Pacotes ofuscados
injetados aparecem dentro de *todos* os namespaces (`com/unity3d/player/a/UuH/`,
`bitter/jnibridge/EiJ/`, `org/fmod/PS/`, `com/and/games505/TerrariaPaid/ZtK/`).

**Mas só 3 métodos no dex inteiro chamam `VMRunner.invoke`:**

| Classe | Método | Relevância |
|---|---|---|
| `com.pairip.StartupLauncher` | `launch` | interno do PairIP |
| `com.pairip.VMRunner$1` | `run` | interno do PairIP |
| `com.unity3d.player.HFPStatus$1` | `onReceive` | receiver de fone bluetooth |

Nenhum método da `UnityPlayer` está protegido por VM. Como o Bunny Loader nunca
instancia a `Application` do jogo nem o `InitContextProvider`, o
`StartupLauncher.launch` — e portanto o `SignatureCheck` e o license check —
**nunca roda**. A única baixa é o `HFPStatus`, que detecta fone bluetooth.

O `libil2cpp.so` **não** está criptografado: o Il2CppDumper extraiu o dump
completo direto dele.

> Se uma atualização do jogo endurecer o PairIP (proteger a `UnityPlayer` ou
> cifrar a `.so`), reconfira a contagem de `VMRunner.invoke` antes de investigar
> qualquer outra coisa.

## 2. A Unity exige que o Context SEJA uma Activity

`UnityPlayer.<init>(Context, IUnityPlayerLifecycleEvents)`:

```
0057: instance-of  v7, v6, Landroid/app/Activity;
005c: check-cast   v7, Landroid/app/Activity;
005e: iput-object  v7, ... UnityPlayer.mActivity
0060: sput-object  v7, ... UnityPlayer.currentActivity
0062: invoke-virtual {v7}, Activity.getRequestedOrientation()
```

É um `instanceof` **puro**, sem desembrulhar `ContextWrapper.getBaseContext()`.
Passar um `ContextWrapper` deixa `mActivity` nulo e a Unity estoura em seguida.

Por isso a `GameActivity` **é** o Context: ela sobrescreve `getResources()`,
`getAssets()`, `getApplicationInfo()` e `getClassLoader()` devolvendo os do jogo,
e passa `this` para a `UnityPlayer`. Enquanto o `GameEnvironment` não estiver
pronto, os getters caem no `super`, então o `super.onCreate()` funciona normal.

## 3. API da UnityPlayer conferida

```
UnityPlayer extends android.widget.FrameLayout

<init>(Context)
<init>(Context, IUnityPlayerLifecycleEvents)

onStart() onResume() onPause() onStop() destroy() quit() unload() lowMemory()
windowFocusChanged(boolean)  configurationChanged(Configuration)
newIntent(Intent)  injectEvent(InputEvent):boolean  getView():View
UnitySendMessage(String,String,String)   [static]
currentActivity : Activity               [static field]
```

A `UnityPlayerActivity` de fábrica usa **`onStart`/`onResume`/`onPause`/`onStop`**
— não os legados `resume()`/`pause()`, que também existem. Seguimos a de fábrica.

`IUnityPlayerLifecycleEvents` tem exatamente dois métodos:
`onUnityPlayerQuitted()` e `onUnityPlayerUnloaded()`.

## 4. O que a UnityPlayerActivity faz (pelos acessadores do PairIP)

Os métodos sintéticos `$NNN` que o PairIP injeta revelam cada chamada de
framework. Em `onCreate`, na ordem:

1. `requestWindowFeature(FEATURE_NO_TITLE)`
2. `getIntent()` → `getStringExtra("unity")` → `updateUnityCommandLineArguments()` → `putExtra()`
3. `new UnityPlayer(this, this)`
4. `setContentView(mUnityPlayer)`
5. `mUnityPlayer.requestFocus()`
6. `getWindow().getAttributes()` / `setAttributes()` — layout de recorte de tela
7. `getDisplay().getSupportedModes()` — taxa de quadros da superfície
8. `getPackageManager().hasSystemFeature(...)`
9. `setOnTouchListener` / `setOnGenericMotionListener` / `setPointerIcon`

Input é todo encaminhado por `injectEvent`: `dispatchKeyEvent`, `onKeyDown`,
`onKeyUp`, `onTouchEvent`, `onGenericMotionEvent`.

## 5. Config do manifest copiada do jogo

| Atributo | Valor do jogo |
|---|---|
| `launchMode` | `2` = `singleTask` |
| `screenOrientation` | `11` = `sensorLandscape` |
| `hardwareAccelerated` | `false` |
| `resizeableActivity` | `false` |
| `configChanges` | `0x40003fff` (praticamente tudo) |

A Unity lê `unity.splash-mode`, `unity.splash-enable`, `unity.launch-fullscreen`,
`unity.auto-report-fully-drawn`, `notch.config` e `android.notch_support` do
pacote **hospedeiro** via PackageManager — ou seja, do *nosso* manifest.
Por isso eles estão replicados lá.
