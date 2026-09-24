# Otimização da ponte JS → IL2CPP

Continuação de [`AVALIACAO-PONTE-E-CRASH.md`](AVALIACAO-PONTE-E-CRASH.md). Um
passo por vez, cada um medido com [`tools/bench`](../tools/bench) e conferido
com os mods de teste antes do próximo. Tudo no MuMu (ver a ressalva de lá: os
números absolutos são de ARM traduzido; o que vale é a comparação).

## Plano

| # | Passo | O que muda | Onde deve aparecer |
|---|---|---|---|
| 0 | Linha de base | nada; duas rodadas para ver o ruído | todas as linhas |
| 1 | Tabela de raízes | `gchandle_new/free` por wrapper → gravar/zerar um slot num bloco que o coletor enxerga (`il2cpp_gc_alloc_fixed`) | `players[0]`, `Main.player[0].whoAmI`, hook de instância |
| 2 | Um wrapper por objeto | mapa ponteiro → wrapper; a segunda leitura reaproveita; `===` passa a valer | idem, e identidade |
| 3 | Cache de método | `obj['assinatura']` devolve sempre o mesmo `GameMethod` | `p['...']()` a cada vez |
| 4 | Índice de array rápido | átomo inteiro lido direto; tipo do elemento guardado no array | `bt[0]`, `players[0]` |
| 5 | Argumentos emprestados no hook | `self`/argumentos sem raiz durante o callback; raiz só se o JS guardar | hook de instância |

Cada passo tem de manter verdes:

- [`tools/tests/nullable`](../tools/tests/nullable) (a ponte inteira: tipos,
  structs, arrays, hooks);
- [`tools/tests/wrappers`](../tools/tests/wrappers) (criado para esta rodada):
  objeto segurado só pelo JS sobrevive a `GC.Collect()`, identidade, `self`
  guardado depois do hook continua válido.

## Método

- [`tools/bench/run.sh`](../tools/bench/run.sh) instala os mods, abre o jogo,
  entra no mundo de teste e salva o log; [`compare.py`](../tools/bench/compare.py)
  faz a média das rodadas e o delta contra a linha de base.
- O benchmark pega o **menor de 7** para cada item, e a bateria inteira roda
  duas vezes por sessão. Com "melhor de 3" o mesmo código variava até 70% entre
  sessões (875 × 1494 ns no hook de instância); assim, fica entre 5 e 10%.
  Dados brutos: [`dados/otimizacao/`](dados/otimizacao/).
- **Controle negativo do teste de wrappers**: com a âncora sabotada (o slot não
  grava o ponteiro), o teste acusa os 4 casos — o coletor recolheu os itens e a
  memória foi reaproveitada pelo lixo (dano 5, tipo 1). Ou seja, o teste pega
  o erro que estas mudanças podem introduzir.

## Resultados

Média de duas rodadas; ns acima do laço vazio.

### Passo 0 → 1: tabela de raízes

`Roots` ([`script/Roots.cpp`](../app/src/main/cpp/script/Roots.cpp)): blocos
de 4096 ponteiros em `il2cpp_gc_alloc_fixed` (o coletor varre, não recolhe),
lista de slots livres, gravação pela barreira de escrita. Coletor deste
Terraria: **não incremental** (log do boot).

| Item | Base | Passo 1 | Δ |
|---|---:|---:|---:|
| `players[0]` (elemento-objeto) | 424 | 319 | **−25%** |
| `Main.player[0].whoAmI` | 784 | 582 | **−26%** |
| hook de instância (`self`) | 840 | 706 | **−16%** |
| demais itens | | | ±10% (ruído) |

Testes: `wrappers` e `nullable` verdes, 0 segfaults.

### Passo 1 → 2: um wrapper por objeto — e a causa do crash em aberto

Mapa ponteiro → wrapper em `script/Bindings.cpp` (`wrapperFor`), sem contar
referência; o finalizador tira a entrada. Identidade passou a valer:
`Main.player[0] === Main.player[0]` e `kept[0] === array[0]` dão `true`.

**Com o passo 2, entrar no mundo com bench + testes crashou em 3 de 4
rodadas** (0 de 6 nos passos 0 e 1). Mesma falha do crash ao criar mundo
(inicialização de classe genérica no runtime, SIGSEGV em laço), agora ao
carregar: hooks JS aninhados na thread principal (`DoDraw` → `DrawInterface`)
e a thread de carregamento do mundo entrando no motor pelos hooks de
`Item.SetDefaults`.

