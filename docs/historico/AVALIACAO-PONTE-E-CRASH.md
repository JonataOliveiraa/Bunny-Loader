# Avaliação da ponte JS → IL2CPP, e o crash ao criar mundo

Rodada de 2026-09-23, no MuMu (x86_64 traduzindo ARM pelo houdini), Terraria
1.4.5.6.4, `libbunny` compilada com `-O2` (vale também para o APK de debug:
conferido no `compile_commands.json`). Dados brutos em
[`dados/2026-09-23/`](dados/2026-09-23/); ferramentas em
[`tools/bench`](../../tools/bench), [`tools/crash-trials`](../../tools/crash-trials)
e [`tools/disasm`](../../tools/disasm).

> **O MuMu não é o aparelho.** Tudo aqui roda traduzido de ARM, e o
> interpretador do QuickJS (despacho indireto a cada bytecode) é justamente o
> tipo de código que a tradução mais penaliza. Os números absolutos valem para
> comparar entre si; num celular devem ser de 5 a 10 vezes menores. Falta medir
> num ARM real.

## Parte 1 — desempenho

### Como foi medido

[`tools/bench`](../../tools/bench) é um mod: na primeira atualização do jogador
dentro do mundo, na thread do jogo, roda cada operação 20 000 vezes (melhor de
3) e desconta um laço vazio. O custo do hook é (chamada com hook) − (a mesma
chamada sem hook), medido antes e depois de instalar hooks que só repassam ao
original.

### Resultado

Laço vazio (`s += i`): **55 ns** por iteração. Custo *acima* dele:

| Operação | ns/op | Observação |
|---|---:|---|
| `p.statLife` (ler campo int) | 60 | cruzar a ponte custa o mesmo que uma iteração de JS |
| `p.statLife = v` (escrever) | 63 | |
| `p.moveSpeed` (ler float) | 54 | |
| `Main.maxTilesX` (campo estático) | 65 | |
| `Main.myPlayer` (propriedade C#, chamada direta) | 121 | |
| `f(p)`, método guardado numa variável | 112 | |
| `p['bool CanBePushedByWind()']()` a cada vez | **402** | +290 ns para montar o objeto do método em todo acesso |
| método estático, 2 floats | 216 | |
| `bt[0]` (elemento de `int[]`) | **179** | 3× um campo |
| `players[0]` (elemento que é objeto) | **556** | cria wrapper + `gchandle` |
| `p.position.X` (vista de struct) | **488** | cria um objeto JS por acesso |
| `Main.player[0].whoAmI` | **1039** | dois wrappers com `gchandle` |
| aritmética pura em JS, por iteração | 128 | o interpretador |
| `{x, y}` (alocar objeto JS) | 261 | |
| **hook estático (2 floats) que repassa** | **+829** | por chamada |
| **hook de instância (`self`) que repassa** | **+1400** | por chamada |

### O que os números dizem

1. **Campo e propriedade estão bons.** Ler ou escrever um campo custa o mesmo
   que uma iteração de laço vazio. O cache de membro de `script/bridge/Members.cpp`
   funciona.
2. **O caro é criar wrapper.** Toda vez que um objeto do jogo entra no JS
   (elemento de array, `self` do hook, retorno de método) nasce um objeto JS,
   um `Pinned` no heap C++ e um `gchandle`, e morre tudo no fim. É isso que
   põe `players[0]` em 556 ns e responde por uns 570 dos 1400 ns do hook de
   instância (1400 − 829).
3. **Três desperdícios fáceis de tirar:**
   - `obj['assinatura']` monta um `GameMethod` novo a cada acesso
     (`makeGameMethod`: `malloc` + objeto JS + `method_get_flags`). Guardar um
     por `MethodInfo` elimina os ~290 ns.
   - Índice de array converte o átomo em string, depois em `std::string`,
     depois `strtol`, e pergunta a classe do elemento ao IL2CPP a cada acesso
     (`ga_exotic_get`). Átomo inteiro é o caso comum e dá para ler direto; o
     tipo do elemento pode morar no `Pinned` do array.
   - A vista de struct cria um objeto por leitura de `p.position`.
4. **O hook é o ponto que decide o quadro.** Com +1,4 µs por chamada no MuMu:
   um hook em `Player.Update` (1 por quadro) não aparece; um em
   `Projectile.AI` com 300 projéteis custa ~0,4 ms; um método chamado 10 000
   vezes por quadro estoura o orçamento de 16,6 ms sozinho. Não é o QuickJS
   que pesa aí, é o que a ponte faz em volta: dois wrappers com `gchandle`, a
   trava do motor, e o `original()` soltando e pegando a trava de novo.

### Melhorias, em ordem de retorno

1. **Wrapper emprestado no hook**: `self` e argumentos-objeto sem `gchandle`
   durante o callback (o objeto está vivo, o jogo o segura), promovendo para
   fixado só se o JS guardar a referência (refcount > 1 no fim do callback). Do
   mesmo jeito para elemento de array. Deve tirar perto de metade do custo do
   hook de instância.
2. **Cache do `GameMethod` por `MethodInfo`** (−290 ns por chamada via
   propriedade).
3. **Caminho rápido de índice inteiro** no array e tipo do elemento guardado.
4. Pool de vistas de struct.

### Com V8 seria pior?

**Na ponte, não seria melhor, e provavelmente seria um pouco pior. Em JS
puro, seria muito melhor.**

- O custo aqui é **cruzar a fronteira**, não executar JS. No V8, o equivalente
  do nosso getter exótico é um *interceptor*, que tira o acesso do caminho
  otimizado (sem *inline cache*, uma chamada C++ por acesso). Chamar JS a
  partir do C++ exige `HandleScope`, entrada de contexto e, com várias
  threads, `v8::Locker`. Por travessia, o V8 fica na mesma faixa do QuickJS ou
  acima.
- Onde o V8 ganha de verdade é no corpo do JS: os 128 ns por iteração de
  aritmética interpretada viram ~1 ns com JIT. Para mod que faz conta pesada
  (gerador de mundo em JS, IA complexa), faria diferença; para o mod típico,
  que lê e escreve campos do jogo, quase nenhuma.
- A única forma de o V8 ganhar também na fronteira seria redesenhar a ponte:
  gerar acessores em JS que leem a memória do objeto por um `DataView` sobre o
  heap do IL2CPP, que o JIT transforma em uma instrução de load. É outra
  arquitetura, com seus próprios riscos (objeto que o coletor move ou recolhe
  por baixo do `DataView`).
- Custos do V8 que não existem hoje: +7 a 10 MB de biblioteca por ABI,
  memória base bem maior, build pesado (gn/depot_tools), JIT gerando código
  ARM que o houdini tem de traduzir em tempo de execução no emulador. E um
  problema sério para esta ponte: o V8 só libera wrappers quando o coletor
  dele roda. O QuickJS libera **na hora** (refcount), e é isso que devolve os
  `gchandle` do IL2CPP imediatamente. Com V8 os `gchandle` se acumulariam
  entre coletas.

**Conclusão:** não trocar de motor. O gargalo é o desenho da ponte, e ele se
conserta no QuickJS (lista acima). Se um dia houver mod que precise de JS
pesado, a alternativa a avaliar é o V8 sem JIT (`--jitless`) ou o Hermes, não
por velocidade de fronteira.

## Parte 2 — crash ao criar mundo

> **Atualização (2026-09-24): causa encontrada e corrigida** — referência
> pendurada no `original()` dos hooks (`script/bridge/JsHook.cpp`): com hooks
> aninhados na mesma thread, o resultado era gravado em memória já liberada.
> Ver [`PONTE-OTIMIZACAO.md`](PONTE-OTIMIZACAO.md), passo 2 (0 crashes em 10
> rodadas depois da correção). O `JsSuspend`, que cheguei a apontar, era
> inocente. O texto abaixo é a investigação como estava antes disso.

### Sintoma

Ao tocar em **Criar** num mundo novo, o jogo "congela": na verdade a thread
falhou com SIGSEGV e ficou presa num laço de sinais repetidos (o houdini
reencaminha o sinal a cada ~10 ms) até o app ser fechado.

### Onde falha

Sempre o mesmo lugar, dentro do runtime do IL2CPP:

```
#00 libil2cpp.so +0x6ac7ac   ldr x9, [x2]    x2 = 0   (comparador de chave de 2 ponteiros)
#01 libil2cpp.so +0x6e7868   busca numa tabela hash
#02 libil2cpp.so +0x6e773c
#03 libil2cpp.so +0x6d9d4c
 ... inflação recursiva de tipo genérico ...
    libil2cpp.so +0x6e945c   Runtime::ClassInit (a função chamada de 549 lugares)
```

É a tabela de instâncias genéricas do runtime, com um balde de chave nula:
o sintoma clássico de uma thread lendo a tabela enquanto outra a reconstrói.
Os registradores `x6 = 0xfefefeff…` e `x7 = 0x7f7f7f7f…` são as constantes das
rotinas de string do libc, o que ajudou a achar a função.

Em dois crashes, duas threads diferentes caíram no mesmo ponto:

1. **Thread principal**, dentro do nosso hook
   ([`bunny_crash1-logcat.txt`](dados/2026-09-23/bunny_crash1-logcat.txt)):
   `Main.Draw` → hook JS em `Main.DoDraw` → `JS_Call` → `original()` →
   `callRaw` → `Main.DoDraw` → `DrawMenu` → `GUIStatusMenu.Draw` →
   `GameTipsDisplay.AddNewTip` → `LanguageManager.RandomFromCategories` →
   `Dictionary.get_Keys` → `ClassInit` → **falha**.
2. **Thread de geração de mundo do próprio jogo**, sem nenhum frame nosso
   ([`cfgD_crash1-logcat.txt`](dados/2026-09-23/cfgD_crash1-logcat.txt)):
   `WorldGen.worldGenCallback` → `GenerateWorld` → passo da masmorra →
   `DungeonCrawler.SetupDungeonData` → `new DungeonData` → `ClassInit` →
   **falha**.

Ou seja: a tabela já está estragada (ou sendo disputada) quando alguém chega
nela; cai quem chegar primeiro.

### Tentativas (processo novo a cada uma)

O crash só pode acontecer na **primeira** criação de mundo de cada processo,
que é quando essas classes são inicializadas. As primeiras baterias reaproveitavam
o processo e por isso contam só a primeira tentativa. Contagem honesta:

| Configuração | Hooks JS em quais threads | Crashes |
|---|---|---|
| Terraria original (`com.and.games505.TerrariaPaid`) | nenhuma | **0 de 9** |
| Bunny Loader, só um hook JS em `Main.DoDraw` repassando ao original | só a principal | 0 de 7 |
| Bunny Loader + HelloMod + ExampleMod (uso normal) | só a de geração (`Item.SetDefaults`) | **0 de 7** |
| Bunny Loader + HelloMod + ExampleMod + hook em `DoDraw` | **as duas** | 1 de 6 |
| Bunny Loader + HelloMod + ExampleMod + mod de benchmark | **as duas** | 2 de ~3 (inclui o crash do usuário) |

O crash só apareceu com hook JS rodando **nas duas threads ao mesmo tempo**:
0 de 14 com hook numa thread só, 3 de 9 com as duas. O uso normal (só os
mods de exemplo) não caiu nenhuma vez.

> A linha "uso normal" foi medida por engano: a bateria foi "interrompida" com
> o `TaskStop`, mas o `bash` do script sobreviveu e seguiu criando mundos. Das
> 8 tentativas, uma deu TIMEOUT porque os mundos foram apagados no meio dela;
> as outras 7 valem ([`tentativas-cfgE.txt`](dados/2026-09-23/tentativas-cfgE.txt)).
> Para parar o script de verdade: matar o `bash.exe` dele (`Stop-Process`).

### O que foi descartado

- **Não é o mod de benchmark sozinho**: sem ele também caiu; com ele também
  criou.
- **Não é chamar `DoDraw` pelo trampolim do hook**: só o hook em `DoDraw`, 0
  de 7.
- **Não é uma thread nossa**: a sonda termina ~10 s depois do boot e a UI do
  menu só lê valores prontos. Mas código nosso roda, sim, no instante do
  crash: os hooks JS executam dentro das threads do jogo (ver hipótese 1).
- **Não é a troca das 127 tabelas de item** (`content/items/ModItems.cpp`): a cópia
  respeita o tamanho do elemento (tipo de valor por `class_value_size`,
  referência por `Array.Copy`), e nenhuma tabela foi refeita durante a geração.

### Hipóteses em aberto

1. **Dois hooks JS concorrentes, e o `JsSuspend`.** É o que os números
   apontam. Com hook em `DoDraw`, a thread principal passa quase todo o quadro
   dentro do `original()`, e o `callRaw` solta a trava do motor JS
   (`JsSuspend`) enquanto isso. A thread de geração entra no motor pelos hooks
   de `Item.SetDefaults`, milhares de vezes, criando wrappers com `gchandle` e
   chamando a API do IL2CPP. O que exatamente estraga a tabela de genéricos do
   runtime ainda não se sabe; os candidatos são as chamadas da API do IL2CPP
   feitas pela ponte (`describe`, `class_get_*`, `gchandle_*`) ao mesmo tempo
   que a outra thread inicializa classes.
2. **A varredura no boot (`installNamespaceRoots`)**, que percorre todas as
   classes na thread da sonda. **Enfraquecida**: roda em todas as
   configurações, inclusive nas 14 tentativas sem crash.

### Próximo passo proposto

Isolar a hipótese 1 com uma bateria de ~15 tentativas em cada variação:
hook em `DoDraw` + mods de exemplo **com o `JsSuspend` desligado** (o
`original()` segura a trava do motor). Se zerar, o problema está em deixar
duas threads dentro da ponte ao mesmo tempo, e o conserto é serializar o que
a ponte faz no IL2CPP. Em qualquer caso, vale mover o boot da sonda para o
primeiro `DoUpdate`, porque tocar o IL2CPP pelas threads do jogo é o
desenho correto.

### Como reproduzir

```bash
# 1. deixar o jogo na tela do personagem com um mundo salvo (ou nenhum)
# 2. mods desejados em files/mods/<uid>/ (ver tools/crash-trials)
bash tools/crash-trials/trials2.sh 10 resultado.txt      # Bunny Loader
bash tools/crash-trials/trials_vanilla.sh 10 base.txt    # Terraria original
```

Os scripts tocam na tela do MuMu em 1600×900, reiniciam o app a cada
tentativa, contam mundo criado pelo `.wld` novo em
`/sdcard/Android/data/<pacote>/Worlds/` e crash pela linha
`Forwarding signal` do logcat (salvam o logcat de cada crash).
