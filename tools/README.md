# tools/: ferramentas de PC

Nenhuma roda sozinha no build; todas são chamadas à mão.

## Desenvolvimento

| Script | O que faz |
|---|---|
| [`ui.sh`](ui.sh) | Ciclo curto de interface: compila sem os assets e as `.so` do jogo, instala, abre e printa (~6 s). |
| [`dump.sh`](dump.sh) | Regenera `refs/` a partir do jogo instalado (pull + extração + Il2CppDumper). |
| [`dumpgrep.sh`](dumpgrep.sh) | Extrai o bloco de uma classe, struct ou enum do `refs/dump.cs`. |
| [`gen-cheatdata.py`](gen-cheatdata.py) | Gera `cheatbridge/bunny/CheatData.java` (os NPCs escolhidos a mão) a partir do `refs/dump.cs`. Os itens não passam por aqui: o menu os separa pelos campos do próprio jogo. |
| [`build-cheatbridge-dex.sh`](build-cheatbridge-dex.sh) | Compila o menu de cheats em dex e embute como header C. |
| [`bin2header.py`](bin2header.py) | Binário → array C. Usado pelo script acima. |
| [`ui-sprites.py`](ui-sprites.py) | Recorta as texturas de interface do jogo que o menu usa (horas da Jornada, setas) e o "?" do item de mod ausente (`content/items/UnloadedIcon.h`). Recebe a pasta `Images` do jogo. |
| [`mod-art.py`](mod-art.py) | As capas e os ícones dos mods de exemplo. |

```bash
tools/ui.sh                                     # iterar em tela
tools/dump.sh                                   # pipeline completo
tools/dump.sh --skip-pull                       # reaproveita refs/base.apk
tools/dumpgrep.sh Projectile Terraria           # inspecionar uma classe
```

Não haverá empacotador de `.bmod`: o pacote é montado à mão pelo autor, com a
documentação como referência.

## Testes

Os testes são **mods**: cada pasta de [`tests/`](tests) instala no jogo, faz o
que testa de verdade (usa o item, invoca o chefe, salva e recarrega o mundo) e
escreve no log uma linha por checagem, terminando em `<teste> FIM: tudo ok` ou
`<teste> FIM: N falha(s)`.

[`bench/run.sh`](bench/run.sh) roda um ciclo no emulador: instala os mods
pedidos, abre o jogo, entra no **primeiro** mundo da lista, espera o `FIM` de
cada teste e salva as linhas de resultado.

```bash
tools/bench/run.sh saida.txt samples/ExampleMod tools/tests/boss
BL_DEVICE=127.0.0.1:16416 tools/bench/run.sh saida.txt tools/tests/refs   # outro emulador
```

- Pré-condições: o APK instalado, um mundo e o personagem de teste ("Bench")
  primeiro da lista.
- **Um teste por vez.** O `run.sh` não tira os mods de rodadas anteriores, e
  juntos eles se atrapalham (o tiro do `hooks` mata o slime do `npcs`): apague
  os `test-*` de `bunny_packs/` entre um e outro.
- Os poderes do Mod Menu ficam salvos; o `run.sh` os apaga antes de abrir.
- Com mais de um emulador, `BL_DEVICE` escolhe qual. O `run.sh` só toca na
  tela com o Bunny Loader na frente (um toque cego numa tela inicial do
  emulador já instalou um jogo da loja).

