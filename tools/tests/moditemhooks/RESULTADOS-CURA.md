# ModItem: resultado da etapa 3

Implementados GetHealLife, GetHealMana, ModifyPotionDelay e ApplyPotionDelay. A conferência incluiu as assinaturas do tModLoader, o patch de Player, o dump móvel, o disassembly de cura e seleção e a alternativa de QuickHeal/QuickMana do ExMod do TL Pro. O Bunny Loader mantém os helpers nativos de aplicação, seleção e consumo; os contratos e as diferenças estão em [Cura de ModItem](../../../docs/referencia/moditem-cura.md).

## Testes automatizados

| Suite | Verificações aprovadas |
| --- | --- |
| ModItem | 138 |
| ModPlayer | 44 |
| ModNPC | 46 |
| GlobalProjectile | 59 |
| Ciclo de morte de projéteis | 16 |
| Localização | 95 |
| Total | 398 |

A suite de ModItem confere 29 assinaturas e seus nomes de parâmetros contra refs/dump.cs. Os 40 novos casos verificam refs, valores aplicados, canais com base zero, normalização de números, limite contra overflow, campos temporários, contexto por jogador, seleção, vetos, erros de callback e do helper, filtros e ordens de registro.

O modelo do dispatcher agora diferencia métodos estáticos e de instância ao ler o argumento de um filtro. Um teste confirma que a seleção de cura lê o quarto argumento do método estático, sem aplicar o deslocamento de self de um método de instância. As duas ordens de registro de ModItem e ModPlayer produziram um único hook de cura e um único hook de atraso, com modificadores na mesma ordem.

## Execução no APK

MuMu Android arm64, uma instância, singleplayer. O fixture repetiu os 56 casos anteriores e acrescentou 30 verificações de cura: **86 verificações, zero falhas**. Log: [native-healing-results.log](native-healing-results.log).

- O helper normal aplicou vida e mana modificadas, respeitou os máximos e manteve a doença de mana nativa.
- Os campos de cura do item voltaram aos valores base após a aplicação.
- GetHealLife participou da classificação dos candidatos de QuickHeal.
- QuickHeal e QuickMana reais aplicaram as refs, forneceram quickHeal true e consumiram uma unidade pelo fluxo nativo.
- ModifyPotionDelay alterou o contador e o buff para a mesma duração; veto e duração zero conservaram o contador anterior.
- QuickMana continuou curando e consumindo quando ApplyPotionDelay foi vetado.
- ModItem compôs com ModPlayer uma vez; o veto de qualquer um prevaleceu, e ModPlayer recebeu a duração modificada mesmo após o veto do item.
- RestorationPotion, Eggnog e Mushroom mantiveram suas durações específicas. StrangeBrew conservou o sorteio nativo de atraso.
- Cada operação possui um único hook compartilhado entre ModItem e ModPlayer.

Antes de registrar o observador de cura de ModPlayer, foram realizadas 20 aplicações de cura e 20 de atraso em cada grupo: vanilla e mod sem sobrescritas. Os dois grupos produziram cura_js=0 e atraso_js=0. AddBuff chamado fora do contexto de poção também produziu zero entradas no novo hook. Observadores globais ampliam as marcas quando registrados; a medição não afirma zero entradas nesse caso e não mede FPS.

O fixture preservou o inventário, campos do jogador e buffs em finally. O processo foi encerrado após a coleta do log; as preferências próprias do teste foram restauradas e sua pasta removida pelo script que verifica o caminho. A instância de emulador iniciada para a sessão foi desligada.

## Revisão e adaptações

A aplicação normal usa ModItem, ModPlayer e o helper nativo nessa ordem. A seleção rápida usa o jogador que pediu a seleção, em vez de Main.myPlayer. O contexto de QuickHeal/QuickMana identifica o jogador e é restaurado em finally, inclusive após falhas. Canais com base zero não executam modificadores, incluindo ModPlayer, conforme a elegibilidade dos hooks do tModLoader.

Os planos de sobrescritas e os rótulos de erro são preparados no registro. Marcas por tipo evitam entrada no JavaScript para os itens que não participam da operação. Os hooks de buff e fome usam whileIn; chamadas de buff fora do método marcado não entram no dispatcher JS. O custo é O(1) por candidato, mantendo a varredura nativa de slots O(s). O código novo não acrescenta comentários inline.

ModifyPotionDelay modifica o atraso já calculado pelo jogo móvel. O dump não possui Player.PotionDelayModifier, usado pelo tModLoader depois desse callback; as reduções móveis não devem ser aplicadas novamente. O sorteio de vida de StrangeBrew também permanece nativo e pode usar o healLife modificado por ModPlayer como limite inferior. O veto de atraso não interrompe a cura nem o consumo. As diferenças e o comportamento de fome de Mushroom estão registrados na referência.

## Build e catálogo

Build Android arm64 debug aprovado em 23 s, com um worker e heap máximo de 1536 MiB. A assinatura APK v2 foi verificada. O APK instalado, testado e copiado para out/bunny-loader.apk tem SHA-256:

```text
0984A51F6D288DAA128A5BA21DAFA52FA007CE8CBBF8CF3AD4AB8267FFA70214
```

O catálogo contém 1.070 registros: 1.060 declarados e 10 indisponíveis, em 64 classes e 8 objetos/enums. JSON, CSV e Excel foram conferidos célula a célula, incluindo filtros e painéis fixos. A planilha mantém o formato existente e as assinaturas de cura foram renderizadas e revisadas.

Esta etapa foi validada em singleplayer. A validação entre dois processos permanece pendente. Estão concluídos 27 dos 53 métodos solicitados; desenho, coleta, pilhas, estado e reforja seguem no plano.
