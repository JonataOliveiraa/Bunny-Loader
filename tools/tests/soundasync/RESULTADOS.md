# Áudio customizado: avaliação e correção do despacho

Execução de 07/10/2026, emulador Android arm64 `127.0.0.1:16384`.
Dados verificáveis em [measurements.json](measurements.json); procedimento,
alternativas e casos de teste em [README.md](README.md).

## Análise

O filtro nativo já impedia sons vanilla de entrar no hook JS. O gargalo
restante era a espera síncrona de `SoundPool.play` pelo caminho
QuickJS → JNI → Android. No perfil adicional da versão filtrada, essa
chamada consumiu 2.103,91 dos 2.318,44 ms dos lotes mistos, aproximadamente
90,7%. Esse tempo inclui espera nativa; não é uma medição de CPU do QuickJS.

A hipótese desta etapa foi retirar essa espera da thread do jogo sem criar
uma fila ilimitada. Aceitar um pedido e iniciar áudio são resultados
distintos: filas, falhas do Android, cancelamento e pausa precisam ser
observáveis. A implementação proposta pelo desenvolvedor motivou a análise,
mas não foi copiada integralmente.

## Implementação avaliada

- Um worker `Bunny-SFX` executa play/stop/pause/resume fora do lock do
  produtor. O worker não executa JavaScript.
- Até 32 pedidos pendentes, incluindo a chamada nativa em andamento; até
  32 streams iniciados e 256 registros de histórico. A busca frequente usa
  apenas o conjunto de pedidos ativos, separado do histórico.
- Pedidos que aguardaram mais de 50 ms são descartados antes de chamar
  o Android. Esse limite não inclui a duração de `SoundPool.play` nem a
  latência do dispositivo de áudio.
- `MaxInstances`, `IgnoreNew`, `ReplaceOldest` e cancelamento incluem
  pendências. Pausa cancela pendências e conserva a duração restante
  estimada dos streams já iniciados, usando relógio monotônico.
- `SoundEngine.PlaySound` retorna um identificador de pedido aceito;
  `GetSoundState` distingue pendente, iniciado, falha e descarte.
  `FindActiveSound` exclui pendentes/falhos/pausados. O baixo nível
  `bl.sounds.play/stop` mantém seu contrato síncrono com streams Android.
- O filtro nativo foi preservado. Música mantém o caminho anterior.

A primeira versão da fila apresentou custo adicional em `IgnoreNew` por
consultar registros encerrados. A versão final separa registros ativos e
histórico. Somente as medições do APK final entram na tabela abaixo.

## Método e artefatos

Baseline: APK com filtro nativo e reprodução síncrona.

```text
512b77dcf49ef67816bcadeb1376d07692086baf180369ac9cf46eba2b16590c
```

Candidato: APK com filtro nativo e fila limitada.

```text
8d952564605976d37747a9c7021c3930c80e5d5047976a7f3151380763435298
```

Foram três rodadas por APK, na ordem A1/B1/B2/A2/A3/B3, A = candidato.
Cada fase teve 60 atualizações de aquecimento e 180 atualizações medidas,
com 32 NPCs estacionários inofensivos, música customizada e quatro efeitos
WAV de 200 ms. As rodadas principais rodaram sem build ou renderização
pesada concorrente. Perfis com wrappers e teste de ciclo de vida são
diagnósticos separados, não entram nas medianas.

O lote é o trecho de solicitações de áudio dentro de uma atualização.
`updateMs` mede o hook de atualização; `frameGapMs` mede o intervalo entre
desenhos observados. São medidas distintas; 180 atualizações não significam
180 frames renderizados. Valores abaixo são medianas das médias por rodada;
os p95 são medianas dos p95, não percentis de uma amostra combinada. Os
intervalos e dados por rodada estão no JSON.

O APK medido coincide com `app/build/outputs/apk/debug/app-debug.apk`.
Comparando as entradas ZIP com o baseline, só mudaram `classes3.dex`,
`classes5.dex`, `classes6.dex` e `lib/arm64-v8a/libbunny.so`; nenhuma entrada
foi adicionada/removida e os assets têm os mesmos CRCs e tamanhos.

