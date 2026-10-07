# Golpes: acertar, mudar o dano e reagir

Todo golpe do jogo passa pelos mesmos três momentos, como no tModLoader:

1. **CanHit**: alguém pode vetar (`false`) ou forçar (`true`) o acerto;
2. **ModifyHit**: o golpe vira um `HitModifiers` (no NPC) ou um
   `HurtModifiers` (no jogador), que cada um ajusta;
3. **OnHit**: depois do dano, com o resultado (`HitInfo` ou `HurtInfo`).

Os métodos têm os nomes e a ordem do tModLoader 1.4.4 e valem igual para o
item, o projétil e o NPC de mod (`ModItem`, `ModProjectile`, `ModNPC`), para os
Globais (`GlobalItem`, `GlobalProjectile`, `GlobalNPC`, também nos do jogo) e
para o `ModPlayer`. Quem junta tudo é o `CombatLoader`.

## Quem é chamado em cada golpe

**Item corpo a corpo num NPC** (a espada do jogador):

| | Item | NPC acertado | Jogador |
|---|---|---|---|
| CanHit | `CanHitNPC(item, player, target)` | `CanBeHitByItem(npc, player, item)` | `CanHitNPC`, `CanHitNPCWithItem` |
| ModifyHit | `ModifyHitNPC(item, player, target, modifiers)` | `ModifyHitByItem(npc, player, item, modifiers)` | `ModifyHitNPCWithItem`, `ModifyHitNPC` |
| OnHit | `OnHitNPC(item, player, target, hit, damageDone)` | `OnHitByItem(npc, player, item, hit, damageDone)` | `OnHitNPCWithItem`, `OnHitNPC` |

**Projétil num NPC**:

| | Projétil | NPC acertado | Dono (jogador) |
|---|---|---|---|
| CanHit | `CanHitNPC(proj, target)` | `CanBeHitByProjectile(npc, proj)` | `CanHitNPCWithProj` |
| ModifyHit | `ModifyHitNPC(proj, target, modifiers)` | `ModifyHitByProjectile(npc, proj, modifiers)` | `ModifyHitNPCWithProj`, `ModifyHitNPC` |
| OnHit | `OnHitNPC(proj, target, hit, damageDone)` | `OnHitByProjectile(npc, proj, hit, damageDone)` | `OnHitNPCWithProj`, `OnHitNPC` |

**NPC em NPC** (o inimigo que encosta no morador):

| | Quem bate | Quem apanha |
|---|---|---|
| CanHit | `CanHitNPC(npc, target)` | `CanBeHitByNPC(npc, attacker)` |
| ModifyHit | `ModifyHitNPC(npc, target, modifiers)` | |
| OnHit | `OnHitNPC(npc, target, hit)` | |

Em todo golpe num NPC, por último: `ModifyIncomingHit(npc, modifiers)` do NPC
acertado (também no golpe sem autor: armadilha, explosão).

**No jogador** (contato de NPC, projétil hostil, PvP):

| | NPC | Projétil hostil | PvP com item | PvP com projétil | Jogador acertado |
|---|---|---|---|---|---|
| CanHit | `CanHitPlayer(npc, target, cooldownSlot)` | `CanHitPlayer(proj, target)` | `CanHitPvp(item, player, target)` e `ModPlayer.CanHitPvp` | `CanHitPvp(proj, target)` e `ModPlayer.CanHitPvpWithProj` | `CanBeHitByNPC`, `CanBeHitByProjectile`, `ImmuneTo` |
| ModifyHit | `ModifyHitPlayer(npc, target, modifiers)` | `ModifyHitPlayer(proj, target, modifiers)` | `ModifyHitPvp(item, player, target, modifiers)` | `ModifyHitPlayer(proj, ...)` | `ModifyHitByNPC`, `ModifyHitByProjectile`, `ModifyHurt` |
| OnHit | `OnHitPlayer(npc, target, hurtInfo)` | `OnHitPlayer(proj, target, info)` | `OnHitPvp(item, player, target, hurtInfo)` | `OnHitPlayer(proj, ...)` | `OnHitByNPC`, `OnHitByProjectile`, `OnHurt`, `PostHurt` |

