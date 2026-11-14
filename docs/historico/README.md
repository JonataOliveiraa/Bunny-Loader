# History

Records of how Bunny Loader got where it is: decisions, investigations and
measurements. They stay here as they were written, with what was known **at
the time**. Where something changed later, there is a note in the document
itself; for the current state, the [wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki) applies, and
[Native core](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Native-Core) for the core.

| Document | What it records |
|---|---|
| [UNITY-HOSTING.md](UNITY-HOSTING.md) | What Terraria's `classes.dex` revealed about hosting `UnityPlayer`, and the PairIP wall. |
| [DECISAO-ARQUITETURA.md](DECISAO-ARQUITETURA.md) | The paths evaluated for putting `libbunny.so` inside the game, and what was measured on each (including the discovery that the hook works on the emulator). The final design was a different one: the game runtime integrated into the app itself (see [Native core](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Native-Core#the-process)). |
| [AVALIACAO-PONTE-E-CRASH.md](AVALIACAO-PONTE-E-CRASH.md) | The first measurement of the JS → IL2CPP bridge, the comparison with V8, and the hunt for the crash when creating a world. |
| [PONTE-OTIMIZACAO.md](PONTE-OTIMIZACAO.md) | Optimizing the bridge step by step (roots, identity, method cache, fast index, cheap wrapper), the real cause of the crash when creating a world, and the JS engine's per-thread stack. |
| [FPS-45-TELA-90HZ.md](FPS-45-TELA-90HZ.md) | The fps stuck at 45 on a 90 Hz screen: Unity's `targetFrameRate` rounding, Samsung's game list (which ignores the manifest) and the target of 61. |
| [FUNDO-SUMINDO-NUVEM-DE-MOD.md](FUNDO-SUMINDO-NUVEM-DE-MOD.md) | The background that vanished on the phone: the horizon cloud mask (`CloudMasks`) without the mod types, read without checking bounds. **Confirmation on the phone pending.** |
| [DESEMPENHO-LOADER.md](DESEMPENHO-LOADER.md) | How much the mod loader costs per frame (the fixed cost of entering JS, hooks for methods the class does not write, two-part drawing) and the three optimizations: per-type marks, the `flag` filter and per-frame JS without closures. **Measurement on the phone pending.** |
| [pesquisa-shaders.md](pesquisa-shaders.md) | Static analysis of how the mobile game loads and uses shaders, before the screen effects existed (2026-10-03). |
| [TRAVAMENTO-SAVE-DO-MAPA.md](TRAVAMENTO-SAVE-DO-MAPA.md) | The game freezing when entering the world: the map save hook held `LockObject` outside each chunk's lock, inverting the lock order with the game thread. |

## The JavaScript engine

Bunny Loader first ran mods in JavaScript (QuickJS). It was removed when the C#
API took over; the last version with it is the `js-final` tag (and the
`release-js` branch). Its documentation stays here:

| Document | What it records |
|---|---|
| [mods-js/](mods-js/README.md) | The JS mod guides, from the first hook to a boss with music. |
| [referencia-js/](referencia-js/metodos.md) | The JS API reference: methods by class, what each class had, the bridge and `bl`. |
| [ponte-js.md](ponte-js.md) | The JS ↔ game bridge: names, types, structs, objects pinned against the garbage collector. |
| [threads-e-motor-js.md](threads-e-motor-js.md) | The engine lock, the per-thread stack, `ifBusy`. |
| [compatibilidade-tmodloader-desenho-banners.md](compatibilidade-tmodloader-desenho-banners.md) | The tModLoader contracts for item drawing, `CloneDefaults` and banners, as analyzed and built in the JS loaders (2026-10-08). |
| [plano-csharp.md](plano-csharp.md) | The plan that moved every loader to C# and removed JavaScript, phase by phase, with what each phase delivered. What is left is in [Migrating from 0.3](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Migrating-From-0.3#what-is-left-of-the-migration). |

The raw data (crash logs, attempts, benchmark rounds) is in
[`dados/`](dados/).
