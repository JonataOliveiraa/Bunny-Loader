# 3. Custo e desempenho

Todo mod gasta um pouco do tempo de cada quadro do jogo. Na maioria das vezes
esse pouco é tão pequeno que não aparece. Mas um hook no lugar errado, ou um
laço dentro de um método que o jogo chama milhares de vezes por quadro, pode
derrubar o jogo de 60 para 20 quadros por segundo. Este guia explica **de onde
vem o custo**, **quanto custa cada coisa** e **como escrever um mod leve**.

Pré-requisito: o [guia 1](01-hooks-do-zero.md), até *Hooks*.

## O orçamento de um quadro

O Terraria atualiza e desenha o mundo 60 vezes por segundo. Isso dá **16,6 ms
por quadro** para tudo: o jogo, a Unity, o desenho, e os mods. O próprio jogo
já usa boa parte: no emulador de teste, a atualização (`Main.DoUpdate`) leva
~3 ms e o desenho (`Main.DoDraw`) ~6 ms, com picos maiores. O que sobra é a
folga dos mods, e ela é dividida entre **todos** os mods ligados.

Um jeito útil de pensar: **1 ms por quadro é muito** para um mod. 0,1 ms é
confortável. 0,01 ms não existe.

## De onde vem o custo

O JavaScript de um mod roda num interpretador (o QuickJS, sem JIT). E todo
acesso ao jogo **atravessa a ponte**: um nome JS vira um campo do C#, um valor
do C# vira um valor JS. Cada travessia custa pouco, mas custa.

Um hook soma quatro coisas a cada chamada do método:

1. **O despacho**: o jogo chama o método, cai na função do Bunny Loader, que
   confere os filtros, pega o motor JS (a trava) e monta os argumentos;
2. **Os argumentos**: o `self` e cada objeto do jogo viram um objeto JS (um
   *wrapper*); números viram números;
3. **O seu callback**: o JS que você escreveu, e cada acesso ao jogo dentro
   dele;
4. **O `original()`**: solta o motor, roda o método do jogo, pega o motor de
   volta.

A parte **fixa** (1, 2 e 4) é paga em toda chamada, mesmo que o seu callback
não faça nada. É por isso que **quantas vezes** o método é chamado importa mais
que qualquer outra coisa.

## Quanto custa cada coisa

Medido com [`tools/bench`](../../tools/bench), no emulador MuMu (que roda o
ARM do jogo traduzido para x86), em nanossegundos (ns; 1 ms = 1.000.000 ns),
acima do custo de um laço JS vazio:

| Operação | ns | Exemplo |
|---|---:|---|
| Ler ou escrever um campo (`int`, `float`) | ~35 | `p.statLife`, `p.statLife = 400` |
| Ler um campo estático | ~47 | `Main.maxTilesX` |
| Ler um elemento de `int[]` | ~48 | `Main.npcFrameCount[t]` |
| Ler um elemento que é objeto | ~64 | `Main.npc[i]` |
| O mesmo, com objeto que o JS ainda não tinha | ~146 | `Main.item[i]` varrendo 400 itens |
| Propriedade C# (`get_X`) | ~85 | `Main.myPlayer` |
| `Main.player[0].whoAmI` (estático + elemento + campo) | ~157 | |
| Vista de struct | ~181 | `p.position.X` |
| Chamar método guardado numa variável | ~95 | `const f = Player['...']; f(p)` |
| Chamar método buscando pela assinatura toda vez | ~154 | `p['bool CanBePushedByWind()']()` |
| Método estático com 2 floats | ~146 | |
| Criar um objeto JS | ~180 | `{ x: 1, y: 2 }` |
| Uma volta de laço JS com uma conta | ~95 | `s = (s + i * i) % 1000003` |
| **Hook que só repassa ao `original()`** | **~550–610** | por chamada |
| **Hook que não chama o original** | **~270** | por chamada |

> **Os números são do emulador.** O MuMu roda o ARM do jogo traduzido, e o
> interpretador do QuickJS é justamente o tipo de código que a tradução mais
> penaliza. Num celular, os valores absolutos são outros. O que vale é a
> **proporção**: um hook custa o mesmo que ~15 leituras de campo; uma vista de
> struct, o mesmo que ~5.