Os `Can` que devolvem `null` (os de "bool?" do tModLoader) deixam o jogo
decidir: um `false` de qualquer um veta; senão um `true` força. Os que
devolvem `true` por padrão (`CanHitPlayer`, `CanHitPvp`, `CanHitNPC` de NPC
em NPC) só vetam.

## HitModifiers: o golpe no NPC

```js
ModifyHitNPC(item, player, target, modifiers) {
    modifiers.SourceDamage.Multiplicative *= 1.5;   // antes da defesa
    modifiers.ArmorPenetration += 10;
    if (target.boss) modifiers.FinalDamage.Flat += 20;   // depois da defesa e do crítico
    modifiers.SetCrit();
}
```

A conta é a do tModLoader: `SourceDamage` no dano bruto, mais
`FlatBonusDamage`, `ScalingBonusDamage` e `TargetDamageMultiplier`; tira a
defesa (`Defense`, `ArmorPenetration`, `ScalingArmorPenetration`,
`DefenseEffectiveness`, 0,5 por padrão, como no jogo); aplica `CritDamage` (×2
por padrão) ou `NonCritDamage`; e por fim `FinalDamage`. Também:
`SetMaxDamage(n)`, `SetInstantKill()`, `DisableCrit()`, `SetCrit()`,
`DisableKnockback()`, `HideCombatText()`, `Knockback` (`StatModifier`) e
`HitDirectionOverride`.

Os campos `AddableFloat` e `MultipliableFloat` do C# são números aqui:
`modifiers.ArmorPenetration += 5`, `modifiers.TargetDamageMultiplier *= 2`.
Os nomes de antes (`modifiers.damage`, `knockBack`, `hitDirection`, `crit`)
continuam valendo.

O `hit` do `OnHit` é um `HitInfo`: `Damage` (o que o NPC levou),
`SourceDamage`, `Crit`, `Knockback`, `HitDirection`, `InstantKill`,
`HideCombatText`.

**Multijogador:** o jogo do celular manda ao servidor o dano bruto do golpe (a
mensagem 28), e o servidor refaz a conta da defesa. O loader converte o dano
final num bruto que, pela conta do jogo, dá o mesmo número; com defesa ímpar e
crítico pode sobrar 1 ponto.

## HurtModifiers: o golpe no jogador

```js
ModifyHurt(player, modifiers) {
    modifiers.SourceDamage.Multiplicative *= 0.8;
    if (modifiers.PvP) modifiers.SetMaxDamage(50);
}
```

`SourceDamage` e `IncomingDamageMultiplier` antes da defesa;
`ArmorPenetration` e `ScalingArmorPenetration` tiram defesa; `FinalDamage` e
`SetMaxDamage` depois dela. `Cancel()` desfaz o golpe; `DamageSource`, `PvP`,
`CooldownCounter`, `Dodgeable` e `HitDirectionOverride` como no tModLoader.
O jogo do celular não tem repulsão variável no jogador: `Knockback` fica para
quem lê. Os nomes de antes (`damage`, `hitDirection`, `quiet`, `crit`,
`dodgeable`) continuam.

O `HurtInfo` dos `OnHit`: `Damage`, `SourceDamage`, `DamageSource`, `PvP`,
`CooldownCounter`, `Dodgeable`, `HitDirection`, `Crit`, `Quiet`.

## Limites

- O `CanHitNPC` do projétil e o `CanBeHitByProjectile` valem para os projéteis
  do jogador (o jogo só pergunta por eles); o projétil de um morador acerta
  sem eles, mas passa pelo `ModifyHit` e pelo `OnHit`.
- `true` no `CanHitNPC` de um projétil não faz ele acertar um NPC amigo (o
  jogo decide isso antes de perguntar); no do item, faz.
- Custo: cada golpe entra no JS uma vez por hook. O do item e o do projétil só
  são instalados quando alguma classe escreve um desses métodos; o do jogador
  (`Player.Hurt`) só com um método do jogador acertado.
