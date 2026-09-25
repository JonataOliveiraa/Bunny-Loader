# 3. `ref` e `out`

Muitos métodos do jogo devolvem resultado por um parâmetro `ref` ou `out`: a
rolagem da pesca, a taxa de spawn, a colisão ao subir degrau. No Bunny Loader
dá para **chamar** esses métodos e **hookar** esses métodos, lendo e mudando o
parâmetro. Este guia mostra como.

Pré-requisito: o [guia 1](01-hooks-do-zero.md), até *Hooks*.

## O que são

Em C#, um argumento comum é uma **cópia**: o método mexe nela e quem chamou não
vê. Com `ref` ou `out`, o método recebe **a própria variável de quem chamou**, e
o que ele escrever ali fica valendo.

```csharp
int tempo;
Main.TryGetBuffTime(0, out tempo);       // o método escreve em `tempo`

Collision.StepUp(ref position, ref velocity, ...);   // pode mudar os dois
```

- **`out`**: um retorno a mais. O método escreve; o valor de entrada não importa.
- **`ref`**: entra com um valor e pode sair mudado.
- **`in`**: entra, só para leitura. Raro no jogo.

É por esses parâmetros que muitos pontos bons para mod passam. Mudar um `out`
num hook é o equivalente ao `ref int damage` do tModLoader.

## O `Ref`

No JS, um parâmetro `ref`/`out` é um objeto **`Ref`**, com o valor em
`.value`. `Ref` é global, como `Vector2`.

```js
const r = new Ref();      // vazio: para `out`
const s = new Ref(10);    // com valor: para `ref`
r.value = 5;
bl.log(r.value, String(r));   // 5 "Ref(5)"
```

Na **assinatura**, escreva o parâmetro como no dump. `out int`, `ref Vector2`
e `in T` valem:

```js
'bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'
```

## Chamando um método com `ref`/`out`

Passe um `Ref` no lugar do parâmetro. Depois da chamada, `.value` tem o que o
método deixou lá:

```js
const Main = Terraria.Main;
const tempo = new Ref();
if (Main['bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'](0, tempo)) {
    bl.log('o primeiro buff ainda dura', tempo.value, 'quadros');
}
```

Vários `out` de uma vez, cada um no seu `Ref`:

```js
const nomes = ['common', 'uncommon', 'rare', 'veryrare', 'legendary', 'crate'];
const outs = nomes.map(() => new Ref());
const bobber = Terraria.Projectile.new();
bobber['void .ctor()']();
bobber['void FishingCheck_RollDropLevels(int fishingLevel, out bool common, out bool uncommon, out bool rare, out bool veryrare, out bool legendary, out bool crate)'](30, ...outs);
nomes.forEach((n, i) => bl.log(n, outs[i].value));
```

Passar um número onde o método quer `ref`/`out` dá erro, e a mensagem pede o
`new Ref(...)`. Um `Ref` vazio entra num `out` como zero.

## Hookando um método com `ref`/`out`

No hook, o parâmetro chega como um `Ref` **preso à variável de quem chamou**:

- ler `.value` lê a variável do jogo;
- escrever em `.value` muda o que o jogo vai usar depois;
- passe o `Ref` para o `original` como veio. Ele recebe a variável verdadeira.

Um `out` só tem valor **depois** do `original`, como em C#: antes, é o que
sobrou naquela memória.

### Mudar um resultado: mais caixotes na pesca

O jogo rola o que a isca vai pescar num método com seis `out bool`. Depois do
original, basta trocar o `crate`:

```js
Terraria.Projectile['void FishingCheck_RollDropLevels(int fishingLevel, out bool common, out bool uncommon, out bool rare, out bool veryrare, out bool legendary, out bool crate)'].hook(
    (original, self, level, common, uncommon, rare, veryrare, legendary, crate) => {
        original(self, level, common, uncommon, rare, veryrare, legendary, crate);
        if (!crate.value && Math.random() < 0.10) crate.value = true;
    });
```

`self` é o projétil da boia. O dono é `Terraria.Main.player[self.owner]`, e dá
para exigir um buff ou um acessório dele antes de mexer.

### Ler e mudar dois retornos: taxa de spawn

O jogo chama este método sozinho, todo quadro, para decidir os spawns perto de
cada jogador. `spawnRate` é o intervalo entre spawns (menor = mais inimigos), e
`maxSpawns` é o limite de inimigos perto do jogador:

```js
Terraria.NPC.Spawner['void GetSpawnRate(Player player, out int spawnRate, out int maxSpawns)'].hook(
    (original, self, player, spawnRate, maxSpawns) => {
        original(self, player, spawnRate, maxSpawns);
        spawnRate.value = Math.floor(spawnRate.value / 2);
        maxSpawns.value *= 2;
    });
```

No multijogador, quem faz os spawns é o host: o hook vale no aparelho de quem
hospeda.

### Mudar a entrada

