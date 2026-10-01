# Os 45 fps cravados numa tela de 90 Hz

Rodada de 2026-09-30, num Galaxy A15 5G (SM-A156M, Android 16, One UI, tela de
90/60 Hz), Terraria 1.4.5.8.6. O jogo ficava em **45 fps fixos** no Bunny
Loader (30 a 35 com zoom) e em 60 no TL Pro, no mesmo aparelho. Ferramentas:
[`tools/tests/frametime`](../../tools/tests/frametime),
[`tools/bisect`](../../tools/bisect) e o adb pelo ZeroTier.

## O que não era

- **O JS dos mods.** Sem o ExampleMod dava 45 igual, e sem mods nenhum hook JS
  roda por quadro (`bl.hookStats`).
- **Uma regressão no código.** Todas as versões, até as de 27/09 e as da
  1.4.5.6, davam 45 no aparelho. No MuMu, sem mods, 28/09, 29/09 e 30/09 ficam
  entre 5 e 6 ms de desenho. Duas medições de 8,9 ms foram outra instância do
  MuMu ligada no PC dividindo a CPU.
- **O MuMu.** Ele só tem tela de 60 Hz; o problema não aparece lá.

## O que era

O Unity arredonda `taxa da tela ÷ Application.targetFrameRate` para saber
quantas atualizações cada quadro ocupa. O Terraria pede 60
(`XNAUnityRunner.Awake`, `SetAndroidSurfaceRate`, `Main.SetPlatform60FPS`).
Numa tela de 90 Hz, 90/60 = 1,5 vira **2**: um quadro a cada duas
atualizações, 45 fps. Acima de 22,2 ms por quadro (zoom), três: 30.

O TL Pro escapa porque a Samsung o conta como **jogo** e, para jogos, libera o
modo de 60 Hz da tela: o `SwappyDisplayManager` do Unity escolhe 60 e 60/60 = 1.
Para app fora da lista, com a suavidade de movimento em "Alta", a Samsung trava
a tela em 90 (`mDesiredDisplayModeSpecs` 90–90) e o app só enxerga esse modo
(`Display.getSupportedModes()` devolve um).

A lista de jogos da Samsung (`dumpsys gamemanager`, "Package Data in SGM"):

- **ignora a categoria do manifest** ("Game (Manifest excluded)"): o
  `android:appCategory="game"` não põe o app lá;
- conta a dica do **instalador** (`setApplicationCategoryHint`, que a Play Store
  dá; o Android só a aceita do instalador de registro), a lista dela e o usuário
  (adicionar ao Game Launcher não passou para lá);
- em 2026-09-28 21:39 o GOS aplicou uma política nova aos jogos da lista
  (`requestToRemoveRefreshRate`, `setPerformanceMode`): o "anteontem era 60".

## A correção

O usuário achou no console que `targetFrameRate = 61` segura os 60: 90/61 ≈
1,47 vira **1**, e quem limita a 60 é o passo fixo do XNA. Em 60 Hz (60/61 → 1)
e 120 Hz (120/61 ≈ 1,97 → 2) dá o mesmo que 60; o 30 da economia de bateria
(90/30 = 3) fica. Implementado em
[`boot/FramePacing.cpp`](../../app/src/main/cpp/boot/FramePacing.cpp): hook
nativo no `Application.set_targetFrameRate` (60 vira 61) e, uma vez na thread
do jogo, o `Main.Setting_Target60FPS` vai e volta para o jogo refazer a escolha
já pelo hook (o setter sai cedo se o valor não muda).

## Para a próxima vez

Fps travado em 45 ou 30 no celular: olhar a tela antes do tempo de CPU.

```bash
adb shell dumpsys display | grep -E 'mDesiredDisplayModeSpecs|mActiveModeId'
```

```bash
adb shell dumpsys gamemanager
```
