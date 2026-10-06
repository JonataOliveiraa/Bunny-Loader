# Análise de desempenho em 6 de outubro de 2026

Esta é a medição anterior às mudanças de produção. A [comparação após as otimizações](RESULTADOS-OTIMIZACOES.md) registra a implementação, os dois APKs e as regressões.

Medição no QuickJS do APK debug atual, em uma instância MuMu ARM64, singleplayer, mesmo personagem e mundo de teste, com os outros pacotes temporariamente desativados. O APK instalado foi conferido pelo SHA-256:

```text
0215698C160FF97100D1F2E82D7E8E90F2300EEF939E42D2C5D23184C7B63CCA
```

O código de produção não foi alterado nesta análise. Foram acrescentados um fixture, validação dos seus registros e suporte a isolamento no auxiliar de dispositivo. O APK conserva o estado atual do workspace, incluindo alterações já existentes fora desta análise; esta medição não compara somente os últimos commits.

## Desenho: principal oportunidade encontrada

A capacidade real do DrawDataCache foi 1.024 entradas. `ModPlayer.AddDrawData` reaproveita esse array, escreve um elemento e incrementa o contador; não instala hooks e tem tempo/espaço adicionais constantes.

| Consulta pela ponte | Mediana | Chamadas medidas |
| --- | ---: | ---: |
| Inserção manual com contador | 2,463 µs | 7.000 |
| Inserção com `ModPlayer.AddDrawData` | 4,479 µs | 7.000 |

As duas consultas incluem restauração do contador. O DrawData e seus argumentos foram criados uma vez antes da medição. Esses tempos não incluem a criação de um marcador a cada quadro.

O caminho de `ModifyDrawLayerOrdering` foi o mais caro entre os cenários de desenho exercitados:

| Cenário | Hook principal: custo JS por desenho | Entradas JS das camadas em 300 desenhos | Custo JS somado das camadas por desenho |
| --- | ---: | ---: | ---: |
| Ordem vazia | 105,367 µs | 0 | 0 |
| Apenas AddDrawData | 128,489 µs | 0 | 0 |
| Ocultar HeldItem | 88,377 µs | 14.100 | 85,700 µs |
| Reordenar Skin após Head | 799,975 µs | 14.100 | 159,350 µs |
| Ordem vazia, repetição | 111,350 µs | 0 | 0 |

O hook principal é `LegacyPlayerRenderer.DrawPlayer_UseNormalLayers`. As camadas tiveram 14.100 chamadas nativas em cada cenário, correspondendo a 47 passagens por desenho. Sem mudança de ordem ou ocultação, o filtro impediu todas as passagens dessas camadas pelo JavaScript.