| Teste | O que confere | Precisa do Example Mod |
|---|---|---|
| **A ponte** | | |
| `nullable` | Tipos, structs, arrays, `Nullable<T>`, hooks com struct. | |
| `wrappers` | Objeto segurado só pelo JS sobrevive ao coletor; identidade (`===`). | |
| `structindex` | `proj.ai[0]`, `localAI.get_Item`, `oldPos[i].X`, `hideMisc[i]` e os limites; uma linha de log de 6 KB inteira no arquivo. | |
| `extrafields` | `bl.defineField`, `bl.defineMethod`. | |
| `refs` | `ref`/`out` na chamada e no hook, struct por `ref`, `Ref` repassado e solto. | |
| `files` | `bl.file`, `bl.directory`, `bl.path`. | |
| `hookslots` | Centenas de hooks de uma vez, e a profundidade de hooks encadeados no mesmo método. Com 25 hooks nos nomes, as listas do menu demoram a aceitar toque: se parar na seleção de mundo, toque em Jogar. | sim |
| `ifbusy` | Hook que não espera o motor JS preso noutra thread (`ifBusy`). | **não**: rodar sem |
| `enginethreads` | A thread do jogo e a do save no `original()` ao mesmo tempo, cada uma com a sua pilha JS. | |
| `wrappermap` | Fuzz do `WrapperMap` contra `std::unordered_map` (roda como binário, `wrappermap/run.sh`). | |
| **Conteúdo** | | |
| `moditems` | As tabelas de item de mod. | |
| `modsave` | Save de item de mod com o mod ligado e desligado (rodar mais de uma vez). | |
| `projectiles`, `npcs`, `buffs`, `tiles` | Cada tipo de conteúdo de mod. | |
| `tilesave` | Três rodadas: com o mod, sem ele e com ele de novo. | |
| `tileframes` | Duas rodadas: o quadro de um 3x3 de mod e de um de pedra ao reabrir o mundo. | |
| `hooks` | Os hooks das classes: IA, spawn natural, Bestiário, tooltip, receita, uso de item. | |
| `recipes` | Receitas e grupos. | |
| `modplayer` | `ModPlayer` (quadro, dano, morte, dados salvos). | |
| `modcontent` | `ModContent` e o que fica fora do Mod Menu. | sim |
| `exmod1`, `exmod2` | O Example Mod inteiro, usado. | sim |
| `summons` | Pets, lacaio e sentinela usados como o dedo usaria. | sim |
| `townnpc` | Morador de mod em quatro rodadas: com o mod, com, sem e com de novo. | sim |
| `townshop` | Conversa, loja, felicidade, retrato e perfil. | sim |
| `townfight` | Ataque e gore do morador. | sim |
| `boss` | O chefe nascendo pelo item de invocação. | sim |
| `drops` | Drop de item de mod pelas regras do jogo. | sim |
| `fishline` | A linha da vara de mod. | sim |
| `sounds` | `SoundStyle`, `SoundEngine.PlaySound`, `MaxInstances`, `UseSound` de item usado. | sim |
| `music` | O chefe trocando a música do jogo pela dele, cada troca conferida no meio do fade. | sim |
| `crossmod` (+ `crossmodtarget`) | `ModLoader.TryGetMod` e `Call`, nas duas ordens de carga. | |
| **Multijogador** | | |
| `mprecipes`, `mpplayer`, `mpitems`, `mptiles` | Um cliente e um host (ver `mp-session.sh`): itens de mod usados pelo cliente e conferidos no host, o dash com toque de verdade. | sim |

[`mp-session.sh`](mp-session.sh) sobe uma sessão de multijogador entre duas
instâncias do MuMu: uma hospeda, a outra entra, sem inimigos novos e de dia.

## Medição e investigação

| Pasta | O que é |
|---|---|
| [`bench/`](bench) | O mod de benchmark da ponte JS → IL2CPP (cada operação 20 mil vezes, melhor de 7) e o `compare.py`, que compara rodadas. Os números estão no [guia de custo](../docs/mods/03-custo-e-desempenho.md) e no [histórico da otimização](../docs/historico/PONTE-OTIMIZACAO.md). |
| [`crash-trials/`](crash-trials) | Cria mundos em série no MuMu, reiniciando o app a cada tentativa, e conta crashes ([histórico](../docs/historico/AVALIACAO-PONTE-E-CRASH.md)). |
| [`disasm/`](disasm) | Desassembly anotado da `libil2cpp.so` (`prep.sh` gera os caches a partir de `refs/`). Limites compilados: `scan_limits.py` (`cmp #N`), `scan_loops.py` (fim de laço em bytes), `scan_movs.py` (`new T[N]`) e `scan_halved.py` (o `0 < x < N` feito pela metade, do drop). Ver [conteúdo por dentro](../docs/nucleo/conteudo.md#os-limites-compilados). |
