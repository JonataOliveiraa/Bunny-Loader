# 1. Um mod do zero, com hooks

Este guia faz um mod que muda o que o jogo **já tem**, e no caminho mostra a
ponte entre o JavaScript e o Terraria. Para criar item, NPC e projétil novos,
ver o [guia 2](02-moditem-modnpc.md) — ele usa tudo daqui.

## O primeiro mod: dano em dobro

Crie esta pasta:

```
DanoEmDobro/
  manifest.json
  content/
    main.js
```

`manifest.json` (gere um `uid` seu — ver o [README](README.md#manifestjson)):

```json
{
  "uid": "0b8f2a64-3c1d-4e7a-9f50-6d2e8c1b4a73",
  "id": "danoemdobro",
  "name": "Dano em Dobro",
  "version": "1.0.0",
  "author": "Você",
  "category": "Armas",
  "summary": "Toda arma bate o dobro.",
  "blVersion": 1,
  "entry": "main.js"
}
```

`content/main.js`:

```js
const SetDefaults = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'];

SetDefaults.hook((original, self, type, variant) => {
    original(self, type, variant);   // o jogo preenche o item primeiro
    if (self.damage > 0) {
        self.damage = self.damage * 2;
    }
});

bl.log('Dano em Dobro: ativo');
```

Instale (ver o [README](README.md#instalando)), abra o jogo e toda arma nasce com
o dobro do dano. O log (`Android/data/com.bunnyloader/logs/`, ou
`adb logcat -s BunnyLoader`) mostra `Dano em Dobro: ativo`.

As quatro linhas do hook são a anatomia de quase todo mod:

1. **pegar o método** do jogo pelo nome da classe e pela assinatura;
2. **pendurar um hook** nele;
3. **chamar o original**, para o jogo fazer o que já fazia;
4. **mexer** no resultado.

`Item.SetDefaults` é onde o Terraria preenche um item novo — dano, cadência,
tamanho da pilha. Ele roda uma vez por item criado, então mudar aqui vale para
tudo: o que você fabrica, o que cai de inimigo, o que já está no baú.

## Achando classes e métodos

O jogo é C# compilado para código nativo (IL2CPP). Os nomes de classe, campo e
método são os do C#, e você os vê no **dump** do jogo: `refs/dump.cs`, gerado por
`tools/dump.sh` a partir do jogo instalado (ver `refs/README.md`). Para ler uma
classe:

```bash
tools/dumpgrep.sh Player Terraria
```

As classes ficam na árvore de namespaces do C#:

```js
Terraria.Player
Terraria.Item
Terraria.ID.ItemID
Microsoft.Xna.Framework.Vector2
Terraria.GameContent.ItemDropRules.ItemDropRule
```

Classe **sem namespace** (a interface do celular: `GUIBuffs`, `GUIInstance`,
`GUIChest`...) é global, pelo nome: `GUIBuffs['void Draw()']`.

Quando a árvore não chega numa classe (nome estranho, classe gerada), use
`bl.classOf('Terraria', 'Player')` (sem namespace: `bl.classOf('', 'GUIBuffs')`).

Classe aninhada é propriedade da de fora: `SpriteFont.Glyph`.

## Campos

Campo é propriedade. Estático na classe, de instância no objeto:

```js
Terraria.Main.dayTime                  // estático
Terraria.ID.ItemID.Minishark           // constante: 98

const p = Terraria.Main.player[Terraria.Main.myPlayer];
p.statLife = p.statLifeMax2;           // de instância
```

Propriedade C# (`get_X`/`set_X`) também vira propriedade:
`Terraria.Main.myPlayer` chama `get_myPlayer()`.

O tipo do campo decide a escrita: `item.shootSpeed = 10` grava um `float`,
`item.useTime = 4` um `int`. Enum sai como **número** (`Terraria.PartyHatColor.Pink`
é `2`).

Array é array: `Terraria.Main.player[0]`, `Terraria.Main.npc.length`.

### Structs são vistas, não cópias

`Vector2`, `Color`, `Rectangle` dentro de um objeto são uma **vista** para
dentro dele. Escrever nela altera o jogo:

```js
npc.position.X = 100;          // move o NPC
item.color.R = 255;
npc.position = outroVector2;   // troca o struct inteiro
bl.log(npc.position);          // Vector2(X=100, Y=42)
```

Duas exceções, que são a semântica de valor do C#: o struct que um **método
devolve** e o que chega como **argumento de um hook** são cópias. Escrever
neles não muda nada no jogo.

## Métodos

Pela **assinatura**, copiada do dump como está lá:

```js
const Update = Terraria.Player['void Update(int i)'];
Terraria.Main['int get_myPlayer()']();
item['void SetDefaults(int Type, ItemVariant variant)'](98, null);
```

O objeto é o `this`, como em qualquer método JS. Se o método tem um só
overload, o nome basta: `Terraria.Recipe.UpdateWhichItemsAreCrafted()`. Com mais
de um, o nome sozinho é **recusado**, com a lista dos overloads na mensagem —
nunca é escolhido um ao acaso.

`null` vale para qualquer objeto. Nullable (`float?`, `Rectangle?`) é `null` ou o
valor, e na assinatura vale qualquer grafia (`float?`, `Nullable<float>`,
``Nullable`1``):

```js
Terraria.Main['void StartRain(bool instant, float? strengthOverride, bool garenteeCoinRain)'](true, null, false);
```

Método do jogo que lança exceção vira exceção JS.

### Criando objetos

`new Item()` do C# é `.new()` e depois o construtor:

```js
const it = Terraria.Item.new();
it['void .ctor()']();

const v = Microsoft.Xna.Framework.Vector2.new();
v['void .ctor(float x, float y)'](5, 10);
```

## Hooks

`.hook(callback)` intercepta o método. O callback recebe `original`, o objeto
(`self`, se o método não for estático) e os argumentos:

```js
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    // depois do jogo
});
```

**Chamar ou não o original** é escolha sua:

- chamar e mexer depois — o mais comum;
- mexer e chamar depois — para mudar a entrada;
- não chamar — o método do jogo não roda (bloquear um efeito, por exemplo).

`original` aceita **argumentos novos**; o que você não passar fica como veio:

```js
Dano.hook((original, self, dano) => original(self, dano * 2));
```

O que o callback **devolve** vira o retorno do método — número, bool, objeto ou
struct pequeno:

```js
Distancia.hook((original, a, b) => original(a, b) * 10);
```

### Hook roda para todos

`Player.Update(int i)` é chamado para **cada** jogador do mundo. Sozinho, só
existe você; no multijogador, os outros também passam por ali. Para mexer só no
seu personagem:

```js
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Terraria.Main.myPlayer) return;
    if (self.statLife < self.statLifeMax2) self.statLife = self.statLifeMax2;
});
```

O mesmo vale para NPC e projétil: o hook pega **todos**, não só os seus. Confira
o `type` (ou o `whoAmI`, o `owner`) antes de mexer.

### Custo

Um hook num método de todo quadro (`Player.Update`, `NPC.AI`, `Projectile.Update`)
roda centenas de vezes por segundo. Faça pouco ali dentro, e não crie objetos a
cada chamada se der para criar uma vez fora.

Quando o hook só interessa para tipos novos (de mod), o filtro nativo evita
entrar no JS para os do jogo:

```js
metodo.hook(callback, { minType: bl.items.vanillaCount });
```

`minType` compara o campo `type` do objeto; só chama o callback a partir dele.

### O que ainda não dá

- Hookar método com mais de 8 argumentos inteiros ou 8 de ponto flutuante: o
  excedente viaja pela pilha, que o hook não captura. É recusado, com mensagem.
- Hookar método que devolve struct grande (mais de 16 bytes, ou mais de 4
  floats): também recusado, dizendo o tipo e o tamanho.

## Texturas e desenho

`bl.loadTexture('caminho.png')` carrega um PNG/JPG do mod como `Texture2D` do
jogo. O caminho é relativo a `content/`. Duas regras, que vêm da Unity:

1. só funciona com o jogo rodando, na thread dele — carregue **dentro de um
   hook**, na primeira chamada, nunca no topo do arquivo;
2. desenhar exige um `SpriteBatch` aberto.

```js
let tex = null;
Terraria.Main['void DrawInterface(GameTime gameTime)'].hook((original, self, gt) => {
    original(self, gt);
    if (!tex) tex = bl.loadTexture('meu.png');
    const sb = Terraria.Main.spriteBatch;
    sb['void Begin(SpriteSortMode sortMode, bool defferedBatch)'](0, true);
    sb['void Draw(Texture2D texture, Vector2 position, Color color)'](tex, posicao, cor);
    sb['void End()']();
});
```

## Vários arquivos

Um mod pode se dividir em arquivos com `import`/`export`, sempre com caminho
**relativo** e dentro do próprio mod:

```js
// content/util/dano.js
export function dobra(item) { item.damage *= 2; }

