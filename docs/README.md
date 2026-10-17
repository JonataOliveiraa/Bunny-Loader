# Bunny Loader documentation

Bunny Loader is a mod loader for **Terraria Mobile**: an Android app that
boots the game and runs mods written in JavaScript inside it, in the
tModLoader style.

The documentation has three parts, for three kinds of readers:

## For mod makers

[**Mod guides**](mods/README.md): from the first hook to a boss with music, in
reading order.

0. [How it works: from TL Pro to Bunny Loader](mods/00-como-funciona.md)
1. [Hooks: changing what the game already has](mods/01-hooks-do-zero.md)
2. [`ref` and `out`](mods/02-ref-e-out.md)
3. [Cost and performance](mods/03-custo-e-desempenho.md)
4. [New content: the ideas](mods/04-conteudo-novo.md)
5. [Items](mods/05-itens.md)
6. [Projectiles](mods/06-projeteis.md)
7. [NPCs: enemies, town NPCs and bosses](mods/07-npcs.md)
8. [Player and buffs](mods/08-jogador-e-buffs.md)
9. [Tiles](mods/09-blocos.md)
10. [Sounds and music](mods/10-sons-e-musica.md)
11. [Talking between mods](mods/11-conversa-entre-mods.md)
12. [Globals and the world](mods/12-globais-e-mundo.md)
13. [The package page](mods/13-pagina-do-pacote.md)
14. [Mod options](mods/14-opcoes-do-mod.md)
15. [Damage classes](mods/15-classes-de-dano.md)
16. [Hits](mods/16-golpes.md)
17. [Screen effects](mods/17-efeitos-de-tela.md)
18. [Touch buttons](mods/18-botoes-de-toque.md)
19. [Dust](mods/19-poeiras.md)
20. [Interface: panels, buttons and resource bars](mods/20-interface.md)

[Texture packs](mods/14-pacotes-de-textura.md): replacing sprites with PNGs, no scripts.

## Reference

- [Methods by class](referencia/metodos.md): the complete generated index of
  the JavaScript API, with signatures, inheritance, the base implementation's
  return value and limits. The [Excel spreadsheet](referencia/metodos.xlsx)
  and the [CSV](referencia/metodos.csv) let you filter the same records by
  class and method.
- [What each class has today](referencia/classes.md): `ModItem`, `ModNPC`,
  `ModProjectile`, `ModPlayer`, `ModBuff`, `ModTile`... field by field, with
  the game method behind each one and what is still missing compared to
  tModLoader.
- [ModPlayer hooks](referencia/modplayer-hooks.md): contracts for combat,
  items, jumps, drawing, inventory, saving and networking.
- [PlayerDrawLayer](referencia/playerdrawlayers.md): custom layers and inserting into the draw cache.
- [ModSystem hooks](referencia/modsystem-hooks.md): world generation, GenPass steps, updates, lighting and networking.
- [ModItem use and ammo](referencia/moditem-uso-municao.md): animation, frames, selection, consumption and how the mobile game differs.
- [ModItem healing](referencia/moditem-cura.md): normal and quick heal, refs, potion delay and composition with ModPlayer.
- [ModItem combat](referencia/moditem-combate.md): modifiers, permissions,
  hitbox, PvP, ordering between classes and native filters.
- [The bridge and `bl`](referencia/ponte-e-bl.md): the syntax for talking to
  the game and every `bl.*` function.

## For people working on Bunny Loader

[**The native core**](nucleo/README.md): the C++ that runs inside the game.

- [Overview and boot](nucleo/README.md)
- [Hooks under the hood](nucleo/hooks.md): ShadowHook, the chain, the slots,
  the dispatcher.
- [Threads and the JS engine](nucleo/threads-e-motor-js.md): the engine lock,
  the per-thread stack, `ifBusy`.
- [The JS ↔ game bridge](nucleo/ponte.md): names, types, structs, objects
  pinned against the garbage collector.
- [New content under the hood](nucleo/conteudo.md): per-type tables, compiled
  limits, saves.
- [Joining a server](nucleo/multijogador.md): syncing mods and textures from
  the Host screen, the handshake on join and the automatic return.
- [Shader research](nucleo/pesquisa-shaders.md): internal investigation notes,
  with evidence, limitations and next experiments; support not validated yet.

[**History**](historico/README.md): the architecture decisions, the crash
investigations and the optimization rounds, with the measured data.

The PC tools (fast build, game dump, tests, benchmark) are in
[`tools/`](../tools/README.md).
