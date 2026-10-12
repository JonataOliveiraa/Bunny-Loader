# unity-classes.jar

**Out of Git.** The jar belongs to Unity Technologies, so it is not published
with the code: whoever builds extracts it with the "Provenance" recipe below
and puts it here, at `app/libs/unity-classes.jar`. Without it the app builds
(`UnityPlayer` is loaded through reflection), but the game does not boot.

Java classes of the **Unity Android Player 2021.3.56f2** (`il2cpp/Release`
variation), which is exactly the target Terraria's engine:

    Unity : Built from '2021.3/respin/2021.3.56f2-4f19b0708d3a-v2'
            Version '2021.3.56f2 (667925324b48)'   <- changeset used below

## Why we need them

Bunny Loader hosts the game in its own process. That requires a **clean**
`com.unity3d.player.UnityPlayer`.

The classes that ship in the Terraria APK do NOT work: PairIP encrypts the
string constants of the whole app, including inside the Unity classes — for
example `UnityPlayer.getNaturalOrientation()` reads a string that should be
`"window"` and gets `null`, blowing up in `getSystemService(null)`. That is
what took down Phase 1. See `docs/historico/UNITY-HOSTING.md`.

## Provenance (to reproduce)

Unity's official package, the Android module for the 2021.3.56f2 editor. We use
the `.pkg` (macOS) because it is a plain xar — the classes are Java and do not
depend on the OS, and the Windows installer requires administrator privileges:

    https://download.unity3d.com/download_unity/667925324b48/
      MacEditorTargetInstaller/UnitySetup-Android-Support-for-Editor-2021.3.56f2.pkg

Inside: `Payload` (gzip + cpio odc) -> `./Variations/il2cpp/Release/Classes/classes.jar`.

It is NOT Terraria content — it is Unity's Android runtime.
