# 9. Content classes

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · [3. `ref`, `out`, `Nullable`](03-ref-out-e-nullable.md) · [4. Values and objects](04-valores-e-objetos.md) · [5. C# × JS × tModLoader](05-comparacao.md) · [6. Performance](06-desempenho.md) · [7. Moving to C#](07-migracao-para-csharp.md) · [8. Under the hood](08-por-baixo-dos-panos.md) · **9. Content classes**

Hooks change what the game already has. **Content classes** add new things:
a class that extends `ModItem` becomes a new item. They follow tModLoader:
the same names, in the same namespace (`Terraria.ModLoader`), loaded in the
same order. C# has `ModItem` today; the other content classes (`ModNPC`,
`ModProjectile`, `ModSystem`...) come one by one (see
[the C# plan](../nucleo/plano-csharp.md)).

## A new item

```csharp
using Terraria;
using Terraria.ModLoader;

namespace MyMod.Content.Items;

public class ShinyRock : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.buyPrice(silver: 1);
    }
}
```

`maxStack` starts at 1, as in tModLoader (the phone's game would start every
item at 9999): set it for anything that stacks.

The texture is `content/Content/Items/ShinyRock.png`: the namespace plus the
class name, as in tModLoader, and the first part (the mod's name) may be
missing on disk. The name and tooltip come from the mod's translations,
`content/Localization/en-US_Mods.MyMod.hjson` (see
[Translations](#translations)):

```hjson
Items: {
	ShinyRock: {
		DisplayName: Shiny Rock
		Tooltip: It glows a little
	}
}
```

Nothing registers the class by hand: Bunny Loader finds it in your `.dll`.
`ModContent.ItemType<ShinyRock>()` is its item ID, and
`ModContent.GetInstance<ShinyRock>()` is the registered object (both are `0`
and `null` for a class that did not load, as in tModLoader). A complete
example is [`samples/ExampleModCSharp`](../../samples/ExampleModCSharp), the
port of tModLoader's ExampleMod.

## What gets loaded

Every class in your `.dll` that implements `ILoadable` (every `ModType`, like
`ModItem`, does) is loaded on its own, in the order of its full name, when it:

- is not abstract and has a constructor without parameters;
- has no `[Autoload(false)]`, on itself or on a base class;
- has no `[Autoload(Side = ModSide.Server)]` (the phone has no dedicated
  server, so server-only content never loads);
- returns `true` from `IsLoadingEnabled(mod)` (the default).

```csharp
// Only when the option is on.
public override bool IsLoadingEnabled(Mod mod) => MyOptions.ExtraItems;
```

A class left out of the autoload can still be loaded from `Mod.Load`:

```csharp
[Autoload(false)]
public class SecretItem : ModItem { /* ... */ }

public class MyMod : Mod
{
    public override void Load()
    {
        AddContent<SecretItem>();
    }
}
```

To turn the autoload off for the whole mod, set
`ContentAutoloadingEnabled = false` in the mod's constructor and call
`AddContent` for each class. `AddContent` only works while the mod loads: from
`PostSetupContent` on, it throws.

Your own classes can join the load too: implement `ILoadable` and its
`Load(Mod mod)` runs with the content's, and
`ModContent.GetInstance<T>()` finds the instance.

## The order

| When | What runs | Thread |
|---|---|---|
| 1. The mod loads (the game is opening) | each content class: `IsLoadingEnabled`, then its `Load()`; then your `Mod.Load()` | the loader's |
| 2. The game knows the new types | each item's and each `ModSystem`'s `SetStaticDefaults()` | the game's |
| 3. All mods' content is in the game | `Mod.PostSetupContent()`, then each `ModSystem`'s | the game's |
| 4. | `Mod.AddRecipeGroups()`, then each `ModSystem`'s | the game's |
| 5. | `Mod.AddRecipes()`, each `ModSystem`'s, each item's and each `GlobalItem`'s `AddRecipes()` | the game's |
| 6. | `Mod.PostAddRecipes()`, then each `ModSystem`'s | the game's |

Each of steps 3 to 6 runs in **every** C# mod (in load order) before the next
one starts, as in tModLoader: in `AddRecipes` you can use any mod's items. An
exception in one mod's step goes to the log and the error panel, and the
other mods go on.

Two differences from tModLoader:

- `Unload()` exists (on `Mod`, `ModType` and `ILoadable`) but is **never
  called**: Bunny Loader does not unload mods while the game runs; changing
  mods restarts the game.
- `ModSystem` has, for now, only these steps (`SetStaticDefaults`,
  `PostSetupContent`, `AddRecipeGroups`, `AddRecipes`, `PostAddRecipes`);
  its other hooks come later.

## The template and its instances

The object Bunny Loader creates and registers is the **template**. In
`Load` and `SetStaticDefaults` it has no game item yet: `Item.IsNull` is
`true`. Each time the game fills in an item of your type (`Item.SetDefaults`),
your `SetDefaults` runs in a **new instance**, with `Item` set to that item and
`Type` and `Mod` copied from the template.

By default the new instance is created by your class's constructor, so
fields have their initial values, not the template's. To copy the template
instead (the fields you set in `Load`, for example), override
`CloneNewInstances`:

```csharp
protected override bool CloneNewInstances => true;
```

The copy is shallow: a list or array in a field is shared with the template.
Override `Clone(Item newEntity)` to copy it deeper:

```csharp
public List<int> Charges = new();

public override ModItem Clone(Item newEntity)
{
    var copy = (ShinyRock)base.Clone(newEntity);
    copy.Charges = new List<int>(Charges);
    return copy;
}
```

### `Item.ModItem`

The instance stays with its item: `item.ModItem` gives it back, the same
object every time, so its fields are per item.

```csharp
if (Main.LocalPlayer.HeldItem.ModItem is ShinyRock rock)
    rock.Polish++;
```

- `item.ModItem` is `null` for an item of the game, and becomes `null` again
  when `SetDefaults` turns the item into a vanilla one.
- `Item.Clone()` gives the copy a `Clone` of the instance (with your `Clone`
  override, if there is one), so the data follows the item into the copy.
- An item that became your type without `SetDefaults` (its `type` written by
  hand) gets a new instance the first time `item.ModItem` is read.
- The data lives as long as the game keeps the item: Bunny Loader holds no
  item alive. To keep it across sessions, save it (next section); it is not
  sent in multiplayer yet (`NetSend` comes later, see the plan).

### Saving: `SaveData` and `LoadData`

```csharp
using Terraria.ModLoader.IO;

public class ShinyRock : ModItem
{
    public int Polish;

    public override void SaveData(TagCompound tag) => tag["polish"] = Polish;

    public override void LoadData(TagCompound tag) => Polish = tag.GetInt("polish");
}
```

The item's data goes with the item wherever the game saves it: the player's
inventory, equipment (armor, accessories, dyes), piggy bank, safe and the
other banks (in `<player>.plr.bl`, next to the `.plr`) and chests
(`<world>.wld.bl`). Items on the ground are not saved,
as in the game. Bunny Loader stores each item by its name, so the data
survives other mods being added or removed; an item of a mod that is not
loaded keeps its data until the mod comes back.

`TagCompound` is tModLoader's:

| Call | Does |
|---|---|
| `tag["key"] = value` | sets (or replaces) a value |
| `tag.Add("key", value)` | sets; throws if the key is already there |
| `tag.GetInt("key")`, `GetFloat`, `GetBool`, `GetString`, `GetByteArray`, `GetIntArray`, `GetCompound`, `GetList<T>` | reads; a missing key gives `0`, `""`, an empty list or an empty `TagCompound` |
| `tag.Get<T>("key")` | reads as `T` (numbers convert); a value of another kind throws |
| `tag.TryGet("key", out T value)` | `false` instead of throwing |
| `tag.ContainsKey("key")`, `tag.Remove("key")` | as in a dictionary |

What can go in: `bool`, the number types, `string`, `byte[]`, `int[]`, lists
of these, and other `TagCompound`s. For a `Vector2` or a `Color`, save their
fields.

- If `LoadData` throws, the error goes to the log, and the saved data is kept
  as it was: the next save writes it back untouched instead of losing it.
- The data is stored as JSON, the same format JavaScript mods use, so a JS
  mod ported to C# keeps its players' saved items.
- `TagIO.ToFile(tag, path)` and `TagIO.FromFile(path)` write and read a
  `TagCompound` to a file of your own, in tModLoader's binary format
  (compressed by default).

A `GlobalItem` has no `SaveData` yet: its data is not saved.

## Using an item

These run while a player holds or uses the item. Each one exists on
`ModItem` (for your item) and on `GlobalItem` (with the `Item` first, for any
item), with tModLoader's signatures:

| `ModItem` | When | Return |
|---|---|---|
| `CanUseItem(Player player)` | Before a use starts | `false` stops it (every mod is asked; one `false` is enough) |
| `UseItem(Player player)` | The frame the use starts | `bool?`: `true` makes it count as used (the use time is applied); `null` leaves it to the game |
| `UseAnimation(Player player)` | Before the game computes the animation length | change `Item.useAnimation` here to change it |
| `UseStyle(Player player, Rectangle heldItemFrame)` | Every frame of a use, after the game places the item | |
| `HoldStyle(Player player, Rectangle heldItemFrame)` | Every frame the item is held but not used | |
| `HoldItem(Player player)` | Every frame the item is in hand | |
| `HoldoutOffset()` | With the styles | `Vector2?`: moves the item in the hand (x forward, y down) |
| `UpdateInventory(Player player)` | Every frame the item is in the inventory (the 58 slots, coins and ammo too) | |
| `UseTimeMultiplier(Player player)`, `UseAnimationMultiplier(Player player)`, `UseSpeedMultiplier(Player player)` | When a use starts | `float`: the use time is multiplied by `UseTimeMultiplier`, the animation by `UseAnimationMultiplier`, and both divided by `UseSpeedMultiplier` (the `GlobalItem`s multiply in) |
| `CanAutoReuseItem(Player player)` | When the game decides whether the use repeats | `bool?`: `true` repeats it even without `autoReuse`, `false` never |
| `AltFunctionUse(Player player)` | When the player uses the tile control (right click) with the item | `true`: the item is used with `player.altFunctionUse == 2` (check it in `UseItem`, `CanUseItem`, `Shoot`...) |
| `UseItemFrame(Player player)` / `HoldItemFrame(Player player)` | Every frame, after the game picks the player's body frame, while the item is used / held | change `player.bodyFrame` |
| `ModifyManaCost(Player player, ref float reduce, ref float mult)` | When the game checks or spends the mana of a use | `reduce` starts at `player.manaCost`; the cost is the item's mana × `reduce` × `mult` (multiply `mult`) |
| `OnMissingMana(Player player, int neededMana)` | When the player has less mana than the use needs | |
| `OnConsumeMana(Player player, int manaConsumed)` | After the mana is spent | |

Healing items (potions, food that heals) have their own:

| `ModItem` | When | Return |
|---|---|---|
| `GetHealLife(Player player, bool quickHeal, ref int healValue)` | When the item heals, and when the quick heal button picks the best potion | change `healValue` |
| `GetHealMana(Player player, bool quickHeal, ref int healValue)` | When the item restores mana | change `healValue` |
| `ModifyPotionDelay(Player player, ref int baseDelay)` | When a potion gives Potion Sickness | change the length, in ticks |
| `ApplyPotionDelay(Player player, int potionDelay)` | Right after | `false`: no Potion Sickness this time |

`quickHeal` is `true` when the heal comes from the quick heal or quick mana
button.

Picking an item up from the ground:

| `ModItem` | When | Return |
|---|---|---|
| `CanPickup(Player player)` | Before the player grabs the item | `false`: it stays on the ground (the `GlobalItem`s are asked first; one `false` is enough) |
| `GrabRange(Player player, ref int grabRange)` | When the game measures how close the item has to be | change `grabRange`, in pixels; more pulls it from farther, less takes it only closer |
| `GrabStyle(Player player)` | Every frame the item is pulled toward the player | `true`: you moved it (change its `WorldItem`'s `velocity` or `position`) and the game does not |
| `ItemSpace(Player player)` | When the inventory is full | `true`: it is picked up anyway (pair it with `OnPickup`) |
| `OnPickup(Player player)` | When the player touches the item | `false`: the item leaves the ground and does not go into the inventory |

On the phone an item on the ground is a `WorldItem` that holds the `Item`
(`WorldItem.inner`). The pickup hooks get the `Item`, as in tModLoader;
`Item.GetWorldItem()` gives its `WorldItem` (its `IsNull` is `true` when the
item is not on the ground), which has the position and the velocity:

```csharp
public override bool GrabStyle(Player player)
{
    WorldItem w = Item.GetWorldItem();
    if (w.IsNull) return false;
    Vector2 toPlayer = player.position - w.position;
    w.velocity = toPlayer / Math.Max(1f, toPlayer.LengthValue) * 8f;
    return true;
}
```

Opening an item (a bag, a crate):

| `ModItem` | When | Return |
|---|---|---|
| `CanRightClick()` | When the game loads (for the touch "Open" button) and when the player opens it | `true`: the item opens |
| `RightClick(Player player)` | When it opens: give the contents here (`player.QuickSpawnItem`) | |
| `ConsumeItem(Player player)` | Right after | `false`: the item is not spent |

The phone's game opens only the items in `ItemID.Sets.OpenableBag`: Bunny
Loader adds every item whose `CanRightClick()` returns `true` when the game
loads (asked on a fresh item of that type), so the "Open" button shows up for
it. A `GlobalItem` can do the same for a vanilla item, and its `RightClick`
also runs when a vanilla bag or crate opens (the game still gives what is
inside). A boss bag of yours (`ItemID.Sets.BossBag`) can also give a
developer set, as in tModLoader, unless you mark it pre-Hardmode in
`SetStaticDefaults` (`ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;`, a
tModLoader set that Bunny Loader adds, with the game's pre-Hardmode bags
already marked). `ModifyItemLoot` comes with the loot rules
(`ItemLoot`, `IItemDropRule`).

The tooltip, as a list of `TooltipLine`:

```csharp
public override void ModifyTooltips(List<TooltipLine> tooltips)
{
    TooltipLine? damage = tooltips.Find(l => l.Mod == "Terraria" && l.Name == "Damage");
    if (damage != null) damage.Text += " (charged)";
    tooltips.Add(new TooltipLine(Mod, "Charge", $"Charge: {Charge}"));
}
```

The game's lines have `Mod` `"Terraria"` and tModLoader's names
(`ItemName`, `Damage`, `CritChance`, `Speed`, `Knockback`, `Defense`,
`Tooltip0`, `Tooltip1`..., `Material`, `PrefixDamage`, `SetBonus`...).
`Hide()` takes a line out; `IsModifier`/`IsModifierBad` draw it in the green
or red of a prefix line. A `GlobalItem` gets `ModifyTooltips(Item item,
List<TooltipLine> tooltips)` for any item, after the `ModItem`. `OverrideColor` gives a line its own color, and `[c/RRGGBB:text]` colors a
part of a line (the phone's game has no such tags; Bunny Loader draws them).

Drawing the tooltip yourself, as in tModLoader:

| `ModItem` | When | Return |
|---|---|---|
| `PreDrawTooltip(ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)` | Before the tooltip is drawn | `false`: no line is drawn (the line hooks still run); change `x`/`y` to move it |
| `PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)` | Before each line | `false`: the line is not drawn; change `line.X`/`Y`, `Color`, `Rotation`, `Origin`, `BaseScale`, `Spread`; `yOffset` moves the lines below |
| `PostDrawTooltipLine(DrawableTooltipLine line)` | After each line | |
| `PostDrawTooltip(ReadOnlyCollection<DrawableTooltipLine> lines)` | After the tooltip | |

The game still lays out the tooltip and draws its box; Bunny Loader steps
in when it draws each line's text. The phone's tooltip box has no item name:
the `ItemName` line is in the list for `ModifyTooltips`, but it is never
drawn, so the line hooks do not run for it. The price lines (shop, reforge), the
One Drop logo and the materials grid of the crafting guide are drawn by the
game without the line hooks.

Drawing the item:

| `ModItem` | When | Return |
|---|---|---|
| `GetAlpha(Color lightColor)` | Whenever the game asks the item's color (on the ground, in the inventory) | `Color?`: the color to use; `null` leaves it to the game |
| `PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)` | Before the item on the ground is drawn | `false`: the game does not draw it (draw it yourself); change `rotation` and `scale` to draw it differently |
| `PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)` | After it, also when `PreDrawInWorld` returned `false` | |
| `PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)` | Before the item's icon is drawn in a slot | `false`: the game does not draw the icon |
| `PostDrawInInventory(...)` | After it, with the same arguments | |

`whoAmI` is the item's index in `Main.item`. `Color` and `SpriteBatch` are in
`Microsoft.Xna.Framework.Graphics` (the phone's XNA keeps `Color` there).
The `GlobalItem`s are asked first for `GetAlpha` (the first color wins) and
for `PreDrawInInventory`; for the others the `ModItem` goes first, as in
tModLoader.

A hook only costs anything when some mod overrides it: Bunny Loader hooks the
game method behind it the first time a loaded `ModItem` or `GlobalItem`
overrides that hook. An exception in a hook goes to the log once (not every
frame), and the game goes on as if the hook were not there.

### Combat: the weapon's numbers

| `ModItem` | When | Return |
|---|---|---|
| `ModifyWeaponDamage(Player player, ref StatModifier damage)` | Whenever the game asks the weapon's damage (the use, the tooltip) | change `damage`: `damage += 0.5f` (additive), `damage *= 1.2f`, `damage.Flat += 3` |
| `ModifyWeaponCrit(Player player, ref float crit)` | Whenever the game asks the crit chance | change `crit`, in percent |
| `ModifyWeaponKnockback(Player player, ref StatModifier knockback)` | Whenever the game asks the knockback | change `knockback` |
| `ModifyItemScale(Player player, ref float scale)` | When the item is drawn and when its melee hitbox is measured | change `scale` |
| `UseItemHitbox(Player player, ref Rectangle hitbox, ref bool noHitbox)` | When the melee hitbox is measured | change `hitbox`; `noHitbox = true` hits nothing |
| `MeleeEffects(Player player, Rectangle hitbox)` | Every frame of a swing, with the hitbox | dusts, light |

`damage` starts as the player's total for the weapon, as in tModLoader: the
phone's game computes it from the player's fields, and Bunny Loader hands it
to you as one additive bonus (`Additive` is the game's damage over
`Item.damage`), so `+=`, `*=`, `Base` and `Flat` give tModLoader's numbers.
`knockback` works the same way.

### Shooting and ammo

| `ModItem` | When | Return |
|---|---|---|
| `CanShoot(Player player)` | Before the item shoots | `false`: nothing is shot (the use time still runs) |
| `ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)` | Right before the projectile is made | change any of them |
| `Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)` | Right after | `false`: the game does not make its projectile (make your own with `Projectile.NewProjectile(source, ...)`) |
| `NeedsAmmo(Player player)` | When the game looks for ammo | `false`: the weapon shoots without ammo (as if it had the basic ammo of its `useAmmo`) |
| `CanChooseAmmo(Item ammo, Player player)` | For each ammo in the inventory | `bool?`: `true` takes it even of another kind, `false` skips it |
| `CanConsumeAmmo(Item ammo, Player player)` | Before the ammo is spent | `false`: it is not spent this time |
| `OnConsumeAmmo(Item ammo, Player player)` | After it is spent | |

The ammo item has the other side: `CanBeChosenAsAmmo(Item weapon, Player
player)`, `CanBeConsumedAsAmmo(Item weapon, Player player)`,
`OnConsumedAsAmmo(Item weapon, Player player)` and `PickAmmo(Item weapon,
Player player, ref int type, ref float speed, ref StatModifier damage, ref
float knockback)` (the projectile, speed and damage it gives the shot). The
`GlobalItem` versions take the weapon and the ammo first, as in tModLoader.

```csharp
public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
{
    for (int i = 0; i < 8; i++)
    {
        Vector2 spread = velocity.RotatedByRandom(MathHelper.ToRadians(15)) * (1f - Main.rand.NextFloat(0.3f));
        Projectile.NewProjectileDirect(source, position, spread, type, damage, knockback, player.whoAmI);
    }
    return false;
}
```

`ModifyShootStats` and `Shoot` run on the first projectile the game makes for
the shot. A game weapon that makes several (a shotgun) has its first one
changed; the others come as the game makes them. `Main.rand.NextBool`,
`NextFloat`, `RotatedBy`, `RotatedByRandom`, `ToRotation` and `SafeNormalize`
are extensions, as in tModLoader (the phone's `Utils` has them only as plain
static methods).

### Hitting an NPC

| `ModItem` | When | Return |
|---|---|---|
| `CanHitNPC(Player player, NPC target)` | Before a swing hits an NPC | `bool?`: `false` skips it, `true` hits it even if the game would not (a town NPC) |
| `CanMeleeAttackCollideWithNPC(Rectangle meleeAttackHitbox, Player player, NPC target)` | When the swing's hitbox is checked against the NPC | `bool?`: `true` hits it out of reach, `false` misses |
| `ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)` | Right before the damage | change `modifiers` |
| `OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)` | After the damage | `hit.Damage`, `hit.Crit`, `hit.Knockback`... |

`NPC.HitModifiers` and `NPC.HitInfo` are tModLoader's, with the same math
(`SourceDamage`, `FlatBonusDamage`, `Defense`, `ArmorPenetration`,
`ScalingArmorPenetration`, `CritDamage`, `FinalDamage`, `Knockback`,
`SetMaxDamage`, `DisableCrit`, `SetCrit`, `SetInstantKill`,
`HideCombatText`...). The phone's game takes the damage before defense and
sends that to the server; Bunny Loader works out the final damage the
tModLoader way and gives the game the raw damage that comes to it, so the
server reaches the same number. These are the melee hits; the projectile's
side comes with `ModProjectile`, and `CanHitPvp`/`ModifyHitPvp`/`OnHitPvp`
with the player's hurt hooks (phase 2).

## Rarities: `ModRarity`

```csharp
public class Teal : ModRarity
{
    public override Color RarityColor => new(0, 200, 200);

    public override int GetPrefixedRarity(int offset, float valueMult) => offset < 0 ? ItemRarityID.Blue : Type;
}
```

`Item.rare = ModContent.RarityType<Teal>();` gives it to an item. The type
comes after the game's 12 (`RarityLoader.VanillaCount`), counted together
with the JS mods' rarities. `RarityColor` is read each time the color is
drawn (it can blink with `Main.DiscoR`...). When a prefix changes how much
the item is worth, `offset` goes from -2 to +2 and `GetPrefixedRarity`
decides (the game alone would turn every rarity above 11 into purple). The
color shows on the text that rises when the item is picked up and on the
item's name under the cursor and in the tooltip.

## Prefixes: `ModPrefix`

```csharp
public class Shiny : ModPrefix
{
    public override PrefixCategory Category => PrefixCategory.Melee;

    public override void SetStats(ref float damageMult, ref float knockbackMult, ref float useTimeMult, ref float scaleMult,
                                  ref float shootSpeedMult, ref float manaMult, ref int critBonus)
    {
        damageMult *= 1.2f;
        critBonus += 5;
    }

    public override void ModifyValue(ref float valueMult) => valueMult *= 1.1f;
}
```

`Category` says which items can roll it (`Melee`, `Ranged`, `Magic`,
`Summon`, `AnyWeapon`, `Accessory`; `Custom` only where `CanRoll` or the
item lets it). `RollChance` is its weight against the game's prefixes (each
weighs 1). The game applies the stats (a prefix whose stats change nothing on
an item does not roll there), then `Apply(Item item)` runs;
`ApplyAccessoryEffects(Player player)` runs every frame while the accessory
is equipped, and `GetTooltipLines(Item item)` adds lines after the game's
prefix lines. The name comes from `Mods.<Mod>.Prefixes.<Name>.DisplayName`.
Prefix numbers are shared with the JS mods' and saved by name.

An item decides too: `AllowPrefix(int pre)`, `ChoosePrefix(UnifiedRandom
rand)` (a number forces that prefix), `PrefixChance(int pre, UnifiedRandom
rand)` (`false`: no prefix; `true`: always one), and `MeleePrefix()`,
`RangedPrefix()`, `MagicPrefix()`, `SummonPrefix()`, `WeaponPrefix()` for
which of the game's prefixes it rolls (by default, from `Item.melee`,
`ranged`, `magic`, `summon` and `noUseGraphic`). A `GlobalItem` has the first
three, with the `Item` first.

## Recipes

As in tModLoader, a recipe is the game's `Recipe`, created in `AddRecipes`
(of the `Mod`, a `ModSystem`, a `ModItem` or a `GlobalItem`):

```csharp
public override void AddRecipes()
{
    CreateRecipe(999)
        .AddIngredient(ItemID.DirtBlock, 10)
        .AddRecipeGroup(RecipeGroups.IronBar, 2)
        .AddTile(TileID.WorkBenches)
        .AddCondition(Condition.NearWater)
        .Register();
}
```

`Recipe.Create(result, amount)` and `Mod.CreateRecipe(result, amount)` do the
same for any item. A recipe takes at most 15 ingredients (the game's limit;
one more throws `RecipeException`) and one crafting station. The rest is
tModLoader's: `AddIngredient<T>()`, `AddIngredient(Mod, "Name")`,
`AddRecipeGroup` (by object, registered id or key: `"Wood"`, `"IronBar"`...),
`AddCondition` (a `Condition`, or a `LocalizedText` and a `Func<bool>`),
`AddOnCraftCallback`, `AddConsumeIngredientCallback` (how many of an
ingredient is used, after the Alchemy Table's discount), `DisableDecraft`,
`Clone`, `DisableRecipe`, and to change recipes in `PostAddRecipes`:
`TryGetIngredient`, `RemoveIngredient`, `RemoveCondition`, `ReplaceResult`,
`HasIngredient`, `HasTile`... (`Main.recipe[i]` up to `Recipe.numRecipes`).

`Condition` has tModLoader's ready ones (`NearWater`, `TimeNight`,
`InDesert`, `Hardmode`...). Its text comes from the translation key; without
one, from the key's last word (`Conditions.NearWater` reads "Near Water").
A recipe whose condition fails, or a disabled one, leaves the crafting menu;
the Guide still lists it, unless it is disabled.

Groups go in `AddRecipeGroups`:

```csharp
ExampleRecipeGroup = RecipeGroup.Register("ExampleMod:ExampleItem",
    ModContent.GetInstance<ExampleItem>().DisplayName.Key, ModContent.ItemType<ExampleItem>());
RecipeGroups.Bugs.Add(ModContent.ItemType<MyBug>());
```

The text is "Any <name of the key>". `Register(key, () => "Text", items)`
shows the function's text instead. Registering a key that exists (yours,
another mod's, or a game group's name: `"SilverBar"`) adds the items to that
group, as in tModLoader. The phone's `ValidItems` is a game `HashSet` that C#
cannot reach yet: add items with `group.Add(type)`.

Not on the phone yet: `DecraftConditions` are kept but the Shimmer does not
read them (`DisableDecraft` does work), `SortBefore`/`SortAfter`, and mod
tiles as stations (`AddTile<T>()`, with `ModTile`). The game has 3603 recipes
and stops at 3700: the ones past it are added by Bunny Loader to the crafting
menu and the Guide. Crafting runs `ModItem.OnCreated` and
`GlobalItem.OnCreated` with a `RecipeItemCreationContext`.

## Armor, accessories and wings

```csharp
[AutoloadEquip(EquipType.Head)]
public class ExampleHelmet : ModItem
{
    public override bool IsArmorSet(Item head, Item body, Item legs) =>
        body.type == ModContent.ItemType<ExampleBreastplate>() && legs.type == ModContent.ItemType<ExampleLeggings>();

    public override void UpdateArmorSet(Player player) => player.meleeDamage += 0.2f;
}
```

`[AutoloadEquip(EquipType.Head, ...)]` takes the worn textures next to the
item's: `ExampleHelmet_Head.png`, `_Body`, `_Legs`, `_Wings`, `_Shield`,
`_Back`... Each gets a slot after the game's (`Item.headSlot`,
`Item.wingSlot`... are set before your `SetDefaults`), counted together with
the JS mods' slots. In `SetStaticDefaults`, `Item` is the template item, as in
tModLoader, so wings can write
`ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(180, 9f, 2.5f);`.
`EquipLoader.AddEquipTexture(mod, texture, type, name: "...")` in `Load`
adds one by hand; its slot exists from `SetStaticDefaults` on
(`EquipLoader.GetEquipSlot(mod, name, type)`).

The hooks are tModLoader's: `UpdateEquip`, `UpdateAccessory(player,
hideVisual)`, `UpdateVanity`, `CanAccessoryBeEquippedWith`, `IsArmorSet` and
`UpdateArmorSet` (on each worn piece), `IsVanitySet` (by the drawn slots; by
default the `IsArmorSet` of those slots' items), `PreUpdateVanitySet`,
`UpdateVanitySet`, `ArmorSetShadows`, `SetMatch`, `EquipFrameEffects`,
`VerticalWingSpeeds`, `HorizontalWingSpeeds`, `WingUpdate`. A `GlobalItem`
has them with the `Item` first and the set as a string, and an
`EquipTexture` subclass can replace them for one texture.

The set bonus text is tModLoader's `player.setBonus`, set in
`UpdateArmorSet` (or anywhere in the player's update):

```csharp
public static LocalizedText SetBonusText { get; private set; }

public override void SetStaticDefaults() =>
    SetBonusText = this.GetLocalization("SetBonus").WithFormatArgs(AdditiveGenericDamageBonus);

public override void UpdateArmorSet(Player player)
{
    player.setBonus = SetBonusText.Value;
    player.GetDamage(DamageClass.Generic) += AdditiveGenericDamageBonus / 100f;
}
```

The game has no `Player.setBonus` since 1.4.5.8 (its sets are in
`ArmorSetBonuses`); the Bunny Loader keeps the text per player, clears it at
the start of each `UpdateArmorSets` and adds a `SetBonus` line, in the game's
"Set Bonus: ..." format and green, to the worn pieces' tooltip (an item the
game already gives a set line keeps the game's). `ModifyTooltips` sees it as
`Terraria/SetBonus`. `WithFormatArgs` makes a new text with the arguments
filled in; it does not follow a language change made after it.

`DrawArmorColor(drawPlayer, shadow, ref color, ref glowMask, ref
glowMaskColor)` (head, body and legs) and `ArmorArmGlowMask(drawPlayer,
shadow, ref glowMask, ref color)` (the body's arms) run through the worn
slot's `EquipTexture` (by default the item's) and then the `GlobalItem`s,
after the game's `PlayerDrawSet.BoringSetup`. Since that is after the game's
own effects (buff tints, stealth), a part the game made fully transparent
keeps its color; a changed glow color gets the game's immunity blink
(`GetImmuneAlphaPure`). `glowMask` is one of the game's glow masks
(`GlowMaskID`).

`CanEquipAccessory(player, slot, modded)` (`false` refuses) is asked when
the item is dropped on an accessory slot (`ItemSlot.PickItemMovementAction`),
on the quick swap (`ItemSlot.ArmorSwap`: the swap is undone and the item
stays where it was) and by the phone's `ItemSlot.CanEquipAccessoryInSlot` /
`CanEquipAccessoryInVanitySlot`. `slot` is the index in `player.armor`;
`modded` is always `false` (there are no mod accessory slots yet).

tModLoader's drawing flags are on the game's sets, sized to the slots
(the mod ones included), with tModLoader's defaults for the game's:
`ArmorIDs.Head.Sets.DrawFullHair`, `DrawHatHair`,
`DrawsBackHairWithoutHeadgear` (read after `Player.GetHairSettings`),
`ArmorIDs.Body.Sets.HidesTopSkin`, `HidesBottomSkin`, `HidesHands` (default
`true`), `HidesArms` and `ArmorIDs.Legs.Sets.HidesTopSkin`,
`HidesBottomSkin` (applied after `BoringSetup`). Set them in
`SetStaticDefaults`, as `ExampleRobe` does:

```csharp
public override void SetStaticDefaults() => ArmorIDs.Body.Sets.HidesHands[Item.bodySlot] = false;
```

They only add to what the game decides: a `true` hides, a `HidesHands` of
`false` shows the hand, a `HidesArms` of `true` hides the arm. The hooks are
installed the first time a mod reads one of these sets.

Not yet: `ArmorIDs.Head.Sets.IsTallHat` and `DrawHead`, and the PvP item
sync, which comes with the PvP hooks in phase 2.

## Damage classes: `DamageClass`

An item's class is `Item.DamageType`, as in tModLoader:

```csharp
Item.DamageType = DamageClass.Melee;
Item.DamageType = ModContent.GetInstance<ExampleDamageClass>();
```

The game's classes are `DamageClass.Default`, `Generic`, `Melee`,
`MeleeNoSpeed`, `Ranged`, `Magic`, `Summon`, `SummonMeleeSpeed`,
`MagicSummonHybrid` and `Throwing`, with tModLoader's types (0 to 9). A class
of your own extends `DamageClass` and is loaded like any other content; it
can override `GetModifierInheritance` (whose bonuses it takes, as a
`StatInheritanceData`), `GetEffectInheritance` (which classes it counts as,
for effects such as the Magma Stone), `GetPrefixInheritance`,
`SetDefaultStats`, `UseStandardCritCalcs` and `ShowStatTooltipLine`. Its name
in the tooltip is `Mods.<Mod>.DamageClasses.<Class>.DisplayName`, and should
include the word "damage" ("example damage").

The player's bonuses per class are tModLoader's `ref` methods, reset every
frame:

```csharp
player.GetDamage(DamageClass.Generic) += 0.25f;
player.GetDamage(DamageClass.Generic) *= 1.12f;
player.GetDamage(DamageClass.Generic).Flat += 5;
player.GetCritChance(DamageClass.Melee) += 10;
player.GetAttackSpeed(DamageClass.Ranged) += 0.15f;
player.GetArmorPenetration(DamageClass.Magic) += 5;
player.GetKnockback<ExampleDamageClass>() += 1f;
```

`GetTotalDamage`, `GetTotalCritChance`, `GetTotalAttackSpeed`,
`GetTotalArmorPenetration` and `GetTotalKnockback` add the inherited classes,
and `GetWeaponAttackSpeed`/`GetWeaponArmorPenetration` give an item's.
`Item.CountsAsClass` and `Projectile.CountsAsClass` follow the class's
`GetEffectInheritance`.

The phone's game has no damage classes, only the item's `melee`, `ranged`,
`magic` and `summon` flags and the player's fields (`meleeDamage`,
`meleeCrit`, `meleeSpeed`, `minionDamage`...). What you add to `Generic`,
`Melee`, `Ranged`, `Magic` and `Summon` goes into those fields once a frame,
so the game's own code sees it. Any other class (yours, `Generic`,
`MagicSummonHybrid` or `Throwing` set on an item) sets the flags of the
classes it counts as or takes prefixes from, so the game's effects and
prefixes follow, and its damage, crit, speed, knockback and armor
penetration come from the class. The tooltip names the class and shows its
crit. `DamageType` set in `SetDefaults` is the item type's; set anywhere
else, it is that item's only. `ModItem.Item` is a `ref Item`, so
`Item.DamageType = ...` works on it.

## Changing every item: `GlobalItem`

A `GlobalItem` runs for items that are not yours: the game's and other mods'.
It loads like any content class (by itself, from your `.dll`).

```csharp
public class HeavierWeapons : GlobalItem
{
    public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
        lateInstantiation && entity.damage > 0;

    public override void SetDefaults(Item entity) => entity.knockBack *= 1.5f;
}
```

- **`AppliesToEntity(entity, lateInstantiation)`** picks the items it is for
  (`true` for all, by default). It is asked twice for each `SetDefaults`:
  first with `lateInstantiation` `false`, when only `type` is set (to choose
  by type), then with `true` for the globals that said no, after the item's
  own `SetDefaults` (to choose by `damage`, `useStyle`...).
- **`SetDefaults(Item entity)`** runs after the item's own (the game's, or
  the `ModItem`'s), for every global that applies.
- **`SetStaticDefaults`** runs once, with every item in the game, before
  `PostSetupContent`.

### Data per item: `InstancePerEntity`

By default there is one object of your `GlobalItem` for the whole game, so it
cannot have instance fields: Bunny Loader refuses to load one that does, as
tModLoader does. For data per item, ask for an instance per item:

```csharp
public class KillCounter : GlobalItem
{
    public int Kills;

    public override bool InstancePerEntity => true;
}

// anywhere:
if (item.TryGetGlobalItem(out KillCounter counter)) counter.Kills++;
```

| Call | Gives |
|---|---|
| `item.GetGlobalItem<KillCounter>()` | this item's instance; throws if the global does not apply to it |
| `item.TryGetGlobalItem(out KillCounter counter)` | `true` and the instance, or `false` |
| `item.EntityGlobals` | every global that applies to the item |

A global without `InstancePerEntity` gives back the same object for every
item. `Item.Clone()` gives the copy a `Clone(from, to)` of each instance
(shallow, as `MemberwiseClone`; override it to copy lists). A global's data is
not saved or sent in multiplayer yet.

Today a `GlobalItem` has `SetDefaults` and the use hooks above; the others
(combat, tooltips, drawing...) come with the rest of the item hooks (phase 1 of
the plan).

## The player: `ModPlayer`

```csharp
public class ExampleLuckPlayer : ModPlayer
{
    public override void ModifyLuck(ref float luck)
    {
        if (Main.hardMode) luck += 0.5f;
    }
}
```

Each `ModPlayer` class gets one instance per player, made the first time
something asks for it (`Initialize` runs then), with `Player` set to that
player. From anywhere: `player.GetModPlayer<ExampleLuckPlayer>()`,
`player.TryGetModPlayer(out ExampleLuckPlayer luck)` or `player.ModPlayers`.
Fields are per player, so keep the state there and reset what an effect
turns on in `ResetEffects`.

The hooks so far, with tModLoader's signatures, each installed only when a
loaded `ModPlayer` overrides it:

| Hook | When |
| --- | --- |
| `ResetEffects`, `ModifyMaxStats(out health, out mana)` | after the game's `Player.ResetEffects`; the max stats are tModLoader's `StatModifier`s applied to `statLifeMax`/`statManaMax` and added to `statLifeMax2`/`statManaMax2` (the saved base is not touched) |
| `PreUpdate`, `PostUpdate` | around `Player.Update` |
| `PreUpdateBuffs`, `PostUpdateBuffs` | around `Player.UpdateBuffs` |
| `UpdateEquips`, `PostUpdateEquips` | after `Player.UpdateEquips`; after the armor sets (`UpdateArmorSets`) |
| `UpdateBadLifeRegen`, `UpdateLifeRegen` | before and after `Player.UpdateLifeRegen` |
| `PreModifyLuck(ref luck)`, `ModifyLuck(ref luck)` | around `Player.RecalculateLuck`; `false` skips the game's luck |
| `FrameEffects` | after the drawn slots are set (`PlayerFrame`) |
| `UpdateDead` | after `Player.UpdateDead` |
| `PreKill`, `Kill` | around `Player.KillMe` (not in god mode); `false` keeps the player alive. `playSound` and `genDust` are not used yet |
| `OnEnterWorld`, `OnRespawn` | after `Player.Hooks.EnterWorld`; after `Player.Spawn` when the player was dead, as in tModLoader |
| `CanUseItem(item)` | before the item's own `CanUseItem` |
| `ResetInfoAccessories` | after `ResetEffects`, and after `Player.RefreshInfoAccs` |
| `PreUpdateMovement` | once per update, before the first of the game's collision steps (wet, dry, sloping, borders), not when dead |
| `PostUpdateMiscEffects` | after `Player.CapAttackSpeeds` |
| `PostUpdateRunSpeeds` | before `Player.HorizontalMovement` |
| `NaturalLifeRegen(ref float regen)` | inside `Player.UpdateLifeRegen`, on the natural regen value (a native hook in the middle of the method) |
| `UpdateDyes`, `UpdateVisibleAccessories`, `UpdateVisibleVanityAccessories` | after `Player.UpdateDyes`; after `Player.UpdateVisibleAccessories` (the phone does both kinds in one method) |
| `UpdateAutopause` | after `Main.DoUpdate_WhilePaused`, for the local player |
| `ProcessTriggers(TriggersSet triggersSet)` | after `TriggersSet.CopyInto` for the local player |
| `ArmorSetBonusActivated`, `ArmorSetBonusHeld(int holdTime)` | after `Player.KeyDoubleTap`/`KeyHoldDown` on the set bonus key (down, or up with `Main.ReversedUpDownArmorSetBonuses`) |
| `OnEquipmentLoadoutSwitched(int oldLoadoutIndex, int loadoutIndex)` | after `Player.TrySwitchingLoadout` changed the loadout |
| `SaveData`, `LoadData`, `PreSavePlayer`, `PostSavePlayer`, `PreSaveCustomData` | the character's `<file>.plr.bl.json`, under `<mod uid>/<class>` (shared with the JS mods' `ModPlayer` data) |

The item hooks run inside the same game points as the `ModItem` ones, in
tModLoader's `CombinedHooks` order: the item and its globals first, then the
player, except `CanShoot`, `Shoot`, `CanConsumeAmmo`, `OnConsumeAmmo`,
`ApplyPotionDelay`, `CanUseItem` and `CanAutoReuseItem`, where the player
comes first. A `ModPlayer` hook covers every item, the game's included.

| Hook | When |
| --- | --- |
| `PreItemCheck`, `PostItemCheck` | around `Player.ItemCheck`; `false` skips the game's item use for the frame |
| `ModifyWeaponDamage`, `ModifyWeaponCrit`, `ModifyWeaponKnockback`, `ModifyItemScale`, `MeleeEffects` | as the item's |
| `UseTimeMultiplier`, `UseAnimationMultiplier`, `UseSpeedMultiplier`, `CanAutoReuseItem` | multiplied with the item's |
| `ModifyManaCost(item, ref reduce, ref mult)`, `OnMissingMana`, `OnConsumeMana` | as the item's |
| `GetHealLife`, `GetHealMana`, `ApplyPotionDelay` | as the item's |
| `CanShoot`, `ModifyShootStats`, `Shoot`, `CanConsumeAmmo`, `OnConsumeAmmo` | as the item's |
| `CanHitNPC(target)`, `CanHitNPCWithItem`, `CanMeleeAttackCollideWithNPC` | an item's melee hit; `CanHitNPC` `false` refuses every hit of this player |
| `ModifyHitNPC`, `ModifyHitNPCWithItem`, `OnHitNPC`, `OnHitNPCWithItem` | an item's melee hit, after the item's hooks |
| `OnHitAnything(x, y, victim)` | after the game's `Player.OnHit` |

### `ModPlayer` in multiplayer

As in tModLoader, `ExampleStatIncreasePlayer`:

```csharp
public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
{
    ModPacket packet = Mod.GetPacket();
    packet.Write((byte)ExampleMod.MessageType.ExampleStatIncreasePlayerSync);
    packet.Write((byte)Player.whoAmI);
    packet.Write((byte)exampleLifeFruits);
    packet.Write((byte)exampleManaCrystals);
    packet.Send(toWho, fromWho);
}

public override void CopyClientState(ModPlayer targetCopy) =>
    ((ExampleStatIncreasePlayer)targetCopy).exampleLifeFruits = exampleLifeFruits;

public override void SendClientChanges(ModPlayer clientPlayer)
{
    if (exampleLifeFruits != ((ExampleStatIncreasePlayer)clientPlayer).exampleLifeFruits)
        SyncPlayer(toWho: -1, fromWho: Main.myPlayer, newPlayer: false);
}
```

| Hook | When |
| --- | --- |
| `SyncPlayer(toWho, fromWho, newPlayer)` | after the game's `NetMessage.SyncOnePlayer` (`newPlayer` the first time for that slot), and on the client right after it enters the world (`toWho -1`, `newPlayer true`) |
| `CopyClientState(targetCopy)` | when the game copies the local player (`Player.clientClone`) |
| `SendClientChanges(clientPlayer)` | every client update, with the copy from the last one |
| `PlayerConnect()`, `PlayerDisconnect()` | on the server when a player joins or leaves (also when the slot just goes empty) |

The phone's host plays in the same game: its `Main.netMode` is 3, so it is
both a client (`(Main.netMode & 1) != 0`, it runs `SendClientChanges`) and
the server (`(Main.netMode & NetmodeID.Server) != 0`). Code from
tModLoader that tests `Main.netMode == NetmodeID.Server` to pass a packet on
to the other clients has to use the `&` form, as the ExampleMod's
`HandlePacket` does.

### Getting hurt: `Player.HurtModifiers` and `HurtInfo`

```csharp
public class ExampleImmunityPlayer : ModPlayer
{
    public bool HasExampleImmunityAcc;

    public override void ResetEffects() => HasExampleImmunityAcc = false;

    public override void PostHurt(Player.HurtInfo info)
    {
        if (HasExampleImmunityAcc && !info.PvP) Player.AddImmuneTime(info.CooldownCounter, 60);
    }
}
```

The hurt hooks run around the game's `Player.Hurt`, so every hit is seen:
an NPC's, a projectile's (its index is in the `PlayerDeathReason`), a fall,
lava, a debuff.

| Hook | When |
| --- | --- |
| `ImmuneTo(damageSource, cooldownCounter, dodgeable)` | first; `true` skips the hit |
| `CanBeHitByNPC(npc, ref cooldownSlot)`, `CanBeHitByProjectile(proj)` | when the hit comes from an NPC or a projectile; `false` skips it |
| `ModifyHitByNPC`, `ModifyHitByProjectile`, then `ModifyHurt(ref modifiers)` | on tModLoader's `Player.HurtModifiers` |
| `FreeDodge(info)`, `ConsumableDodge(info)` | for a dodgeable hit on the local player; `true` skips it |
| `OnHitByNPC`, `OnHitByProjectile`, `OnHurt(info)` | after the game applied the hit, with the damage it took |
| `PostHurt(info)` | after that, if the player is still alive |

`HurtModifiers` does tModLoader's math: `SourceDamage` and
`IncomingDamageMultiplier` act before the defense, `ArmorPenetration`,
`ScalingArmorPenetration`, `FinalDamage` and `SetMaxDamage` after it, and
`Cancel()` skips the hit. The game still does the rest of its `Hurt` (the
crit of the phone's `Hurt`, endurance, the beetle and paladin's shield
reductions), and `HurtInfo.Damage` in `OnHurt` is what it took. Not used
yet: `Knockback`, `KnockbackImmunityEffectiveness`, `HitDirectionOverride`
only changes the direction passed to the game, and `DisableDust` and
`DisableSound` have no effect.

### PvP

| Hook | Where it runs |
| --- | --- |
| `ModItem.CanHitPvp(player, target)`, `GlobalItem.CanHitPvp`, `ModPlayer.CanHitPvp(item, target)` | on the attacker, for each player its melee swing touches; `false` skips that player |
| `ModItem.ModifyHitPvp(player, target, ref modifiers)`, `GlobalItem.ModifyHitPvp` | on the victim's own game, before its `ModifyHurt` |
| `ModItem.OnHitPvp(player, target, hurtInfo)`, `GlobalItem.OnHitPvp` | on the victim, after the hit, with the damage it took |
| `ModPlayer.CanHitPvpWithProj(proj, target)` | on the game that applies a PvP projectile hit, for the projectile's owner |

The weapon is the one in the attacker's hand: the game's death reason only
carries its type and prefix, so the Bunny Loader sends the attacker's item
data (its `NetSend`, or its `SaveData` without one, and the globals'
`NetSend`) right before the game's hurt message, and the victim's
`ModifyHitPvp`/`OnHitPvp` see a copy with that data. The attacker's own copy
of the victim takes the game's damage, as in the base game.

### In the world: pickups, slots, shops, the nurse, critters

| Hook | When |
| --- | --- |
| `OnPickup(WorldItem item)` | before `Player.PickupItem`; `false` destroys the item on the ground (`TurnToAirAndSync`), as in tModLoader |
| `CanBeTeleportedTo(teleportPosition, context)` | before `Player.Teleport` (`"TeleportRod"` for style 1, `"TeleportationPotion"` for 2, `"Teleport"` for the rest) and `UnityTeleport` (`"Wormhole"`); `false` keeps the player in place |
| `HoverSlot(inventory, context, slot)` | in `ItemSlot.OverrideHover` for the local player; `true` skips the game's cursor choice (set `Main.cursorOverride` with `CursorOverrideID`) |
| `ShiftClickSlot(inventory, context, slot)` | in `ItemSlot.OverrideLeftClick` while `ItemSlot.ShiftInUse`; `true` skips the game's click |
| `CanBuyItem`, `PostBuyItem(vendor, shopInventory, item)` | a purchase in `ItemSlot.HandleShopSlot`; `false` blocks it; `item` is the one now on the cursor |
| `CanSellItem`, `PostSellItem(vendor, shopInventory, item)` | around `Player.SellItem` while talking to a vendor |
| `ModifyNurseHeal(nurse, ref health, ref removeDebuffs, ref chatText)` | before the nurse's price and heal; `false` shows `chatText` and heals nothing |
| `ModifyNursePrice(nurse, health, removeDebuffs, ref price)` | after `Main.GetNurseHealCost` |
| `PostNurseHeal(nurse, health, removeDebuffs, price)` | after a paid heal |
| `CanCatchNPC(target, item)`, `OnCatchNPC(npc, item, failed)` | a bug net swing (`ItemCheck_CatchCritters`) or `NPC.CatchNPC`; `true` catches, `false` refuses; `failed` is `true` when the critter hurt the player instead |
| `AddStartingItems(mediumCoreDeath)`, `ModifyStartingInventory(itemsByMod, mediumCoreDeath)` | a new character (`GUIPlayerCreateMenu.SetupStartingItems`) and a mediumcore death (`Player.DropItems`); the game's items are under `"Terraria"`, each mod's under its `Name`; what does not fit in the 50 slots goes to `GetOrDropItem` |

The arrays are copies: changing `inventory[slot]` to another `Item` puts it
back in the game's array after the hook. `new Item(type, stack, prefix)` makes
a new item, as in tModLoader.

```csharp
public class ExampleInventoryPlayer : ModPlayer
{
    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    {
        if (mediumCoreDeath)
            return [new Item(ItemID.HealingPotion)];
        return [new Item(ModContent.ItemType<ExampleItem>()), new Item(ItemID.GoldOre, 256)];
    }

    public override void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath)
    {
        itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.IronAxe);
    }
}
```

On the phone, `ItemSlot.ShiftInUse` does not read `ItemSlot.ShiftForcedOn`.

### Fishing

| Hook | When |
| --- | --- |
| `GetFishingLevel(fishingRod, bait, ref fishingLevel)` | after `Player.GetFishingConditions`; `fishingLevel` is the level multiplier (`LevelMultipliers`), as in tModLoader, and `FinalFishingLevel` is worked out again from it |
| `ModifyFishingAttempt(ref attempt)` | before `Projectile.FishingCheck_ProbeForQuestFish`, with the drop levels rolled; `rolledItemDrop` and `rolledEnemySpawn` are put back |
| `CatchFish(attempt, ref itemDrop, ref npcSpawn, ref sonar, ref sonarPosition)` | after `FishingCheck_RollItemDrop`; a `sonar.Text` shows with the Sonar Potion |
| `ModifyCaughtFish(fish)` | in `AI_061_FishingBobber_GiveItemToPlayer`, before the fish goes to the player |
| `CanConsumeBait(bait)` | when the bobber uses the bait; `false` keeps it, `true` always uses it, `null` lets the game roll |
| `AnglerQuestReward(rareMultiplier, rewardItems)` | after `Player.GetAnglerReward`, with the game's rewards in the list; the list is what the player gets |
| `GetDyeTraderReward(rewardPool)` | in `Player.GetDyeTraderReward`, on the pool the dye comes from |

`PlayerFishingConditions` on the phone has the types (`PoleItemType`,
`BaitItemType`), not tModLoader's `Pole`/`Bait` items.

```csharp
public class ExampleFishingPlayer : ModPlayer
{
    public bool hasExampleCrateBuff;

    public override void ResetEffects() => hasExampleCrateBuff = false;

    public override void ModifyFishingAttempt(ref FishingAttempt attempt)
    {
        if (hasExampleCrateBuff && !attempt.crate && Main.rand.Next(100) < 10)
            attempt.crate = true;
    }
}
```

### Extra jumps: `ExtraJump`

An `ExtraJump` is a double jump: the game's ones are `ExtraJump.CloudInABottle`,
`SandstormInABottle`, `BlizzardInABottle`, `FartInAJar`, `TsunamiInABottle`,
`UnicornMount`, `SantankMount`, `GoatMount`, `BasiliskMount` and `Flipper`. A
mod adds its own as in tModLoader, and turns it on each frame with
`player.GetJumpState<T>().Enable()`:

```csharp
public class SimpleExtraJump : ExtraJump
{
    public override Position GetDefaultPosition() => new After(BlizzardInABottle);

    public override float GetDurationMultiplier(Player player) => 2.25f;

    public override void UpdateHorizontalSpeeds(Player player)
    {
        player.runAcceleration *= 1.75f;
        player.maxRunSpeed *= 2f;
    }
}

public override void UpdateAccessory(Player player, bool hideVisual) =>
    player.GetJumpState<SimpleExtraJump>().Enable();
```

`GetDefaultPosition` puts it before or after one of the game's jumps, and
`GetModdedConstraints` orders it among the mods' jumps there
(`ExtraJumpLoader.OrderedJumps`). `GetJumpState` of a game jump reads and
writes the game's own fields (`hasJumpOption_*`, `canJumpAgain_*`,
`isPerformingJump_*`), so `Enable()`, `Disable()` and `Available` work on them
too. Also there: `player.ExtraJumps`, `blockExtraJumps`, `AnyExtraJumpUsable()`,
`StopExtraJumpInProgress()`, `ConsumeAllExtraJumps()`.

| Hook | When |
| --- | --- |
| `ModifyExtraJumpDurationMultiplier(jump, ref duration)` | when a jump starts; the jump time is `Player.jumpHeight * duration`, for the game's jumps too |
| `CanStartExtraJump(jump)` | before a jump starts in the air; `false` skips it and leaves it available |
| `OnExtraJumpStarted(jump, ref playSound)` | after it starts; `playSound = false` silences it (the game's jump sounds as well) |
| `OnExtraJumpEnded(jump)` | when it stops (`CancelAllJumpVisualEffects`, or when it is disabled mid-jump) |
| `OnExtraJumpRefreshed(jump)` | when the jumps come back (`RefreshDoubleJumps`, on the ground) |
| `ExtraJumpVisuals(jump)`, `CanShowExtraJumpVisuals(jump)` | in `DoubleJumpVisuals`; `false` hides the jump's effects |
| `OnExtraJumpCleared(jump)` | when an available jump is taken away (unequipped in the air, a mount that blocks jumps, `ConsumeAllExtraJumps`) |

### Drawing the player: `PlayerDrawLayer`

The player is drawn layer by layer, as in tModLoader: the game's layers are
`PlayerDrawLayers.Wings`, `Head`, `Torso`, `HeldItem` and the rest, and a mod
adds its own:

```csharp
public class ExamplePlayerDrawLayer : PlayerDrawLayer
{
    public override bool IsHeadLayer => true;

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
        drawInfo.drawPlayer.HeldItem.type == ModContent.ItemType<ExampleItem>();

    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Head);

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        var texture = TextureAssets.Item[ModContent.ItemType<ExampleItem>()];
        var position = drawInfo.Center + new Vector2(0f, -20f) - Main.screenPosition;
        drawInfo.DrawDataCache.Add(new DrawData(texture.Value, position, null, Color.White, 0f, texture.Size() * 0.5f, 1f, SpriteEffects.None, 0));
    }
}
```

`GetDefaultPosition` takes `Between(layer1, layer2)` (either may be `null`),
`BeforeParent`/`AfterParent` (drawn with the parent, and hidden with it) or
`Multiple`. The order is set once after loading (`PlayerDrawLayerLoader.DrawOrder`).
`drawInfo.DrawDataCache` works as tModLoader's list (`Add`, `Insert`,
`RemoveAt`, `RemoveAll`, `FindIndex`, `Count`, the indexer, `foreach`) over the
phone's array and count, and `DrawData` has tModLoader's constructors (a `null`
source rectangle is the whole texture). `TextureAssets.Item[type]` is an
`Asset<Texture2D>` and loads the game's texture when it is not loaded yet.

The phone draws the same layers as the PC 1.4.5, which tModLoader (1.4.4) does
not all have: there is `PlayerDrawLayers.ArmorBackCoat` (between `BalloonAcc`
and `Skin`) and no `FinchNest`, and `HeldItem` behind the front arm and
`FrontAccFront` over the arm keep the game's torso offset.

| Hook | When |
| --- | --- |
| `DrawEffects(drawInfo, ref r, ref g, ref b, ref a, ref fullBright)` | after `PlayerDrawSet.BoringSetup`; the player's colors are multiplied, and `fullBright` ignores the light |
| `ModifyDrawInfo(ref drawInfo)` | right after it |
| `ModifyDrawLayerOrdering(positions)` | once, when the order is built |
| `HideDrawLayers(drawInfo)` | every draw, before the layers (call `layer.Hide()`) |
| `TransformDrawData(ref drawInfo)` | before `DrawPlayer_RenderAllLayers`, with every layer's `DrawData` |
| `DrawPlayer(camera)` | after `LegacyPlayerRenderer.PrepareDrawForFrame`, once per player per frame |
| `ModifyScreenPosition()` | after `Main.DoDraw_UpdateCameraPosition`, for the local player |
| `ModifyZoom(ref zoom)` | when the game sets `Main.GameViewMatrix.Zoom` |

### Extra crafting materials

`AddMaterialsForCrafting(out ItemConsumedCallback itemConsumedCallback)`
returns items the player can craft with as if they were in a nearby chest; the
callback runs for each of them that a craft used, with its index in what the
hook returned:

```csharp
public class BagPlayer : ModPlayer
{
    public Item[] Bag = { new Item(ItemID.Ruby, 5) };

