# ModPlayer, ModItem e DrawDataCache: multiplayer

Execução em 6 de outubro de 2026, com dois processos reais do Terraria móvel 301720 em MuMu. Host: jogador 0, `Main.netMode = 3`; cliente remoto: jogador 1, `Main.netMode = 1`. Os dois APKs instalados tiveram o mesmo SHA-256, conferido em cada dispositivo:

```text
F91E09059537E238C8A813A42E8F306987B0B1094C5BC50C94211C1618B07C10
```

O DrawDataCache passou no multiplayer. A rodada completa reprovou por falhas nos fluxos de `CanHitPvp`, `ModifyHitPvp` e `OnHitPvp` de ModItem. Esta execução adiciona testes e evidências; não corrige esses fluxos na implementação do loader.

## Resultado auditado

| Etapa | Verificações | Aprovadas | Falhas |
| --- | ---: | ---: | ---: |
| Sessão inicial do host | 130 | 125 | 5 |
| Sessão inicial do cliente | 132 | 129 | 3 |
| Desconexão, reconexão e nova sincronização no host | 4 | 4 | 0 |
| DrawDataCache após reconexão no host | 4 | 4 | 0 |
| DrawDataCache após reconexão no cliente | 4 | 4 | 0 |
| Total em jogo | 274 | 266 | 8 |

Sete falhas são verificações de PvP. A oitava é a confirmação do host de que o cliente terminou sem falhas, que também reprova quando o cliente relata suas três falhas. Os números foram conferidos contando as linhas de resultado, além dos totais declarados pelo fixture. A [evidência nativa](native-draw-moditem.log) contém apenas os registros da suíte e a auditoria, sem o restante do log Android.

As regressões automatizadas de ModPlayer aprovaram 49 verificações e conferiram 153 assinaturas nativas. ModItem aprovou 138 verificações e conferiu 29 assinaturas. Total de comportamento automatizado nesta rodada: 187 verificações aprovadas. Sintaxe JavaScript, Bash e Python e `git diff --check` conferidos. Não houve mudança no código de produção nem novo build nesta rodada.

## ModPlayer e ModItem fora do PvP

Os cenários existentes de ModPlayer passaram: isolamento entre jogadores, cópia de estado, sincronização inicial, dois deltas com confirmação de origem, ausência de envios extras, câmera e zoom locais, movimento, regeneração, salto extra, bônus de armadura, pesca, teleporte, mana, poções, munição, tiro, dano de item/projétil contra NPC e ausência de callbacks de ataque duplicados no host.

Foram observados 84 hooks distintos no jogador local do host e 85 no cliente. Essa observação não constitui cobertura funcional completa dos 133 métodos declarados de ModPlayer. Loja, enfermeira, persistência e os demais limites descritos no [roteiro](README.md) continuam precisando de cenários próprios.

Os 27 callbacks de ModItem implementados nas etapas 1 a 3 foram observados no atacante local de cada processo. Os cenários funcionais dos 24 métodos fora do PvP passaram nos dois processos:

| Grupo | Comportamento exercitado |
| --- | --- |
| Arma e colisão | Dano, crítico, knockback e escala; item conservado; veto de NPC; dano real 5; colisão `false`, `null` e `true`; hitbox e efeitos; estado independente por item. |
| Uso e frames | Animação antes do cálculo nativo, frame durante uso e frame ocioso. Trinta chamadas de `PlayerFrame` com item vanilla tiveram zero entradas no hook de frames de ModItem. |
| Munição | Seleção e veto da arma/munição; categoria alternativa; munição virtual; estatísticas por referência; `dontConsume`; vetos de ModItem e ModPlayer; última unidade e notificações únicas antes de `TurnToAir`. |
| Cura e poções | Vida e mana reais; uso normal, `QuickHeal` e `QuickMana`; restauração dos campos do item; ManaSickness; contador e buff de atraso; composição e veto de ModPlayer. |

Cura, munição e consultas são executadas no jogador local de cada processo. Esses resultados não verificam a replicação de cada recurso. O NPC é criado pelo host e replicado; a vida e defesa de teste também são preparadas no cliente porque `lifeMax` personalizado não é transmitido pela mensagem vanilla. Os métodos ainda não implementados de ModItem ficam fora desta rodada.

## Falhas de PvP reproduzidas

O fixture mantém ambos os jogadores em PvP, remove imunidade e defesa durante cada fase e dispara `ItemCheck_MeleeHitPVP` com o retângulo do alvo remoto. Mensagens do mod coordenam as fases e uma janela de 45 atualizações locais observa o `OnHurt` da vítima. Inventário, flags e cooldowns são restaurados após os testes.

| Direção e fase | Cópia do alvo no atacante | Envio nativo | Processo da vítima |
| --- | --- | --- | --- |
| Cliente → host, veto | `CanHitPvp = false` impede dano e callbacks posteriores | `SendPlayerHurt` ainda recebe dano 9 | Host recebe dano 9: veto falha entre processos |
| Cliente → host, permitido | Modificador e notificação executam uma vez; dano 3 | `SendPlayerHurt` recebe dano 11 | Host recebe 11; nenhum callback do item para o atacante remoto |
| Host → cliente, veto | Veto local funciona | `SendPlayerHurt` recebe dano 9 | Nenhum `OnHurt` observado na janela; não prova funcionamento do veto, pois o ataque permitido também não chegou |
| Host → cliente, permitido | Modificador e notificação executam uma vez; dano 3 | `SendPlayerHurt` recebe dano 11 | Nenhum `OnHurt` nem callback do item observado na janela |

