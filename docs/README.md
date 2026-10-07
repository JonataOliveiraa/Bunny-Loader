# Documentação do Bunny Loader

O Bunny Loader é um loader de mods para o **Terraria Mobile**: um app Android
que sobe o jogo e roda mods escritos em JavaScript dentro dele, no formato do
tModLoader.

A documentação tem três partes, para três leitores:

## Para quem faz mods

[**Guias de mod**](mods/README.md): do primeiro hook ao chefe com música, na
ordem de leitura.

0. [Como funciona: do TL Pro ao Bunny Loader](mods/00-como-funciona.md)
1. [Hooks: mudar o que o jogo já tem](mods/01-hooks-do-zero.md)
2. [`ref` e `out`](mods/02-ref-e-out.md)
3. [Custo e desempenho](mods/03-custo-e-desempenho.md)
4. [Conteúdo novo: as ideias](mods/04-conteudo-novo.md)
5. [Itens](mods/05-itens.md)
6. [Projéteis](mods/06-projeteis.md)
7. [NPCs: inimigos, moradores e chefes](mods/07-npcs.md)
8. [Jogador e buffs](mods/08-jogador-e-buffs.md)
9. [Blocos](mods/09-blocos.md)
10. [Sons e música](mods/10-sons-e-musica.md)
11. [Conversa entre mods](mods/11-conversa-entre-mods.md)

[Pacotes de textura](mods/14-pacotes-de-textura.md): substituição de sprites usando PNGs, sem scripts.

## Para consultar

- [Métodos por classe](referencia/metodos.md): índice completo gerado da API
  JavaScript, com assinaturas, herança, retorno da implementação base e limites.
  A [planilha Excel](referencia/metodos.xlsx) e o [CSV](referencia/metodos.csv)
  permitem filtrar os mesmos registros por classe e método.
- [O que cada classe tem hoje](referencia/classes.md): `ModItem`, `ModNPC`,
  `ModProjectile`, `ModPlayer`, `ModBuff`, `ModTile`... campo a campo, com o
  método do jogo por trás de cada um e o que ainda falta em relação ao
  tModLoader.
- [Hooks de ModPlayer](referencia/modplayer-hooks.md): contratos de combate,
  itens, saltos, desenho, inventário, save e rede.
- [PlayerDrawLayer](referencia/playerdrawlayers.md): camadas personalizadas e inserção no cache de desenho.
- [Hooks de ModSystem](referencia/modsystem-hooks.md): geração, etapas GenPass, atualização, iluminação e rede.
- [Uso e munição de ModItem](referencia/moditem-uso-municao.md): animação, frames, seleção, consumo e diferenças do jogo móvel.
- [Cura de ModItem](referencia/moditem-cura.md): cura normal e rápida, refs, atraso de poções e composição com ModPlayer.
- [Combate de ModItem](referencia/moditem-combate.md): modificadores, permissões,
  hitbox, PvP, ordem entre classes e filtros nativos.
- [A ponte e o `bl`](referencia/ponte-e-bl.md): a sintaxe para falar com o
  jogo e todas as funções `bl.*`.

## Para quem mexe no Bunny Loader

[**O núcleo nativo**](nucleo/README.md): o C++ que roda dentro do jogo.

- [Visão geral e boot](nucleo/README.md)
- [Hooks por dentro](nucleo/hooks.md): ShadowHook, a cadeia, os slots, o
  despachante.
- [Threads e o motor JS](nucleo/threads-e-motor-js.md): a trava do motor, a
  pilha por thread, `ifBusy`.
- [A ponte JS ↔ jogo](nucleo/ponte.md): nomes, tipos, structs, objetos
  segurados pelo coletor.
- [Conteúdo novo por dentro](nucleo/conteudo.md): tabelas por tipo, limites
  compilados, saves.
- [Entrada num servidor](nucleo/multijogador.md): sincronização de mods e
  texturas pela tela de Host, a conversa na entrada e a volta automática.
- [Pesquisa de shaders](nucleo/pesquisa-shaders.md): base interna de investigação,
  com evidências, limitações e próximos experimentos; suporte ainda não validado.

[**Histórico**](historico/README.md): as decisões de arquitetura, as
investigações de crash e as rodadas de otimização, com os dados medidos.

As ferramentas de PC (build rápido, dump do jogo, testes, benchmark) estão em
[`tools/`](../tools/README.md).
