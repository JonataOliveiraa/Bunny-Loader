# ModItem: animation, frames and ammo

The callbacks receive the instance's `item` as the first argument. Override only the methods you need. Weapon methods receive the ammo as the second argument; ammo methods receive the weapon in that position.

## Animation and frames

| Method | Contract |
| --- | --- |
| `UseAnimation(item, player)` | Before `Player.ApplyItemAnimation` computes the duration. Changes to `item.useAnimation` take part in the native calculation. ModPlayer's speed modifiers are applied next. |
| `UseItemFrame(item, player)` | After `Player.PlayerFrame`, with `itemAnimation > 0`. Can change `player.bodyFrame`, `legFrame` and the player's other drawing fields. |
| `HoldItemFrame(item, player)` | After `Player.PlayerFrame`, with `itemAnimation <= 0`, as long as `CanVisuallyHoldItem` allows drawing the item. |

The frames are an adaptation of tModLoader's insertion point: they run after the mobile game's complete native method. Their return values are ignored. The native filter reads `inventory[selectedItemState.selected].type` and checks the per-type mark before taking the JavaScript lock. It does not walk the inventory every frame. Items without overrides do not enter this dispatcher callback. The existing armor hook has its own flow and its own counters.

## Selection and consumption

| Method | Who receives it | Default and behavior |
| --- | --- | --- |
| `NeedsAmmo(item, player)` | Weapon | `true`. `false` allows using a temporary instance of the default ammo when no candidate is found. It does not enter the inventory and is not consumed. The category must match the default item's `ammo` field. |
| `CanChooseAmmo(item, ammo, player)` | Weapon | `null`. `false` rejects the candidate; `true` allows another category. |
| `CanBeChosenAsAmmo(item, weapon, player)` | Ammo | `null`. Combines with the weapon's decision. Any `false` wins. With no explicit decision, `ammo.ammo === weapon.useAmmo` applies. |
| `CanConsumeAmmo(item, ammo, player)` | Weapon | `true`. `false` keeps the stack without preventing the shot. |
| `CanBeConsumedAsAmmo(item, weapon, player)` | Ammo | `true`. `false` keeps the stack. |
| `OnConsumeAmmo(item, ammo, player)` | Weapon | Notification of actual consumption. |
| `OnConsumedAsAmmo(item, weapon, player)` | Ammo | Notification of actual consumption. |
| `PickAmmo(item, weapon, player, type, speed, damage, knockback)` | Ammo | `type`, `speed` and `knockback` are `Ref<number>`s. `damage` is a `StatModifier` applied to the final damage computed by the mobile game. |

`undefined` also keeps the permissions' default decision. Empty stacks and air items are not candidates. Returning `true` indiscriminately allows any item with a positive stack, including the weapon itself; restrict that permission to the candidate you want.

The selection keeps the slot lists the game provides and the mobile version's ammo switching modes. The switching index is applied only to the accepted candidates; weapons with the native selection exceptions keep the first stack. The scan costs O(s), with s equal to the number of slots in that range, and only happens when the selection is queried. An override on the ammo needs to observe vanilla weapons; its marks are widened on registration, including types registered later.

## Order and mobile adaptation

1. Select a candidate and ask the weapon's, the ammo's and ModPlayer's consumption vetoes.
2. Run the native `PickAmmo`, keeping the game's bonuses, projectile combinations and savings chances.
3. Run the ammo's `ModItem.PickAmmo`, adjusting the results before returning them to the caller.
4. On an actual decrement, call the weapon's `OnConsumeAmmo`, the ammo's `OnConsumedAsAmmo` and `ModPlayer.OnConsumeAmmo`, in that order.
5. Clear the last unit with the native `TurnToAir`.

In tModLoader, the notifications come before the decrement. In Bunny Loader, the decrement stays in the native method; the callbacks receive the already reduced stack. On the last unit, `stack` is zero, but the type and the instance remain available until the notifications finish. `PickAmmo`'s `StatModifier` operates on mobile's total damage, not on the ammo's separate contribution used by tModLoader.

`dontConsume`, non-consumable ammo, a veto or native savings produce no notifications. `PickAmmo` can still adjust the results of a query without consumption. Do not force another `stack` reduction in a notification.

The final projectile and damage values are non-negative integers. Speed and knockback are finite and non-negative. Non-finite values return the previous native result. Callback exceptions are attributed to the class and the method; the other notifications go on, and the cleanup and temporary fields are restored in `finally`.

There is a single shared `Player.PickAmmo` hook, regardless of the registration order of ModItem and ModPlayer. The auxiliary item selection and cleanup hooks only enter JavaScript during that context. These callbacks send no extra packets; the selection takes part in the existing local flow. Running across two processes still needs specific validation.

## Examples

```js
NeedsAmmo(item, player) {
    return false;
}

CanConsumeAmmo(item, ammo, player) {
    return player.itemAnimation <= item.useTime;
}
```

In the ammo's class:

```js
CanBeChosenAsAmmo(item, weapon, player) {
    return weapon.useAmmo === Terraria.ID.AmmoID.Arrow ? true : null;
}

PickAmmo(item, weapon, player, type, speed, damage, knockback) {
    speed.value += 2;
    damage.Flat += 5;
}
```

Contracts consulted: [ModItem](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs), [ItemLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ItemLoader.cs) and [insertion points in Player](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Player.cs.patch). The `UseAnimation` alternative in TL Pro's ExMod was also checked. Local evidence: `refs/dump.cs` and the disassembly of the mobile animation, selection, use and consumption routines.