Um hook sem `original()` custa menos porque não solta e pega o motor de volta
(ver [por que o `original()` solta o motor](../nucleo/threads-e-motor-js.md#segurar-o-motor-durante-o-método-do-jogo)).

## Quantas vezes o jogo chama cada método

É aqui que o custo se multiplica. Valores típicos, por quadro:

| Método | Chamadas por quadro | Hook que só repassa custa |
|---|---|---:|
| `Main.DoUpdate`, `Main.DrawInterface` | 1 | ~0,6 µs |
| `Player.Update` | 1 por jogador no mundo | ~0,6 µs por jogador |
| `NPC.AI`, `NPC.UpdateNPC` | 1 por NPC ativo (até 200) | até ~0,12 ms |
| `Projectile.AI`, `Projectile.Update` | 1 por projétil ativo (até 1000), às vezes mais (`extraUpdates`) | até ~0,6 ms |
| `Item.SetDefaults` | 0 no jogo parado; **milhares** ao gerar ou carregar mundo | segundos a mais na carga |
| Poeira e partículas (`Dust`) | milhares | ms por quadro |
| `SpriteBatch.DrawString` | cada texto da tela: milhares | ms por quadro |
| Luz, colisão, tiles | dezenas de milhares | **o quadro inteiro** |

Contas de exemplo, com os números do emulador:

- Um hook em `Player.Update` que lê 10 campos: `0,6 + 10 × 0,035 ≈ 1 µs` por
  quadro. **Irrelevante**.
- Um hook em `NPC.AI` sem filtro, com 150 NPCs, que lê 5 campos de cada:
  `150 × (0,6 + 5 × 0,035) ≈ 0,12 ms` por quadro. **Aceitável**, mas é
  desperdício se só os NPCs de mod interessam (ver *Filtros nativos*).
- Um hook em `SpriteBatch.DrawString` sem filtro: ~3000 textos por quadro na
  tela de inventário, `3000 × 0,6 ≈ 1,8 ms`. **Sente-se** no jogo.
- Varrer `Main.npc` (201 posições) lendo 3 campos de cada: `201 × (0,064 + 3
  × 0,035) ≈ 34 µs`. Uma vez por quadro, tudo bem. **Dentro** de um hook de
  `NPC.AI` (uma varredura por NPC), são 200 varreduras: ~7 ms por quadro.

A última é o erro mais comum: um laço que é barato sozinho, dentro de um hook
que roda para cada entidade, vira quadrático.

## As classes de mod já são econômicas

Os métodos de `ModItem`, `ModNPC`, `ModProjectile`, `ModTile`... só custam
alguma coisa se a sua classe os **escreve**:

- o hook do jogo por trás de cada método só é **instalado** quando alguma
  classe registrada sobrescreve aquele método;
- ele é instalado **uma vez**, para todos os mods;
- quase todos têm **filtro nativo de tipo**: um `NPC.AI` de um NPC do jogo nem
  entra no JS. Bater em terra não passa pelo `ModTile`. Um item do jogo no
  inventário não passa pelo `UpdateInventory`.

Então escrever um `AI(npc)` no seu `ModNPC` custa só para os **seus** NPCs. Se
o que você quer fazer é com conteúdo seu, use a classe em vez de um hook
direto: ela já vem filtrada. A [referência das classes](../referencia/classes.md)
diz, para cada método, qual método do jogo está por trás e se há filtro.

## Filtros nativos

Quando você escreve um hook direto e só uma parte das chamadas interessa, o
filtro nativo decide **antes** do JS, sem trava, sem wrapper, lendo a memória
do objeto direto. Quem não passa custa quase nada (uma leitura de memória).

### `minType`: só entidades de um tipo em diante

```js
// só NPCs de mod (o tipo deles começa depois dos do jogo)
Terraria.NPC['void AI()'].hook(callback, { minType: bl.npcs.vanillaCount });

// só projéteis de mod
Terraria.Projectile['void AI()'].hook(callback, { minType: bl.projectiles.vanillaCount });
```

`minType` compara o campo `type` do `self`. Com `on`, o filtro olha um
**parâmetro** (pelo índice, a partir de 0) em vez do `self`; com `field`, outro
campo `int`:

```js
// ItemCheck_Shoot(int i, Item sItem, ...): o item é o parâmetro 1
Terraria.Player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)']
    .hook(callback, { minType: bl.items.vanillaCount, on: 1 });

// o gancho de escalar: interessa o item.shoot, não o item.type
Terraria.Player['void FireGrapple(Item grappleItem)']
    .hook(callback, { minType: bl.projectiles.vanillaCount, on: 0, field: 'shoot' });
```

Para blocos, o tipo vem do **mundo**: `tile: i` (o parâmetro `i` é um `Tile`) ou
`tileAt: [i, j]` (os parâmetros `i` e `j` são a posição):

```js
Terraria.WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)']
    .hook(callback, { minType: bl.tiles.vanillaCount, tileAt: [0, 1] });
```

### `whileIn`: só dentro de outro hook

Para um método chamado milhares de vezes, mas que só interessa num momento: o
callback só roda enquanto a thread está **dentro do hook de outro método**
(que precisa ter um hook JS antes).

```js
const tooltip = Terraria.Main['void MouseText_DrawItemTooltip(Main.MouseTextCache info, int rare, byte diff, int X, int Y)'];
tooltip.hook((original) => original());

const drawString = Microsoft.Xna.Framework.Graphics.SpriteBatch['void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'];
drawString.hook(callback, { whileIn: tooltip });   // só os textos do tooltip
```

É assim que o tooltip colorido do Bunny Loader mexe no `DrawString` sem pagar
os outros milhares de textos da tela.

## Quando o motor está ocupado: `ifBusy`

O motor JS é um só, e às vezes outra thread do jogo está com ele (o save do
personagem rodando o `SaveData` de um `ModPlayer`, a geração de mundo criando
itens). Um hook que chega nessa hora **espera**, até 3 s; se não conseguir,
roda o método sem o mod e avisa no log uma vez.

Para hook de **todo quadro** que pode ficar **um quadro sem o mod**, não
esperar é melhor:

```js
// um fade: se pular um quadro, ninguém vê
Terraria.Main['void UpdateAudio()'].hook(fade, { ifBusy: 'original' });

// uma decisão que o quadro anterior já tomou (só em método void)
Terraria.Main['void UpdateAudio_DecideOnNewMusic()'].hook(decide, { ifBusy: 'skip' });
```

| `ifBusy` | Com o motor ocupado |
|---|---|
| `'wait'` (padrão) | Espera até 3 s; depois roda sem o mod. |
| `'original'` | Roda só o método do jogo, na hora. |
| `'skip'` | Não roda nada (só `void`): fica o que o quadro anterior fez. |

Não use `ifBusy` em hook que **tem** de rodar (um save, um dano, uma
receita): perder uma chamada ali é um bug. O funcionamento por dentro está em
[threads e o motor JS](../nucleo/threads-e-motor-js.md#quando-o-motor-está-ocupado-ifbusy).

## Receitas para um mod leve

**1. Pegue métodos e classes uma vez, fora do callback.**

```js
// bom: resolvido uma vez
const Main = Terraria.Main;
const GetDamage = Terraria.Player['int GetWeaponDamage(Item sItem)'];
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    const d = GetDamage(self, self.inventory[self.selectedItem]);
});

// pior: busca a assinatura a cada quadro (~60% mais caro)
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    const d = self['int GetWeaponDamage(Item sItem)'](self.inventory[self.selectedItem]);
});
```

**2. Guarde a vista de struct numa variável.** Cada `npc.position` cria uma
vista (~180 ns). Lida duas vezes, são duas:

```js
const pos = npc.position;        // uma vista
if (pos.X > 100 && pos.Y < 500) { /* ... */ }
// em vez de: npc.position.X > 100 && npc.position.Y < 500
```

**3. Saia cedo.** O que vem antes do primeiro `return` é o que toda chamada
paga:

```js
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer) return;   // os outros jogadores param aqui
    // ...
});
```

**4. Não crie objetos por chamada** se der para criar um e reusar. `{ x, y }`
custa ~180 ns; um `Vector2.new(x, y)` do jogo, mais.

**5. Nada de laço por entidade dentro de hook por entidade.** Se cada NPC
precisa saber "o jogador mais perto", calcule uma vez por quadro (num hook de
`Main.DoUpdate` ou `Player.Update`) e guarde.

**6. Espalhe trabalho pesado.** Varrer o mundo inteiro (8400 × 2400 tiles num
mundo grande) não cabe num quadro. Faça um pedaço por quadro, ou faça no
`PostSetupContent`, na carga.

**7. Nunca `bl.log` a cada quadro.** Cada linha vai para um arquivo e para o
logcat. Um log por quadro enche o arquivo e custa escrita em disco. Para
depurar, logue uma vez a cada N quadros, ou só quando algo muda.

**8. Um erro repetido também custa.** Um erro num hook de todo quadro é
logado **a cada chamada** (nos métodos das classes de mod, uma vez só). Além
do log, o despachante roda o original pelo callback quebrado: o jogo segue,
mas mais lento.

**9. Um hook instalado e não chamado não custa nada.** Não precisa economizar
hooks em métodos que o jogo raramente chama (`Player.KillMe`, `WorldGen.KillTile`,
um evento de UI). Economize nos de todo quadro.

## Medindo o seu mod

`performance.now()` dá o tempo em milissegundos, com fração:

```js
let total = 0, n = 0;
Terraria.NPC['void AI()'].hook((original, npc) => {
    const t0 = performance.now();
    original(npc);
    // ... o seu código ...
    total += performance.now() - t0;
    if (++n === 6000) {             // ~100 quadros com 60 NPCs
        bl.log('AI: media ' + (total / n * 1000).toFixed(1) + ' us por chamada');
        total = 0; n = 0;
    }
});
```

Para medir a ponte em si, o mod [`tools/bench`](../../tools/bench) roda cada
operação 20 mil vezes, pega o melhor de 7 e desconta o laço vazio; os
resultados deste guia saíram dele. O emulador varia muito entre rodadas (até
70%): compare sempre **alternando** as versões (antes, depois, antes, depois),
nunca uma rodada contra outra de outro dia.

## Resumo

| Situação | O que fazer |
|---|---|
| Conteúdo seu (item, NPC, projétil) | Use a classe de mod: já vem filtrada. |
| Hook direto em método por entidade | `minType` (e `on`/`field`) se só os de mod interessam. |
| Método chamado milhares de vezes | `whileIn`, ou não hooke. |
| Hook de todo quadro que pode pular um | `ifBusy: 'original'` ou `'skip'`. |
| Dentro do callback | Saia cedo, guarde métodos e vistas, não aloque, não logue. |
| Trabalho grande | Um pedaço por quadro, ou na carga. |
