# A ponte JS ↔ jogo

> Parte da documentação do núcleo ([visão geral](README.md)). O lado de quem
> escreve mod está no [guia 1](../mods/01-hooks-do-zero.md) e na
> [referência da ponte](../referencia/ponte-e-bl.md).

## O que a ponte faz

Um mod escreve `Terraria.Main.player[0].statLife = 400`. Nada disso existe no
JavaScript: `Terraria` não é um objeto JS, `Main` é uma classe C# compilada,
`player` é um array do heap do IL2CPP e `statLife` é um `int` num offset fixo
dentro de um objeto `Player`. A ponte transforma cada pedaço desse caminho:

1. **nome → coisa do jogo**: `Terraria.Main` vira a classe `Il2CppClass*`,
   `player` vira o campo estático, `statLife` vira um campo de instância com
   offset e tipo;
2. **memória → valor JS**: o `int` no offset vira um número JS; um objeto do
   jogo vira um *wrapper*; um `Vector2` vira uma vista;
3. **valor JS → memória**: `400` vira um `int` escrito no offset certo.

Tudo por **nome**, resolvido em tempo de execução pela API do IL2CPP
(`il2cpp_class_from_name`, `il2cpp_class_get_field_from_name`...). Nenhum
offset ou endereço fica fixo no código: uma versão nova do jogo muda todos,
e a ponte continua achando pelo nome.

## Os objetos que o mod vê

[`Bindings.cpp`](../../app/src/main/cpp/script/bridge/Bindings.cpp) define
seis tipos de objeto JS, cada um com um *getter/setter* exótico do QuickJS (a
função que responde a **qualquer** nome de propriedade):

| Objeto JS | Exemplo | O que embrulha |
|---|---|---|
| **Namespace** | `Terraria`, `Terraria.ID` | Só um prefixo de texto. |
| **GameClass** | `Terraria.Item` | Uma classe: campos e propriedades estáticos, métodos, `.new()`, `.makeGeneric()`. |
| **GameObject** | `Main.player[0]` | Um objeto do jogo: campos e propriedades de instância, métodos. |
| **GameArray** | `Main.player` | Um array do jogo: `[i]`, `.length`, `.cloneResized(n)`. |
| **GameStruct** | `player.position` | Uma vista (ou cópia) de um struct por valor. |
| **GameMethod** | `Item['void SetDefaults(int Type, ItemVariant variant)']` | Um método: chamável e hookável (`.hook`). |

### Namespaces preguiçosos

`Terraria.ID.ItemID` funciona sem nenhuma declaração. No boot, a ponte varre as
classes do jogo **uma vez**, só para saber quais **raízes** de namespace
existem (`Terraria`, `System`, `Microsoft`, `ReLogic`...), e publica um global
para cada. O resto da árvore é preguiçoso: `Terraria.ID` é um objeto com o
prefixo `"Terraria.ID"`; acessar `.ItemID` tenta resolver
`Terraria.ID.ItemID` como classe e, se não for classe, devolve outro namespace
com o prefixo estendido. Materializar a árvore inteira custaria varrer
dezenas de milhares de classes.

Classes **sem namespace** (a interface do celular: `GUIBuffs`,
`GUIInstance`...) viram globais preguiçosos: o getter acha a classe no
primeiro acesso e se troca pelo valor. Um nome que já existe (`Math`, `bl`)
fica como está.

`bl.classOf(ns, nome)` resolve direto, para o caso raro em que a árvore não
chega (nome gerado pelo compilador, por exemplo).

## Resolver um nome: o cache de membros

`self.statLife` num hook de `Player.Update` roda todo quadro. Resolver o nome
do zero a cada vez (converter o átomo em texto, varrer as centenas de campos
de `Player` com `strcmp`, subir a hierarquia de classes, perguntar o tipo) é
caro. [`Members.cpp`](../../app/src/main/cpp/script/bridge/Members.cpp)
resolve **uma vez** por `(classe, nome)` e guarda:

- a chave é o **`JSAtom`**, não o texto: o QuickJS já internou o nome, e
  comparar um inteiro é o que torna o caminho quente barato;
- o resultado diz o que o nome é: campo (offset e tipo), propriedade C#
  (`get_X`/`set_X`), método por assinatura, método de nome único, classe
  aninhada, ou um nome do próprio protótipo JS (`new`, `hook`, `toString`),
  que fica com o JS;
- há três espaços: **estático** (`Terraria.Main.x`), **instância**
  (`player.x`, offset desde o começo do objeto) e **struct**
  (`player.position.X`, offset desde os dados do struct).