    public override IEnumerable<Item> AddMaterialsForCrafting(out ItemConsumedCallback itemConsumedCallback)
    {
        itemConsumedCallback = (item, index) => Main.NewText($"slot {index}: {item.stack} left");
        return Bag;
    }
}
```

The items stay the mod's: when a craft uses one up, the same `Item` turns to
air instead of being replaced. The rest of tModLoader's `ModPlayer` (the
projectile hits) comes with the projectiles.

## Buffs: `ModBuff` and `GlobalBuff`

```csharp
public class ExampleLifeRegenDebuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = true;
        Main.buffNoSave[Type] = true;
        BuffID.Sets.BuffTimeIsExtendedWithGameDifficulty[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex) =>
        player.GetModPlayer<ExampleLifeRegenDebuffPlayer>().lifeRegenDebuff = true;
}
```

A `ModBuff` gets a type after the game's (`BuffID.Count` is 401):
`ModContent.BuffType<T>()`, `BuffLoader.GetBuff(type)`. The icon is the
class's texture (`ExampleLifeRegenDebuff.png`, 32x32), the name and
description come from `Mods.<Mod>.Buffs.<Name>.DisplayName`/`Description`.
`SetStaticDefaults` runs when the game's buff tables (`Main.debuff`,
`Main.buffNoSave`, `BuffID.Sets`...) have the type; give it to a player with
`player.AddBuff(type, time)` or to an item with `Item.buffType`. The
Bunny Loader also patches the places where the phone's game had the buff
count compiled in (the buff bar, the nurse, the network) and saves the mod
buffs of a character by name.

| Hook (`ModBuff`; `GlobalBuff` has the type first, for every buff) | When |
| --- | --- |
| `Update(Player player, ref int buffIndex)` | each frame, after the game's `Player.UpdateBuffs` |
| `Update(NPC npc, ref int buffIndex)` | each frame, after the game's `NPC.UpdateNPC_BuffSetFlags` |
| `ReApply(Player player, int time, int buffIndex)`, `ReApply(NPC ...)` | when the buff is given again; `true` keeps the game from renewing the time |
| `ModifyBuffText(ref string buffName, ref string tip, ref int rare)` | for the name (`Lang.GetBuffName`) and the tip (`Main.GetBuffTooltip`) the buff bar shows; `rare` has no effect on the phone |
| `RightClick(int buffIndex)` | when the player taps the buff to cancel it; `false` keeps it |

`GlobalBuff.Update` runs for each active buff, the game's included, after
the `ModBuff`'s; a hook that removes the buff lowers `buffIndex`, as in
tModLoader.

The icon in the buff bar: `PreDraw(spriteBatch, buffIndex, ref BuffDrawParams
drawParams)` (`GlobalBuff`s first, then the `ModBuff`) and `PostDraw`. The
params start as the game draws it: `Position` is the icon's top left corner,
`TextPosition` the time text's, `DrawColor` the bar's fade
(`Main.buffAlpha`). A change to `Texture`, `Position`, `SourceRectangle` or
`DrawColor` goes to the game's draw of that icon, a change to `TextPosition`
moves its time text, and `false` hides the icon (the text stays, as in
tModLoader). The phone draws the bar at a 0.8 scale.

The buff's tip (tapping the icon shows it through `Main.MouseText`):
`GlobalBuff.CustomBuffTipSize(buffTip, sizes)` adds the sizes of what a mod
draws under it, so the game moves the box back on screen, and
`DrawCustomBuffTip(buffTip, spriteBatch, originX, originY)` draws it, with the
origin just under the tip text. `FontAssets.MouseText`, `ItemStack` and
`DeathText` are `Asset<SpriteFont>`s, as in tModLoader:

```csharp
public override void DrawCustomBuffTip(string buffTip, SpriteBatch spriteBatch, int originX, int originY) =>
    spriteBatch.DrawString(FontAssets.MouseText.Value, "More text", new Vector2(originX, originY), Color.Yellow);
