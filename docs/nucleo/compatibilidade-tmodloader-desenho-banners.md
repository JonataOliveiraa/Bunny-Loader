# Desenho de itens, CloneDefaults e banners: contratos do tModLoader

Análise e implementação de 08/10/2026. Referência fixada: tModLoader,
branch `1.4.5`, commit `39e7995fa519a94468ebaf4699508d25903e143e`.
O binário mobile conferido é Terraria 1.4.5.8, versão Android `301720`, ARM64.
Compatibilidade de nomes não garante compatibilidade de comportamento:
é necessário conferir quem chama cada callback, os argumentos, a ordem,
os vetos, o estado das instâncias e o caminho dos pacotes.

## Análise: entradas, saídas e invariantes

| Fluxo | Entrada | Resultado exigido | Casos que precisam de atenção |
|---|---|---|---|
| Desenho no mundo | `WorldItem`, luz, alfa, rotação, escala e índice | Callbacks na posição correta da rotina; refs chegam ao desenho nativo | Vários globais, veto, falha do mod, desenho recursivo, item inativo |
| Desenho no inventário | `Item`, posição, quadro, cores, origem e escala calculados pelo jogo | Veto pula corpo vanilla e conserva `PostDraw` e a escala retornada | Quadros animados, cores recalculadas, filtros por entidade, recursão |
| Cópia de defaults | NPC de destino e tipo de origem | Copiar atributos clássicos, conservar identidade e aplicar dificuldade uma vez | Origem de outro mod, globais por entidade, reentrada, fonte circular, ausência de globais |
| Banner | NPC, representante, item e estilo de tile | Abates, resgate e bônus usam a mesma associação | NPCs compartilhando banner, mod ausente, ordem de IDs diferente, salvar versus enviar pela rede |

Os formatos de structs, as assinaturas e a localização dos pontos nativos
foram conferidos em `refs/dump.cs` e `refs/libil2cpp.so`. Antes de instalar
cada etapa de desenho, o código compara as instruções esperadas. O teste
em Node simula as chamadas nativas: os contratos de pilha/registradores
foram verificados separadamente no MuMu, dentro do jogo.

## Desenho: alternativas e escolha

| Abordagem | Tempo | Memória | Manutenção e comportamento |
|---|---|---|---|
| Reescrever toda a rotina em JS | O(efeitos + assinantes), atravessando o bridge em cada operação | Estado e objetos de todas as etapas | Duplicaria casos especiais do Terraria e mudaria junto com eles |
| Interceptar somente `SpriteBatch.Draw` | O(desenhos), inclusive chamadas sem interesse antes do filtro | Contexto para identificar qual desenho pertence ao item | Um veto ainda executaria partículas, glowmasks e notificações anteriores/posteriores |
| Inserir Pre/Post nas fronteiras da rotina nativa | O(assinantes) nos itens marcados; corpo vanilla preservado | Quadro ARM64 de 768 bytes por etapa e argumentos dos callbacks | Exige verificar instruções e posições de registradores em cada versão do jogo |

A implementação usa a terceira opção. O custo de manutenção por versão é
explícito, e o contrato de veto corresponde ao fluxo do tModLoader.
Os hooks são instalados apenas quando há sobrescrita; marcas por tipo
evitam entrar no JS para itens sem callbacks. Um GlobalItem amplia as
marcas, e `AppliesToEntity` seleciona os assinantes no registro global.

Fontes: [ItemLoader.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/ItemLoader.cs),
[Main.cs.patch](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/Main.cs.patch),
[ItemSlot.cs.patch](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/UI/ItemSlot.cs.patch).

| Callback | Ordem |
|---|---|
| `PreDrawInWorld` | ModItem, depois todos os GlobalItem aplicáveis |
| `PostDrawInWorld` | ModItem, depois todos os GlobalItem aplicáveis |
| `PreDrawInInventory` | Todos os GlobalItem aplicáveis, depois ModItem |
| `PostDrawInInventory` | ModItem, depois todos os GlobalItem aplicáveis |

Todos os Pre aplicáveis são chamados, mesmo após um veto. Um `false`
pula o corpo vanilla até a etapa Post. `PostDraw` continua rodando.
Os nomes oficiais são `PreDrawInWorld`, `PostDrawInWorld`,
`PreDrawInInventory` e `PostDrawInInventory`, sem `Item` no nome.

