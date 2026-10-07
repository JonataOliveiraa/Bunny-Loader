# Resultados do filtro nativo de áudio

Execução: 7 de outubro de 2026, Android ARM64 no emulador
`127.0.0.1:16384`. APKs completos; a comparação dos arquivos dentro dos
APKs encontrou diferença somente em `lib/arm64-v8a/libbunny.so`.

**O filtro removeu as entradas vanilla no callback de áudio. O custo dos
sons customizados continuou alto sob rajadas; não houve ganho geral de
frametime demonstrado na carga mista.**

## Mudança e ambiente

Somente `SoundLoader.Install` mudou na produção: marca o tipo 1000 e
instala o filtro por argumento/marca antes do lock do QuickJS. Kotlin e
música permanecem com o comportamento anterior.

Cada rodada habilitou apenas o fixture, com quatro tons customizados de
200 ms, música customizada em loop e 32 NPCs temporários. Depois de
aquecimento, cada fase mediu 180 atualizações. Os NPCs não executam a IA
de um boss real; o teste reproduz a quantidade de pedidos de áudio, o
registro de ModNPC e a seleção de música, com o mundo/renderização ativos.

A sequência principal foi A1, B1, B2, A2, A3, B3. A = APK anterior;
B = filtro nativo. Cada rodada partiu dos mesmos bytes iniciais de
personagem/mundo. Os testes de perfil abaixo são separados dessa sequência.

## Contadores reais da ponte

Mediana de três rodadas. Cada número de entradas corresponde às
180 atualizações da fase, incluindo eventuais sons naturais do mundo.

| Carga por atualização | Entradas JS no hook antes | Depois | Resultado |
| --- | ---: | ---: | --- |
| 48 sons vanilla | 8.641 | 0 | Vanilla deixou de executar o callback |
| 48 vanilla + 12 customizados pelo marcador | 10.800 | 2.160 | Cerca de 80% menos entradas; somente os marcadores customizados |
| 12 customizados pelo marcador | 2.160 | 2.160 | O custo do customizado permanece |
| 12 customizados pela API JS direta | 0 | 0 | Esse caminho já executava SoundLoader.Play diretamente, sem o hook Legacy |

Zero na última linha não significa ausência de JS: os pedidos e o
SoundLoader.Play ainda são JavaScript. Em todas as rodadas filtradas,
o número de entradas nesse hook foi exatamente o número de marcadores
customizados pedidos pelo fixture.

O callback vanilla consumia uma mediana de 14,925 ms no conjunto das
180 atualizações, aproximadamente **0,083 ms/atualização**. Esse trecho
deixou de ser executado. A ponte ainda tem seu despacho/filtro nativo e
o jogo continua processando o áudio original.

## Tempo dos lotes e limites da comparação

Mediana das médias de três rodadas, em milissegundos por atualização.
O lote é o tempo para disparar os pedidos, não o frame inteiro.

| Carga | Antes | Depois | p95 antes | p95 depois |
| --- | ---: | ---: | ---: | ---: |
| Vanilla | 0,251 | 0,170 | 0,404 | 0,247 |
| Mista | 7,792 | 13,659 | 39,593 | 58,036 |
| Customizada pelo marcador | 11,983 | 11,690 | 47,996 | 44,866 |
| Customizada direta | 11,673 | 11,394 | 45,908 | 42,013 |

Na fase vanilla, o lote ficou aproximadamente **32% mais rápido**. Trata-se
de uma economia pequena em termos absolutos, cerca de 0,081 ms para os
48 pedidos dessa carga. O benefício adicional de evitar disputa pelo
lock é garantido pelo posicionamento do filtro, mas não foi quantificado
por um experimento concorrente nesta execução.

Na carga mista, a versão filtrada ficou mais lenta no conjunto principal.
Os tempos médios de lote variaram de **3,910 a 13,586 ms** antes e
**12,422 a 15,072 ms** depois. Essa piora observada não foi omitida nem
convertida em uma alegação de melhoria de FPS. A economia no callback
vanilla não explica o restante desses tempos; o perfil adicional localiza
a maior parte do tempo no backend, mas não estabelece a causa exata da
diferença entre as rodadas principais.

