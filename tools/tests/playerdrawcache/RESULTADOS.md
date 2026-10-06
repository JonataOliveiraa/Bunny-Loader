# Inserção de DrawData no cache do jogador

## Diagnóstico

No Terraria móvel integrado, `PlayerDrawSet.DrawDataCache` é `DrawData[]` e `DrawDataCacheCount` informa a quantidade ativa. O renderer limpa o contador antes de `BoringSetup`. O callback `ModifyDrawInfo` roda depois dessa preparação, antes das camadas nativas. Escrever em `DrawDataCache[DrawDataCacheCount]` sem aumentar o contador permite que a próxima camada sobrescreva a entrada.

O [PlayerDrawSet do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/DataStructures/PlayerDrawSet.cs.patch) usa `List<DrawData>`. A inserção nesse formato não se transfere diretamente para o array móvel. A leitura do dump e do renderer nativo confirmou o contrato do contador; o teste em jogo confirmou o efeito.

O trecho completo do mod que motivou o relato não foi fornecido. A reprodução cobre a escrita sem atualizar o contador. Não estabelece a causa de um trecho que já atualize corretamente esse campo.

## Correção e custo

`ModPlayer.AddDrawData(drawInfo, drawData)` escreve no próximo slot ativo e incrementa `DrawDataCacheCount` depois da escrita. Retorna `false` se faltar o cache ou o dado, o contador for inválido ou não houver espaço. Uma falha na escrita nativa não avança o contador.

O helper usa tempo e espaço adicional constantes, reaproveita o array nativo e não instala hooks. Os dados inseridos em `ModifyDrawInfo` precedem as camadas do jogador e passam pelas transformações e shaders nativos. O helper não altera a ordem dessas camadas nem chama `DrawData.Draw`.

## Validação em singleplayer

Execução Android em MuMu, em 6 de outubro de 2026, com Terraria móvel 301720. O [fixture](README.md) cria um marcador magenta com `MagicPixel` e observa o cache depois de `DrawPlayer_RenderAllLayers`. A [evidência resumida](native-results.log) registra os resultados.

| Inserção | Desenhos observados | Resultado |
|---|---:|---|
| Array sem incrementar o contador | 22 | Entrada sobrescrita pelas camadas nativas |
| Array e contador atualizados manualmente | 40 | Entrada preservada até o renderer |
| `ModPlayer.AddDrawData` | 54 | Entrada preservada até o renderer |
| Entradas perdidas nos casos válidos | 0 | Nenhuma |
| Resultados inesperados | 0 | Nenhum |

A captura de 1600 × 900 mostrou o marcador à direita do jogador, sem desenho direto pelo fixture. A conferência dos pixels encontrou 625 pixels magenta no retângulo de `(910, 403)` a `(934, 427)`. A captura completa está em `build/playerdrawcache/visual/native-screen.png`.

A primeira captura, com o marcador afastado do centro, não mostrou o desenho apesar da presença no cache. Depois de aproximar a posição do centro, a conferência visual passou. Essa tentativa não foi usada como evidência de renderização.

O processo foi encerrado, o fixture removido, suas preferências restauradas e a instância de teste desligada. O fixture não altera inventário ou dados do personagem. Esta validação foi em singleplayer; não é uma execução de multiplayer.

## Validação posterior em multiplayer

Na [rodada de 6 de outubro com dois processos](../mpmodplayer/RESULTADOS-DRAW-MODITEM.md), as 20 verificações iniciais e oito após reconexão passaram. A inserção manual e o helper chegaram ao renderer para o jogador local e remoto de ambos os processos, inclusive com reordenação de camadas. Os marcadores magenta e ciano ficaram visíveis nas duas telas, sem perdas nem mistura entre jogadores. As falhas de PvP encontradas nessa rodada não pertencem ao DrawDataCache.

## Regressões, build e documentação

| Suíte | Verificações aprovadas |
|---|---:|
| ModPlayer | 49 |
| ModItem | 138 |
| ModNPC | 46 |
| GlobalProjectile | 59 |
| Ciclo de vida de projéteis | 16 |
| Localização | 95 |
| Total | 403 |

Os cinco casos novos de ModPlayer cobrem inserção antes das camadas nativas, preservação dos slots existentes, reordenação de camadas, rejeição de cache cheio ou contador inválido e falha na escrita nativa. Os testes JS verificam composição e limites; a execução Android verifica o cache real e a renderização.

Build debug concluído, assinatura APK v2 verificada e artefato copiado para `out/bunny-loader.apk`. SHA-256: `F91E09059537E238C8A813A42E8F306987B0B1094C5BC50C94211C1618B07C10`.

A referência de ModPlayer e o catálogo foram atualizados. JSON, CSV e Excel conferidos integralmente: 64 classes, 1061 registros declarados e 10 não implementados. O catálogo conserva filtros e painéis fixos; a assinatura e o contrato do helper foram conferidos visualmente.