```

## Projectiles: `ModProjectile`

```csharp
public class ExampleBullet : ModProjectile
{
    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.aiStyle = ProjAIStyleID.Arrow;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 5;
        Projectile.timeLeft = 600;
        AIType = ProjectileID.Bullet;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Projectile.penetrate--;
        if (Projectile.penetrate <= 0) Projectile.Kill();
        else Projectile.velocity = -oldVelocity;
        return false;
    }
}
```

The texture is `Texture` (`ExampleBullet.png`, frames stacked vertically:
`Main.projFrames[Type]` in `SetStaticDefaults`), the name
`Mods.<Mod>.Projectiles.<Name>.DisplayName`, and the type comes after the game's
1136 (`ModContent.ProjectileType<T>()`, `ProjectileLoader.GetProjectile`). An
item shoots it with `Item.shoot`, and `Projectile.NewProjectile` spawns it.
`Projectile.ModProjectile` is the mod's instance for that projectile.

| Hook | When |
| --- | --- |
| `SetStaticDefaults`, `SetDefaults` | after the game has the type; for each new projectile (`Projectile.SetDefaults`) |
| `OnSpawn(source)` | after `Projectile.NewProjectile`, with the spawn source |
| `PreAI`, `AI`, `PostAI` | around the game's AI (`aiStyle`); `AIType` runs the AI of that game projectile; `PreAI` `false` skips the game's AI and `AI` |
| `OnTileCollide(oldVelocity)` | when the projectile hits a tile while moving; `false` keeps it alive where the game would kill it |
| `PreKill(timeLeft)`, `OnKill(timeLeft)` | before `Projectile.Kill`; `PreKill` `false` removes it without the game's death effects |
| `ShouldUpdatePosition` | `false` keeps it where it is this update |
| `Colliding(projHitbox, targetHitbox)` | a custom hit test against NPCs and players |
| `CanDamage`, `MinionContactDamage`, `ModifyDamageHitbox` | in `Projectile.Damage` |
| `GetAlpha(lightColor)` | the color it is drawn with |
| `PreDraw(player, ref lightColor)`, `PostDraw(player, lightColor)` | around the game's draw (`Main.DrawProjDirect`); `Projectile.GetDefaultDrawData(player, lightColor)` gives the draw the game would make, for afterimages |

`Projectile.oldPos` (with `ProjectileID.Sets.TrailCacheLength[Type]`) reads by
index as in tModLoader. `Projectile.CloneDefaults(type)` copies another
projectile's defaults and keeps the type, the `ModProjectile` and the globals.
`TileCollideStyle`, `CutTiles`, `PreDrawExtras` and
`SendExtraAI`/`ReceiveExtraAI` come next.

### Grappling hooks

A hook is a projectile with `aiStyle = ProjAIStyleID.Hook` (7), shot by an item
whose `shoot` is that type; `Projectile.CloneDefaults(ProjectileID.GemHookAmethyst)`
is the usual start.

| Hook | When |
| --- | --- |
| `CanUseGrapple(player)` | before the hook is shot; `false` stops it, `true` shoots it even past the game's limit of hooks in flight, `null` leaves it to the game |
| `UseGrapple(player, ref type)` | right before the projectile is created; change `type` to shoot another hook (the Dual Hook does this) |
| `GrappleRange()` | how far the hook flies before it comes back (300 by default); a hook with `AIType` keeps that game hook's range |
| `NumGrappleHooks(player, ref numHooks)` | how many hooks may be latched at once; the oldest goes when a new one latches past it |
| `GrappleRetreatSpeed(player, ref speed)` | the speed back to the player when it misses (11 by default) |
| `GrapplePullSpeed(player, ref speed)` | how fast the player is pulled (11 by default); the first latched hook decides |
| `GrappleTargetPoint(player, ref grappleX, ref grappleY)` | the point the player is pulled to; starts at the sum of the latched hooks' centers so far |
| `GrappleCanLatchOnTo(player, x, y)` | whether the hook may latch onto the tile at `(x, y)`; `null` uses the game's rule (solid tiles) |

`GlobalProjectile` has the same hooks with the `Projectile` first, except
`CanUseGrapple(type, player)` and `UseGrapple(player, ref type)`, which take the
hook's type (`AppliesToEntity` is asked on a sample projectile of that type), and
`GrappleRange`, which is only on `ModProjectile`. As in tModLoader, a global's
`false` in `GrappleCanLatchOnTo` wins over everything else.

### Hits

| Hook | When |
| --- | --- |
| `CanHitNPC(target)` | before the game checks whether the projectile may hit this NPC; `false` never hits it, `true` hits it even where the game would skip it (critters), `null` leaves it to the game |
| `ModifyHitNPC(target, ref modifiers)`, `OnHitNPC(target, hit, damageDone)` | around the strike, as for items: `modifiers` start with the projectile's `DamageType` and the NPC's defense |
| `CanHitPlayer(target)`, `CanHitPvp(target)` | before a hostile projectile (or a PvP one) hurts a player; `false` stops it |
| `ModifyHitPlayer(target, ref modifiers)`, `OnHitPlayer(target, info)` | around `Player.Hurt` when the projectile is the cause |

The order is tModLoader's: for a hit on an NPC, the owner's `ModPlayer.CanHitNPC`
and `CanHitNPCWithProj`, then the projectile's `CanHitNPC` (the first answer that
is not `null` wins, a `false` before it stops); then `ModifyHitNPC` on the
projectile, its `GlobalProjectile`s, the owner's `ModPlayer.ModifyHitNPC` and
`ModifyHitNPCWithProj`, and `OnHitNPC` in the same order. A `true` from
`CanHitNPC` does not make a friendly NPC hittable.

The strike is found without running C# for every projectile: a native marker on
`Projectile.Damage_PVE` says which projectile is dealing damage, and only the
strike itself calls the mod. `CanHitNPC` is the exception: the game asks it for
every NPC near every friendly projectile, so override it only when needed.

## Every projectile: `GlobalProjectile`

```csharp
public class ExampleProjectileModifications : GlobalProjectile
{
    public override bool InstancePerEntity => true;
    public bool applyBuffOnHit;

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (applyBuffOnHit) target.AddBuff(BuffID.OnFire, 50);
    }
}
```

A `GlobalProjectile` has the same hooks as `ModProjectile`, with the
`Projectile` first, plus `SetDefaults(Projectile entity)`. They run for the
game's projectiles and the mods'. `AppliesToEntity(entity, lateInstantiation)`
picks the projectiles (first before the projectile's `SetDefaults`, then after
it), and with `InstancePerEntity` each projectile gets its own instance:
`projectile.GetGlobalProjectile<T>()`, `TryGetGlobalProjectile<T>(out T)` and
`EntityGlobals` read it. The order is tModLoader's: in `PreAI`, `OnTileCollide`,
`PreKill`, `PreDraw`, `CanHitNPC`, `CanHitPlayer`, `CanHitPvp`, `Colliding`,
`GetAlpha` and `CanDamage` the globals answer before the `ModProjectile`; in
`AI`, `PostAI`, `OnKill`, `OnSpawn`, `ModifyHitNPC`, `OnHitNPC`, `ModifyHitPlayer`,
`OnHitPlayer` and `PostDraw` the `ModProjectile` runs first. The loader's
`ProjectileLoader.PreAI`, `OnHitNPC` and the others are public, as in
tModLoader.

`projectile.TryGetOwner(out Player player)` gives the owning player of a
player's projectile.

## Mounts: `ModMount`

A mount is set up as in tModLoader: `SetStaticDefaults` fills `MountData`
(speeds, frames, offsets, `buff`, `spawnDust`), and the textures come from
`Texture + "_Back"`, `"_Front"`, `"_BackGlow"` and the other
`MountTextureType`s, when the files exist. A buff mounts it:

```csharp
public class ExampleMountBuff : ModBuff
{
    public override void Update(Player player, ref int buffIndex)
    {
        player.mount.SetMount(ModContent.MountType<ExampleMount>(), player);
        player.buffTime[buffIndex] = 10;
    }
}
```

and an item with `Item.mountType = ModContent.MountType<ExampleMount>()` gives
the buff. The hooks: `SetMount(player, ref skipDust)`,
`Dismount(player, ref skipDust)`, `UpdateEffects`, `UpdateFrame` (`false` skips
the game's animation), `JumpHeight`, `JumpSpeed`, `UseAbility`, `AimAbility` and
`Draw` (`false` draws nothing; what the hook adds to `playerDrawData` is drawn
behind the mount). `player.mount._mountSpecificData` holds any C# object for
that mount until it dismounts. The types come after the game's (`MountID.Count`,
`MountLoader.GetMount`), and `MountID.Sets` grow with them.

`MountData.backTexture` and the other texture fields are `Asset<Texture2D>`, and
`MountData.playerYOffsets` is an `int[]` copied in and out of the game's array:
set it whole (`MountData.playerYOffsets = new[] { ... }`), as tModLoader's
examples do.

## Hairstyles: `ModHair`

```csharp
public class ExampleHair : ModHair
{
    public override bool AvailableDuringCharacterCreation => false;

