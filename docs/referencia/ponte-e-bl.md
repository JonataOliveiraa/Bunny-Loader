# Referência: a ponte e o `bl`

Tudo o que um mod usa para falar com o jogo, numa página. Explicado com calma
no [guia 1](../mods/01-hooks-do-zero.md); as classes de mod estão em
[classes](classes.md).

## A ponte

| Escrita | O que é |
|---|---|
| `Terraria.X.Y` | Classe, pela árvore de namespaces (`Terraria.ID.ItemID`, `Microsoft.Xna.Framework.Vector2`). |
| `GUIBuffs`, `GUIInstance` | Classe sem namespace: global. |
| `Classe.Aninhada` | Classe aninhada (`SpriteFont.Glyph`, `Player.Hooks`). |
| `bl.classOf(ns, nome)` | Classe, quando a árvore não chega (`bl.classOf('', 'GUIBuffs')`). |
| `Classe.campo`, `Classe.campo = v` | Campo ou propriedade **estática**. |
| `obj.campo`, `obj.campo = v` | Campo ou propriedade de **instância**. O tipo do campo decide a conversão. |
| `obj.position.X = 1` | Struct dentro de objeto é **vista**: escrever muda o jogo. |
| `proj.ai[0]`, `proj.oldPos[3].X` | Struct de campos numerados aceita índice. |
| `arr[i]`, `arr.length` | Array do jogo. |
| `arr.cloneResized(n)` | Cópia do array com `n` posições. |
| `Classe['ret Nome(T a, U b)']` | Método, pela **assinatura** do dump. |
| `Classe.Nome`, `obj.Nome` | Método, se houver **um só** com esse nome. |
| `metodo(args)`, `obj['...'](args)` | Chamar. Em método de instância, o objeto é o `this` ou o 1º argumento. |
| `Classe.new()` + `['void .ctor(...)']()` | Criar um objeto (memória zerada, depois o construtor). |
| `Classe.makeGeneric(T, ...)` | Classe genérica: `List.makeGeneric(Vector2)`. |
| `null` | Qualquer objeto; também o `null` de um `Nullable<T>` (`float?`). |
| `new Ref(v)`, `r.value` | Parâmetro `ref`/`out` ([guia 2](../mods/02-ref-e-out.md)). |
| `metodo.hook(cb, opções?)` | Hook ([guia 1](../mods/01-hooks-do-zero.md#hooks)). |

### Assinaturas

Copiadas do dump como estão, com os nomes de tipo do C#: `void`, `bool`,
`int`, `float`, `double`, `string`, `long`, `byte`, `short`, nomes de classe
sem namespace (`Item`, `Vector2`), arrays (`int[]`), genéricos (`List<Item>`),
`Nullable` em qualquer grafia (`float?`, `Nullable<float>`, ``Nullable`1``) e
`ref`/`out`/`in` antes do tipo. Construtor é `.ctor`.

```js
'void SetDefaults(int Type, ItemVariant variant)'
'bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'
'void StartRain(bool instant, float? strengthOverride, bool garenteeCoinRain)'
'void .ctor(float x, float y)'
```

### Opções do `hook`

| Opção | Efeito |
|---|---|
| `minType: N` | Só entra no JS se o `type` do `self` for ≥ `N`. |
| `on: i` | O filtro olha o parâmetro `i` (a partir de 0), não o `self`. |
| `field: 'nome'` | O filtro olha outro campo `int`. |
| `tile: i` | O parâmetro `i` é um `Tile`; o filtro olha o tipo do bloco. |
| `tileAt: [i, j]` | Os parâmetros `i` e `j` são a posição; o filtro olha o bloco do mundo. |
| `whileIn: metodo` | Só entra no JS dentro do hook JS de `metodo`. |
| `ifBusy: 'wait' \| 'original' \| 'skip'` | Com o motor JS noutra thread: esperar até 3 s, rodar só o jogo, ou nada. |

Detalhes no [guia de custo](../mods/03-custo-e-desempenho.md#filtros-nativos).

## `bl`: geral

| | |
|---|---|
| `bl.log(...valores)` | Uma linha no log do jogo e no logcat. Objeto e array JS saem em JSON. |
| `bl.mod` | O `Mod` de quem chama: `id`, `name`, `version`, `uuid`, `path`, `root`, `dataDirectory`. |
| `bl.info.appDirectory` | `Android/data/com.bunnyloader` (onde ficam `Players/`, `Worlds/`). |
| `bl.info.logsDirectory` | A pasta dos logs. |
| `bl.info.terrariaVersionCode` | A versão do jogo (301543 = 1.4.5.6.4). |
| `bl.onContentReady(fn)` | Chama `fn` quando todo o conteúdo de mod está no jogo (o mesmo momento do `PostSetupContent`). |

## `bl`: arquivos

Caminhos relativos são da pasta do `main.js` do mod que chama; absolutos valem
como estão. Para dados que o mod grava, use `bl.mod.dataDirectory`: a pasta do
pacote é trocada inteira quando o mod é atualizado.

| | |
|---|---|
| `bl.readJson(caminho)` | O JSON lido, ou `undefined`. |
| `bl.file.exists(caminho)` | |
| `bl.file.read(caminho)` | Texto, ou `undefined`. |
| `bl.file.readBytes(caminho)` | `Uint8Array`, ou `undefined`. |
| `bl.file.write(caminho, texto \| bytes)` | Cria as pastas do caminho. |
| `bl.file.append(caminho, texto \| bytes)` | |
| `bl.file.delete(caminho)` | `true` se apagou. |
| `bl.directory.exists(caminho)` | |
| `bl.directory.create(caminho)` | Com as pastas do meio. |
| `bl.directory.delete(caminho)` | E tudo dentro. |
| `bl.directory.listFiles(caminho)` | Os arquivos (caminhos relativos). |
| `bl.directory.listDirectories(caminho)` | As subpastas. |
| `bl.path.join(a, b, ...)` | |
| `bl.path.getName(p)`, `getParentPath(p)`, `getExtension(p)` | |

## `bl`: texturas

Só na **thread do jogo** (num hook, no `SetStaticDefaults`, no
`PostSetupContent`), nunca no topo do `main.js`.

| | |
|---|---|
| `bl.loadTexture(caminho)` | PNG/JPG do mod → `Texture2D` do jogo, nova a cada chamada. |
| `bl.loadTextureAsset(caminho)` | PNG/JPG do mod → `Asset<Texture2D>` já carregado (o tipo das tabelas `TextureAssets`). |
| `ModContent.Request(caminho)`, `ModContent.Texture(caminho)` | O mesmo, carregado **uma vez** por arquivo ([guia 4](../mods/04-conteudo-novo.md#modcontent)). |

## `bl`: campos e objetos

| | |
|---|---|
| `bl.defineField(Classe, 'nome')` | Um campo novo em toda instância da classe (e das filhas), guardado ao lado do objeto. |
| `bl.defineMethod(Classe, 'nome', function () {...})` | Um método novo; `this` é o objeto do jogo. |
| `bl.addressOf(obj)` | O endereço do objeto (número). |
| `bl.objectAt(endereco)` | O objeto de volta. Só para quem sabe que ele ainda existe. |

## `bl`: conteúdo (baixo nível)

As classes de mod usam estes por baixo; um mod raramente precisa deles
diretamente.

| | |
|---|---|
| `bl.items.vanillaCount`, `bl.projectiles.vanillaCount`, `bl.npcs.vanillaCount`, `bl.buffs.vanillaCount`, `bl.tiles.vanillaCount` | O primeiro tipo de mod de cada conteúdo (6147, 1111, 697, 389, 753). Útil no `minType`. |
| `bl.items.isModItem(t)`, `bl.projectiles.isModProjectile(t)`, `bl.npcs.isModNpc(t)`, `bl.buffs.isModBuff(t)`, `bl.tiles.isModTile(t)` | É de mod? |
| `bl.items.typeOf('Classe')` (e os outros) | O tipo de um conteúdo **deste** mod pelo nome; -1 se não há. |
| `bl.items.modItemsIn(player)` | Os itens de mod nos 58 espaços do inventário do jogador. |
| `bl.npcs.freeSlot()` | O próximo slot livre de `Main.npc`. |
| `bl.tiles.typeAt(x, y)` | O tipo do tile ativo numa posição (-1 se não há). |
| `bl.items.register`, `bl.npcs.register`... | O registro nativo. Use as classes (`ModItem.register`). |

## `bl.menu`: o Mod Menu

| | |
|---|---|
| `bl.menu.itemCategory(nome, icone)` | Uma pasta de itens na entrada do mod. `icone`: caminho de PNG do mod. |
| `bl.menu.npcCategory(nome, icone)` | Uma pasta de NPCs. |
| `bl.menu.addItem(pasta, tipo)`, `bl.menu.addNpc(pasta, tipo)` | Põe na pasta. |
| `bl.menu.hide('item' \| 'npc' \| 'buff', tipo)` | Tira do Mod Menu (o mesmo que `HideFromModMenu`). |
| `bl.menu.isHidden('item' \| 'npc' \| 'buff', tipo)` | |

## Globais do JavaScript

Além do padrão do JavaScript (ES2020):

| | |
|---|---|
| `performance.now()` | Tempo em ms, com fração: para medir. |
| `import`/`export` | Entre arquivos do mesmo mod, com caminho relativo. |
| `Terraria`, `Microsoft`, `System`, `ReLogic`... | As raízes de namespace do jogo. |