// content/main.js
import { dobra } from './util/dano.js';
```

`bl.readJson('config.json')` lê um JSON do mod (ou `undefined`, se não existe).

## Arquivos

Caminho relativo é relativo à pasta do `main.js`; absoluto vale como está.

```js
bl.mod.name            // "Dano em Dobro" (do manifest.json)
bl.mod.uuid            // o uid
bl.mod.path            // a pasta do main.js
bl.mod.root            // a pasta do pacote (a do manifest.json)
bl.mod.dataDirectory   // Android/data/com.bunnyloader/mod_data/<uid>

bl.info.appDirectory   // Android/data/com.bunnyloader (Players/, Worlds/...)
bl.info.logsDirectory
bl.info.terrariaVersionCode

bl.file.exists('config.json')
bl.file.read('config.json')              // texto, ou undefined
bl.file.readBytes('dados.bin')           // Uint8Array, ou undefined
bl.file.write(caminho, 'texto')          // ou Uint8Array; cria as pastas
bl.file.append(caminho, 'mais texto')
bl.file.delete(caminho)                  // true se apagou

bl.directory.exists('Textures')
bl.directory.create(caminho)             // com as pastas do meio
bl.directory.delete(caminho)             // e tudo dentro
bl.directory.listFiles('Textures/Bg')    // ['Textures/Bg/a.png', ...]
bl.directory.listDirectories('Textures')