    public override void SetStaticDefaults() => HairID.Sets.DrawBackHair[Type] = true;

    public override IEnumerable<Condition> GetUnlockConditions()
    {
        yield return Condition.TimeNight;
    }
}
```

The textures are `Texture` and `AltTexture` (`ExampleHair.png` and
`ExampleHair_Alt.png`, the one under a hat), set in `AutoStaticDefaults`
into `TextureAssets.PlayerHair`/`PlayerHairAlt` (now `Asset<Texture2D>`
arrays, as in tModLoader). The types come after the game's 228 (`HairID.Count`,
`HairLoader.Count`, `HairLoader.GetHair`). The hair shows at character
creation when `AvailableDuringCharacterCreation`, and at the Stylist when every
`GetUnlockConditions` condition is met. `HairID.Sets.DrawBackHair` decides the
back hair, the game's hairs included.

The game's player file keeps the hair as a number the game checks against its
228, so a mod hair is saved there as 0 and by name in the character's
`.plr.bl.json` (`bunny:hair`, the key the JS mods use). In multiplayer the
game's appearance message turns a mod hair into 0; the Bunny Loader sends the
real one right after it. Not used yet: `RandomizedCharacterCreationGender`.
JS and C# mod hairs at the same time would share type numbers.

## The life and mana bar: `ModResourceOverlay`

A `ModResourceOverlay` sees every heart and star the bar draws and can draw
over it, as in tModLoader:

| Hook | When |
| --- | --- |
| `PreDrawResource(context)` | before each piece of a heart or star (`GUIHearts.HeartDraw`, `GUIMana.StarDraw`); `false` skips the game's draw |
| `PostDrawResource(context)` | after it; `context.Draw()` draws `context.texture` with the context's position, source, color, rotation, origin, scale and effects |
| `PreDrawResourceDisplay(snapshot, displaySet, drawingLife, ref textColor, out drawText)` | before the whole life or mana display (`GUIHearts.Draw`, `GUIMana.Draw`); `false` skips it, `drawText = false` hides the life text |
| `PostDrawResourceDisplay(...)` | after it |

`context.texture` is the game's `Asset<Texture2D>`, so the comparisons of
tModLoader's examples work: `asset == TextureAssets.Heart`, or
`asset == Main.Assets.Request<Texture2D>("Images/UI/PlayerResourceSets/FancyClassic/Heart_Fill")`
(the phone's bar uses the same images as the PC's). `context.resourceNumber`
is the heart or star (0 is the first), `context.snapshot` has the player's
`Life`, `LifeMax`, `Mana`, `ManaMax` and `AmountOfLifeHearts`/`AmountOfManaStars`.
The phone has no hover text on the bar, so `DisplayHoverText` is not called,
and `drawText` only hides the life text.

## NPCs: `ModNPC` and `GlobalNPC`

A `ModNPC` registers a new NPC type after the game's types. Its texture and
display name follow the same conventions as items:

```csharp
using Terraria;
using Terraria.ModLoader;

