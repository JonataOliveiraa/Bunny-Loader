# 5. Bunny Loader × tModLoader

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · [3. `ref`, `out`, `Nullable`](03-ref-out-e-nullable.md) · [4. Values and objects](04-valores-e-objetos.md) · **5. Bunny Loader × tModLoader** · [6. Performance](06-desempenho.md) · [7. Migration to C#](07-migracao-para-csharp.md) · [8. Under the hood](08-por-baixo-dos-panos.md) · [9. Content classes](09-conteudo.md) · [API reference](referencia/README.md)

Bunny Loader's API is tModLoader's: same namespaces, same class names, same
member signatures. A tModLoader mod ports by copying its classes and fixing
what the mobile game really lacks. This page lists both sides.

## The same mod, both ways

"Every weapon hits twice as hard."

```csharp
using Terraria;
using Terraria.ModLoader;

public class DoubleDamage : GlobalItem
{
    public override void SetDefaults(Item item)
    {
        if (item.damage > 0) item.damage *= 2;
    }
}
```

This file compiles on both. So does the hook version:

```csharp
public class DoubleDamage : Mod
{
    public override void Load()
    {
        On_Item.SetDefaults += (orig, self, type, variant) =>
        {
            orig(self, type, variant);
            if (self.damage > 0) self.damage *= 2;
        };
    }
}
```

(`SetDefaults` on the phone takes an `ItemVariant`; on PC the hook has the PC
game's parameters. Hooks follow the game they run on.)

## What is the same

| | tModLoader | Bunny Loader |
|---|---|---|
| Content classes | `ModItem`, `ModNPC`, `ModProjectile`, `ModTile`, `ModWall`, `ModBuff`, `ModPlayer`, `ModSystem`, `ModBiome`, `ModConfig`, `ModMenu`... | the same ([guides 9-22](09-conteudo.md)) |
| Globals | `GlobalItem`, `GlobalNPC`, `GlobalProjectile`, `GlobalTile`... with `InstancePerEntity` | the same |
| Load order | `Load`, `SetStaticDefaults`, `PostSetupContent`, `AddRecipes`... | the same |
| Content lookup | `ModContent.ItemType<T>()`, `Find`, `Request<Texture2D>` | the same |
| Saving | `TagCompound`, `SaveData`/`LoadData` | the same |
| Networking | `ModPacket`, `HandlePacket`, `NetSend`/`NetReceive` | the same |
| Translations | `Localization/*.hjson`, `this.GetLocalization` | the same |
| Hooks | `On_Item.SetDefaults += (orig, self, ...) => ...` | the same, with `ref`/`out`, `+=`/`-=`, last added runs first |
| The game's names | `Player`, `Item.useTime`, `Main.player`, `ItemID.Minishark` | the same |
| XNA | `new Vector2(1, 2)`, operators | the same |

## What is different

| | tModLoader | Bunny Loader |
|---|---|---|
| The game | Terraria for PC | Terraria **Mobile**: some members differ (`Main.myPlayer` is a property, `Color` is in `Microsoft.Xna.Framework.Graphics`, a few XNA methods throw `NotImplementedException`) |
| IL editing (`IL_Player.Update += ...`) | yes | **no, and it cannot exist**: the mobile game is compiled to machine code (IL2CPP), so there is no IL to edit. Use `On_` hooks |
| Creating a game object | `new Item()` | `Item.New()` ([why](04-valores-e-objetos.md#objects-new-not-new)) |
| `int?`, `Item[]` from the game | the C# types | `GameNullable<int>`, `GameArray<Item>` (index and `HasValue` work the same; `foreach` and LINQ work on game arrays too, over a copy of the whole array, so use a plain `for` in code that runs every frame) |
| Game `List<T>` / `Dictionary<K, V>` | the C# types | `GameList` / `GameDictionary` handles |
| Overload names in hooks | `SacrificeItem_refItem_refInt32_bool_bool` | `SacrificeItem_refItem_outint_bool_bool` (keyword names, `out` written as `out`) |
| Input | mouse and keyboard, `ModKeybind` | touch: `ModKeybind` becomes an on-screen button ([guide 19](19-botoes-de-toque.md)); hover becomes a tap |
| Building | tModLoader compiles your source in game | the phone compiles the `.cs` files of a package, or you ship a `.dll` built on a PC ([guide 1](01-primeiro-mod.md)) |
| Reloading | *Reload Mods* | restart the game |
| `Unload` | called when mods unload | not called: the game process ends instead |

Where a content class works differently on the phone, its guide says so next
to the class.

The [API reference](referencia/README.md) compares both, class by class and member
by member, and says why each thing that tModLoader has is missing here.

Next tab: [6. Performance](06-desempenho.md).
