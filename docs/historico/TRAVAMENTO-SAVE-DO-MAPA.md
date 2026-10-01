# O jogo congelando ao entrar no mundo (save do mapa com tile de mod)

Rodada de 2026-09-30, no MuMu. Às vezes, logo depois de entrar no mundo, o jogo
congelava na tela "Desenhando o mapa: 100%": nenhum quadro a mais, todas as
threads dormindo (num futex), sem crash. Aparecia mais ou menos uma rodada em
três com vários mods de teste juntos (`tilename`, `modfurniture`, `prefix`,
`boss`); sozinho, nunca.

## Como foi achado

- As pilhas do `debuggerd` não servem no MuMu (o ARM é traduzido): as duas
  `UnityMain` aparecem só como `<unknown>`.
- O log parava sempre no mesmo ponto: o save do jogador que roda logo depois de
  entrar (`saveToonWhilePlaying`) começa a gravar o mapa, a nossa varredura das
  células de mod termina, e mais nada. A thread do jogo, no quadro 1 ou 2.
- Um log novo no motor JS (a espera do `JsSuspend` por mais de 5 s diz quem está
  com o motor) não disparou: não era o motor.
- [`tools/bench/repeat.sh`](../../tools/bench/repeat.sh) repete a rodada até
  travar e guarda o logcat.

## A causa

O hook do `MapHelper.InternalSaveMapCompressed` (o `.map` não pode levar índice
de tile de mod, ver [`ModTileMap.cpp`](../../app/src/main/cpp/content/tiles/ModTileMap.cpp))
pegava o `LockObject` do mapa e o segurava durante o save inteiro, para a thread
do jogo não escrever célula de mod num pedaço já limpo.

Só que o save do jogo não usa o `LockObject`: ele trava **cada pedaço**
(`lock(Main.Map.Chunks[i])`), comprime e solta. Com o nosso por fora, a thread
do save pegava `LockObject` → pedaço; a do jogo, mexendo no mapa logo ao entrar
(o `modfurniture` monta uma casa no quadro 1), pegava as duas na ordem contrária.
Cada uma esperando a outra.

Antes de achar isso, uma primeira correção (soltar o `LockObject` antes do
`FileUtilities.WriteAllBytes`, onde o jogo grava o arquivo) não resolveu: a
inversão acontecia antes, na compressão.

## A correção

Sem trava nossa, pedaço a pedaço, como o jogo:

1. antes do save, os pedaços com célula de mod ficam carregados e sujos (com a
   trava de cada um, a mesma do jogo), para o jogo comprimi-los de novo;
2. um hook no `WorldMapChunk.SaveCompressed`, só na thread do save e já dentro
   da trava do pedaço, tira as células de mod, comprime e as devolve (e marca o
   pedaço sujo, para o próximo descarregar comprimir com elas).

Conferido: `tilemap` nas três rodadas (o `.map` sem as células, o `.map.bl` com
elas, e elas voltam) e 12 rodadas da mistura que travava, sem travar.

## Para o próximo

Hook que pega trava do jogo tem de pegar **a mesma** que o jogo pega naquele
ponto, e não uma maior por fora: a ordem das travas da thread do jogo é a do
jogo, e uma trava a mais em volta inverte essa ordem.