namespace MyMod.Content.NPCs;

public class FloatingFriend : ModNPC
{
    public override void SetDefaults()
    {
        NPC.width = 24;
        NPC.height = 26;
        NPC.lifeMax = 120;
        NPC.friendly = true;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.aiStyle = -1;
    }
}
```

The texture is `content/Content/NPCs/FloatingFriend.png`, and the name is
`Mods.MyMod.NPCs.FloatingFriend.DisplayName` in the localization files.
`ModContent.NPCType<FloatingFriend>()` gives the type,
`NPCLoader.GetNPC(type)` and `ModContent.GetModNPC(type)` give the registered
template, and `ModContent.Find<ModNPC>("MyMod/FloatingFriend")` finds it by name.
`NPCLoader.NPCCount` includes every registered type, including JavaScript NPCs;
the game tables have that size by `SetStaticDefaults`.

The template has a real `NPC` in `SetStaticDefaults`, and its `NPC.ModNPC` is
the template itself. `AutoStaticDefaults` assigns `TextureAssets.Npc[Type]`
before that method. Set `Main.npcFrameCount[Type]` there for a sprite sheet;
`TextureAssets.Npc` exposes `Asset<Texture2D>` entries, including a setter.

Each `NPC.SetDefaults` creates a new mod instance. Read it through
`npc.ModNPC`; it stays the same until the object receives new defaults.
Constructor fields start fresh unless `CloneNewInstances` is true, in which
case `Clone(newEntity)` copies the template. `ModNPC.NPC` is a `ref` property,
so assignments to the pointer struct work as they do on tModLoader's class.
The instances use the same weak-handle storage as items and do not keep game
objects alive.

`NPC.CloneDefaults(type)` copies another type's defaults, including its mod
and globals, at Classic difficulty and scale 1. It restores the receiving
NPC's type, net ID, mod instance, globals and player interaction flags. The
outer defaults apply the requested difficulty once; `NPCSpawnParams` size
and difficulty overrides also work for mod NPCs. `SetDefaults(type)` and
`SetDefaultsKeepPlayerInteraction(type, spawnParams)` are available. The
game's `NPC.Clone()` also clones mod and per-entity global state; override
`Clone` to copy reference fields such as lists.

A `GlobalNPC` uses the same `InstancePerEntity`, `AppliesToEntity`, cloning
and instance-field validation as `GlobalItem`. Its hooks currently available
are `SetStaticDefaults` and `SetDefaults(NPC entity)`. For each defaults run:

1. Globals that apply with `lateInstantiation == false` are attached.
2. The `ModNPC.SetDefaults` runs, then those globals' `SetDefaults`.
3. The remaining globals are checked with `lateInstantiation == true` and
   attached, then their `SetDefaults` run.

Use `npc.GetGlobalNPC<T>()`, `GetGlobalNPC(baseInstance)`,
`TryGetGlobalNPC(out T value)`, `TryGetGlobalNPC(baseInstance, out T value)`
or `npc.EntityGlobals`. A global without `InstancePerEntity` is shared;
per-entity globals are reset even when the game reuses the same NPC object.
For vanilla NPCs these hooks run after the mobile game's own `SetDefaults`,
as in the JavaScript loader; its difficulty scaling has already run. For mod
NPCs the final difficulty scaling runs after all the defaults hooks.

`NPC.NewNPC(default, x, y, type)` uses this path too. The initial
`PartyZombie` port demonstrates registration, frame count, texture and base
stats. Its borrowed zombie AI/animation, sounds, natural spawn, loot,
combat callbacks, banners and bestiary/biome integration arrive with the
corresponding phase 4 steps. It uses the mobile fighter style number 3 until
`NPCAIStyleID` is ported. No C# AI, drawing or UpdateNPC hook is installed by
this registration step. Extra AI networking and custom saved NPC data are
also later steps.

## Finding content by name

The **full name** of a content class is `<mod>/<Name>`: the mod's internal
name (the name of its assembly) and the class name. Any mod can find any
other mod's content by it:

```csharp
ModItem rock = ModContent.Find<ModItem>("MyMod/ShinyRock");
if (ModContent.TryFind("OtherMod/Ruby", out ModItem ruby))
    Mod.Logger.Info($"OtherMod is here, and its ruby is item {ruby.Type}");

