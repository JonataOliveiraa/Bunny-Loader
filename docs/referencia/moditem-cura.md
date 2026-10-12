# ModItem: healing and potion delay

The four methods receive the instance's `item` as the first argument. The modifiers use `Ref<number>`: change `.value`. The modifiers' return value is ignored.

| Method | Contract |
| --- | --- |
| `GetHealLife(item, player, quickHeal, healValue)` | Modifies the life restored when `item.healLife > 0`. Also takes part in selecting `QuickHeal` candidates. |
| `GetHealMana(item, player, quickHeal, healValue)` | Modifies the mana restored when `item.healMana > 0`. |
| `ModifyPotionDelay(item, player, baseDelay)` | Modifies the delay in ticks computed by the mobile game, before the permission and the application of the counter and the buff. |
| `ApplyPotionDelay(item, player, potionDelay)` | Default `true`. `false` prevents this application's counter and potion sickness buff. |

## Normal and quick healing

`quickHeal` is `true` in the quick heal selection and in the application done by `QuickHeal` or `QuickMana`; in a normal application it is `false`. The context identifies the player who started the operation. Another player's healing during that context still receives `false`.

For each channel with a positive base value, the order is `ModItem`, `ModPlayer` and the native application. The `healLife` and `healMana` fields are replaced temporarily for the calculation and restored in `finally`, including on native helper failures. Max life and mana, visual effects, mana sickness and consumption stay with the game. The callback must not consume the item.

`QuickHeal`'s selection uses the modified life value to compare candidates and restores the field after each comparison. It may query the same item before applying it; avoid persistent effects in this modifier. Empty stacks, items that are not potions and candidates without a positive base heal do not run the selection callback. `QuickMana` keeps the native choice of the first valid candidate with positive mana.

Negative values are clamped to zero and fractions are truncated. Non-finite values or values of another type keep the previous native value. Very large values are clamped to prevent overflow in the game's integer sum. A channel with a zero base value is not turned into a healing channel by these hooks, including ModPlayer.

`StrangeBrew` keeps the native life roll, which uses `healLife` as its lower bound. A ModPlayer change on that item changes that bound, and does not replace the rolled result. Keep the range the native roll accepts when changing it. New ModItem items do not have the vanilla StrangeBrew type.

## Potion delay

1. The game computes its delay, keeping the duration fields and the special cases of RestorationPotion, Eggnog, Mushroom and StrangeBrew.
2. `ModItem.ModifyPotionDelay` receives that result in a `Ref`.
3. `ModItem.ApplyPotionDelay` and `ModPlayer.ApplyPotionDelay` receive the final integer. Both are asked; any `false` wins.
4. If the result is positive and allowed, `player.potionDelay` and the `PotionSickness` buff get the same duration.

A veto or a zero duration keeps the previous counter and prevents the buff. It also prevents the hunger normalization that happens inside `ApplyPotionDelay`. Mushroom's native use has an extra hunger call outside that method; it keeps its behavior. Vetoing the delay lets healing and consumption go on.

The permission callbacks see the previous counter, before the new value is applied. `AddBuff` from another player, another buff type or outside the `ApplyPotionDelay` context keeps the original behavior. Callback exceptions are logged with class and method; the other callbacks go on. A native helper failure restores the previous counter and releases the context, without undoing native effects that already happened.

In tModLoader, `ModifyPotionDelay` acts before `Player.PotionDelayModifier`. That modifier does not exist in the mobile dump Bunny Loader uses. Here, `baseDelay` represents the delay already computed by the mobile game, including the native reductions. Do not apply those reductions again. This difference was checked in the [ModItem signatures](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs) and in [tModLoader's Player flow](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Player.cs.patch).

## Filters and validation

The override plans are cached per class. Per-type marks filter healing and delay before entering JavaScript. The quick heal comparison reads the native static method's fourth argument and uses `whileIn` to restrict the selection. The auxiliary buff and hunger hooks only get in during the marked delay method. The queries cost O(1) per candidate and the selection keeps the native O(s) scan, with s equal to the number of slots consulted.

A ModPlayer observer widens the marks once on registration, including items registered later. ModItem and ModPlayer share a single healing application hook and a single delay hook, regardless of registration order.

Running in the APK validated normal healing, `QuickHeal`, `QuickMana`, consumption, refs, counter and buff, life selection and composition with ModPlayer in single player. Validation across two processes is still pending. Evidence: [step 3 results](../../tools/tests/moditemhooks/RESULTADOS-CURA.md).

## Example

```js
GetHealLife(item, player, quickHeal, healValue) {
    healValue.value += 25;
}

GetHealMana(item, player, quickHeal, healValue) {
    healValue.value *= 1.5;
}

ModifyPotionDelay(item, player, baseDelay) {
    baseDelay.value = Math.trunc(baseDelay.value * 0.8);
}

ApplyPotionDelay(item, player, potionDelay) {
    return true;
}
```