Com `ref`, o valor de entrada importa. Escreva no `Ref` **antes** do
`original`, e o método do jogo já roda com o valor novo. Depois do `original`,
você muda o que ele devolve.

### O hook vê as chamadas do mod

Um hook também pega a chamada que o **próprio mod** fizer: ela passa pelo hook
como qualquer outra. Com este hook, o `TryGetBuffTime` do começo do guia
responde um segundo a mais:

```js
Terraria.Main['bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'].hook((original, slot, tempo) => {
    const r = original(slot, tempo);
    tempo.value += 60;   // quem perguntou vê um segundo a mais
    return r;
});
```

### Um caso real: o tooltip

O tooltip do celular sai de um método com seis `ref`:
`Main.MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int
researchLine, ref int materialsLine, float oldKB, ref int numLines, string[]
toolTipLine, ...)`. Ele acrescenta as linhas ao `toolTipLine` e devolve quantas
são em `numLines`. Hookando, dá para inserir uma linha logo abaixo do nome:

```js
Terraria.Main['void MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, ref int setBonusLine, ref Color setBonusColour)'].hook(
    (original, item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor) => {
        original(item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor);
        const n = numLines.value;
        for (let i = n; i > 1; i--) { lines[i] = lines[i - 1]; pre[i] = pre[i - 1]; bad[i] = bad[i - 1]; }
        lines[1] = 'Logo abaixo do nome';
        pre[1] = bad[1] = false;
        numLines.value = n + 1;
        for (const r of [yoyo, research, materials, setBonus]) if (r.value >= 1) r.value += 1;
    });
```

Na prática, um item de mod não precisa disso: o `ModifyTooltips` do `ModItem`
já é esse hook, com cor por linha e por trecho (guia 2, *Tooltip colorido*).

## Struct por `ref`

Com struct (`ref Vector2`, `ref FishingAttempt`), `.value` devolve uma
**cópia**. Mude a cópia e escreva de volta:

```js
Terraria.Projectile['void FishingCheck_ProbeForQuestFish(ref FishingAttempt fisher)'].hook((original, self, fisher) => {
    const a = fisher.value;       // cópia
    a.crate = true;
    fisher.value = a;             // grava no jogo
    original(self, fisher);
});
```

Mexer em `fisher.value.crate` direto **não** muda o jogo: muda uma cópia que
some em seguida. É o mesmo cuidado de um struct devolvido por método em C#.

Para chamar um método que quer `ref` de struct, comece o `Ref` com um struct:

```js
const a = Terraria.DataStructures.FishingAttempt.new();
a.fishingLevel = 25;
const fisher = new Ref(a);
bobber['void FishingCheck_ProbeForQuestFish(ref FishingAttempt fisher)'](fisher);
bl.log(fisher.value.questFish);
```

## Por que o `Ref` do hook "se solta"

A variável de quem chamou vive na **pilha** dele. Quando o método volta, aquele
endereço vira lixo e é reusado por outra chamada. Um mod que guardasse o
ponteiro e escrevesse nele depois corromperia a memória do jogo, e o jogo
fecharia sem explicação.

Por isso, quando o callback do hook termina, o `Ref` **se solta**: guarda o
último valor e esquece o endereço. Se você o guardou numa variável, ele continua
legível, e escrever nele não toca mais o jogo.

```js
let ultimo = null;
metodo.hook((original, x) => { original(x); ultimo = x; });
// depois: ultimo.value é o valor do fim daquela chamada; ultimo.value = 1 não faz nada no jogo
```

Para mudar o jogo, faça isso **dentro** do hook.

## Limites

- **Chamar** com `ref`/`out` funciona pelo caminho direto da ponte, que é o de
  quase todo método. Um método que cai no `runtime_invoke` (sem código próprio,
  ou com float demais indo pela pilha) recusa o `ref`, com mensagem.
- Cada `ref`/`out` conta como um argumento inteiro (é um ponteiro) nos limites
  de hook: até 16 inteiros contando `this`, e 8 de ponto flutuante. O
  `RollDropLevels`, com 9, cabe.
- `.value` de struct é sempre cópia: nada de vista direta.

## Referência rápida

| | |
|---|---|
| `new Ref()` / `new Ref(v)` | variável para `out` / para `ref` |
| `r.value`, `r.value = v` | ler, escrever |
| `'... (out int x)'`, `'... (ref Vector2 v)'` | assinatura com `ref`/`out`/`in` |
| no hook, o parâmetro | `Ref` preso à variável do jogo, até o callback voltar |
| struct por `ref` | `.value` é cópia; escreva de volta com `.value = copia` |

O teste `tools/tests/refs` cobre cada caso deste guia: `out` numa chamada,
hook mudando `out`, os seis `out bool` da pesca, `ref FishingAttempt`, o
`GetSpawnRate` chamado pelo jogo, `ref Vector2` no `Collision.StepUp` e o `Ref`
depois do hook.