Primeiro experimento: o mesmo código com o `JsSuspend` desligado (o
`original()` segura a trava do motor). Deu 0 em 4, e **concluí errado** que o
`JsSuspend` era a causa. Com ele removido, a bateria seguinte voltou a cair
(1 crash e 1 rodada com métodos vindo `undefined`: corrupção de memória).

**A causa real: referência pendurada no `original()` do hook**
([`script/JsHook.cpp`](../app/src/main/cpp/script/JsHook.cpp)). O
`js_original` guardava `Frame& f = g_frames.back()` e gravava o resultado nele
depois de chamar o método do jogo. Se o método disparava outro hook na mesma
thread, o `push_back` realocava o `vector`, e a gravação (~200 bytes) caía em
memória já devolvida — que o IL2CPP reusava nas tabelas de genéricos. Acontece
sempre que o aninhamento passa da capacidade do `vector` da thread, o que
inclui a PRIMEIRA vez numa thread nova: os mods de exemplo têm dois hooks JS
encadeados no `Item.SetDefaults` (um chama o outro pelo `original()`), e as
threads de gerar e de carregar mundo são novas a cada vez. Daí "sempre na
primeira geração de mundo do processo". Corrigido guardando o ÍNDICE.

| Configuração | Rodadas | Crashes |
|---|---:|---:|
| com `JsSuspend`, sem a correção | 4 | 3 |
| sem `JsSuspend`, sem a correção | 7 | 1 (+1 corrompida) |
| **com a correção, sem `JsSuspend`** | 5 | **0** |
| **com a correção, com `JsSuspend`** | 5 | **0** |

O `JsSuspend` era inocente. Custo dele, medido: ~100 ns a mais por hook que
chama o original (+27–30% contra +6–8% sobre a base).

Desempenho (antes da correção, sem `JsSuspend`, 4 rodadas consistentes; as
10 rodadas depois da correção ficaram na mesma faixa —
[`frame-fix-*`](dados/otimizacao/)):

| Item | Base | Passo 1 | Passo 2 | Δ total |
|---|---:|---:|---:|---:|
| `players[0]` (elemento-objeto) | 424 | 319 | **176** | **−59%** |
| `Main.player[0].whoAmI` | 784 | 582 | **281** | **−64%** |
| hook de instância (`self`) | 840 | 706 | **437** | **−48%** |
| hooks estáticos | 490–538 | | 456–460 | −7 a −14% (sem o `JsSuspend`) |
| demais itens | | | | ±10% (ruído) |

Testes `wrappers` e `nullable` verdes nas 5 rodadas que chegaram ao fim.

**Decisão (usuário):** manter o `JsSuspend`. Ele existe para um hook longo
(desenho) não fazer os hooks de outras threads (geração de mundo) esperarem a
trava; o custo de ~100 ns por hook vale isso.

Medição final do passo 2 (correção + `JsSuspend`, 5 rodadas,
[`frame-fix-comsuspend-*`](dados/otimizacao/)):

| Item | Base | Passo 2 final | Δ |
|---|---:|---:|---:|
| `players[0]` (elemento-objeto) | 424 | 189 | **−55%** |
| `Main.player[0].whoAmI` | 784 | 305 | **−61%** |
| hook de instância (`self`) | 840 | 619 | **−26%** |
| hooks estáticos com original | 490–538 | 624–696 | +27–30% (sessão +5–10% mais lenta; ver acima) |

### Passo 2 → 3: cache de método

`makeGameMethod` guarda um `GameMethod` por `MethodInfo` (segura a referência
para sempre, como os callbacks de hook; o runtime JS vive até o processo
morrer). 4 rodadas, [`passo3-*`](dados/otimizacao/):

| Item | Base | Passo 2 | Passo 3 | Δ total |
|---|---:|---:|---:|---:|
| `p['bool CanBePushedByWind()']()` a cada vez | 294 | 325 | **157** | **−47%** |
| `f(p)`, método guardado (referência) | 102 | 101 | 102 | 0% |
| demais itens | | | | como no passo 2 |