Os intervalos médios entre desenhos, também pela mediana das três rodadas,
foram 17,514 → 17,962 ms na fase vanilla e 28,291 → 58,950 ms na mista.
Logo, **não há evidência de ganho global de FPS nem garantia de 60 FPS**.
O teste de saturação customizado permanece crítico. O jogo executou
várias atualizações entre desenhos nessas fases; 180 atualizações não
representam 180 frames renderizados.

## Perfil adicional: JavaScript versus chamada ao backend

Uma rodada diagnóstica por APK instrumentou `bl.sounds.play` somente no
fixture, sem mudança em Kotlin. Essas rodadas não entram nas medianas
anteriores. Os tempos incluem JNI/despacho/espera no Android; não são
medidas de CPU exclusiva do SoundPool nem do QuickJS.

| Fase | Parcela do lote dentro de bl.sounds.play, antes/depois | Retornos zero em 2.160 chamadas, antes/depois |
| --- | ---: | ---: |
| Mista | 90,2% / 91,2% | 313 / 324 |
| Customizada pelo marcador | 92,5% / 91,7% | 275 / 298 |
| Customizada direta | 92,8% / 92,9% | 293 / 327 |

O diagnóstico sustenta que a maior parte do tempo dessa carga ocorre
enquanto o JS aguarda o caminho nativo de reprodução. Não sustenta que
90%–93% seja CPU gasta pelo interpretador. Os retornos zero indicam pedidos
sem reprodução confirmada; a razão interna de cada falha não foi
instrumentada. O contrato síncrono existente conserva esse retorno zero.

Também foi testada a repetição de **um único efeito** com
`MaxInstances: 1` e `SoundLimitBehavior.IgnoreNew`, mantendo 12 pedidos por
atualização. Dos 2.160 pedidos, só **15 chegaram ao backend** e nenhum
retornou zero. O lote médio ficou em **0,206 / 0,216 ms** antes/depois, com
intervalo médio entre desenhos de 16,759 / 16,853 ms.

Esse último teste muda a política de reprodução e usa um único estilo,
portanto não representa uma otimização transparente da carga com quatro
efeitos ilimitados. Demonstra que o controle de instâncias já disponível
é efetivo para efeitos repetitivos quando essa política combina com o mod.
Nenhum limite ou default foi alterado globalmente nesta mudança.

## Validação funcional e restauração

| Verificação | Resultado |
| --- | --- |
| JS com classes de produção e ponte simulada | 19 verificações passaram |
| Android: seis rodadas principais | 132 verificações, zero falhas |
| Android: duas rodadas de perfil | 50 verificações, zero falhas funcionais |
| Gradle assembleDebug + testDebugUnitTest | Build passou; 69 testes unitários sem falhas, resultados reaproveitados do cache |

Passaram: instalação e marcadores distintos, ReplaceOldest, IgnoreNew,
distância, FindActiveSound, item usado de verdade com UseSound customizado,
música tocando junto e os 32 NPCs ativos/removidos. A ausência de falhas
funcionais do fixture não significa que todos os pedidos do teste de
saturação resultaram em áudio; os retornos zero foram registrados acima.

Ao final de ambos os conjuntos, as preferências de configuração, mods e
poderes foram restauradas byte a byte. Os arquivos de saves originais e
sidecars na raiz de Players/Worlds foram conferidos por SHA-256. O pacote,
os saves e diretórios temporários do fixture foram removidos. O APK
filtrado ficou instalado, com o launcher aberto.

Os dados estão em [measurements.json](measurements.json); logs completos,
backups e os APKs usados permanecem em `build/soundfilter/`. O
[README](README.md) documenta reprodução, geração dos assets e limites.

## Avaliação para o próximo passo

O filtro é apropriado para eliminar as entradas vanilla desnecessárias e
foi integrado sem mudar os contratos dos efeitos. Ele **não basta para
impedir sobrecarga causada pelos próprios sons customizados**.

O perfil dá uma justificativa mensurável para investigar o transporte de
pedidos ao Android. Uma eventual fila precisa preservar falhas, handles,
stop e pausa, além de limitar/cancelar pendências; a proposta revisada
anteriormente não atendia a esses requisitos. A carga mista também precisa
ser medida novamente em aparelho físico e, quando disponível, com o boss
do Split e seus arquivos/limites reais. Não foi integrada uma fila nem
mudada a música nesta etapa.