Um nome com `(` é uma **assinatura**: `'void SetDefaults(int Type, ItemVariant variant)'`
é casada contra os métodos da classe (com os nomes de tipo do C#, e aceitando
`float?`/`Nullable<float>`/``Nullable`1``, `out int`, `ref Vector2`). Um nome
puro de método vale se houver **um só** método com aquele nome; com mais de
um, a ponte **recusa** e lista os overloads na mensagem. Escolher um em
silêncio já produziu uma exceção a cada quadro.

O C# roda o construtor estático de uma classe no primeiro acesso a um
estático dela; o `field_static_get_value` do IL2CPP não. Sem cuidado,
`AmmoID.Sets.IsArrow` lido antes de o jogo usar munição vinha `null`. A ponte
chama `il2cpp_runtime_class_init` antes do primeiro uso de cada membro
estático, uma vez.

## Ler e escrever: o sistema de tipos

[`Value.cpp`](../../app/src/main/cpp/script/bridge/Value.cpp) é o **único**
lugar que toca a memória do jogo para ler ou escrever um valor. Antes, cada
ponto de contato (campo de instância, estático, elemento de array, argumento,
retorno, argumento de hook) tinha o próprio `if` de tipos, e as seis cópias
divergiam: um enum funcionava como argumento e explodia como campo.

O tipo de cada coisa vira um `TypeDesc`, resolvido uma vez:

| `Prim` | No JS | Detalhe |
|---|---|---|
| `Bool`, `I8`...`U64`, `Char` | número (ou `boolean`) | Escrita confere o tipo: texto onde se espera número é `TypeError`, nunca 0 calado. |
| `F32`, `F64` | número | |
| enum | número | O `prim` já é o tipo subjacente. |
| `String` | texto | Convertido de/para UTF-16 do .NET. |
| `Object` | wrapper (`GameObject`), ou `null` | Ver identidade, abaixo. |
| `Array` | `GameArray` | Um array JS também é aceito onde o jogo espera `int[]`, `string[]`...: a ponte monta um na hora. |
| `Struct` | vista ou cópia (`GameStruct`) | Ver structs, abaixo. |
| `Nullable<T>` | `null` ou o próprio `T` | Nunca o struct `{hasValue, value}`. |
| `ref`/`out` | `Ref` | Ver [`ref` e `out`](../mods/02-ref-e-out.md). |

### Structs: vista ou cópia

Um `Vector2` dentro de um objeto (`npc.position`) é uma **vista**: um objeto JS
que guarda o dono e o offset, e lê e escreve direto na memória do dono.
`npc.position.X = 100` move o NPC. A vista segura o dono vivo enquanto ela
existe.

Duas exceções seguem a semântica de valor do C#: o struct que um **método
devolve** e o que chega como **argumento de hook** são **cópias**. Escrever
neles não muda o jogo.

Campo **estático** de struct é um caso à parte: o IL2CPP só entrega uma cópia
do bloco de estáticos, nunca o endereço. A vista guarda o `FieldInfo` e
**devolve o valor a cada escrita**; sem isso, `Main.screenPosition.X = 0` não
faria nada.

As vistas saem de um *pool* (`StructRef`), reusadas: criar uma por leitura de
`player.position` foi medido em ~330 ns e caiu para ~170 com o pool.

Structs com campos iguais numerados (`proj.ai` é um `Float_FixedArray_3`, com
`_0`, `_1`, `_2`) ganham **indexador**: `proj.ai[0]` lê o campo direto, com
limite (`RangeError` fora dele). Sem essa forma, o indexador cai no
`get_Item`/`set_Item` do jogo.

## Chamar um método do jogo

[`Invoke.cpp`](../../app/src/main/cpp/script/bridge/Invoke.cpp) tem dois
caminhos:

1. **Chamada direta** (o normal): monta os registradores pelo mesmo plano da
   ABI que o hook usa ([hooks](hooks.md#o-plano-da-abi)) e salta para o
   `methodPointer`, o mesmo endereço que o jogo usa. Num método hookado, quem
   atende é o hook, como numa chamada do jogo.
2. **`il2cpp_runtime_invoke`**: o caminho genérico do runtime. Ele monta um
   vetor de ponteiros para os argumentos e **encaixota o retorno** (uma
   alocação no coletor por chamada, até para devolver um `int`). Só é usado
   quando o plano não fecha (argumentos demais para os registradores, struct
   de retorno grande).

O argumento escondido do IL2CPP (o `const MethodInfo*` no fim) é posto pela
ponte, que aqui é quem chama. Uma exceção C# vira `InternalError` no JS.

Um `GameMethod` é criado uma vez por `MethodInfo` e reusado:
`obj['assinatura']` a cada quadro devolve o mesmo objeto (antes, montar um novo
custava ~290 ns por acesso).

## Objetos do jogo segurados pelo JS

O coletor de lixo do IL2CPP (Boehm, não incremental neste Terraria) varre a
pilha e as raízes do jogo, não a memória do QuickJS. Um objeto que **só o mod**
segura (um `Item.new()` guardado numa variável) seria recolhido, e o ponteiro
ficaria pendurado.

### A âncora: `Roots`

[`Roots.cpp`](../../app/src/main/cpp/script/bridge/Roots.cpp) mantém blocos de
4096 ponteiros alocados com `il2cpp_gc_alloc_fixed`: memória que o coletor
**varre como raiz**, mas não recolhe. Ancorar um objeto é gravar o ponteiro
num slot livre; soltar é zerar o slot. Antes era um `gchandle` por objeto (uma
trava e uma tabela do runtime a cada objeto que entrava e saía do JS). Sem a
API de memória fixa (outra versão do runtime), cai de volta no `gchandle`.

### Identidade: `WrapperMap`

`Main.player[0]` lido duas vezes devolve o **mesmo** objeto JS, e
`Main.player[0] === self` dá `true`, como no C#.
[`WrapperMap.h`](../../app/src/main/cpp/script/bridge/WrapperMap.h) é um mapa
ponteiro → wrapper com endereçamento aberto (sondagem linear, remoção por
deslocamento para trás), sem alocação por entrada.

- O mapa **não** segura o wrapper. Quem o mantém vivo é o JS; quando a
  última referência cai, o QuickJS (que conta referências) finaliza **na
  hora**, e o finalizador tira a entrada e solta a âncora. O mapa nunca
  devolve um wrapper morto.
- A chave é estável porque o Boehm **não move** objetos, e a âncora impede
  que o endereço seja recolhido e reusado enquanto a entrada existe.
- Os `Pinned` (o registro por wrapper) saem de uma lista livre, reusados.

O mapa tem um *fuzz* próprio contra `std::unordered_map`
([`tools/tests/wrappermap`](../../tools/tests/wrappermap), 9 milhões de
operações, com controle negativo).

O QuickJS liberar na hora é uma das razões de não trocar de motor: o V8 só
libera wrappers quando o coletor dele roda, e as âncoras se acumulariam entre
coletas (ver [histórico](../historico/AVALIACAO-PONTE-E-CRASH.md#com-v8-seria-pior)).

## Campos e métodos que o mod põe no jogo

No IL2CPP o tamanho de um objeto e a posição de cada campo estão gravados no
binário, e todo o código compilado lê por posição fixa: não dá para acrescentar
um campo de verdade a `Item`.
[`ExtraFields.cpp`](../../app/src/main/cpp/script/bridge/ExtraFields.cpp)
guarda os campos de mod (`bl.defineField(Terraria.Item, 'ModItem')`) numa
tabela ao lado, indexada pelo objeto, com **referência fraca**
(`il2cpp_gchandle_new_weakref`): a tabela não segura o objeto, e quando o
coletor o recolhe a entrada é descartada. A referência fraca distingue o
objeto de um novo que nasça no mesmo endereço.

É assim que `item.ModItem`, `proj.ModProjectile`, `npc.ModNPC` e
`player.ModPlayers` existem. `bl.defineMethod` faz o mesmo com métodos
(`player.GetModPlayer(...)`). Um nome que a classe do jogo já tem vence o do
mod.

## `Ref`: `ref` e `out`

[`Ref.cpp`](../../app/src/main/cpp/script/bridge/Ref.cpp): um objeto com
`.value`. No hook, o `Ref` chega **preso** ao endereço da variável de quem
chamou (ler lê a variável, escrever muda o que o jogo vai usar) e se **solta**
quando o callback volta (guarda o último valor, esquece o endereço, que era da
pilha). Numa chamada do mod, um `Ref` solto vira uma variável temporária com o
valor dele e recebe de volta o que o método deixou. Um `Ref` ainda preso,
repassado de dentro de um hook, vai como o endereço original. Struct por `ref`
sai sempre como cópia. O uso completo está no [guia 2](../mods/02-ref-e-out.md).

## Genéricos e arrays

- `List.makeGeneric(Vector2)` passa pelo próprio .NET
  (`Type.MakeGenericType`). Só funciona para combinações que o jogo já usa: o
  IL2CPP compila só o código das instâncias genéricas que existem no jogo.
  Para as outras, o runtime lança e a mensagem diz qual.
- `arr.cloneResized(n)` cria um array novo do mesmo tipo, copia o que cabe e
  deixa o resto zerado. É a ferramenta de quem precisa crescer uma tabela do
  jogo (`Main.recipe = Main.recipe.cloneResized(4000)`).
- Índice de array é lido direto do **átomo inteiro** do QuickJS (bit 31 mais o
  valor), com a codificação conferida uma vez contra `JS_NewAtomUInt32`; o
  tipo do elemento fica guardado no wrapper do array. O passo do índice é o
  tamanho do elemento: ponteiro (8 bytes) num `Player[]`, o struct inteiro num
  `Vector2[]`.

## Onde cada número de custo nasce

As medições da ponte, passo a passo, estão no
[histórico da otimização](../historico/PONTE-OTIMIZACAO.md); o resumo prático
para quem escreve mod está no [guia de custo](../mods/03-custo-e-desempenho.md).
