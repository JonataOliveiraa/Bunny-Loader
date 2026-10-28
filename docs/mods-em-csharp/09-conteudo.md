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

## Logging from content

A content class has `Mod`, so it logs through `Mod.Logger`:

```csharp
public override void SetStaticDefaults()
{
    Mod.Logger.Info($"{Name} is item {Type}");
}
```

Next: back to the [overview](README.md).
