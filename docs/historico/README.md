# History

Records of how Bunny Loader got where it is: decisions, investigations and
measurements. They stay here as they were written, with what was known **at
the time**. Where something changed later, there is a note in the document
itself; for the current state, the [core documentation](../nucleo/README.md)
applies.

| Document | What it records |
|---|---|
| [UNITY-HOSTING.md](UNITY-HOSTING.md) | What Terraria's `classes.dex` revealed about hosting `UnityPlayer`, and the PairIP wall. |
| [DECISAO-ARQUITETURA.md](DECISAO-ARQUITETURA.md) | The paths evaluated for putting `libbunny.so` inside the game, and what was measured on each (including the discovery that the hook works on the emulator). The final design was a different one: the game runtime integrated into the app itself (see the [core](../nucleo/README.md#the-process)). |
| [AVALIACAO-PONTE-E-CRASH.md](AVALIACAO-PONTE-E-CRASH.md) | The first measurement of the JS → IL2CPP bridge, the comparison with V8, and the hunt for the crash when creating a world. |
| [PONTE-OTIMIZACAO.md](PONTE-OTIMIZACAO.md) | Optimizing the bridge step by step (roots, identity, method cache, fast index, cheap wrapper), the real cause of the crash when creating a world, and the JS engine's per-thread stack. |
| [FPS-45-TELA-90HZ.md](FPS-45-TELA-90HZ.md) | The fps stuck at 45 on a 90 Hz screen: Unity's `targetFrameRate` rounding, Samsung's game list (which ignores the manifest) and the target of 61. |
| [FUNDO-SUMINDO-NUVEM-DE-MOD.md](FUNDO-SUMINDO-NUVEM-DE-MOD.md) | The background that vanished on the phone: the horizon cloud mask (`CloudMasks`) without the mod types, read without checking bounds. **Confirmation on the phone pending.** |
| [DESEMPENHO-LOADER.md](DESEMPENHO-LOADER.md) | How much the mod loader costs per frame (the fixed cost of entering JS, hooks for methods the class does not write, two-part drawing) and the three optimizations: per-type marks, the `flag` filter and per-frame JS without closures. **Measurement on the phone pending.** |
| [TRAVAMENTO-SAVE-DO-MAPA.md](TRAVAMENTO-SAVE-DO-MAPA.md) | The game freezing when entering the world: the map save hook held `LockObject` outside each chunk's lock, inverting the lock order with the game thread. |

The raw data (crash logs, attempts, benchmark rounds) is in
[`dados/`](dados/).
