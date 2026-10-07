# Resultados de AIType em ModNPC

Execução em 7 de outubro de 2026, no build Android completo do Bunny Loader.

## Comportamento e regressões

| Suíte | Verificações | Resultado |
|---|---:|---|
| `npcaitype/check.mjs` | 39 | Passou |
| `modnpchooks/check.mjs` | 46 | Passou; 23 assinaturas nativas conferidas |
| `globalprojectilehooks/check.mjs` | 59 | Passou; 22 assinaturas nativas conferidas |
| `projectilekill/check.mjs` | 16 | Passou |
| `modplayerhooks/check.mjs` | 55 | Passou; 153 assinaturas nativas conferidas |
| `:app:testDebugUnitTest` | 69 | Zero falhas e erros |

`:app:assembleDebug :app:testDebugUnitTest` concluiu com `BUILD SUCCESSFUL`.
O APK completo foi instalado no emulador `127.0.0.1:16384`.

## Terraria Android

| Registro do fixture | Verificações | Falhas |
|---|---:|---:|
| ModNPC antes de GlobalNPC | 19 | 0 |
| GlobalNPC antes de ModNPC | 19 | 0 |
| ModNPC sem GlobalNPC | 17 | 0 |
| Total | 55 | 0 |

Os resultados nativos estão em [native-results.log](native-results.log).
Os logs completos de cada rodada permanecem em
`build/npcaitype/127.0.0.1-16384/`.

Foi observado o tipo emprestado dentro de `AI_002_FloatingEye` e
`AI_003_Fighters`. Os callbacks de mod viram o tipo original; `type` e
`netID` permaneceram corretos após a execução. A velocidade comparada
com um Wandering Eye vanilla teve `dx=0` e `dy=0` nas três rodadas, com
direções iguais.

Um NPC que configura somente `AIType` foi acompanhado durante 30 ticks
do loop normal do mundo. O fixture não chamou sua IA diretamente nesse
intervalo; as chamadas foram feitas pelo jogo. Cada rodada também
executou 300 chamadas diretas reais para conferir restauração e identidade.

Passaram os casos de herança, duas instâncias com valores diferentes,
mudança durante `PreAI`, zero, negativo, veto local, veto global quando
presente e independência de `aiStyle` e `AnimationType`. A instância
condicional de `GlobalNPC` conservou sua identidade e seu campo de teste
ao atravessar a troca de tipo.

## Ambiente e limites

As rodadas usaram cópias do personagem e do mundo. Os NPCs criados foram
desativados, o pacote temporário foi removido e as preferências de
configuração, mods e poderes foram restauradas. Os arquivos restaurados
foram comparados com os backups, byte a byte. O APK testado ficou instalado.

A integração foi singleplayer. Os cenários `netMode` 1 e 2 foram verificados
na suíte JS, com ponte simulada; não houve sessão multiplayer entre dois
aparelhos nesta execução. A restauração após exceção da chamada original
também foi verificada por injeção na simulação, sem provocar falha nativa
no jogo. Os testes Android cobrem duas famílias de IA, não todos os NPCs
vanilla nem a adequação de cada combinação de `aiStyle` e `AIType`.

A conferência do catálogo de documentação validou Markdown, JSON, CSV e
Excel, incluindo os 1.138 registros, filtros e painéis fixos da planilha.
