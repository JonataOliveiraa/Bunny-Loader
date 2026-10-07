# Despacho limitado de efeitos customizados

## Análise

Entrada: pedidos de SoundStyle, incluindo UseSound/HitSound/DeathSound e a
API direta. Saída: identificador consultável, início confirmado pelo retorno
do Android ou estado terminal, sem esperar pelo SoundPool na thread do jogo.

Fatos medidos na etapa anterior: o filtro elimina as entradas vanilla no
QuickJS; em uma rodada diagnóstica, cerca de 90–93% do tempo dos lotes
customizados estava dentro de bl.sounds.play. Esse intervalo inclui JNI e
espera no Android, não mede CPU pura do QuickJS.

Hipóteses verificadas nesta etapa: retirar essa espera deve reduzir o tempo
dos lotes e das atualizações; uma fila precisa ter limites e descartar
efeitos antigos para não transformar travadas em áudio atrasado.

Casos considerados: carga/arquivo indisponível, fila cheia, backend 0 ou
exceção, pitch inválido, grupos distintos, ReplaceOldest, IgnoreNew,
cancelamento pendente ou em andamento, pausa concorrente, retomada após uma
pausa longa, limite global de streams e retenção de milhares de pedidos.

## Design

| Alternativa | Tempo e espaço | Avaliação |
|---|---|---|
| Limitar instâncias no JS e continuar síncrono | Trabalho por pedido proporcional às instâncias do estilo; backend ainda bloqueia o chamador | Reduz chamadas, mas altera quantos sons cada mod pede e mantém o gargalo |
| Executor com tarefa/limpeza por pedido | Fila e timers crescem com o número de pedidos; precisa sincronizar tokens e ciclo de vida | A sugestão original apresentou regressões de falha, pausa e acúmulo de tarefas |
| Worker único com histórico e pendências limitados | Busca ativa O(L), L ≤ 64 pedidos/streams em andamento; histórico H ≤ 256; até 32 pendências incluindo a chamada nativa; memória O(H + L) | Implementado em SfxQueue, separado do Android para testes determinísticos |

O worker chama play/stop/autoPause/autoResume fora do lock do produtor. Não
executa JS. Limites contam pendências, duração começa após o retorno positivo
de play e usa relógio monotônico congelado durante a pausa aplicada. Um
cancelamento durante play conserva o registro até poder parar o stream
retornado. Os pedidos em andamento ficam separados do histórico: IgnoreNew
não varre 256 registros encerrados a cada recusa. Não há ScheduledFuture
por efeito nem fila genérica ilimitada.

Pedidos que aguardaram mais de 50 ms são descartados antes da chamada
Android. Isso limita a espera para despacho, não a duração de SoundPool.play
nem a latência física do alto-falante. O backend continua com 32 streams.
Em saturação há recusas, descarte e preempção; os resultados registram esses
custos. O fim de um stream continua sendo uma estimativa por duração/pitch,
porque SoundPool não oferece uma consulta de posição/fim por stream.

O contrato de SoundEngine foi atualizado explicitamente: PlaySound devolve
um pedido aceito, GetSoundState distingue pendente/iniciado/falha, StopSound
cancela também pendências e FindActiveSound exclui pendentes/falhos/pausados.
O baixo nível bl.sounds.play/stop continua síncrono e usa streams Android;
não intercambie esses streams com os pedidos de SoundEngine. Seu uso direto
fica fora do controle de limites da fila. Música mantém o caminho anterior.

## Testes

```powershell
node tools/tests/soundfilter/check.mjs
# Com o JDK/Gradle do projeto:
gradle :app:testDebugUnitTest :app:assembleDebug --console=plain
```

SfxQueueTest testa o código de produção com relógio e backend controlados.
Inclui um worker real bloqueado enquanto outro produtor faz mil pedidos e
cancela. Os doubles verificam estados e ordenação, sem alegar medir áudio
Android ou latência do dispositivo.

