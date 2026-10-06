# ModItem: plano de implementação

O pedido será entregue em etapas completas, com contratos documentados, testes automatizados, revisão dos pontos nativos e commits separados. Um método só entra na API quando possui uma implementação funcional.

## Análise

- O Bunny Loader usa o Terraria móvel 1.4.5; as assinaturas do tModLoader precisam ser conferidas contra `refs/dump.cs` e o código nativo.
- A API existente recebe `item` explicitamente como primeiro argumento. Esse padrão será preservado.
- ModPlayer, ModNPC e GlobalItem já interceptam alguns dos mesmos fluxos. ModItem precisa compartilhar esses pontos, respeitando vetos e modificadores existentes.
- Os filtros `marks`, `flag` e `whileIn` são avaliados pelo dispatcher nativo antes de entrar no JavaScript.
- Hipótese a validar: os métodos de desenho e persistência podem exigir contratos próprios do jogo móvel. Eles permanecem em investigação até a verificação.
- Casos relevantes: retornos nulos, veto entre classes, itens sem sobrescritas, herança, registro tardio, exceções, ataques aninhados, cópia de estado por item, consumo da última unidade e sincronização entre processos.

## Projeto

| Abordagem | Custo por chamada | Memória | Manutenção |
| --- | --- | --- | --- |
| Hook separado por classe e callback | Proporcional ao número de interceptadores | Interceptadores adicionais | Ordem depende do registro; duplica contextos |
| Dispatcher JavaScript sem filtro | Busca constante por tipo, mas entra em JS para todos os itens | Plano por classe | Centraliza contratos; aumenta custo dos caminhos frequentes |
| Pontos compartilhados com marcas e contextos nativos | Consulta nativa constante; callbacks apenas quando necessários | Plano por classe e marcas por tipo | Mantém um fluxo por operação; exige testes da composição |

A terceira abordagem será usada. As listas de sobrescritas e os rótulos de erro serão preparados no registro. O caminho frequente não fará reflexão sobre protótipos. Observadores globais ampliarão as marcas uma vez no registro, incluindo tipos adicionados posteriormente.

## Etapas

| Etapa | Métodos | Validação |
| --- | --- | --- |
| 1. Combate | ModifyWeaponDamage, ModifyWeaponCrit, ModifyWeaponKnockback, ModifyItemScale, CanHitNPC, ModifyHitNPC, CanHitPvp, ModifyHitPvp, OnHitPvp, CanMeleeAttackCollideWithNPC, MeleeEffects, UseItemHitbox | Assinaturas, ordem e veto entre classes, modificadores, hitbox, PvP, exceções, reentrada e filtragem; build e execução nativa |
| 2. Uso e munição | UseAnimation, UseItemFrame, HoldItemFrame, NeedsAmmo, CanChooseAmmo, CanBeChosenAsAmmo, CanConsumeAmmo, CanBeConsumedAsAmmo, OnConsumeAmmo, OnConsumedAsAmmo, PickAmmo | Seleção, munição ausente, veto, última unidade, não consumo e animação |
| 3. Cura | GetHealLife, GetHealMana, ApplyPotionDelay, ModifyPotionDelay | Uso normal e rápido, valor aplicado, atraso e restauração do item |
| 4. Desenho | PreDrawInWorld, PostDrawInWorld, PreDrawInInventory, PostDrawInInventory, ModifyItemDraw, DrawArmorColor, ArmorArmGlowMask, ModifyEquipTextureDraw | Conferência do contrato móvel, veto, cores, ordem das camadas, refs e teste visual |
| 5. Coleta e pilhas | CanPickup, OnPickup, ItemSpace, GrabRange, CanStack, OnStack, SplitStack, OnCreated, CanEquipAccessory, CanConsumeBait | Inventário cheio, troca de slots, pilhas com estado distinto, divisão, criação e consumo |
| 6. Estado e reforja | SaveData, LoadData, NetSend, NetReceive, CanReforge, PreReforge, PostReforge, ReforgePrice | Ida e volta do estado, dados ausentes/inválidos, clone, sincronização em dois processos, preço e veto |

Cada etapa atualizará a referência da API. Diferenças de contrato e métodos indisponíveis serão registrados com a evidência correspondente. A presença de um nome na tabela não indica que ele já foi implementado.

## Estado em 6 de outubro de 2026

Os 12 métodos da etapa 1, os 11 da etapa 2 e os 4 da etapa 3 estão implementados: 27 dos 53 métodos solicitados. A suite de ModItem possui 138 verificações automatizadas; o fixture nativo em singleplayer aprovou 86 verificações, com zero falhas. Evidências da etapa 1: [RESULTADOS.md](RESULTADOS.md); da etapa 2: [RESULTADOS-USO.md](RESULTADOS-USO.md); da etapa 3: [RESULTADOS-CURA.md](RESULTADOS-CURA.md). As etapas 4 a 6 ainda não foram implementadas.

A [validação multiplayer](../mpmodplayer/RESULTADOS-DRAW-MODITEM.md) observou os 27 callbacks em ambos os atacantes locais e aprovou os cenários dos 24 métodos fora do PvP. `CanHitPvp`, `ModifyHitPvp` e `OnHitPvp` reprovaram entre processos: o veto ainda permite envio de dano, e o contexto do item recebido por rede não é reconstruído na vítima. A rodada registra também ausência de dano no sentido host → cliente, com causa ainda não isolada. Os três métodos não estão aprovados para multiplayer; sua correção continua pendente.

Referência de contratos: [ModItem do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs). Alternativas móveis: `C:/Scripts/Terraria/curso/ExMod_v1.5.0/Modified/1.mod/TL`.
