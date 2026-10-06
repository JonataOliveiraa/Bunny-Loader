# Combate de ModItem

Esta é a primeira etapa do [plano de expansão](../../tools/tests/moditemhooks/PLANO.md). A API do Bunny Loader recebe o `item` explicitamente como primeiro argumento. As referências mutáveis são objetos `Ref`, alterados por `.value`.

| Método | Contrato | Ponto nativo |
| --- | --- | --- |
| `ModifyWeaponDamage(item, player, damage)` | `damage` é `StatModifier`. O retorno numérico também é aceito. | `Player.GetWeaponDamage` |
| `ModifyWeaponCrit(item, player, crit)` | `crit` é `Ref<number>`; o resultado vira inteiro. | `Player.GetWeaponCrit` |
| `ModifyWeaponKnockback(item, player, knockback)` | `knockback` é `StatModifier`; resultado negativo ou não finito vira zero. | `Player.GetWeaponKnockback` |
| `ModifyItemScale(item, player, scale)` | `scale` é `Ref<number>` do multiplicador. Inclui a luva nativa. Não altere `Item.scale` neste callback. | `Player.GetAdjustedItemScale` e `ItemCheck_GetMeleeHitbox` |
| `CanHitNPC(item, player, target)` | `false` veta, `true` permite a elegibilidade, `null`/`undefined` conserva o jogo. A colisão continua sendo verificada. | `Player.ProcessHitAgainstNPC` e `CanNPCBeHitByPlayerOrPlayerProjectile` |
| `CanMeleeAttackCollideWithNPC(item, player, hitbox, target)` | `false` veta, `true` permite a colisão da hitbox com o alvo, `null`/`undefined` conserva o jogo. | `Player.ProcessHitAgainstNPC` e `Rectangle.Intersects` |
| `ModifyHitNPC(item, player, target, modifiers)` | Modifica um ataque corpo a corpo antes de `StrikeNPC`. | `NPC.StrikeNPC`, no contexto do item |
| `CanHitPvp(item, player, target)` | `false` veta o golpe corpo a corpo antes de `Hurt`. | `Player.ItemCheck_MeleeHitPVP` e `Hurt` |
| `ModifyHitPvp(item, player, target, modifiers)` | Objeto com `damage`, `hitDirection`, `quiet`, `crit`, `dodgeable`. | `Player.Hurt`, no contexto do ataque corpo a corpo |
| `OnHitPvp(item, player, target, hurtInfo)` | Só após dano positivo. Recebe os dados efetivamente aplicados. | retorno de `Player.Hurt` |
| `MeleeEffects(item, player, hitbox)` | Efeitos após os visuais nativos de uso. | `Player.ItemCheck_EmitUseVisuals` |
| `UseItemHitbox(item, player, hitbox, noHitbox)` | `hitbox` é `Ref<Rectangle>` e `noHitbox` é `Ref<boolean>`. Roda depois da hitbox nativa. | `Player.ItemCheck_GetMeleeHitbox` |

`ModifyHitNPC` recebe `damage`, `knockBack`, `hitDirection`, `crit`, `SourceDamage` e `Knockback`. Os dois últimos são `StatModifier`. Também pode chamar `SetCrit()` e `DisableCrit()`. O loader aplica os modificadores e limita dano e repulsão a valores finitos não negativos. Golpes recebidos com `fromNet` não reaplicam esses modificadores.

`hurtInfo` contém `DamageSource`, `Damage`, `HitDirection`, `PvP`, `Quiet`, `Crit`, `CooldownCounter` e `Dodgeable`. `Damage` é o retorno positivo do `Hurt` nativo. Esquivas e vetos não produzem `OnHitPvp`. Os callbacks de PvP desta etapa dependem do contexto de `ItemCheck_MeleeHitPVP`: um `Hurt` isolado, inclusive recebido por mensagem de rede, não reconstrói a arma de origem. A validação entre dois processos e a identificação de origem permanecem pendentes no plano.

Os objetos de modificadores são contratos JavaScript do Bunny Loader. Eles não expõem todos os campos de `NPC.HitModifiers` ou `Player.HurtModifiers` do tModLoader. O `OnHitNPC` existente mantém a assinatura `OnHitNPC(item, player, npc, damageDone, knockBack, crit)`.

## Ordem e vetos

O dano da arma passa por ModItem, GlobalItem e ModPlayer, usando um único hook compartilhado. Os retornos numéricos de GlobalItem/ModPlayer e a atribuição legada `ModPlayer.WeaponDamage` continuam válidos. Crítico, repulsão, escala e modificadores de acerto passam pelo ModItem antes do ModPlayer. Qualquer veto de ModPlayer ou ModNPC vence uma permissão do item.

## Exemplo

```js
ModifyWeaponDamage(item, player, damage) {
    damage.Additive += 0.15;
}

ModifyWeaponCrit(item, player, crit) {
    crit.value += 5;
}

ModifyItemScale(item, player, scale) {
    scale.value *= 1.2;
}

ModifyHitNPC(item, player, target, modifiers) {
    modifiers.SourceDamage.Multiplicative *= 1.1;
}

UseItemHitbox(item, player, hitbox, noHitbox) {
    const box = hitbox.value;
    hitbox.value = Rectangle.new(box.X - 8, box.Y - 8, box.Width + 16, box.Height + 16);
}
```

## Desempenho e validação

Os hooks com argumento de item usam marcas por tipo avaliadas antes da entrada no JavaScript. Itens vanilla e itens de mod sem a sobrescrita correspondente seguem diretamente para o original, enquanto não houver um observador global daquele fluxo. ModPlayer, GlobalItem e DamageClass ampliam somente as marcas necessárias no registro. Tipos de mod registrados depois recebem essas marcas.

As sobrescritas herdadas e os rótulos de erro são preparados por classe. `Rectangle.Intersects` usa `whileIn` e uma flag: só entra no JavaScript quando a colisão foi forçada durante o ataque. Os contextos e as alterações temporárias de escala são restaurados em `finally`.

Veja os [testes e limites da etapa](../../tools/tests/moditemhooks/README.md). Os contratos foram confrontados com o [ModItem do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs), o dump móvel e a desassemblagem dos métodos utilizados.
