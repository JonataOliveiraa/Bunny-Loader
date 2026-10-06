# Revisão de custo de ModPlayer

A [medição de 6 de outubro no APK atual](../hookperf/RESULTADOS.md) acrescenta comparação de 0, 1 e 8 classes, filtros de ModItem, AddDrawData e custo de ocultação/reordenação de camadas. O relatório abaixo descreve a revisão anterior.

## Análise

A API registra hooks sob demanda: uma classe que não sobrescreve um método não instala seu grupo de hooks. O custo depende dos métodos realmente sobrescritos, dos jogadores atualizados e das chamadas nativas observadas. A lista de classes interessadas em cada método já era armazenada em cache.

A revisão encontrou trabalho evitável no despacho por callback, nos estados de saltos, no desenho e em hooks globais que observavam chamadas fora de seu contexto. Também encontrou uma falha na ponte: `whileIn` consultava apenas o primeiro hook instalado para um método. Quando esse hook tinha um filtro que rejeitava a chamada, outro hook ativo do mesmo método não abria o contexto esperado.

As restrições são preservar a ordem e a frequência dos callbacks, o isolamento dos jogadores, os vetos e as decisões anuláveis, a recuperação após exceções e o registro posterior de classes. Os casos exercitados incluem ausência de interessados, chamadas fora do contexto, falhas nativas, substituição de slots e reconexão.

## Projeto

| Abordagem | Tempo | Espaço | Manutenção |
| --- | --- | --- | --- |
| Reduzir alocações e filtrar chamadas antes do JavaScript | Despacho continua O(M); filtros O(1); acompanhamento de rede O(C) por tick local. | Menos objetos temporários; cache e estados limitados aos participantes. | Mantém os pontos existentes do jogo e a frequência dos hooks. |
| Executar callbacks com menor frequência | Reduz chamadas conforme o intervalo escolhido. | Exige estado de agendamento. | Muda a semântica de métodos de atualização e efeitos. |
| Reunir os pontos em novos hooks nativos | Pode reduzir travessias da ponte. | Exige novas estruturas e código nativo. | Exige localizar pontos equivalentes e preservar sua ordem em cada versão do jogo. |

Foi aplicada a primeira abordagem. M é o número de classes interessadas em um callback; C é o número de jogadores acompanhados como conectados. Nenhum callback foi limitado a executar a cada vários ticks.

## Implementação

- `PlayerLoader`: despacho direto, sem closure e rótulo por instância. As exceções continuam isoladas e reportadas por `Safe.Report`. Vetos continuam consultando todas as classes; `First` mantém sua interrupção no primeiro `true`. Chamadas sem interessados não criam instâncias. O registro de uma classe invalida o cache.
- `PlayerJumpHooks`: nomes de campos preparados uma vez e estados em máscaras de bits para os nove saltos existentes. Um veto sem entrada de salto evita ler todos os estados. `OnExtraJumpCleared` sozinho não instala `JumpMovement` nem hooks de som. Sons só entram no JavaScript dentro do movimento de salto.
- `PlayerDrawHooks`: cinco referências de `DrawEffects` só são criadas se houver interessado. Dados de segmentos só são lidos quando há reordenação. As chamadas das camadas são filtradas nativamente quando nenhuma camada precisa de alteração. Os estados e filtros são restaurados ao sair, inclusive após exceções.
- `PlayerCombatHooks` e `PlayerItemHooks`: colisões, dano contextual, criação de projéteis e limpeza de munição usam filtros nativos. A colisão global não é instalada para uma classe que só modifica dano de item.
- `PlayerNetworkHooks`: guarda o endereço na conexão, consulta o array de jogadores uma vez por varredura e remove jogadores desconectados da coleção percorrida a cada tick. A deduplicação fica em uma coleção separada, sem manter referências ao jogador antigo após notificá-lo.
- Ponte nativa: `whileIn` usa um contador compartilhado pelos hooks do mesmo método, por thread, em O(1). O controle de reentrância continua separado por hook. A tabela adicional usa 5.888 bytes por thread nesta configuração de slots.

As referências entregues ao mod não são reutilizadas entre callbacks: o mod pode guardar esses objetos. A cópia do estado do cliente e a detecção de desconexão continuam na frequência original. Grupos que observam pontos diferentes da atualização ainda podem instalar vários hooks em `Player.Update`; esta revisão não altera a ordem dessa cadeia.

## Testes e medição

```bash
node tools/tests/modplayerhooks/check.mjs
node tools/tests/modplayerperf/bench.mjs
node tools/tests/modplayerperf/bench.mjs 44bd638
```

O benchmark Node serve para comparar o despacho em isolamento. Seu runtime é V8; seus tempos não representam o QuickJS do Android.

Para a medição nativa, empacote `manifest.json` e `content/`, instale este pacote em um aparelho com o APK desejado e entre em um mundo de teste. O pacote registra oito ModPlayers com callbacks simples, aquece por 120 ticks, mede sete amostras de 20.000 chamadas e coleta estatísticas de 300 ticks. A saída começa com `modplayerperf`.

`CapAttackSpeeds_8` inclui a chamada nativa, a travessia da ponte e o callback `PostUpdateMiscEffects` das oito instâncias. `ItemCheck_veto_8` inclui o veto pelas oito instâncias e `PostItemCheck`; o corpo nativo é vetado. Esses números não são o tempo total de um quadro.

`whileIn_multiplos_hooks=ok` exige que um segundo hook abra o contexto quando o primeiro foi rejeitado por um filtro. Também verifica que a chamada fora desse contexto não passa pelo JavaScript.

Compare APKs no mesmo aparelho, personagem, mundo e conjunto de mods. O número de quadros desenhados pode variar durante os 300 ticks; normalize estatísticas de desenho por suas próprias chamadas. Os tempos de `bl.hookStats` excluem `original()` e não medem o custo do caminho nativo que rejeita um callback. Variação de carga do emulador impede interpretar uma única comparação como ganho garantido de FPS.

A suíte [multiplayer](../mpmodplayer/README.md) verifica comportamento em host e cliente reais após as otimizações. As regressões de lógica incluem filtragem, ausência de interessados, registro posterior, exceções e restauração de estado.
