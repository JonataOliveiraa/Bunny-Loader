# PlayerDrawLayer

Suíte para camadas JS personalizadas no renderer móvel 301720.

## Automação

```powershell
node tools/tests/playerdrawlayers/check.mjs
node tools/tests/moditemhooks/check.mjs
node tools/tests/hairsettings/check.mjs
```

`check.mjs` reutiliza o harness de ModPlayer, executando suas 55 verificações
antes das 28 verificações novas. As 153 assinaturas de hooks e quatro etapas
internas continuam conferidas contra `refs/dump.cs`.

Os cenários novos verificam registro manual e automático, conteúdo pronto,
registro tardio, cache de posições, filhos anteriores/posteriores, nomes
entre mods, ciclos, exceções, shaders, preservação do cache, mudança de pai,
movimento do grupo nativo e restauração de filtros. Um cenário inicia o
carregador somente com uma camada personalizada, sem nenhum ModPlayer.
Outro envolve desenhos aninhados de dois jogadores com shaders diferentes.

O exemplo `WingsGlowmask` também é executado: configuração por slot,
validação de frames e offsets, quadro de textura, direção, transparência,
shader e exclusão de jogadores mortos, invisíveis ou sem asas configuradas.
O teste confirma que o cache recebe o `DrawData` construído.

Uma camada somente nas asas precisa acionar exatamente um hook nativo de
camada no JavaScript. Camadas invisíveis não acionam esses hooks. Reordenar
um pai nativo preserva seu grupo e os dados antes/depois do grupo.

## Teste Android

O fixture usa autoload para quatro camadas exportadas e registra uma quinta
durante a execução. O renderer normal desenha marcadores com `DrawData`,
sem chamada direta a `SpriteBatch`. O observador examina as entradas ativas
depois de `DrawPlayer_RenderAllLayers`.

Etapas: visível, pai nativo oculto, pai personalizado invisível, mudança de
pai, reordenação nativa, ciclo, registro tardio e desenho sem alterações.
Também confere que os filtros de Wings/Head estão ativos no desenho visível
e que Skin permanece fora do JavaScript. O estado final exige contagens
positivas nas oito etapas, nenhuma falha, cinco inicializações e nove
consultas de posição.

Prepare somente em um dispositivo de teste. O helper salva os valores
originais e restaura as preferências alteradas no cleanup:

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture playerdrawlayers --run-id layers --isolate
adb -s 127.0.0.1:16384 shell am start -n com.bunnyloader/dev.bunnyloader.LauncherActivity
```

Inicie o jogo pelo launcher. Após 470 atualizações do jogador local,
procure `playerdrawlayers FIM` no log do processo `com.bunnyloader:game`.
Confirme `passed: true` e os marcadores na tela. Ao terminar:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture playerdrawlayers --run-id layers
```

Resultados auditados e limites: [RESULTADOS.md](RESULTADOS.md).
Contrato público: [PlayerDrawLayer](../../../docs/referencia/playerdrawlayers.md).
