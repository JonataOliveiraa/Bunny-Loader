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

The texture is `content/Content/Items/ShinyRock.png`: the namespace plus the
class name, as in tModLoader, and the first part (the mod's name) may be
missing on disk. The name and tooltip come from
`content/Localization/<culture>.json`:

```json
{
  "ItemName": { "ShinyRock": "Shiny Rock" },
  "ItemTooltip": { "ShinyRock": "It glows a little" }
}
```

Nothing registers the class by hand: Bunny Loader finds it in your `.dll`.
`ModContent.ItemType<ShinyRock>()` is its item ID, and
`ModContent.GetInstance<ShinyRock>()` is the registered object. A complete
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
| 2. The game knows the new types | each item's `SetStaticDefaults()` | the game's |
| 3. All mods' content is in the game | `Mod.PostSetupContent()` | the game's |
| 4. | `Mod.AddRecipeGroups()` | the game's |
| 5. | `Mod.AddRecipes()`, then each item's `AddRecipes()` | the game's |
| 6. | `Mod.PostAddRecipes()` | the game's |

Each of steps 3 to 6 runs in **every** C# mod (in load order) before the next
one starts, as in tModLoader: in `AddRecipes` you can use any mod's items. An
exception in one mod's step goes to the log and the error panel, and the
other mods go on.

Two differences from tModLoader:

- `Unload()` exists (on `Mod`, `ModType` and `ILoadable`) but is **never
  called**: Bunny Loader does not unload mods while the game runs; changing
  mods restarts the game.
- There is no recipe API in C# yet, so `AddRecipes` has nothing to call. It
  runs at the right time, ready for when `Recipe` arrives.

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
Override `Clone(Item newEntity)` to copy it deeper.

For now the instance is not kept on the item afterwards (that comes with
`Item.ModItem`, see the plan): put data that has to last in the item's own
fields.

## Logging from content

A content class has `Mod`, so it logs through `Mod.Logger`:

```csharp
public override void SetStaticDefaults()
{
    Mod.Logger.Info($"{Name} is item {Type}");
}
```

Next: back to the [overview](README.md).
