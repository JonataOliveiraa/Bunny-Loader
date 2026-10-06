# ModNPC: contratos e validação

## Análise

Entrada: NPC nativo ligado a uma instância de ModNPC, fontes de nascimento,
atacantes, referências mutáveis e chamadas de desenho ou loja. Saída:
eventos por entidade, decisões de morte e dano e parâmetros usados pelo jogo.

Fatos conferidos: assinaturas e nomes de parâmetros em `refs/dump.cs`;
desmontagem dos métodos da versão móvel 301720; contratos de ModNPC e
ordenação de NPCLoader no ramo stable do tModLoader. O mobile não contém
as estruturas NPC.HitModifiers, NPC.HitInfo e Player.HurtModifiers do
tModLoader. Os callbacks de combate usam os objetos JavaScript já adotados
no fluxo de ModPlayer do Bunny Loader, com os campos descritos na referência.

Premissa: os mods respeitam esses campos e os tipos de retorno. Nenhum
parâmetro ausente é preenchido com um valor estimado. Os métodos abaixo
foram descartados conforme autorizado na solicitação.

Casos de borda: null e undefined, veto de ModPlayer após permissão de ModNPC,
alfa zero, duas entidades do mesmo tipo, golpes aninhados, projétil com dono
255, dano zero, golpe recebido da rede, NPC inativo, tipo sem sobrescritas,
registro posterior, exceções e restauração de contexto de desenho e combate.

## Projeto

| Abordagem | Tempo por chamada | Espaço | Manutenção |
|---|---|---|---|
| Hooks de combate separados do ModPlayer | Duas entradas JS nos métodos já compartilhados, buscas duplicadas | Dois contextos de atacante | Ordem e notificações dependem da instalação |
| Despacho compartilhado e filtros nativos para eventos e desenho | O(1) para encontrar a instância e o plano por classe; somente callbacks escritos | Plano por classe, contexto por golpe; mapa de NPCs com DrawBehind | Uma sequência de dano e restauração por fluxo |

A segunda abordagem preserva um hook por ponto de combate. Os filtros de
eventos, getters e desenho rejeitam tipos sem sobrescrita antes de entrar
no JavaScript. Os rótulos de erro são montados uma vez por classe. ResetEffects
não cria closures por tick. DrawBehind percorre somente as entidades
rastreadas, removendo inativas e tipos que deixaram de implementar o método.
TownNPCProfile registra o perfil uma vez após o conteúdo, sem hook por frame.

O teste de mudança de cor consulta PackedValue, evitando ler quatro canais
separadamente pela ponte em cada comparação. O dano de Hurt é normalizado
uma vez e o mesmo valor é usado na checagem de esquiva e na chamada nativa.

ResetEffects usa a entrada de UpdateNPC_BuffSetFlags: a desmontagem confirma
que UpdateNPC zera os flags inline imediatamente antes dessa chamada. Hookar
UpdateNPC_BuffFlagsReset não intercepta o tick real, pois o compilador
embutiu seu corpo no chamador. OnSpawn usa a saída de NewNPC e a fonte
preservada no argumento da criação; o corpo nativo de NPC.OnSpawn não usa
source, e seu chamador não conserva esse argumento para um hook externo.

Os pontos de combate sem NPC como argumento, especialmente Damage_PVE,
precisam de um escopo global para identificar o projétil. Eles são instalados
somente se há um callback que depende desse fluxo; são compartilhados com
ModPlayer quando ambos estão presentes. Eles têm custo de entrada em JS nos
ataques, mesmo quando o alvo não é um NPC de mod.

## Implementação

Foram adicionados 23 métodos:

- OnSpawn, ResetEffects, OnHitByItem, OnHitByProjectile.
- CanHitPlayer, ModifyHitPlayer, OnHitPlayer, CanBeHitByItem,
  ModifyIncomingHit, CheckDead e ApplyDifficultyAndPlayerScaling.
- TownNPCProfile, CanChat, ModifyActiveShop e ModifyNPCHappiness.
- PreDraw, PostDraw, DrawEffects, DrawBehind, GetAlpha,
  BossHeadSlot, BossHeadRotation e BossHeadSpriteEffects.

