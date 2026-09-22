# unity-classes.jar

Classes Java do **Unity Android Player 2021.3.56f2** (variação `il2cpp/Release`),
que é exatamente a engine do Terraria alvo:

    Unity : Built from '2021.3/respin/2021.3.56f2-4f19b0708d3a-v2'
            Version '2021.3.56f2 (667925324b48)'   <- changeset usado abaixo

## Por que precisamos delas

O Bunny Loader hospeda o jogo no próprio processo. Para isso é preciso uma
`com.unity3d.player.UnityPlayer` **limpa**.

As classes que vêm no APK do Terraria NÃO servem: o PairIP cifra as constantes de
string de todo o app, inclusive dentro das classes do Unity — por exemplo
`UnityPlayer.getNaturalOrientation()` lê uma string que deveria ser `"window"` e
recebe `null`, estourando em `getSystemService(null)`. Foi o que derrubou a Fase 1.
Ver `docs/UNITY-HOSTING.md`.

## Procedência (reproduzir)

Pacote oficial da Unity, módulo Android para o editor 2021.3.56f2. Usamos o
`.pkg` (macOS) porque é um xar comum — as classes são Java, independem do SO, e
o instalador Windows exige privilégio de administrador:

    https://download.unity3d.com/download_unity/667925324b48/
      MacEditorTargetInstaller/UnitySetup-Android-Support-for-Editor-2021.3.56f2.pkg

Dentro: `Payload` (gzip + cpio odc) -> `./Variations/il2cpp/Release/Classes/classes.jar`.

NÃO é conteúdo do Terraria — é o runtime Android da Unity.