No mundo, Pre recebe `Ref` para rotação e escala; Post recebe os números.
No inventário, as cores dos callbacks vêm de `GetAlpha(itemLight)` e
`GetColor(environmentColor)`. A escala retornada por `DrawItemIcon`
continua sendo a escala calculada pelo jogo, inclusive com veto.
O Bunny mantém sua convenção de passar `item` explicitamente ao ModItem
no inventário; o tModLoader o disponibiliza por `this.Item`.

Implementação: `ItemDrawHooks.cpp`, `Arm64Frame.h` e `ItemDrawLoader.js`.
As etapas são `Main.DrawItem + 0x418/+0x20a8` e
`ItemSlot.DrawItemIcon + 0x380/+0x1198`. A trava do motor, o guard de
reentrada durante callbacks e `finally` protegem contexto e flags.
O uso de refs não aceita valores não finitos ao devolver floats ao nativo.

## CloneDefaults: identidade e dificuldade

Fonte: [NPC.TML.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/NPC.TML.cs).

O tModLoader salva `type`, `netID`, `ModNPC` e `_globals`, copia com
dificuldade clássica e escala 1 e restaura a identidade. O helper
`SetDefaultsKeepPlayerInteraction` também conserva a participação dos
jogadores nos abates. Não é apenas uma chamada a `SetDefaults`.

No Bunny, `ModNPC.CloneDefaults(type)` faz essa preservação. O bridge evita
reentrar no hook que já está executando; portanto, dentro do SetDefaults do
destino, a origem de mod e seus globais precisam ter os defaults chamados
explicitamente. A instância e os globais do destino são restaurados em
`finally`, inclusive quando a origem falha. Fontes circulares são recusadas.

A dificuldade usa `Terraria.DataStructures.GameDifficultyLevel.Classic`.
No binário conferido ela vale `1`; `0` não significa clássico. O teste
nativo detectou essa distinção ao comparar vida, dano, defesa e aiStyle
com um BlueSlime criado pelo jogo. A finalização externa do NPC de mod
aplica a dificuldade do mundo depois da cópia.

Sem GlobalNPC registrado, o campo auxiliar `__globalNPCs` ainda não existe
no bridge. Sua preservação é condicionada à criação do registro global.
Essa condição tem teste próprio e foi verificada no dispositivo.

## Banners: quatro relações, persistência e rede

Fontes: [BannerSystem.cs.patch](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/GameContent/BannerSystem.cs.patch),
[NPCLoader.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/NPCLoader.cs),
[ModBannerTile.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/ModBannerTile.cs),
[TileObjectData.TML.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ObjectData/TileObjectData.TML.cs).

1. O NPC aponta para o ID da bandeira em `Banner`. Em conteúdo de mod,
   esse ID é o tipo do NPC representante.
2. O representante aponta para `BannerItem`. O BannerSystem usa esse item
   tanto no resgate quanto para obter o limiar e a força do bônus.
3. O item aponta para o tipo e o estilo do tile que coloca.
4. `ModBannerTile.NearbyEffects` recupera estilo → item → banner e marca
   `SceneMetrics.NPCBannerBuff[banner]` quando a força está habilitada.

IDs de banner vanilla não são NPCIDs. O Bunny intercepta as conversões do
BannerSystem, aumenta seus três arrays e os de SceneMetrics, e mantém o
fluxo nativo de abates, notificações e resgate da 1.4.5. Novos itens recebem
`BannerStrength` habilitado e `KillsToBanner = 50` antes de SetStaticDefaults;
o mod pode alterar esses valores. NPCs da mesma família podem compartilhar
o representante e seu contador.

O helper `GetTileStyle` foi adicionado pelo tModLoader e não existe no
mobile. `TileLoader.StyleAt` usa os metadados nativos para calcular o
estilo, incluindo orientação, wrap, multiplicador e linhas reservadas
para alternates. O caminho anterior que chamava o método ausente foi
substituído pelo cálculo já utilizado no enquadramento de objetos.

Para persistência havia duas opções: gravar índices dinâmicos no arquivo
vanilla, ou guardar identidade estável no sidecar. Foi escolhida a segunda:
`BunnyLoader/Banners` é um ModSystem interno, que guarda contagem,
quantidade resgatável e notificação por UID do mod/nome da classe. Dados
de mods ausentes são retidos. O save vanilla recebe arrays com o tamanho
original, e os arrays vivos são restaurados em `finally`.

No pacote nativo `NetBannersModule.WriteFullState`, os arrays continuam
estendidos. O ModSystem também participa da sincronização de mundo.
O teste nativo lê o tamanho serializado no pacote; `NetPacket.Length`
isoladamente não prova o payload, pois inicialmente representa capacidade.
O teste entre dois processos confirmou o estado inicial, o 50º abate,
resgate pelo cliente, recusa de resgate duplicado e reconexão com 51 abates
e zero banners pendentes. Evidência: [PvP e banners em rede](../../tools/tests/pendingnetwork/README.md).

