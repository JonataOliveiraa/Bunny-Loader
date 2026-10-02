# Desempenho do loader dos mods (ExMod)

Avaliação de 2026-10-01: quanto o loader dos mods (as classes `ModProjectile`,
`ModNPC`, `ModItem`... e os hooks por trás delas) custa por quadro, e as três
otimizações escolhidas a partir da medição. Continua
[`PONTE-OTIMIZACAO.md`](PONTE-OTIMIZACAO.md), que tratou da ponte JS → IL2CPP
operação por operação; aqui o assunto é o que o loader faz com ela num mundo
de verdade.

## Método

- [`tools/tests/perfloader`](../../tools/tests/perfloader): na carga, os
  padrões que os loaders repetem a cada chamada, em JS puro; no mundo, com o
  Example Mod, 300 quadros parados e 300 quadros com **60 projéteis animados**
  (`ExampleAdvancedAnimatedProjectile`: AI, PreDraw, GetAlpha) e **15 slimes
  de mod** vivos. O `bl.hookStats` dá, por hook, as chamadas e o tempo de JS
  (sem o do `original()`); o `DoUpdate` e o `DoDraw`, o tempo de CPU do quadro.
- [`tools/tests/hookcost`](../../tools/tests/hookcost): o custo de um hook que
  só repassa ao `original()`, medido em C++ e por dentro do JS.
- MuMu (ARM traduzido; ver a ressalva em `PONTE-OTIMIZACAO.md`): duas rodadas
  de cada build. Dados brutos em [`dados/desempenho-loader/`](dados/desempenho-loader/).

## O que a medição mostrou

**1. Num hook de uma chamada por quadro, o custo é entrar e sair do JS, não o
JS.** Hooks que só repassam ao `original()`:

| Hook | Pelo C++ | Por dentro do JS |
|---|---:|---:|
| `Main.DoUpdate` | 62 µs | 7 µs |
| `Player.UpdateBiomes` | 49 µs | 5 µs |
| `Main.UpdateAudio` | 26 µs | 6 µs |
| `Player.ResetEffects` | 17 µs | 2 µs |

Com cronômetros em cada trecho do despacho (um build só de medição), a
diferença inteira cai dentro do `JS_Call`, fora do corpo da função: a
preparação dos argumentos, o prólogo e o epílogo do `original()` somam 1 a 3 µs.
O mesmo hook chamado em sequência custa ~0,6 µs (o microbenchmark), e
`Main.get_backTextureValues`, chamado 12 vezes seguidas por quadro, 1,6 µs. É
o interpretador entrando "frio" depois de milissegundos de código do jogo, e o
MuMu (tradução ARM → x86) pesa nisso: **conferir no celular**. Com o Example
Mod, 43 hooks assim davam ~1,9 ms por quadro no MuMu.

**2. Por entidade, os hooks que a classe nem usa.** Basta UM projétil do mod
escrever `OnTileCollide` para o `HandleMovement` e o `Update` serem instalados,
e daí TODO projétil de mod entrava no JS neles; o mesmo com `Damage`,
`CanCutTiles`... Com 60 projéteis: ~21 µs por projétil por quadro em hooks que
a classe dele não escreve.

**3. O desenho em duas partes do `PreDrawExtras`.** Basta um projétil do mod
ter corrente (mangual, gancho) para três hooks de desenho (`EntitySpriteDraw`
duas vezes e `SpriteBatch.Draw`) entrarem no JS dentro do desenho de **todo**
projétil de mod, quase sempre só para repassar: ~10 µs por projétil desenhado.

**4. Os padrões do JS** (na carga, JS puro, MuMu):

| Padrão | ns acima de uma chamada de método |
|---|---:|
| `Safe.Run(n + '.AI', () => m.AI(p))` | ~440–660 |
| o mesmo com o rótulo pronto | ~320–470 |
| `try { m.AI(p) } catch` | ~0 |
| `AnimateTile` com 2 `Ref` novos e closure | ~1400–1850 |
| com os 2 `Ref` reusados, sem closure | ~440–610 |

A AI e o desenho de cada projétil passavam por três `Safe.Run` com rótulo
montado e closure nova (`PreAI`, `AI`, `PostAI`; `PreDrawExtras`, `PreDraw`,
`PostDraw`), chamando inclusive os métodos que a classe não escreve.

## As três otimizações

1. **Marcas por tipo nos hooks de cada método.** O filtro `marks` (que já
   existia para os blocos) passou a valer para o `ModProjectile` (movimento,
   `Kill`, `StatusNPC/Player`, `Colliding`, `Damage`, hitbox, `CutTiles`,
   `GetAlpha`, gancho, desenho), o `ModNPC` (AI, `FindFrame`, `GetChat`,
   `CheckActive`, `NPCLoot`) e o `GetAlpha` do `ModItem`: só os tipos cuja
   classe escreve o método entram no JS.
2. **O filtro nativo `flag`** (`Bridge.h`, `JsHook.cpp`, `bl.hookFlags`): uma
   chave global que o JS liga e desliga; desligada, o método roda sem o JS.
   Os quatro filtros do desenho em duas partes só ligam durante um desenho em
   duas partes (`proj.split`), e o `DrawSunAndMoon` do tema do menu só com um
   tema de mod nos menus (`menu.sky`; antes entrava no JS a cada quadro dentro
   do mundo).
3. **O JS dos hooks de todo quadro sem closure nem rótulo por chamada.** A AI e
   o desenho do `ModProjectile` e a AI do `ModNPC` seguem um plano por classe
   (o que ela escreve e os rótulos do log, montados uma vez) e chamam só o que
   ela escreve, com `try/catch` direto (`Safe.Report` para o aviso único); o
   `AnimateTiles` reusa os dois `Ref`. A luz do desenho só é calculada se o
   `PreDraw` ou o `PostDraw` a recebem.

## Resultado (MuMu, média de 2 rodadas por build)

| | Antes | Depois | Δ |
|---|---:|---:|---:|
| JS nos hooks, mundo parado | 1,88 ms | 1,20 ms | **−36%** |
| JS nos hooks, 60 projéteis + 15 slimes | 8,9 ms | 6,0 ms | **−32%** |
| `DoUpdate`, com as entidades | 9,4 ms | 5,2 ms | **−45%** |
| `DoDraw`, com as entidades | 13,8 ms | 11,7 ms | −15% |
| `Projectile.AI`, por projétil | 18,3 µs | 14,0 µs | −23% |
| hooks que a classe não escreve, por projétil | ~21 µs | 0 | |
| filtros do desenho em duas partes, por projétil | ~10 µs | 0 | |

(O JS nos hooks inclui ~0,85 ms do próprio teste, que mantém os projéteis vivos.)

O que sobra no desenho (~65 µs por projétil) é quase todo o `PreDraw` do
Example Mod: uma dúzia de chamadas à ponte por desenho, cada `Vector2` e
`Rectangle` um struct novo. É o próximo alvo, na ponte, não no loader.

Testes: `projectiles`, `npcs`, `moditems`, `exmod1`, `exmod2`, `hooks`,
`hookchain`, `globals`, `tileframes`, `trailcache`, `oldpos`, `boss`,
`summons` e o novo [`menusky`](../../tools/tests/menusky) verdes (rodados
sozinhos: juntos, alguns se atrapalham, como antes). O `ifbusy` falha no caso
`'original'` com e sem estas mudanças (0 quadros em 1,5 s): falha anterior,
a investigar à parte.

**Pendente:** as mesmas medições no celular (o item 1 depende delas).
