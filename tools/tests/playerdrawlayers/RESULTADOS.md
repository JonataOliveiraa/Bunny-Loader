# Camadas personalizadas de PlayerDrawLayer

Execução em 6 de outubro de 2026. A implementação foi conferida contra a
cópia local de `exmod_tmod/tModLoader`, principalmente `PlayerDrawLayer.cs`,
`PlayerDrawLayer.Positions.cs`, `PlayerDrawLayerLoader.cs` e
`ExamplePlayerDrawLayer.cs`. As assinaturas móveis continuam verificadas
contra `refs/dump.cs`.

## Análise e escolha

Entradas: classes de camada, posições relativas, visibilidade por jogador
e `PlayerDrawSet` nativo. Saída: `DrawData` acrescentado ao cache ativo no
ponto do pai. Restrições: preservar shaders, transformações, ordem nativa,
dados de outros mods e desenhos aninhados. Casos de limite: pai ausente,
ciclo, duplicata, exceção, cache cheio, pós-imagem e registro durante o jogo.

Foram avaliados três caminhos:

| Caminho | Custo e manutenção | Consequência |
|---|---|---|
| Desenho direto em SpriteBatch | Poucas chamadas, mas exige aplicar efeitos e transformações manualmente. | Não integra a camada ao cache e à ordem do renderer. |
| Reconstruir o desenho inteiro em JavaScript | Percorre o desenho completo e replica decisões do jogo. | Manutenção depende de acompanhar todas as regras nativas. |
| Executar filhos junto dos pontos nativos | Plano de relações em cache, visibilidade por desenho e hooks ativos somente nos pontos necessários. | Preserva o renderer e o comportamento de pais e filhos. Caminho adotado. |

O plano mantém relações e listas de filhos. Referências por classe,
instância e nome completo evitam procurar nomes simples entre mods.
Visibilidade e mudanças de posição continuam sendo avaliadas por desenho.
Reordenação nativa mantém a ordenação estável existente e copia os blocos
ativos somente quando há movimento efetivo.

## Verificações automatizadas

| Suíte | Verificações aprovadas |
|---|---:|
| ModPlayer existente | 55 |
| Camadas personalizadas e exemplo de glowmask | 28 |
| ModItem existente | 138 |
| ArmorIDs/HairID | 11 |
| Total | 232 |

As 153 assinaturas nativas de ModPlayer e 29 de ModItem foram conferidas.
As quatro etapas internas registradas pelo harness de ModPlayer continuam
presentes. Os cenários de camadas incluem apenas um anchor entrando no
JavaScript, ausência de despacho fora do renderer, cache dos filtros,
registro sem ModPlayer e isolamento de dois jogadores com desenhos
aninhados e shaders diferentes.

## Renderer Android

Singleplayer em Terraria móvel 301720, com um processo real no MuMu. O
fixture usou registro automático para quatro camadas, registro tardio para
uma quinta e `DrawPlayer_RenderAllLayers` para conferir o cache efetivo.

| Etapa | Desenhos conferidos |
|---|---:|
| Visível | 48 |
| Pai nativo oculto | 47 |
| Pai personalizado invisível | 50 |
| Mudança de pai | 50 |
| Reordenação nativa | 50 |
| Ciclo personalizado | 50 |
| Registro tardio | 50 |
| Sem alterações | 50 |
| Total | 395 |

Resultado: zero falhas, `passed: true`, cinco inicializações e nove
consultas de posição. Os quatro marcadores finais apareceram na tela,
desenhados pelo renderer normal. O shader no cache foi preservado. Neste
personagem, `cWings` era zero; o shader não zero foi verificado no harness.

A [evidência do fixture](native-results.log) contém somente seus resultados.
Uma execução preliminar também passou com 344 desenhos conferidos. O teste
final acima usa o mesmo APK entregue em `out/`.

## Custo dos hooks

Comparação com oito classes de ModPlayer no mesmo dispositivo e fixture.
Cada versão completou seis fases, com 1.800 atualizações e 216.000 callbacks
de tick. Foram observados 1.748 desenhos antes e 1.795 depois. As execuções
auditadas ocorreram após o build, sem compilação concorrente. Uma medição
intermediária com atualização de filtros a cada desenho identificou o
custo que motivou manter os filtros individuais em cache.

| Cenário | APK anterior | APK final |
|---|---:|---:|
| Desenho sem alterações: despachos JS de camadas | 0 | 0 |
| Inserção com AddDrawData: despachos JS de camadas | 0 | 0 |
| Ocultar HeldItem: métodos de camada entrando no JS | 47 | 1 |
| Ocultar HeldItem: despachos JS durante a fase | 14.147 em 301 desenhos | 300 em 300 desenhos |
| Ocultar HeldItem: tempo JS somado das camadas por desenho | 78,945 µs | 5,402 µs |
| Reordenação: tempo próprio do hook principal por desenho | 283,058 µs | 300,137 µs |
| Reordenação: tempo JS somado das camadas por desenho | 143,026 µs | 149,613 µs |

Ocultar uma única camada reduziu o número de métodos que entram no JS de
47 para 1. Na reordenação, o hook principal teve acréscimo observado de
17,079 µs, aproximadamente 6%, junto do suporte novo. Essa medição não
estabelece ganho de FPS nem uma garantia de tempo para outros dispositivos.
As médias de DoDraw variaram entre fases e processos, inclusive nas etapas
sem mudanças de camadas, e não foram usadas para atribuir ganho global.

Os filtros individuais só são alterados quando muda o conjunto de camadas
ativas. Entre desenhos, seus bits permanecem em cache. O filtro nativo
`whileIn` impede despacho fora de `DrawPlayer_UseNormalLayers`. Desenhos
aninhados restauram o conjunto do desenho anterior, e o estado de
visibilidade é restaurado mesmo se o renderer lançar uma exceção.

Evidências: [antes](native-perf-before.log), [depois](native-perf-after.log).
Ambos passaram em `tools/tests/hookperf/check.py --classes 8`.

## Artefato e limites

APK completo ARM64, com sete bibliotecas nativas e 7.685 entradas de assets.
Build com `bl.nativeBuild=true` e `bl.uiOnly=false`. Assinatura APK v2 e
alinhamento de quatro bytes conferidos. Tamanho: 204.436.640 bytes.

```text
Anterior: F9ED0E95B3A70157DFB848A481FA06015BBDCEC09EAF0F7A31DD90472CA172A1
Final:    8175DBA4EA352B07CB0FE4903525358A756859063511AD329D825FC96B985C07
```

`BeforeParent`, `AfterParent`, registro, desenho, visibilidade e composição
com ModPlayer estão implementados. `Between`, `Multiple`, `Transformation`,
`IsHeadLayer` e minimapa não fazem parte desta implementação. O teste de
isolamento entre dois jogadores foi feito no harness; esta rodada não
executou uma nova sessão de multiplayer entre dois processos reais.

Markdown, CSV, JSON e Excel foram atualizados. O Excel foi conferido contra
todos os 1.078 registros do catálogo, com filtros, painéis fixos, ausência
de fórmulas e previews das linhas e contratos alterados. O renderizador
salvou os previews e encerrou com código 1, comportamento já registrado
para este runtime Windows; a geração do Excel sem render terminou com
código 0 e a verificação independente passou.

As preferências e os mods temporários dos testes foram restaurados.
