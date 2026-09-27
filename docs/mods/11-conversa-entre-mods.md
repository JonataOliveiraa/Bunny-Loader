# 11. Conversa entre mods

Um mod pode achar outro mod instalado e chamar o que ele oferece. É assim que,
no PC, o Wikithis recebe o endereço da wiki de cada mod, e um mod de lista de
chefes aprende os chefes dos outros. O jeito é o do tModLoader:
`ModLoader.TryGetMod` acha o outro mod e `Call` chama o que ele oferece.

Pré-requisito: o [guia 4](04-conteudo-novo.md) e o `ModSystem` do [guia 5](05-itens.md#modsystem).

## Chamando outro mod

```js
// content/Common/Systems/WikiCompat.js
export class WikiCompat extends ModSystem {
    PostSetupContent() {
        const wikithis = new Ref();
        if (ModLoader.TryGetMod('wikithis', wikithis)) {
            wikithis.value.Call('AddModURL', bl.mod, 'https://examplemod.wiki.gg/wiki/{}');
        }
    }
}
```

- O nome é o **`id` do manifesto** do outro mod (não o `name`). O `uid` também
  serve.
- `TryGetMod(id, ref)` devolve `true` e põe o `Mod` em `ref.value`, ou devolve
  `false` e põe `null`. É o `out Mod` do C#, com o [`Ref`](02-ref-e-out.md).
- Sem o outro mod instalado, o `if` não entra e nada acontece. Por isso a
  compatibilidade fica opcional: quem não tem o Wikithis joga igual.

Para um mod sem o qual o seu não funciona, `ModLoader.GetMod(id)` devolve o
`Mod` direto e lança erro se ele não está. `ModLoader.HasMod(id)` só responde
se está. `ModLoader.Mods` é a lista de todos, na ordem de carga.

## Oferecendo um `Call`

O `Call` mora na classe do mod, a do `export default` do arquivo de entrada
(todo mod tem uma, e só uma):

```js
// content/main.js
const urls = new Map();

export default class Wikithis extends Mod {
    Call(command, ...args) {
        switch (command) {
            case 'AddModURL': {
                const [mod, domain] = args;
                if (!(mod instanceof Mod) || typeof domain !== 'string') {
                    throw new Error('AddModURL: espera (Mod, "https://...{}")');
                }
                urls.set(mod.id, domain);
                return true;
            }
            case 'GetURL':
                return urls.get(args[0]);
        }
        return undefined;
    }
}
```

- O costume, herdado do tModLoader, é o primeiro argumento dizer o comando.
- Todo valor passa **como está**: número, texto, objeto, função, classe, o
  próprio `Mod`. Todos os mods rodam no mesmo motor JS, então o `Call` é uma
  chamada de função comum. No tModLoader tudo vira `object[]` e o outro lado
  converte; aqui um objeto chega como o mesmo objeto, e uma função recebida
  pode ser chamada.
- Um erro lançado dentro do `Call` chega a quem chamou, como no tModLoader.
  Lance com uma mensagem que diga o que faltou.
- Um comando desconhecido devolve `undefined`. Um mod que não define `Call`
  também: é o `return null` do tModLoader.

Dentro do `Call`, `bl.mod` é o mod **chamado**, e caminhos relativos
(`bl.loadTexture`, `bl.file`) são da pasta dele. Quem chamou chega nos
argumentos, se ele se mandar, como no `AddModURL` acima.

## Quando chamar: no `PostSetupContent`

Os mods carregam em ordem de `uid`, que é sorteado, então não dá para saber se
o outro mod roda antes ou depois do seu. Por isso:

- o `Mod` de todo mod instalado **existe desde o começo**: o `TryGetMod` acha o
  outro mesmo que ele ainda não tenha carregado, e o objeto é o mesmo depois
  (pode guardar);
- o `Call` só é garantido **do `PostSetupContent` em diante**, quando todos os
  mods já carregaram. Na carga (no topo de um arquivo ou no `Load()`), chamar
  um mod que ainda não carregou lança `Mod 'x' ainda nao carregou: chame o Call a partir do
  PostSetupContent`, em vez de responder nada calado.

O `PostSetupContent` pode ser o de um `ModSystem` (como acima) ou o do seu
`Mod`.

## O `Mod`

A classe do `export default` do arquivo de entrada recebe, como o `ModSystem`:

| | |
|---|---|
| `Load()` | Na carga, depois do topo dos arquivos e do registro do conteúdo. |
| `AddRecipeGroups()`, `AddRecipes()` | Com os do `ModSystem`. |
| `PostSetupContent()` | Com o conteúdo de todos os mods pronto. |
| `Call(...args)` | Quando outro mod chama. |

E os dados do pacote, para ler:

| | |
|---|---|
| `id` | O `id` do manifesto: o nome pelo qual outros mods acham este. |
| `uuid` | O `uid` do manifesto. |
| `name`, `version` | Do manifesto, como texto (`"1.2.0"`). |
| `path`, `root` | A pasta do `main.js` e a do pacote. |
| `dataDirectory` | `Android/data/com.bunnyloader/mod_data/<uid>`. |

`bl.mod` é o `Mod` do mod que pergunta. No topo dos arquivos, antes de o
Bunny Loader criar a sua classe, ele é um `Mod` simples, com os mesmos dados;
do `Load()` em diante, é o mesmo objeto, agora da sua classe (e o `this` dela).

## Dois mods com o mesmo `id`

O `id` é um apelido, e o site não garante que ele é único. Se dois mods
instalados tiverem o mesmo, `TryGetMod` pelo `id` não escolhe: devolve `false`
e o log diz para pedir pelo `uid`, que é único.

## Diferenças para o tModLoader

| tModLoader | Bunny Loader |
|---|---|
| `ModLoader.TryGetMod("X", out Mod m)` | `ModLoader.TryGetMod('x', ref)`, com `ref.value` |
| `Mod.Name` | `mod.id` (o `id` do manifesto) |
| `Mod.DisplayName` | `mod.name` |
| `Mod.Version` (tipo `Version`) | `mod.version` (texto) |
| `public override object Call(params object[] args)` | `Call(...args)`, valores como estão |
| `public class X : Mod` | `export default class X extends Mod` no arquivo de entrada |
| `this` (o seu `Mod`) | `this` na classe do mod; `bl.mod` em qualquer arquivo |

Não há `Unload` nem referência entre mods no manifesto para
mudar a ordem de carga: o `dependencies` do manifesto ainda não ordena nada.

## Referência rápida

| | |
|---|---|
| `ModLoader.TryGetMod(id, ref)` | `true` e o `Mod` em `ref.value`; ou `false` e `null` |
| `ModLoader.GetMod(id)` | o `Mod`; lança se não está |
| `ModLoader.HasMod(id)` | está instalado (e não falhou ao carregar)? |
| `ModLoader.Mods` | todos, na ordem de carga |
| `export default class X extends Mod { Call(...args) {} }` | oferecer um `Call` (a classe do mod, uma por pacote) |
| `mod.Call(...)` | chamar; do `PostSetupContent` em diante |
| `bl.mod` | o `Mod` de quem pergunta |

O teste `tools/tests/crossmod` (com o par `tools/tests/crossmodtarget`) cobre
cada caso deste guia: o mod que ainda não carregou, o ausente, o mesmo objeto
antes e depois da carga, objeto e função pelo `Call`, erro que sobe, `bl.mod`
dentro do `Call` e a chamada na direção contrária, durante a carga.