## A/B Android

O runner compartilhado usa --suite soundasync para manter os resultados
separados da etapa de filtro. Requer ADB com su/run-as e sessão de jogo
fechada no prepare. Usa cópias dos saves e restaura preferências e hashes
dos arquivos originais, conforme o README de soundfilter.

Preserve antes do build o APK com apenas o filtro nativo em
build/soundasync/baseline.apk. Compile o candidato completo e não execute
compilações/renderizações pesadas durante as rodadas principais.

```powershell
python tools/tests/soundfilter/device.py prepare --suite soundasync
python tools/tests/soundfilter/device.py run --suite soundasync --variant filtered --label filtered-1 --apk build/soundasync/baseline.apk
python tools/tests/soundfilter/device.py run --suite soundasync --variant async --label async-1 --apk app/build/outputs/apk/debug/app-debug.apk
# Completar três rodadas por variante; compare.py usa A/B/B/A/A/B.
python tools/tests/soundasync/analyze.py build/soundasync/127.0.0.1-16384 --out build/soundasync/summary.json
# Diagnósticos adicionais, com labels separados:
python tools/tests/soundfilter/device.py run --suite soundasync --variant async --label lifecycle-async --lifecycle --apk app/build/outputs/apk/debug/app-debug.apk
python tools/tests/soundfilter/device.py restore --suite soundasync
```

Cada rodada reutiliza 32 NPCs inofensivos, música customizada, quatro WAVs
de 200 ms e as quatro fases da etapa anterior. Acrescenta custom-normal
(um pedido/atualização), custom-burst (48 pedidos a cada 30 atualizações) e
custom-ignore (12 pedidos/atualização, MaxInstances 1/IgnoreNew). --profile
acrescenta os wrappers de tempo. --lifecycle cobre a Activity do jogo por
seis segundos durante um efeito de cinco segundos, retoma a mesma Activity
e verifica o identificador e a duração de pausa registrada pelo worker.

Em emuladores com múltiplos displays, HOME pode atingir outro display e
deixar o jogo rodando. O runner identifica o root task/display do jogo,
abre o launcher e, se preciso, move o root task dele para o mesmo display.
Confere que GameActivity deixou o estado resumed antes de contar a pausa.
Não altera o ciclo de vida de produção para viabilizar o teste.

`compare.py --diagnostics` automatiza três rodadas por APK e os diagnósticos,
restaurando o ambiente em `finally`. `--prepared --resume` continua uma
preparação existente, conferindo o hash de cada APK nas rodadas reaproveitadas.

Para repetir somente o diagnóstico de quatro pedidos por atualização:

```powershell
python tools/tests/soundasync/compare.py --moderate-only --candidate build/soundasync/candidate.apk
```

Essa opção prepara cópias novas, executa um par A/B com `--profile
--normal-rate 4 --only-normal` e restaura o ambiente. `--normal-rate` aceita
1 a 12 pedidos; `--only-normal` executa apenas essa fase e requer
`--suite soundasync`. Quatro pedidos por atualização ainda excedem 32 vozes
se todos os efeitos de 200 ms forem mantidos a 60 atualizações por segundo.
O par registrado é diagnóstico, sem a repetição das rodadas principais.
Os perfis completos registrados nesta execução usaram um pedido por
atualização na fase normal; novas execuções de `--diagnostics` usam quatro.

O resumo apresenta medianas de três rodadas, intervalos, contadores da fila
e histograma da espera antes do despacho. Started/failed/expired podem
atravessar os limites da fase: use pendingAtEnd para avaliar trabalho ainda
em andamento. Não confunda confirmação de SoundPool.play com medição da
saída física. No perfil, `failed` conta retornos zero do transporte: na variante
síncrona são falhas de play; na assíncrona são recusas de admissão. A falha
de início assíncrono é `queue.failed`. Consulte RESULTADOS.md e
measurements.json para a execução.
