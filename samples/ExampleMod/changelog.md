# Novidades

## Ainda sem versão

- Montaria de mod (`ModMount`) e o carrinho de mina do Exemplo.
- Menu de título de mod (`ModMenu`), com a música própria (*Mysterious
  Mystery*); escolha em Configurações > Tema do Menu.
- Bioma de mod: nome, felicidade dos moradores, Bestiário, fundo do mapa e
  spawn natural.
- `ModCommand`, `ModHair`, `ModCloud`, `ModEmoteBubble`, `ModAchievement` e pesca.
- Pilar de Exemplo (`ModPylon`), vendido pelos moradores no bioma de exemplo.
- Tile entity (`ModTileEntity`): o barril que junta água da chuva.
- Árvore de Exemplo (`ModTree`), com muda e folha, no Bloco de Exemplo.
- Barra de vida própria do Olho de ??? (`ModBossBar`).
- Item de coleta (`OnPickup`, `GrabRange`), Espada Curta de Exemplo
  (`ShouldUpdatePosition`), o gancho com `GrappleRange`, `NumGrappleHooks` e as
  velocidades, e o tempo restante na dica da Defesa (`ModifyBuffText`).
- Efeitos de tela: o Último Prisma de Exemplo (porte do `ExampleLastPrism`,
  com a tela ondulando onde os feixes batem), o Cajado da Fenda de Exemplo
  (efeito com shader `.frag` próprio, distorção e os prontos) e as bordas
  vermelhas com pouca vida (`ExampleLowLifePlayer`).
- Globais para todo tile, buff, barra de chefe e receita: o que cai ao sacudir
  árvores (`ShakeTrees`), o Girassol no Bloco de Exemplo (`SunflowerChanges`),
  o tempo restante na dica de todo buff e o ícone da Fogueira tremendo
  (`ExampleGlobalBuff`), a barra do Olho de Cthulhu (`ExampleGlobalBossBar`) e
  o `ExampleGlobalRecipe`.
- Receitas com condição (`AddCondition`) e com callbacks de consumo e de
  criação (a Corrente que não é gasta, os fogos de artifício).
- A bolsa do Chefe de Exemplo marcada como `PreHardmodeLikeBossBag`: como as
  bolsas pré-hardmode do jogo, não dá conjunto de desenvolvedor.
- Quantidade de pesquisa do modo Jornada (`Item.ResearchUnlockCount`): 100 no
  Exemplo de Item, 3 na bolsa do chefe.
- Fonte própria do mod (`ModContent.Request('Assets/Fonts/ExampleFont')`): o
  "CPM" da tela de moedas (`/coins`) é desenhado com a New Rocker.
- Estrutura de exemplo: um castelinho com móveis do Exemplo e um baú com
  recheio, gerado com o mundo (`ModifyWorldGenTasks`, depois dos Girassóis) ou
  pelo comando `/castle` embaixo do jogador.

## 1.8.0 - 2026-09-27

- Bioma de mod com música, fundos e multijogador.
- Os três prefixos do Exemplo (`ModPrefix`).
- Correções: gancho, material, luz, fogo vivo, relógio, bônus de conjunto,
  acessórios e a curva do chicote.

## 1.7.0 - 2026-09-27

- Armaduras e equipáveis aparecem **vestidos**: capacete, peitoral, calças,
  asas, barba, fantasia e manto.

## 1.6.0 - 2026-09-27

- Móveis com comportamento: cadeira que senta, cama, porta, baú, lustre,
  fogueira, caixa de música.

## 1.5.0 - 2026-09-27

- Pacote no formato do tModLoader: `Assets/`, `Common/`, `Content/` e
  `Localization/`, com a classe `Mod` no `main.js`.
- Rede dos mods: `NetSend`/`NetReceive` e `ModPacket`.
- `GlobalItem`, `GlobalNPC`, `GlobalProjectile` e drops globais.

## 1.4.0 - 2026-09-26

- Som e música de mod: `SoundStyle`, `SoundEngine` e a música do chefe.

## 1.3.1 - 2026-09-26

- Drops de item de mod e a linha da vara de pesca.

## 1.3.0 - 2026-09-25

- A Pessoa: moradora com loja, moeda própria, felicidade e retrato.
- O chefe, Olho de ???, e o item de invocação.

## 1.2.0 - 2026-09-25

- Pets, pet de luz, lacaio e sentinela.

## 1.1.0 - 2026-09-25

- Buffs, `ModPlayer`, blocos 1x1 com save seguro e tooltip colorido.

## 1.0.0 - 2026-09-23

- Primeira versão: a espada e o Exemplo de Item.
