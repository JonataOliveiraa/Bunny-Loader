# Desempenho de ModPlayer, ModItem e desenho

O fixture mede o runtime QuickJS no APK real. Os [resultados](RESULTADOS.md) distinguem custo da ponte, consultas pela ponte e tempos de `DoUpdate`/`DoDraw`; nenhum desses números é apresentado como FPS.

As [otimizações e sua comparação entre APKs](RESULTADOS-OTIMIZACOES.md) acrescentam cópia por blocos, busca estável por loops diretos e leituras locais de cabelo, com regressões do cache no renderer.

## Cenários

Execute com 0, 1 e 8 **classes ModPlayer no mesmo jogador singleplayer**. `--classes` não representa jogadores conectados. Cada classe acrescenta 15 callbacks simples de atualização, `PreItemCheck` com veto e `CanStartExtraJump` neutro. As três classes ModItem do fixture estão presentes em todas as execuções para que seus filtros possam ser comparados.

Cada fase aquece por 120 updates e mede 300 updates e os desenhos ocorridos nesse intervalo. As consultas usam sete amostras após 100 chamadas de aquecimento. Dano usa 1.000 chamadas por amostra; frames 500; munição 250; atualização misc e ItemCheck 4.000. Os contadores de filtragem incluem o aquecimento, portanto os casos ativos têm 7.100 callbacks de dano e 3.600 de frame.

Com oito classes, o fixture registra mais um ModPlayer de desenho após a primeira fase e mede: ordem vazia, helper, ocultação de HeldItem, reordenação Skin após Head e ordem vazia novamente. A repetição revela variação do tempo de renderização. O marcador é criado uma vez e reutilizado; a medição do helper exclui a criação de textura, vetores e DrawData. Não há chamada direta a `DrawData.Draw`.

`draw_manual` e `draw_helper` escrevem no mesmo slot e restauram o contador em cada consulta. Ambas incluem essa restauração. O teste de munição no slot 53 mede essa posição da matriz nativa e não afirma ser o último candidato da ordem de busca nem uma reprodução de colocação pela UI.

## Execução

Use somente uma instância Android, o mesmo APK e o mesmo personagem/mundo de teste. A suíte não cria personagem ou mundo. Instale o APK completo e configure o início rápido para o mundo de teste, ou entre nele manualmente pelo launcher.

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture hookperf --run-id minha-rodada --isolate --classes 0
adb -s 127.0.0.1:16384 logcat -c
adb -s 127.0.0.1:16384 shell am start -n com.bunnyloader/dev.bunnyloader.LauncherActivity
```

Inicie o jogo e espere a linha `hookperf` com `kind=FIM`. Guarde o log do PID de `com.bunnyloader:game` antes de preparar a próxima execução. Repita com `--classes 1` e `--classes 8`, conservando o mesmo run-id durante essa sessão. Os arquivos com preferências originais ficam em `build/hookperf/<run-id>/` e não são sobrescritos nas tentativas.

```powershell
python tools/tests/hookperf/check.py log-0.txt log-1.txt log-8.txt
python tools/tests/moditemhooks/device.py cleanup --fixture hookperf --run-id minha-rodada
```

Para conferir uma rodada isolada com oito classes, use `python tools/tests/hookperf/check.py log-8.txt --classes 8`. Isso permite comparar dois APKs sem combinar registros de builds distintos.

`--isolate` desativa temporariamente os outros pacotes. A limpeza restaura somente as chaves alteradas, preserva as demais preferências atuais e remove o UID e a pasta temporária desta suíte após conferir os caminhos. Escolha outro run-id para uma nova sessão depois da limpeza. Use a limpeza também depois de uma execução interrompida.

## Limites da medição

- A comparação de 0, 1 e 8 classes inclui instalação de hooks, despacho e callbacks. O corpo nativo de ItemCheck executa com zero classes e é vetado nos outros casos; suas medianas não são uma comparação do mesmo corpo nativo.
- `bl.hookStats().jsMs` exclui `original()` e a espera pela trava anterior à entrada no JavaScript. Não mede o caminho nativo dos filtros rejeitados. Chamadas aninhadas explícitas podem afetar a soma de custos; os hooks individuais são apresentados separadamente.
- `DoUpdate` e `DoDraw` medem os corpos observados desses métodos, incluindo trabalho disparado por eles. Não abrangem toda a apresentação do quadro, Unity, escalonamento Android ou latência da GPU. O aquecimento e a carga do emulador podem alterar esses tempos.
- O perfil não inclui separadamente os quatro estágios internos de ModPlayer nem todas as opções da API. FrameEffects/armaduras, rede, múltiplos jogadores, sombras, muitos projéteis e mods com callbacks pesados precisam de medições próprias.
- O benchmark Node de [modplayerperf](../modplayerperf/README.md) mede V8 e continua sendo apenas uma comparação de despacho em isolamento.
