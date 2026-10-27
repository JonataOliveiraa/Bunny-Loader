# 5. C# × JS × tModLoader

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · [3. `ref`, `out`, `Nullable`](03-ref-out-e-nullable.md) · [4. Values and objects](04-valores-e-objetos.md) · **5. C# × JS × tModLoader** · [6. Performance](06-desempenho.md) · [7. Moving to C#](07-migracao-para-csharp.md) · [8. Under the hood](08-por-baixo-dos-panos.md) · [9. Damage classes](09-classes-de-dano.md)

## The same mod, four ways

"Every weapon hits twice as hard."

**Bunny Loader, C# (a hook):**

```csharp
using BunnyLoader;
using Terraria;

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

**Bunny Loader, JavaScript (a hook):**

```js
const SetDefaults = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'];

export default class DoubleDamage extends Mod {
    Load() {
        SetDefaults.hook((original, self, type, variant) => {
            original(self, type, variant);
            if (self.damage > 0) self.damage = self.damage * 2;
        });
    }
}
```

**Bunny Loader, JavaScript (a `GlobalItem`):**

```js
export class DoubleDamage extends GlobalItem {
    SetDefaults(item) {
        if (item.damage > 0) item.damage *= 2;
    }
}
```

**tModLoader (a `GlobalItem`):**

```csharp
public class DoubleDamage : GlobalItem
{
    public override void SetDefaults(Item item) {
        if (item.damage > 0) item.damage *= 2;
    }
}
```

What to notice:

- The **C# hook** and the **JS hook** are the same idea. C# finds the method
  by its typed name (`On_Item.SetDefaults`); JS finds it by its signature
  string (`'void SetDefaults(int Type, ItemVariant variant)'`).
- The **`GlobalItem`** versions are shorter because a loader does the hooking
  for you. JS has those loaders today; C# does not yet (see
  [Moving to C#](07-migracao-para-csharp.md)).
- The game's names (`Item`, `damage`, `SetDefaults`) are the same in all
  four.

## C# vs JavaScript on Bunny Loader

| | **C#** | **JavaScript** |
|---|---|---|
| Write it on | a PC (any editor with C#) | a PC, or the phone (in-game editor) |
| Build step | `dotnet build` | none |
| Typos and wrong types | caught **by the compiler**, before the phone | found when the code runs |
| Autocomplete for the game | yes: every class, field, method and hook | no |
| Finding a method | `On_Item.SetDefaults`, `item.SetDefaults(...)` | `Item['void SetDefaults(int Type, ItemVariant variant)']` |
| Speed of your own code | compiled to machine code: **4× to 30× faster** | interpreted |
| Reading/writing game fields | **5–11 ns** | 70–260 ns |
| Calling game methods | 70–340 ns | 160–570 ns |
| Hook overhead | **~400 ns** per call | ~600 ns per call |
| Content classes (`ModItem`, `ModNPC`, `GlobalItem`, `ModSystem`...) | **not yet** | yes, all of them |
| Textures, sounds, localization, UI, screen effects | not yet (they come with the loaders) | yes |
| `ref` / `out` | the language's own `ref` / `out` | `Ref` objects |
| `Vector2` | `new Vector2(1, 2)` (~3 ns) | `Vector2.new(1, 2)` (~1300 ns) |
| Reload after a change | restart the game | restart, or the in-game editor |
| Extra cost on the phone | a .NET runtime starts (~0.5 s) when a C# mod is enabled | the JS engine is always there (can be turned off) |

All numbers are from a Galaxy A15 ([Performance](06-desempenho.md)).

## C# on Bunny Loader vs tModLoader

**What is the same:**

| | tModLoader | Bunny Loader C# |
|---|---|---|
| Hooks | `On_Item.SetDefaults += ...` | `On_Item.SetDefaults += ...` |
| Hook shape | `(orig, self, args...)` | `(orig, self, args...)` |
| Delegate types | `On_Item.orig_SetDefaults`, `hook_SetDefaults` | the same names |
| `ref` / `out` in hooks | yes | yes |
| Add / remove | `+=` / `-=` | `+=` / `-=` |
| Order | last added runs first | last added runs first |
| The game's names | `Player`, `Item.useTime`, `Main.player`, `ItemID.Minishark` | the same |
| XNA | `new Vector2(1, 2)`, operators | the same |
| Entry | `class MyMod : Mod` | `class MyMod : Mod` with `Load()` |

**What is different:**

| | tModLoader | Bunny Loader C# |
|---|---|---|
| `ModItem`, `ModNPC`, `ModProjectile`, `ModPlayer`, `ModSystem`, `Global*` | yes, the heart of tModLoader | **not yet** (planned) |
| Creating a game object | `new Item()` | `Item.New()` ([why](04-valores-e-objetos.md#objects-new-not-new)) |
| Overload names | `SacrificeItem_refItem_refInt32_bool_bool` | `SacrificeItem_refItem_outint_bool_bool` (keyword names, `out` written as `out`) |
| IL editing (`IL_Player.Update += ...`) | yes | **no, and it cannot exist**: the mobile game is compiled to machine code (IL2CPP), so there is no IL to edit |
| `int?`, `Item[]` | the C# types | `GameNullable<int>`, `GameArray<Item>` (index and `HasValue` work the same; LINQ on game arrays does not) |
| The game itself | Terraria for PC | Terraria **Mobile**: some members differ (`Main.myPlayer` is a property, `Color` is in `Microsoft.Xna.Framework.Graphics`, a few XNA methods throw `NotImplementedException`) |
| Building | tModLoader compiles your source in game | you build the `.dll` on a PC |

So: **hooks copied from tModLoader usually compile with small changes**
(an overload name, `new Item()` → `Item.New()`, a mobile difference).
Content (`ModItem` & co.) cannot be ported to C# yet.

## From JS to C#: a translation table

| JavaScript | C# |
|---|---|
| `export default class M extends Mod { Load() {} }` | `public class M : Mod { public override void Load() {} }` |
| `const F = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)']` | nothing: use `On_Item.SetDefaults` or `item.SetDefaults(...)` |
| `F.hook((original, self, a) => { original(self, a); })` | `On_Item.SetDefaults += (orig, self, a, v) => { orig(self, a, v); };` |
| `bl.log('x')` | `Log.Info("x")` |
| `bl.error('x')` | `Log.Error("x")` |
| `Terraria.Main.player[Terraria.Main.myPlayer]` | `Main.player[Main.myPlayer]` |
| `Terraria.ID.ItemID.Minishark` | `ItemID.Minishark` (`using Terraria.ID;`) |
| `Vector2.new(1, 2)` | `new Vector2(1, 2)` |
| `Color.new(255, 0, 0)` | `new Color(255, 0, 0)` |
| `const r = new Ref(); f(0, r); r.value` | `f(0, out int r);` |
| `null` for a `float?` | `null` (a `GameNullable<float>`) |
| a JS number for an `int` | an `int` (the compiler checks it) |

## Which one should I use?

- **Changing the game's behavior, especially every frame:** C#.
- **New items, NPCs, tiles, UI:** JS for now.
- **Both in one mod idea?** Two packages work fine together: the content in
  JS, the heavy logic as C# hooks.

Next tab: [6. Performance](06-desempenho.md).