No host, o dano recebido do cliente contém `PlayerDeathReason._sourcePlayerIndex = 1` e `_sourceItemType = 6198`. Portanto, a origem do item está presente nessa recepção, mas o loader não reconstrói o contexto do ataque a partir dela.

A revisão de `PlayerCombatHooks.InstallPvp` e `PlayerLoader.#HookHurt` confirma que os callbacks do item dependem de `PlayerCombatHooks.PvpAttack`, cujo tempo de vida é a chamada local de `ItemCheck_MeleeHitPVP`. Ao receber dano de rede fora dessa chamada, o contexto está vazio. O filtro `player.HurtActive` também precisa ser considerado em uma correção: com apenas callbacks PvP de ModItem, sua ativação atual é restrita à chamada de ataque local.

A leitura do método nativo `ItemCheck_MeleeHitPVP`, RVA `0x13DE4C8` no binário integrado, mostra uma chamada a `Hurt` seguida de `SendPlayerHurt` com o dano original. O veto aplicado somente dentro de `Hurt` não interrompe esse envio. O envio com dano bruto, por si só, não estabelece defeito em um contrato que aplica o modificador na vítima; a falha medida é a vítima receber 11 e não executar seu modificador para chegar a 3.

O [contrato oficial de ModItem](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs) coloca `CanHitPvp` no cliente atacante e `ModifyHitPvp`/`OnHitPvp` no cliente que recebe o dano. Observar esses callbacks apenas na cópia do alvo no atacante não aprova o comportamento multiplayer.

A ausência de dano no sentido host → cliente permanece sem causa isolada. O teste prova a chamada ao helper de envio e a ausência de `OnHurt` na janela observada; não prova a entrega do pacote ao cliente nem atribui essa ausência exclusivamente ao loader. Os três métodos PvP ficam reprovados para uso multiplayer nesta versão.

## DrawDataCache local e remoto

O fixture desenha com `MagicPixel` nativo em `ModifyDrawInfo`. Não chama `DrawData.Draw`. Compara escrita sem contador, inserção manual com contador e `ModPlayer.AddDrawData`, e observa o cache depois de `DrawPlayer_RenderAllLayers`.

| Processo / jogador desenhado | Sem contador: sobrescritas esperadas | Contador manual: preservados | Helper: preservados | Reordenação: preservados | Perdas válidas / mistura |
| --- | ---: | ---: | ---: | ---: | --- |
| Host / local 0 | 41 | 77 | 121 | 40 | 0 / 0 |
| Host / remoto 1 | 41 | 77 | 121 | 40 | 0 / 0 |
| Cliente / local 1 | 40 | 80 | 122 | 40 | 0 / 0 |
| Cliente / remoto 0 | 40 | 80 | 122 | 40 | 0 / 0 |

As 20 verificações iniciais de desenho passaram. Após reconectar, as oito verificações adicionais passaram nos dois processos. O host conservou o estado do jogador local, reiniciou o diagnóstico quando o objeto remoto mudou e encontrou 437 desenhos com helper para o novo remoto até a verificação. O cliente reiniciado encontrou 120 para cada jogador. Nenhuma perda ou mistura foi observada. Os contadores dependem da taxa de quadros e do tempo transcorrido.

As capturas após reconexão mostraram os dois marcadores: magenta para jogador 0 e ciano para jogador 1. A conferência dos pixels foi restrita à região central `(500, 250)` a `(1099, 449)` para excluir UI e texturas de mundo de outros mods desativados.

| Captura | Magenta: pixels / retângulo inclusivo | Ciano: pixels / retângulo inclusivo |
| --- | --- | --- |
| Host | 625 / `(707, 370)`–`(731, 394)` | 625 / `(612, 370)`–`(636, 394)` |
| Cliente | 400 / `(948, 387)`–`(967, 406)` | 400 / `(874, 387)`–`(893, 406)` |

Capturas completas locais: `build/mpmodplayer/20261006-draw-items/host-rejoin.png` e `cliente-rejoin.png`. A diferença de tamanho corresponde ao zoom de cada cliente. O desenho é calculado em cada processo para seu jogador local e para o remoto; os `DrawData` não são enviados por rede.

## Repetição e encerramento

Os logs brutos e backups da execução estão em `build/mpmodplayer/20261006-draw-items/`. A evidência inicial do host foi preservada a partir do log capturado na desconexão, porque a coleta posterior já não continha a sessão inicial no buffer do logcat. O roteiro agora guarda `host-first.log` e `cliente-first.log` antes de desconectar.

`verify` retornou 1, como exigido pelas falhas iniciais. `verify-lifecycle` retornou 0. A opção `BL_ALLOW_KNOWN_FAILURES=1` foi usada somente para permitir a desconexão e a reconexão depois das falhas; não transforma a rodada completa em aprovada.

Os processos foram encerrados, o fixture removido de ambos os dispositivos, as preferências alteradas restauradas e o encaminhamento de porta removido. As duas instâncias de teste foram desligadas. Hooks de diagnóstico e marcadores existem apenas no fixture; não foram acrescentados ao caminho de produção por frame.