ModItem mine = Mod.Find<ModItem>("ShinyRock");   // in this mod
```

`Find` throws when there is no such content; `TryFind` returns `false`.

## Textures and files

Textures come as `Asset<Texture2D>` (`using ReLogic.Content;` and
`using Microsoft.Xna.Framework.Graphics;`), as in tModLoader:

```csharp
// A PNG of this mod, by its path inside content/ (no extension).
Asset<Texture2D> glow = Mod.Assets.Request<Texture2D>("Content/Items/ShinyRock_Glow");

// The same, from anywhere: the mod's name first.
Asset<Texture2D> same = ModContent.Request<Texture2D>("MyMod/Content/Items/ShinyRock_Glow");

// A game image.
Asset<Texture2D> pickaxe = ModContent.Request<Texture2D>("Terraria/Images/Item_1");
```

The same path gives back the same `Asset`. A missing PNG throws right at the
request; `Mod.Assets.HasAsset<Texture2D>(path)` and
`ModContent.HasAsset("MyMod/...")` ask first.

**The texture loads on the game thread only.** The game's engine (Unity)
kills the app when a texture is created anywhere else, and mods load on
another thread. So a request made in `Load` stays `NotLoaded`, and the
texture loads the first time something reads `Value` (or calls `Wait()`) on
the game thread: in `SetStaticDefaults`, `PostSetupContent`, a hook or a draw
call. Reading `Value` in `Load` throws an `InvalidOperationException` that
says so. Keep the `Asset` from `Load` in a field, and read `Value` later:

```csharp
public static Asset<Texture2D> Glow;