O que sobra entre 157 e 102 é a busca do membro e a leitura da propriedade.
Testes verdes, 0 segfaults. (Uma 5ª rodada saiu vazia: o `run.sh` abortou no
`run-as` logo depois do `adb install`; o script agora espera o pacote.)

### Passo 3 → 4: índice de array rápido

Índice lido direto do átomo inteiro do QuickJS (bit 31 + valor), com a
codificação conferida uma vez contra `JS_NewAtomUInt32`; se um dia mudar, cai
no caminho por texto. Tipo do elemento guardado no wrapper do array. 4
rodadas, [`passo4-*`](dados/otimizacao/):

| Item | Base | Passo 3 | Passo 4 | Δ total |
|---|---:|---:|---:|---:|
| `bt[0]` (elemento de `int[]`) | 148 | 160 | **47** | **−68%** |
| `players[0]` (elemento-objeto) | 424 | 198 | **71** | **−83%** |
| `Main.player[0].whoAmI` | 784 | 305 | **160** | **−80%** |

Tropeço no caminho: a primeira versão usou `JS_AtomToValue`, supondo que
devolvia um inteiro para átomo inteiro — ele monta uma **string**. Os testes
pegaram na primeira rodada (`Main.player[0]` virou `undefined`). E a correção
revelou um byte NUL literal no `Value.cpp` (de antes desta rodada; compilava
igual, mas fazia o `grep` tratar o arquivo como binário e esconder resultados)
— trocado por `'\0'`.

### Passo 4 → 5: wrapper novo mais barato (plano ajustado)

O plano era "argumentos emprestados no hook": não ancorar `self` durante o
callback. Medindo antes de fazer, a âncora já não pesava: desde o passo 1 ela é
gravar um ponteiro num slot. Um item novo no benchmark (`arrayElemFresh`:
elementos distintos de `Main.item`, que o JS não segura — um wrapper NOVO por
acesso, exatamente o caso do `self` de hook em `Projectile.AI`) mostrou
~400 ns, contra ~70 ns do wrapper reaproveitado. O grosso era alocação. O
passo 5 atacou isso, em três partes medidas separadamente:

| Sub-passo | Wrapper novo (ns) | Δ |
|---|---:|---:|
| antes (passo 4) | ~403 | |
| 5a: sem a barreira de escrita quando o coletor não é incremental (o deste Terraria) | ~381 | −5% |
| 5b: `Pinned` reusados numa lista livre, em vez de `new`/`delete` | ~301 | −21% |
| 5c: [`WrapperMap`](../app/src/main/cpp/script/WrapperMap.h) — endereçamento aberto, sem alocação por entrada | **~154** | **−49%** |

O `WrapperMap` tem fuzz próprio contra `std::unordered_map`
([`tools/tests/wrappermap`](../tools/tests/wrappermap), 9 milhões de operações,
roda no MuMu como binário x86_64), com controle negativo: invertendo a condição
da remoção, falha na operação 185. O teste de wrappers ganhou um estresse do
mapa (8 voltas nos 400 itens de `Main.item`, conferindo `whoAmI`).

## Resumo

ns acima do laço vazio, MuMu, média das rodadas de cada passo:

| Operação | Antes | Depois | Δ |
|---|---:|---:|---:|
| `bt[0]` (elemento de `int[]`) | 148 | 50 | **−66%** |
| `players[0]` (elemento-objeto) | 424 | 65 | **−85%** |
| `Main.player[0].whoAmI` | 784 | 150 | **−81%** |
| wrapper novo por acesso (`self` de hook) | ~403¹ | ~154 | **−62%** |
| `p['assinatura']()` a cada vez | 294 | 146 | **−50%** |
| campo, propriedade, método guardado, JS puro | | | sem mudança (ruído) |

¹ medido a partir do passo 4; antes do passo 1 havia ainda um `gchandle` por
objeto, então o ganho real sobre a base é maior.

Além do desempenho, a rodada:

- **achou e corrigiu o crash ao criar/carregar mundo** — referência pendurada
  no `original()` dos hooks (passo 2);
- deu **identidade** aos objetos do jogo no JS (`Main.player[0] === self`);
- tirou um byte NUL literal do `Value.cpp`.

Todos os passos com `tools/tests/wrappers` e `tools/tests/nullable` verdes.
