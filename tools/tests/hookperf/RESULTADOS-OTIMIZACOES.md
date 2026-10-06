# Otimizações de desenho e cabelo em 6 de outubro de 2026

O custo observado dos hooks de reordenação caiu de 1,221 ms para 0,460 ms por desenho no cenário Skin após Head, uma redução de aproximadamente 62,3%. O código mantém a frequência dos callbacks, a ordem estável das camadas, o prefixo inserido por ModifyDrawInfo e os dados de shaders/texturas.

## Análise e escolha

A [medição anterior](RESULTADOS.md) encontrou cópia do cache inteiro de 1.024 elementos, escrita de cada DrawData pela ponte, conjuntos temporários por camada e leituras repetidas de equipamento. Os limites incluem segmentos vazios, ordem já satisfeita, dependências repetidas, ciclos, dados fora da região reordenada, reentrada e desenhos de jogadores distintos.

| Abordagem | Tempo | Espaço adicional | Manutenção |
| --- | --- | --- | --- |
| Copiar a região ativa e escrever cada elemento pela ponte | O(D), com D escritas pela ponte | O(D) | Mudança pequena, conserva o custo por elemento. |
| Copiar a região ativa e mover blocos com Array.Copy | O(D) no jogo, até B chamadas de cópia pela ponte | O(D) | Usa a cópia do runtime, preservando referências dentro de structs. Foi escolhida. |
| Reutilizar buffers entre desenhos | O(D), com B chamadas pela ponte | Capacidade retida por contexto | Exige controlar reentrada, jogadores, vida útil e referências retidas; não foi aplicada. |

D é o tamanho da região que precisa de uma cópia temporária; B é o número de blocos movidos após agrupar segmentos adjacentes. A ordenação continua usando busca estável O(L²), com 47 passagens de camada observadas neste cenário. Loops diretos removem callbacks de busca e conjuntos sem dependências, sem mudar qual camada elegível é escolhida primeiro.

## Implementação

- [PlayerDrawHooks](../../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js) cria conjuntos de arestas somente quando há dependências. O grau -1 identifica camadas já consumidas na busca estável. Ciclos continuam ignorando a reordenação.
- Se todos os segmentos com conteúdo já estão na posição de destino, não há clone nem cópia, mesmo que as posições solicitadas não estejam vazias. Segmentos vazios podem mudar de posição sem mover dados.
- Quando há mudança, o loader lê DrawDataCache uma vez, clona somente até o fim da região reordenada, agrupa segmentos originalmente adjacentes e usa a sobrecarga de cinco argumentos de System.Array.Copy. Prefixos, sufixos e slots não utilizados permanecem no array original. A contagem ativa e a identidade desse array não mudam.
- A cópia usa o runtime do jogo para preservar as referências de texturas dentro de DrawData e suas barreiras de escrita. Não foram introduzidos buffers compartilhados nem cache permanente de DrawData.
- [ArmorSetLoader](../../../app/src/main/cpp/script/js/mod/Loaders/ArmorSetLoader.js) lê head, face, faceHead e hair uma vez por callback. Os resultados e Sets continuam sendo consultados em cada chamada, permitindo mudanças imediatas de equipamento e configuração.

## Comparação no Android

Uma instância MuMu ARM64, QuickJS, singleplayer, mesmo personagem/mundo e apenas o fixture habilitado. Os dois processos receberam oito classes ModPlayer de tick; o ModPlayer de desenho foi registrado após a fase inicial. Houve 120 updates de aquecimento e 300 updates medidos por fase.

| APK | SHA-256 |
| --- | --- |
| Antes | 0215698C160FF97100D1F2E82D7E8E90F2300EEF939E42D2C5D23184C7B63CCA |
| Depois | F9ED0E95B3A70157DFB848A481FA06015BBDCEC09EAF0F7A31DD90472CA172A1 |