`ModBannerTile.PreDraw` enfileira somente a célula superior esquerda como
`MultiTileVine` e suprime o desenho normal. O mobile já possui
`DrawMultiTileVinesInWind`, mas a seleção vanilla de dimensões não conhece
tipos de mod. A passada do Bunny chama esse renderer com Width/Height do
TileObjectData. A sobrecarga nativa `GetTileDrawData(Tile)` recebe os dados
de desenho por um hook C++, sem tentar transportar seus 16 parâmetros pela
ponte JS. O desenho especial não reutiliza o cache de supressão do PreDraw.
O deslocamento do alternate é lido do tile colocado, e SetDrawPositions
compensa os dois pixels do renderer, como no tModLoader.

O teste no jogo ativou a opção de balanço e vento 0,8, observou 120 chamadas
do renderer e 360 sprites com dimensões positivas e rotação variável.
O ExampleMod inclui `ExampleEnemyBannerTile` e `ExampleSlimeBanner`,
vinculados ao `ExampleSlimeNPC`.

## Avaliação dos outros métodos solicitados

`GlobalNPC.SetupShop` já existe no Bunny como compatibilidade, junto de
`ModifyShop` e `ModifyActiveShop`. No tModLoader 1.4.5, a configuração
central é feita por `NPCShop`; transportar apenas o nome legado não
reproduz condições e modificações de itens ativos.

Os contratos modernos `HitModifiers/HitInfo` e `HurtModifiers/HurtInfo`
já estão presentes no CombatLoader, ModNPC, ModProjectile e classes
globais. O ponto de referência é
[CombinedHooks.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/CombinedHooks.cs):
`ModNPC/GlobalNPC.ModifyIncomingHit` configura o modificador primeiro;
depois modificam o atacante, o alvo e ModPlayer. No ModPlayer, o callback
geral precede o específico de item/projétil, tanto Modify quanto OnHit.
Essa ordem foi corrigida e tem teste de composição com operações que
produzem resultados diferentes se forem invertidas. OnHit usa o dano
retornado pela rotina nativa.

Em projéteis, o contrato oficial é `CanHitPvp` seguido de
`ModifyHitPlayer/OnHitPlayer`; não há uma família separada
`ModifyHitPvp/OnHitPvp` de ModProjectile. Em itens, essa família existe.

O PvP de item foi corrigido em uma etapa própria. O veto é executado no
`Player.CanHit(Entity)` dentro de `ItemCheck_MeleeHitPVP`, antes de aplicar
status e enviar dano, com o próprio `Main.player[i]` que o laço passa ao
`CanHit` como alvo: o `whoAmI` de um jogador ativado antes do `Update` dele
ainda é 0 e não serve para achá-lo. ModifyHitPvp/OnHitPvp executam no processo da vítima.
O PlayerDeathReason mobile fornece tipo e prefixo; um envelope no mesmo
fluxo TCP, anterior à mensagem 117, transporta a identidade estável e os
dados NetSend/NetReceive da arma. O servidor conserva esse envelope no
repasse. A vítima reconstrói o item mesmo se o atacante já trocou de arma.
Filas limitadas e contexto restaurado em finally evitam conservar origem
de outro golpe. A [fixture de rede](../../tools/tests/pendingnetwork/README.md)
confirmou veto, dano 7 e callbacks únicos nas duas direções, além dos
cenários de banner. As falhas antigas permanecem nos relatórios históricos.

## Validação reproduzível

```powershell
node tools/tests/apiexpansion/check.mjs
python tools/tests/apiexpansion/verify_native.py
node tools/tests/npcaitype/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/moditemdata/check.mjs
node tools/tests/moditemhooks/check.mjs
node tools/tests/modnpchooks/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/pendingnetwork/check.mjs
```

A fixture nativa fica em `tools/tests/apiexpansion/content/main.js`.
`device.py prepare/run/collect/restore` usa cópias de personagem e mundo,
desabilita os outros mods durante a execução e restaura settings, mods e
powers com comparação byte a byte. O resultado consolidado está no
[relatório dos testes](../../tools/tests/apiexpansion/README.md).

Não foi feita uma medição comparável de FPS no aparelho físico citado
pelo usuário. Esta análise não comprova que o problema do EternalLaser
foi resolvido nem que o custo seja igual ao do TL Pro.
