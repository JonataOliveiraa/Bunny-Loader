# Bunny Loader documentation

Bunny Loader is a mod loader for **Terraria Mobile**: an Android app that
boots the game and runs mods written in C# inside it, in the tModLoader style.

The documentation has three parts, for three kinds of readers:

## For mod makers

[**Making mods in C#**](mods-em-csharp/README.md): hooks and full access to
the game in typed, compiled C#, and the tModLoader content classes.

1. [Your first C# mod](mods-em-csharp/01-primeiro-mod.md)
2. [Hooks](mods-em-csharp/02-hooks.md)
3. [`ref`, `out` and `Nullable`](mods-em-csharp/03-ref-out-e-nullable.md)
4. [Values and objects](mods-em-csharp/04-valores-e-objetos.md)
5. [C# × JS × tModLoader](mods-em-csharp/05-comparacao.md)
6. [Performance](mods-em-csharp/06-desempenho.md)
7. [Moving to C#](mods-em-csharp/07-migracao-para-csharp.md)
8. [Under the hood](mods-em-csharp/08-por-baixo-dos-panos.md)
9. [Content classes](mods-em-csharp/09-conteudo.md)
10. [Tiles](mods-em-csharp/10-tiles.md)
11. [Systems](mods-em-csharp/11-sistemas.md)
12. [World generation](mods-em-csharp/12-worldgen.md)
13. [Biomes and scenes](mods-em-csharp/13-biomas.md)
14. [Title menu](mods-em-csharp/14-menu.md)
15. [Sounds](mods-em-csharp/15-sons.md)
16. [Mod configs](mods-em-csharp/16-configs.md)
17. [Chat commands](mods-em-csharp/17-comandos.md)

## For people working on Bunny Loader

[**The native core**](nucleo/README.md): the C++ that runs inside the game.

- [Overview and boot](nucleo/README.md)
- [Hooks under the hood](nucleo/hooks.md): ShadowHook, the chain, the slots,
  the dispatcher.
- [New content under the hood](nucleo/conteudo.md): per-type tables, compiled
  limits, saves.
- [Joining a server](nucleo/multijogador.md): the welcome that keeps Bunny
  Loader out of vanilla servers, the host rules, syncing mods and textures and
  the automatic return.
- [The C# plan](nucleo/plano-csharp.md) and [the cost of C# hooks](nucleo/custo-csharp.md).
- [Shader research](nucleo/pesquisa-shaders.md): internal investigation notes,
  with evidence, limitations and next experiments; support not validated yet.

[**History**](historico/README.md): the architecture decisions, the crash
investigations and the optimization rounds, with the measured data. The
JavaScript engine, removed in favor of C#, is documented there too: its mod
guides, its reference and the bridge. The last version with it is the
`js-final` tag.

The PC tools (fast build, game dump, tests, benchmark) are in
[`tools/`](../tools/README.md).