As entradas dos dois APKs têm os mesmos nomes. Somente lib/arm64-v8a/libbunny.so mudou de conteúdo, conferido pelo CRC e tamanho das entradas ZIP. A instalação de cada APK também foi conferida pelo hash de base.apk no Android.

Custos médios normalizados pelos desenhos de cada fase, a partir de bl.hookStats().jsMs:

| Cenário | Hook principal antes | Hook principal depois | Camadas antes | Camadas depois |
| --- | ---: | ---: | ---: | ---: |
| Ordem vazia | 143,404 µs | 99,398 µs | 0 | 0 |
| AddDrawData | 139,027 µs | 97,128 µs | 0 | 0 |
| Ocultar HeldItem | 130,671 µs | 101,041 µs | 109,944 µs | 87,163 µs |
| Skin após Head | 1.022,441 µs | 312,196 µs | 198,192 µs | 147,932 µs |
| Ordem vazia, repetição | 127,365 µs | 99,592 µs | 0 | 0 |

O hook principal é DrawPlayer_UseNormalLayers. As camadas registraram 14.100 chamadas nativas por fase de desenho em ambos os APKs. Nas fases sem ocultação/reordenação, todas essas chamadas continuaram fora do JavaScript. GetHairSettings, durante Skin após Head, passou de 121,808 para 79,936 µs por chamada; houve duas chamadas por desenho.

A comparação contém 3.600 updates, 3.516 desenhos e 432.000 callbacks de atualização. Cada fase manteve exatamente 36.000 callbacks. Os filtros de dano/frames de ModItem continuaram com zero entradas JS para itens vanilla e de mod sem sobrescritas; os casos ativos mantiveram 7.100 callbacks de dano e 3.600 de frames, incluindo aquecimento.

O tempo geral do emulador também variou: a mediana DoDraw da ordem vazia foi de 7,645 para 5,136 ms, e sua repetição foi de 7,219 para 5,517 ms. Portanto a redução observada dos hooks não deve ser convertida em ganho garantido de FPS nem atribuída integralmente à implementação sem considerar a variação de carga. jsMs exclui original() e a espera anterior pela trava; não mede filtros rejeitados ou pausas de GC. A frequência de callbacks foi conferida, e o trabalho evitado na cópia e no cálculo da ordem é verificável no código.

Evidência: [native-optimized.log](native-optimized.log). Logs brutos e preferências de teste ficam em build/hookperf/20261006-opt/ e build/hookperf/20261006-opt-final/.

## Regressões e build

Foram aprovadas 204 verificações automatizadas: 55 de ModPlayer, 138 de ModItem e 11 de cabelo. A assinatura Array.Copy foi conferida no dump mobile local. Os novos casos cobrem múltiplos DrawData por segmento, referências de textura/shader, prefixos e sufixos, capacidade não utilizada, dependências duplicadas, ordens sem efeito, segmentos vazios, ciclos, jogadores aninhados e restauração dos filtros após falha de cópia.

O [fixture nativo de cache](../playerdrawcache/README.md) chegou ao renderer com 60 desenhos de inserção manual e 66 com o helper. Conferiu a ordem efetiva dos marcadores de Skin e Head em 32 desenhos reordenados, 16 com ordem já satisfeita e 9 com ciclo ignorado. Resultado: lost=0, failed=0, passou=true. A captura visual confirmou os três marcadores após o término. O caso sem contador reproduziu 26 desenhos em que a inserção foi sobrescrita.

O build completo ARM64 passou, com libs/assets do jogo presentes, assinatura APK v2 e zipalign conferidos. O APK otimizado foi copiado para out/bunny-loader.apk. As chaves de preferências alteradas pelos fixtures foram restauradas, seus diretórios removidos e a única instância iniciada desligada; preferências atualizadas pelo próprio launcher foram preservadas.

Esta execução nativa foi singleplayer. Os testes automatizados verificam isolamento entre jogadores e reentrada, mas não constituem uma nova rodada multiplayer entre processos. Uso de heap e pausas de GC também não foram medidos.
