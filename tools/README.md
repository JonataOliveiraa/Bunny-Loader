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
| [`tests/`](tests) | Mods de teste: da ponte (`nullable`, `wrappers`), das tabelas de item de mod (`moditems`), do save de item de mod com o mod ligado e desligado (`modsave`, rodar mais de uma vez), dos projeteis de mod (`projectiles`) e dos NPCs de mod (`npcs`) — os dois ultimos precisam do Example Mod ligado. Rodam por `bench/run.sh`. Mais o fuzz do `WrapperMap` (`wrappermap/run.sh`). |
| [`ui-sprites.py`](ui-sprites.py) | Recorta as texturas de interface do jogo que o menu usa (horas da Jornada, setas) e o "?" do item de mod ausente (`runtime/UnloadedIcon.h`). Recebe a pasta `Images` do jogo. |
| [`crash-trials/`](crash-trials) | Cria mundos em série no MuMu, reiniciando o app a cada tentativa, e conta crashes. |
| [`disasm/`](disasm) | Desassembly anotado do `libil2cpp.so` (`prep.sh` gera os caches a partir de `refs/`). |