public override void Load() => Glow = Mod.Assets.Request<Texture2D>("Content/Items/ShinyRock_Glow");

// later, in a hook on the game thread:
Texture2D texture = Glow.Value;
int width = texture.Width;
```

For now only `Texture2D` can be requested (sounds, effects and fonts come
with their loaders), and a game image that the mobile version does not have
throws when it loads.

Any other file of the mod is read by its path inside `content/`, with the
extension:

| Call | Gives |
|---|---|
| `Mod.FileExists("Data/drops.json")` | `true` if the file is there |
| `Mod.GetFileBytes("Data/drops.json")` | its bytes |
| `Mod.GetFileStream("Data/drops.json")` | a `Stream` (dispose it) |
| `Mod.GetFileNames()` | every file of the mod, like `"Content/Items/ShinyRock.png"` |
| `ModContent.GetFileBytes("MyMod/Data/drops.json")` | the same, by the mod's name |

A path that leaves the mod's folder (`"../..."`) is never read.

## Other mods

### Finding one: `ModLoader`

```csharp
// In your Mod class.
public override void PostSetupContent()
{
    if (ModLoader.TryGetMod("wikithis", out Mod wikithis))
        wikithis.Call("AddModURL", this, "https://mymod.wiki.gg/wiki/{}");
}
```

| Call | Gives |
|---|---|
| `ModLoader.TryGetMod(name, out Mod mod)` | `true` and the mod, or `false` and `null` |
| `ModLoader.GetMod(name)` | the mod; throws if it is not there |
| `ModLoader.HasMod(name)` | is it loaded? |
| `ModLoader.Mods` | every C# mod that loaded, in load order |

The name can be the other mod's internal `Name` (its assembly, as in
tModLoader), its manifest `id` or its `uid`. If two mods answer to the same
`Name` or `id`, `TryGetMod` picks neither and the log says to ask by `uid`.
Only C# mods are found: a JS mod cannot be called from C# (nor the other way
around) yet.

C# mods load one at a time. In `Load`, a mod finds itself and the mods
loaded before it, which include all of its dependencies; from
`PostSetupContent` on, it finds every mod. Optional compatibility goes in
`PostSetupContent`.

Each `Mod` also has `DisplayName` and `Version`, from the manifest's `name`
and `version`.

### Offering something: `Call`

```csharp
public class Wikithis : Mod
{
    public override object? Call(params object?[] args)
    {
        switch (args[0] as string)
        {
            case "AddModURL":
                // ...
                return true;
        }
        return null;
    }
}
```

The first argument names the command, by convention. Values go through as
they are (it is a plain C# call), an unknown command returns `null`, and an
exception thrown inside `Call` reaches the caller.

### Depending on a mod

A mod yours cannot work without goes in the manifest's `dependencies`, by
`id` or `uid`, as for JS mods ([the JS guide](../mods/11-conversa-entre-mods.md#dependencies)):
it loads before yours, and without it yours does not load.

When the dependency is a C# mod, you can also use its **types** directly, as
with tModLoader's `modReferences`:

```csharp
using OtherMod;