Os parâmetros completos estão em
[classes.md](../../../docs/referencia/classes.md#modnpc).
DrawEffects usa a iluminação do centro como base, como a API de desenho
do Bunny Loader; a cor substituída chega ao tint nativo dos buffs. Se a cor
não mudar, a iluminação específica calculada pelo renderer mobile é
preservada. PreDraw false pula o corpo do desenho nativo e mantém PostDraw.
ModifyActiveShop atende lojas registradas por NPCShop e passa o nome local;
o array mobile possui itens vazios em lugar de entradas null.
TownNPCProfile exige um perfil nativo, pois a ponte não implementa interfaces
C# a partir de objetos JavaScript.

Após a indicação do usuário, foram conferidos ModNPC.js, Hooks/NPC.js,
Hooks/GameContent.js e Loaders/NPCLoader.js do ExMod v1.5.0 local. Duas
alternativas puderam ser usadas com dados reais:

- ApplyDifficultyAndPlayerScaling roda após ScaleStats_ByPlayerCount.
  A contagem é a usada pelo NPC, incluindo overrides; balance vem de
  GetStatScalingFactors. O ajuste segue a convenção tModLoader/ExMod:
  0,85 quando a dificuldade do próprio NPC é Master ou superior, 1 abaixo.
- ModifyNPCHappiness roda depois de AllPersonalitiesModifier.ModifyShopPrice,
  com jogador, NPC, helper e array de vizinhos do HelperInfo nativo. O bioma
  primário usa os IDs e a prioridade do ExMod. Não é uma API ativa do
  tModLoader stable; é a alternativa móvel solicitada, descrita na referência.

A alternativa de UpdateLifeRegen no ExMod chama o callback depois de
UpdateNPC_BuffApplyDOTs e passa um número, não uma referência mutável.
Ela não permite controlar o dano que já foi aplicado nessa etapa;
por isso não foi adotada como equivalente ao contrato pedido.

## Métodos descartados

| Método | Razão verificada |
|---|---|
| CanBeHitByProjectile | O ponto de elegibilidade acessível é Player.CanNPCBeHitByPlayerOrPlayerProjectile. Damage_PVE pula esse ponto em alguns fluxos, incluindo projéteis sem dono jogador. Um veto em StrikeNPC seria tardio, após efeitos de contato; não teria o contrato completo. |
| UpdateLifeRegen | UpdateNPC_BuffApplyDOTs acumula perdas em uma estrutura DOTTally local e aplica dano e efeitos dentro do próprio método. Não expõe a referência de dano do contrato antes da aplicação. |
| ModifyDeathMessage | A mensagem e a cor são variáveis internas de checkDead. DropTombstoneTownNPC expõe apenas o texto da lápide, sem controlar todos os anúncios nem sua cor. |
| TownNPCAttackCooldown, TownNPCAttackShoot, TownNPCAttackSwing, TownNPCAttackMagic | Os parâmetros são locais na AI dos moradores. Não há helpers interceptáveis com as referências completas de cooldown, tiro, hitbox ou aura. Os hooks anteriores de projétil continuam sendo um fluxo próprio documentado. |
| DrawTownAttackGun, DrawTownAttackSwing | Textura, frame, escala e offsets do ataque ficam dentro do renderer. Não há um método equivalente com todos os argumentos mutáveis. |
| DrawHealthBar | Main.DrawHealthBar recebe coordenadas, vida e escala, sem NPC. O loop de barras atende também jogadores e partes de vermes; inferir o NPC por coordenadas ou vida poderia modificar a entidade errada. |

## Execução

```powershell
node tools/tests/modnpchooks/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
```

O primeiro script carrega o código de produção e valida todas as assinaturas
interceptadas, inclusive os nomes de parâmetros, contra o dump móvel.

```powershell
$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
$env:CMAKE_BUILD_PARALLEL_LEVEL = '1'
gradle -I tools/tests/modnpchooks/isolated.gradle :app:assembleDebug --no-daemon --max-workers=1 '-Dorg.gradle.jvmargs=-Xmx1536m' '-Pkotlin.compiler.execution.strategy=in-process'
```

O APK fica em `build/modnpchooks/builds/app/outputs/apk/debug/`. O fixture usa
o UID b19fb975-3058-47d5-8e59-fb737ca9e3e2. Copie somente manifest.json e
content para a pasta desse UID em bunny_packs e entre num mundo singleplayer.
O log termina com `modnpchooks FIM`. Depois remova somente esse pacote.

O fixture cria NPCs e projéteis temporários e os desativa ao terminar; não
coloca blocos nem concede itens. A prova de Hurt usa dano baixo e restaura
vida e imunidade do jogador em finally. Os casos de loja usam um storage
temporário e restauram o alvo da conversa. O benchmark mede chamadas diretas
ao reset nativo, incluindo o despacho e o rastreamento de DrawBehind; não
mede FPS nem custo exclusivo do callback JavaScript.

## Fontes

- [ModNPC.cs](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModNPC.cs).
- [NPCLoader.cs](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/NPCLoader.cs).
- [NPC.cs.patch](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/NPC.cs.patch).
- [Main.cs.patch](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Main.cs.patch).
- `refs/dump.cs` e desmontagem de OnSpawn, UpdateNPC_BuffFlagsReset,
  StrikeNPC_Inner, Damage_PVE, checkDead, ScaleStats_ByPlayerCount,
  UpdateNPC_BuffApplyDOTs, DrawNPCDirect e CacheNPCDraws.
- ExMod v1.5.0 local em `C:\Scripts\Terraria\curso\ExMod_v1.5.0\Modified\1.mod\TL`:
  ModNPC.js, Hooks/NPC.js, Hooks/GameContent.js e Loaders/NPCLoader.js.

Os resultados do singleplayer, a revisão e os limites da medição estão em
[RESULTADOS.md](RESULTADOS.md).