bl.path.join('a', 'b', 'c.png')          // 'a/b/c.png'
bl.path.getName('x/y/z.png')             // 'z.png'
bl.path.getParentPath('x/y/z.png')       // 'x/y'
bl.path.getExtension('z.png')            // '.png'
```

Para guardar dados do mod (configuração, progresso), use `bl.mod.dataDirectory`:
a pasta do pacote é trocada inteira quando o mod é atualizado.

## Referência rápida

| | |
|---|---|
| `Terraria.X.Y` | classe, pela árvore de namespaces |
| `bl.classOf(ns, nome)` | classe, quando a árvore não ajuda |
| `obj.campo` / `obj.campo = v` | campo ou propriedade C# |
| `Classe['ret Nome(T a)']` | método, por assinatura |
| `Classe.Nome` | método, se houver um só overload |
| `Classe.new()` + `['void .ctor(...)']` | criar objeto |
| `metodo.hook((original, self, ...args) => ...)` | interceptar |
| `metodo.hook(cb, { minType })` | interceptar só a partir de um tipo |
| `bl.log(...)` | escreve em `logs/bunny_<data>.txt` e no logcat |
| `bl.loadTexture(caminho)` | PNG/JPG do mod → `Texture2D` |
| `bl.readJson(caminho)` | JSON do mod |
| `bl.file`, `bl.directory`, `bl.path` | ler e escrever arquivos (ver *Arquivos*) |
| `bl.mod`, `bl.info` | o mod que chama; a pasta do app |
| `GUIBuffs`, `GUIInstance`... | classe sem namespace, global |
| `import ... from './x.js'` | outro arquivo do mod |

O comentário do topo de `samples/HelloMod/content/main.js` tem esta mesma API
com mais exemplos, inclusive os casos de borda.