int answer = OtherModApi.Answer;   // a public class of the other mod
```

- **A `.dll` mod:** reference the other mod's `.dll` (or project) in your
  `.csproj` with `<Private>false</Private>`, so it is not copied into your
  package; on the phone it is found in the other package.
- **A mod with sources:** the phone compiles it with the `.dll` of each C#
  dependency as a reference, and compiles again when one of them changes.
  The catalog and the PC tools do not have the other mod, so they cannot
  compile such a mod yet: ship it as a `.dll`, or let the phone compile it.

## Translations

Translations follow tModLoader: [hjson](https://hjson.github.io/) files
with keys like `Mods.MyMod.Items.ShinyRock.DisplayName`.

### The files

Any `.hjson` file of the mod with a culture in its name counts (`bin/` and
`obj/` aside). The part after the culture is a prefix put in front of every
key in the file:

| File | Keys inside start at |
|---|---|
| `Localization/en-US_Mods.MyMod.hjson` | `Mods.MyMod.` |
| `Localization/pt-BR_Mods.MyMod.hjson` | `Mods.MyMod.` |
| `Localization/en-US.hjson` | the root: write `Mods: { MyMod: { ... } }` |
| `Localization/en-US/Mods.MyMod.Items.hjson` | `Mods.MyMod.Items.` |

What hjson allows, and Bunny Loader reads: braces around the whole file are
optional; no quotes around names or around a value that takes the rest of its
line; `#`, `//` and `/* */` comments; commas optional; `"..."` and `'...'`
with `\n`, `\t`, `\"` escapes; multiline text between `'''` lines, with the
indentation of the opening `'''` removed:

```hjson
# Localization/en-US_Mods.MyMod.hjson
Greeting: Hello, {0}!
Items: {
	ShinyRock: {
		DisplayName: Shiny Rock
		Tooltip:
			'''
			It glows a little
			Right click to polish
			'''
	}
}
```

A value that takes the rest of its line ends only at the line's end: write
`{ Name: "x" }` with quotes when an object fits in one line.

The game's culture picks the text: the exact culture (`pt-BR`), then the
same language, then `en-US`, then any. Changing the language in the game
updates every text of the mods.

### In code

| Call | Gives |
|---|---|
| `Language.GetTextValue("Mods.MyMod.Greeting", "Bunny")` | `"Hello, Bunny!"` (the arguments fill `{0}`, `{1}`...) |
| `Language.GetText(key)` | the game's `LocalizedText` for the key |
| `Language.GetOrRegister(key, () => "default")` | the `LocalizedText`; with the default when no file has it |
| `Mod.GetLocalization("Greeting")` | `Mods.MyMod.Greeting` |
| `this.GetLocalization("Glow")` in a `ModItem` | `Mods.MyMod.Items.ShinyRock.Glow` |
| `this.GetLocalizedValue("Glow")` | its text |
| `text.Value`, `text.Format(args)` | the text, and the text with `{0}`... filled |

`using Terraria.Localization;` brings `Language` and `LocalizedText`, which
are the game's own.

A `ModItem`'s `DisplayName` and `Tooltip` are its `GetLocalization("DisplayName")`
and `GetLocalization("Tooltip")`. Without a translation, the name is the class
name split in words (`ShinyRock` → `Shiny Rock`) and the tooltip is empty. To
use a text of the game instead, override them:

```csharp
public override LocalizedText DisplayName => Language.GetText("ItemName.IronPickaxe");
```

**When the texts work:** `GetLocalization(...).Value` works from `Load` on.
`Language.GetTextValue` looks the key up in the game's own table, and the
mods' keys only enter it on the game thread: from `SetStaticDefaults` on.

The old format, `Localization/<culture>.json` with `ItemName` and
`ItemTooltip` sections, is still read (a key of a `.hjson` wins).

## Multiplayer: `ModPacket`

A mod talks to itself on the other side of the connection with packets, as
in tModLoader:

```csharp
// send (a client sends to the server; the server to one client or to all)
ModPacket packet = Mod.GetPacket();
packet.Write((byte)MyMessage.Heal);
packet.Write(Main.myPlayer);
packet.Write(50);
packet.Send();                 // the server: Send(toClient, ignoreClient)

// receive, in your Mod class
public override void HandlePacket(BinaryReader reader, int whoAmI)
{
    switch ((MyMessage)reader.ReadByte())
    {
        case MyMessage.Heal:
            int player = reader.ReadInt32();
            int amount = reader.ReadInt32();
            // ...
            break;
    }
}
```

- `ModPacket` is a `BinaryWriter`: `Write` takes numbers, `bool`, `string`,
  `byte[]`. `HandlePacket` gets a `BinaryReader` positioned at what you wrote:
  read in the same order.
- `whoAmI` is the sender: on the server, the client's player index; on a
  client, `256` (the server).
- `Send()` in single player throws. A packet holds at most 65,000 bytes.
- A packet for a mod that the other side does not have is dropped, with a
  line in the log.

**Joining a server.** When a client joins, it tells the server which C# mods
it has (name and version). A client whose C# mods are not the same as the
server's is refused, with a message that lists the server's mods, the ones
the client is missing and the ones only the client has. JS mods are
checked by the JS side, which can also offer to sync them; for C# mods there
is no offer yet (it comes with the server sync menu, phase 6 of the plan). A
client without the .NET runtime (no C# mod enabled) sends nothing and is not
checked.

Items do not send their own data yet (`ModItem.NetSend`/`NetReceive`): that
comes with the item hooks, in phase 1.

### An item's data on the network

The game sends an item when it changes hands: a player's slot (message 5),
an item on the ground (21 and 90), a chest slot (32). The data of a `ModItem`
goes with it:

```csharp
public class Charged : ModItem
{
    public int Charge;

    public override void NetSend(BinaryWriter writer) => writer.Write(Charge);

    public override void NetReceive(BinaryReader reader) => Charge = reader.ReadInt32();
}
```

`GlobalItem` has the same two, with the `Item` first (`InstancePerEntity`
for per-item fields), for any item. A `ModItem` without `NetSend` that has
`SaveData` sends what `SaveData` writes instead, and the other side runs
`LoadData` with it, so a saved field also reaches the server (which saves
the chests) and the other players. Write and read the same things, in the
same order. The data goes as a C# packet just before the game's message, and
is applied right after the game reads it; on a server, before the game
passes the message on to the other players.

## Logging from content

A content class has `Mod`, so it logs through `Mod.Logger`:

```csharp
public override void SetStaticDefaults()
{
    Mod.Logger.Info($"{Name} is item {Type}");
}
```

Next: back to the [overview](README.md).
