# tools/ — ferramentas de PC

Nenhuma roda sozinha no build; todas são chamadas à mão.

| Script | O que faz |
|---|---|
| [`ui.sh`](ui.sh) | Ciclo curto de interface: compila sem os assets e as `.so` do jogo, instala, abre e printa (~6 s). |
| [`dump.sh`](dump.sh) | Regenera `refs/` a partir do jogo instalado (pull + extração + Il2CppDumper). |
| [`dumpgrep.sh`](dumpgrep.sh) | Extrai o bloco de uma classe/struct/enum do `refs/dump.cs`. |
| [`gen-cheatdata.py`](gen-cheatdata.py) | Gera `cheatbridge/bunny/CheatData.java` (os NPCs escolhidos a mão) a partir do `refs/dump.cs`. Os itens não passam por aqui: o menu os separa pelos campos do próprio jogo. |
| [`build-cheatbridge-dex.sh`](build-cheatbridge-dex.sh) | Compila o menu de cheats em dex e embute como header C. |
| [`bin2header.py`](bin2header.py) | Binário → array C. Usado pelo script acima. |

```bash
tools/ui.sh                                     # iterar em tela
tools/dump.sh                                   # pipeline completo
tools/dump.sh --skip-pull                       # reaproveita refs/base.apk
tools/dumpgrep.sh Projectile Terraria           # inspecionar uma classe
```

## Futuro

- **lrc / lrbridgegen** — só entram no track da LRVM (mods em C#), Fase 6+.

Não haverá empacotador de `.bmod`: o pacote é montado à mão pelo autor, com a
wiki como referência, e o `uid` é emitido pelo site.

## Medição e investigação

Ver [`docs/AVALIACAO-PONTE-E-CRASH.md`](../docs/AVALIACAO-PONTE-E-CRASH.md).

| Pasta | O que é |
|---|---|
| [`bench/`](bench) | Mod de benchmark da ponte JS → IL2CPP. `run.sh` instala mods, entra no mundo e salva o log; `compare.py` compara rodadas. Ver [`docs/PONTE-OTIMIZACAO.md`](../docs/PONTE-OTIMIZACAO.md). |
| [`tests/`](tests) | Mods de teste: da ponte (`nullable`, `wrappers`), das tabelas de item de mod (`moditems`), do save de item de mod com o mod ligado e desligado (`modsave`, rodar mais de uma vez), dos projeteis de mod (`projectiles`), dos NPCs de mod (`npcs`) e dos hooks das classes de mod — IA, spawn natural, Bestiario, tooltip, receita e uso de item (`hooks`), do `ref`/`out` na chamada e no hook (`refs`), dos tiles de mod (`tiles`; `tilesave` em três rodadas: com o mod, sem ele e com ele de novo; `tileframes` em duas: o quadro de um 3x3 de mod e de um de pedra ao reabrir o mundo), dos pets, do lacaio e da sentinela usados como o dedo usaria (`summons`), e de muitos hooks ao mesmo tempo e da profundidade de hooks encadeados (`hookslots`); os tres ultimos precisam do Example Mod ligado. Rodam por `bench/run.sh`, UM de cada vez: o run.sh nao tira os anteriores, e juntos eles se atrapalham (o tiro do `hooks` mata o slime do `npcs`). Mais o fuzz do `WrapperMap` (`wrappermap/run.sh`). |
| [`mp-session.sh`](mp-session.sh) | Sobe uma sessão de multijogador entre duas instâncias do MuMu: uma hospeda, a outra entra, sem inimigos novos e de dia. Os testes de rede partem daqui: `tests/mprecipes`, `tests/mpplayer`, `tests/mpitems` e `tests/mptiles` (cada item de mod que atira e a poção usados pelo cliente, conferidos no host; depois, o dash do escudo com toque de verdade — `input swipe` duplo no analógico, em 238,762). |
| [`ui-sprites.py`](ui-sprites.py) | Recorta as texturas de interface do jogo que o menu usa (horas da Jornada, setas) e o "?" do item de mod ausente (`runtime/UnloadedIcon.h`). Recebe a pasta `Images` do jogo. |
| [`crash-trials/`](crash-trials) | Cria mundos em série no MuMu, reiniciando o app a cada tentativa, e conta crashes. |
| [`disasm/`](disasm) | Desassembly anotado do `libil2cpp.so` (`prep.sh` gera os caches a partir de `refs/`). |