## Resultado das três rodadas por variante

Todos os tempos estão em milissegundos, baseline → candidato.

| Carga por atualização | Média do lote | p95 do lote | Média da atualização | Intervalo médio entre desenhos |
|---|---:|---:|---:|---:|
| 48 sons vanilla | 0,185 → 0,231 | 0,297 → 0,418 | 4,014 → 3,834 | 23,198 → 19,874 |
| 48 vanilla + 12 customizados pelo hook | **14,508 → 0,805** | **57,791 → 1,769** | **17,453 → 4,532** | **69,887 → 19,531** |
| 12 customizados pelo hook | 12,280 → 0,578 | 55,391 → 1,474 | 14,720 → 4,160 | 46,245 → 19,227 |
| 12 customizados pela API JS | **12,297 → 0,509** | **53,877 → 1,246** | **15,159 → 4,049** | **54,453 → 18,663** |
| 1 customizado pela API JS | 0,846 → 0,213 | 1,169 → 0,390 | 3,208 → 3,030 | 16,851 → 16,853 |
| Rajada de 48 a cada 30 atualizações | 0,693 → 0,048 | 0,013 → 0,022 | 3,230 → 3,013 | 17,350 → 16,769 |
| 12 pedidos, mesmo estilo com IgnoreNew/limite 1 | 0,244 → 0,224 | 0,915 → 0,461 | 2,896 → 2,951 | 16,743 → 16,856 |

O lote misto caiu aproximadamente 94,5%; o lote de 12 pedidos pela API,
95,9%. O caminho vanilla teve zero entradas JS nas duas variantes; pequenas
diferenças de tempo não demonstram uma melhoria nesse caminho. Em
`IgnoreNew`, o lote ficou menor, mas a atualização média ficou cerca de
0,055 ms maior; não há ganho uniforme em todas as métricas.

O p95 das rajadas reflete principalmente atualizações sem rajada; os
pedidos acontecem em apenas seis das 180 amostras. A média e os contadores
são necessários para interpretar essa fase.

## Capacidade, descartes e falhas

Com um pedido por atualização, os 540 pedidos das três rodadas assíncronas
foram aceitos e tiveram retorno positivo de início: zero recusas, falhas
ou expirações; p95 da espera antes do despacho ≤ 1 ms. Isso não mede a
saída física do alto-falante.

Sob 12 pedidos por atualização, apenas aproximadamente metade dos pedidos
teve confirmação de início. Há ganho por retirar a espera do chamador e
por limitar trabalho excedente; não se trata de reproduzir todos os sons
com menor custo. A 60 atualizações/s, 12 efeitos de 200 ms por atualização
pediriam cerca de 144 vozes simultâneas, acima dos 32 streams configurados.

| Fase | Pedidos por rodada | Aceitos | Recusados | Iniciados | Falhas de início | Expirados | Pendentes ao fim, por rodada |
|---|---:|---:|---:|---:|---:|---:|---|
| Mista | 2.160 | 1.733 | 427 | 1.016 | 127 | 588 | 0 / 24 / 0 |
| Customizados pelo hook | 2.160 | 1.807 | 353 | 1.080 | 139 | 569 | 19 / 31 / 22 |
| Customizados pela API | 2.160 | 1.819 | 341 | 1.062 | 137 | 581 | 20 / 31 / 0 |
| Rajada | 288 | 196 | 92 | 189 | 0 | 0 | 13 / 16 / 0 |
| IgnoreNew | 2.160 | 14 | 2.146 | 14 | 0 | 0 | 0 / 0 / 0 |

Os contadores são medianas independentes de diferenças antes/depois de
cada fase; não devem ser somados como se fossem uma única rodada. Conclusões
do worker podem atravessar fases. `IgnoreNew` recusa por política do estilo,
não por falha de carregamento. Nas fases pesadas, o p95 da espera de
despacho ficou no intervalo até 50 ms; na rajada, até 20 ms.

O diagnóstico separado com quatro pedidos por atualização teve 720 pedidos
em cada variante, com wrappers de perfil ativos:

| Métrica | Síncrona | Assíncrona |
|---|---:|---:|
| Média do lote | 7,074 ms | 0,520 ms |
| p95 do lote | 19,321 ms | 0,933 ms |
| Média da atualização | 9,985 ms | 4,601 ms |
| Retornos positivos de início | 596 | 550 |
| Retornos zero de início | 124 | 170 |
| Recusas na admissão / expirações | não se aplica | 0 / 0 |

Nesse par, todos os pedidos assíncronos foram aceitos, mas houve mais
falhas de início nativo. Uma rodada não permite atribuir a diferença
exclusivamente à fila. Essa carga ainda pede cerca de 48 vozes a 60 Hz.
O teste demonstra redução da espera do jogo; não demonstra melhoria na
quantidade de efeitos reproduzidos.

Os logs completos registram `SoundPool::Stream: error creating AudioTrack`
e `AudioFlinger: no more tracks available` nas duas variantes. Nas seis
rodadas principais, houve respectivamente 2.057/2.125/2.127 ocorrências do
primeiro texto no baseline e 1.441/1.352/1.492 no candidato. São contagens
de linhas do logcat de toda a sessão, incluindo aquecimento e verificações,
não contadores por pedido; não equivalem a `queue.failed`. Não houve
`FATAL EXCEPTION`, nem exceção Java registrada pela fila. Um retorno
positivo de `SoundPool.play` também pode anteceder uma falha interna do
Android na criação de AudioTrack. A saturação nativa permanece um limite
observado, mesmo com o chamador liberado.

## Verificação funcional e restauração

- 92 testes JUnit, zero falhas/erros/ignorados; 23 testam a fila de produção.
  Cobrem retornos zero e exceções, limites, relógio/pitch, descarte, grupos,
  substituição, cancelamento antes/durante/depois de play, pausa concorrente,
  10.000 pedidos e um worker real bloqueado enquanto o produtor continua.
- 20 verificações JS com os arquivos reais e doubles do backend: roteamento
  vanilla/customizado, argumentos, volume/pan/pitch, cache, API e estados.
  Esses testes não medem a performance do QuickJS.
- 402 verificações funcionais Android, zero falhas, em 11 sessões finais:
  seis A/B principais (240), dois perfis (80), uma sessão de ciclo de vida
  (52) e o par de quatro pedidos por atualização (30).
- O teste de pausa cobriu a Activity do jogo por seis segundos durante um
  WAV de cinco segundos. O worker registrou 6.081,21 ms de pausa; o mesmo
  identificador continuou consultável e cancelável após retomar. O runner
  confirmou a Activity fora de `resumed`, incluindo emulador com múltiplos
  displays. Uma tentativa anterior que só enviava HOME não pausava essa
  Activity; foi invalidada e não entra nos resultados.
- Build completo bem-sucedido; catálogo de API Markdown/JSON/CSV/XLSX
  conferido, 1.139 linhas; diff sem erros de whitespace.
- Preferências restauradas byte a byte; hashes SHA-256 dos 35 arquivos
  originais de Players/Worlds conferidos. As cópias e o mod de teste foram
  removidos; não há sessão de jogo de teste ativa. A verificação dos saves
  cobre os arquivos da raiz dessas pastas, não uma auditoria recursiva dos
  diretórios de mapas. O APK candidato permanece instalado.

## Conclusão e limites da evidência

A correção reduz de forma repetida a espera no caminho customizado da
thread do jogo e mantém memória/trabalho pendente limitados. Estados de
falha, descarte e cancelamento são explícitos. Isso evita transferir uma
fila de áudio crescente para o QuickJS ou para um executor ilimitado.

O SoundPool continua sujeito à capacidade nativa do dispositivo. Esta
execução não gravou a saída de áudio, não mediu dispositivos físicos e não
validou uma luta real com o chefe do Split. Os 32 NPCs são uma carga
controlada, não uma reprodução daquele encontro. Não há evidência para
prometer todos os sons ou 60 FPS em qualquer cenário. Um mixer diferente
ou uma política de prioridade/coalescência por tipo de som seria outra
etapa, com mudanças audíveis que exigem avaliação própria.