A leitura de [PlayerDrawHooks.#Layers/#Reorder](../../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js) explica o trabalho adicional: cria mapas, conjuntos e segmentos, resolve a ordem e copia o cache de capacidade completa antes de reescrever os segmentos. [TypeTables::resizedCopy](../../../app/src/main/cpp/content/common/TypeTables.cpp) aloca um novo array nativo e usa `System.Array.Copy`; portanto a cópia de 1.024 elementos por desenho também produz trabalho para o coletor do jogo. Não foi medida uma pausa de GC nem uso de heap, e esse fato não estabelece vazamento de memória.

Prioridade de otimização: copiar apenas a região ativa, evitar a cópia quando a ordem calculada for idêntica e avaliar cópia por blocos em vez de acesso de cada DrawData pela ponte. Reutilizar buffers exige preservar reentrada, prefixo inserido por ModifyDrawInfo, contagem, shaders e dados distintos por jogador. Nenhuma dessas mudanças foi aplicada nesta rodada.

## Despacho por tick

As classes do teste representam 0, 1 e 8 ModPlayers **no mesmo jogador**, não uma sessão com oito jogadores.

| Consulta nativa pela ponte | 0 classes | 1 classe | 8 classes |
| --- | ---: | ---: | ---: |
| `CapAttackSpeeds` | 0,245 µs | 2,867 µs | 6,023 µs |
| `ItemCheck` | 1,987 µs | 3,947 µs | 9,175 µs |

Cada mediana usa sete amostras de 4.000 chamadas. CapAttackSpeeds inclui o corpo nativo e PostUpdateMiscEffects. ItemCheck executa seu corpo com zero classes e é vetado com uma e oito; seu custo inclui PreItemCheck e PostItemCheck nesses dois últimos casos.

Os 300 updates produziram zero, 4.500 e 36.000 callbacks de atualização respectivamente. Nas seis fases com oito classes, a contagem permaneceu em 36.000 por fase. A frequência dos callbacks foi preservada.

Na fase inicial de oito classes, o perfil dos hooks listados somou 607,991 µs por update. Desses, 187,613 µs vieram de GetHairSettings, também presente com zero classes. Excluindo esse hook comum, o subtotal observado foi 420,378 µs por update. Esse subtotal é uma medida dos hooks listados, não o custo completo de ModPlayer: não separa os estágios internos nem contabiliza o caminho dos filtros ou espera pela trava.

A revisão confirma listas de interessados em cache, despacho proporcional ao número de classes interessadas, instalação compartilhada por grupo e ausência de reflexão sobre protótipos a cada chamada. Ainda existem grupos separados em Player.Update. Uma eventual consolidação precisa preservar ordem e semântica de movimento, exceções, conexões e registro posterior; reduzir a frequência dos callbacks mudaria o contrato.

## ModItem: os filtros funcionaram

| Consulta, execução com oito classes de tick | Mediana | Entradas JS do hook correspondente |
| --- | ---: | ---: |
| GetWeaponDamage, vanilla | 0,683 µs | 0 |
| GetWeaponDamage, mod sem sobrescrita | 0,585 µs | 0 |
| GetWeaponDamage, mod com modificador | 11,387 µs | 7.100 |
| PlayerFrame, vanilla | 1,671 µs | 0 |
| PlayerFrame, mod sem sobrescrita | 1,637 µs | 0 |
| PlayerFrame, mod com callback | 6,624 µs | 3.600 |
| Seleção de munição, sem candidato | 36,493 µs | Caminho de seleção ativo |
| Seleção de munição, slot 54 | 7,229 µs | Caminho de seleção ativo |
| Seleção de munição, slot 53 | 7,044 µs | Caminho de seleção ativo |

Entradas ativas incluem 100 chamadas de aquecimento. Os filtros de dano e frames também deram zero entradas JS nos casos vanilla e sem sobrescritas das execuções com zero e uma classe. Os tempos incluem a chamada do fixture pela ponte, o corpo nativo e o callback quando aplicável; não isolam somente o dispatcher.

ModItem prepara planos e rótulos uma vez por classe e usa marcas por tipo. O filtro de frames consulta o item selecionado diretamente no código nativo, sem varrer o inventário. Flags e whileIn são verificados antes da trava e da conversão de argumentos JavaScript. Um filtro rejeitado continua pagando a passagem pelo hook nativo; zero entradas JS não significa custo zero.

A seleção de munição é proporcional aos candidatos percorridos. O caso vazio mostrou custo maior que um candidato encontrado cedo. Sobrescrever CanBeChosenAsAmmo amplia a marca de seleção para armas vanilla; esse caminho global merece um cenário próprio com muitos candidatos e alternância. O slot 53 desta medição é uma posição da matriz nativa e não representa necessariamente o último candidato ou uma colocação usual pela UI.

## Cabelo e outros caminhos

GetHairSettings executou duas vezes por desenho neste cenário. Seu custo JS médio por chamada variou de aproximadamente 68 a 106 µs entre as fases. Ele está instalado mesmo sem ModPlayers neste APK e não deve ser atribuído ao custo novo de ModItem.

O [callback de cabelo](../../../app/src/main/cpp/script/js/mod/Loaders/ArmorSetLoader.js) repete leituras de head, face e hair através da ponte, consulta faceHead e entrega cinco referências de saída. A otimização de menor alcance seria ler os slots uma vez dentro de cada chamada. Um cache persistente de resultados precisaria observar alterações dos Sets e de equipamento/cabelo em tempo real; não foi aplicado.

FrameEffects usa o dispatcher de armaduras e pode acrescentar leituras de equipamento/vaidade. CopyClientState e SendClientChanges dependem também do tamanho do estado copiado pelo mod. O acompanhamento de conexão percorre os jogadores conectados, com endereço armazenado, sem manter desconectados na varredura. Esses caminhos foram revisados no código e não medidos nesta execução singleplayer.

## Tempos gerais e limites

| Fase | Mediana DoUpdate | Mediana DoDraw | P95 DoDraw |
| --- | ---: | ---: | ---: |
| Zero classes de tick | 3,416 ms | 9,153 ms | 11,792 ms |
| Uma classe de tick | 3,697 ms | 9,523 ms | 12,121 ms |
| Oito classes de tick | 4,248 ms | 9,406 ms | 13,702 ms |
| Desenho vazio | 2,581 ms | 5,165 ms | 9,044 ms |
| Helper | 2,235 ms | 5,408 ms | 9,627 ms |
| Ocultação | 2,835 ms | 4,955 ms | 6,983 ms |
| Reordenação | 2,958 ms | 5,842 ms | 9,450 ms |
| Desenho vazio, repetição | 2,859 ms | 5,759 ms | 10,046 ms |

A repetição vazia variou cerca de 11,5% na mediana de DoDraw. Isso impede atribuir uma diferença pequena do tempo total exclusivamente a um hook. Também impede converter essas medições em ganho garantido de FPS. O perfil individual de reordenação evidencia seu trabalho adicional mesmo com essa variação.

A evidência em [native-results.log](native-results.log) foi extraída dos logs dos três processos. Foram conferidas oito fases, 2.400 updates, 2.314 desenhos e 220.500 callbacks de atualização. As primeiras duas execuções usam o campo `players` para a quantidade de classes; o fixture passou a chamar esse campo de `classes` para evitar confusão com multiplayer. O verificador aceita ambos. Os logs brutos permanecem em `build/hookperf/20261006-current/`.

As preferências alteradas foram conferidas contra os backups após a limpeza, o fixture e seus temporários foram removidos, o processo encerrado e a única instância iniciada para esta medição desligada. Sintaxe e validação dos registros foram conferidas. O [roteiro](README.md) descreve o escopo e as condições para repetir.
