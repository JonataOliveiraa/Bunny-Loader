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

Os mods carregam na ordem da aba Pacotes do launcher, que cada jogador muda,
então não dá para saber se o outro mod roda antes ou depois do seu (a não ser
que ele esteja no seu [`dependencies`](#dependências)). Por isso:

- o `Mod` de todo mod instalado **existe desde o começo**: o `TryGetMod` acha o
  outro mesmo que ele ainda não tenha carregado, e o objeto é o mesmo depois
  (pode guardar);
- o `Call` só é garantido **do `PostSetupContent` em diante**, quando todos os
  mods já carregaram. Na carga (no topo de um arquivo ou no `Load()`), chamar
  um mod que ainda não carregou lança `Mod 'x' ainda nao carregou: chame o Call a partir do
  PostSetupContent`, em vez de responder nada calado.

O `PostSetupContent` pode ser o de um `ModSystem` (como acima) ou o do seu
`Mod`.

## Dependências

Um mod sem o qual o seu não funciona entra no `dependencies` do manifesto,
pelo `id` ou pelo `uid`:

```json
"dependencies": ["wikithis", "7c2e9d41-5b3a-4f8e-9a61-2d0c4e8b7f15"]
```

- Cada dependência **carrega antes** do seu mod, seja qual for a ordem da aba
  Pacotes. O resto fica na ordem do launcher. Com isso, o `Call` de uma
  dependência já vale na carga do seu mod (no topo dos arquivos e no
  `Load()`).
- Se uma dependência não está ligada, o seu mod **não carrega**, e o painel de
  erro diz qual faltou (`requires 'wikithis', which is not enabled`). O mesmo
  vale quando a dependência falha ao carregar e quando dois mods dependem um
  do outro (`dependency cycle: a -> b -> a`). Os outros mods carregam
  normalmente.
- Pelo `id`, a dependência precisa ser única entre os mods ligados; se dois
  usam o mesmo `id`, peça pelo `uid`.
- Compatibilidade opcional não vai aqui: use o `TryGetMod` no
  `PostSetupContent`, como acima.

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

| `modReferences = ModA, ModB@1.0` no `build.txt` | `"dependencies": ["moda", "modb"]` no manifesto (sem versão mínima) |

Não há `Unload` nem `weakReferences`/`sortBefore`/`sortAfter`.

## Referência rápida

| | |
|---|---|
| `ModLoader.TryGetMod(id, ref)` | `true` e o `Mod` em `ref.value`; ou `false` e `null` |
| `ModLoader.GetMod(id)` | o `Mod`; lança se não está |
| `ModLoader.HasMod(id)` | está instalado (e não falhou ao carregar)? |
| `ModLoader.Mods` | todos, na ordem de carga |
| `export default class X extends Mod { Call(...args) {} }` | oferecer um `Call` (a classe do mod, uma por pacote) |
| `mod.Call(...)` | chamar; do `PostSetupContent` em diante (de uma dependência, já na carga) |
| `"dependencies": ["id"]` | carregar depois desse mod; sem ele, não carregar |
| `bl.mod` | o `Mod` de quem pergunta |

O teste `tools/tests/crossmod` (com o par `tools/tests/crossmodtarget`) cobre
cada caso deste guia: o mod que ainda não carregou, o ausente, o mesmo objeto
antes e depois da carga, objeto e função pelo `Call`, erro que sobe, `bl.mod`
dentro do `Call` e a chamada na direção contrária, durante a carga.
