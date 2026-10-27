# 4. Values and objects

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · [3. `ref`, `out`, `Nullable`](03-ref-out-e-nullable.md) · **4. Values and objects** · [5. C# × JS × tModLoader](05-comparacao.md) · [6. Performance](06-desempenho.md) · [7. Moving to C#](07-migracao-para-csharp.md) · [8. Under the hood](08-por-baixo-dos-panos.md)

Everything in the game is one of two kinds of thing, and the kind decides how
you create it. Get this page right and the rest is just reading the game's
names.

## The two kinds

| | **Values** (structs) | **Objects** (classes) |
|---|---|---|
| Examples | `Vector2`, `Vector3`, `Color`, `Rectangle`, `Point`, `Matrix` | `Player`, `Item`, `NPC`, `Projectile`, `Main` |
| What a variable holds | the **data itself** (two floats for a `Vector2`) | an **address**: where the object lives in the game |
| Copying the variable | copies the data | copies the address (same object) |
| Create one with | **`new Vector2(1, 2)`** | **`Item.New()`** (or the game makes it) |
| "Empty" means | zero (`default(Vector2)` is `(0, 0)`) | **null**: no object at all |

Think of a value as a number written on paper, and an object as the address
of a house: copying the paper gives you another number; copying the address
does not build another house.

## Values: `new`, like any C#

```csharp
var pos   = new Vector2(10, 20);
var color = new Color(255, 100, 0);           // ints 0–255, alpha 255
var faded = new Color(1f, 0.4f, 0f, 0.5f);    // or floats 0–1
var area  = new Rectangle(0, 0, 32, 32);
var tile  = new Point(5, 7);
var v3    = new Vector3(1, 2, 3);
```

These constructors are plain C#: they fill in the fields and never talk to
the game, so they are basically free (about 3 ns, see
[Performance](06-desempenho.md)).

Operators and the game's helpers work as you expect:

```csharp
pos += new Vector2(1, 0) * 3f;                 // + - * /
Vector2 zero  = Vector2.Zero;                  // the game's static properties
float   dist  = Vector2.Distance(pos, zero);   // the game's static methods
Vector2 dir   = Vector2.Normalize(pos);
Matrix  scale = Matrix.CreateScale(2f);
```

A struct with no constructor written for it: fill the fields by name.

```csharp
var q = new Quaternion { X = 0, Y = 0, Z = 0, W = 1 };
```

> **`Vector2.New(1, 2)` also exists.** It runs the **game's** constructor
> instead of the C# one: same result, but a trip into the game. You only need
> `.New` on a struct when the game's constructor does something special.

The fields of a struct are real C# fields: `pos.X = 5;`, `area.Width += 2;`.

## Objects: `.New()`, not `new`

An object variable (`Item`, `NPC`...) is a small handle that holds the
**address** of the object inside the game. To make a new game object, ask the
game to allocate it:

```csharp
Item item = Item.New();                       // allocate + the game's constructor
item.SetDefaults(ItemID.Zenith, default);

Item shark = Item.New(ItemID.Minishark, default);   // the game's other constructors
```

> ⚠️ **Never `new Item()`.** It compiles, but it is an **empty handle**: no
> address, no object. Using it throws `NullReferenceException`. Game objects
> are always `Type.New(...)`, or come from the game.

In practice you rarely create objects. You **receive** them:

```csharp
On_Player.Update += (orig, self, i) => { ... };   // self: the player
Player me    = Main.LocalPlayer;                   // the local player (in a world)
Player first = Main.player[0];
NPC    npc   = Main.npc[3];
Item   held  = me.HeldItem;
```

Or you ask the game's own factories, as in tModLoader:

```csharp
// returns the index in Main.projectile
int index = Projectile.NewProjectile(
    me.GetProjectileSource_Item(held),   // who spawned it (the game wants to know)
    me.Center, new Vector2(8, 0),        // where, and speed
    ProjectileID.WoodenArrowFriendly,    // what
    20, 2f, Main.myPlayer,               // damage, knockback, owner
    0, 0, 0, default);                   // ai0, ai1, ai2, modifier
```

`NPC.NewNPC(...)` and `Item.NewItem(...)` work the same way.

### Is it there?

An object handle can be empty. Check before using it:

```csharp
Item maybe = player.inventory[10];
if (!maybe.IsNull && maybe.type > 0)
    Log.Info($"slot 10 has {Lang.GetItemNameValue(maybe.type)}");
```

`==` compares addresses: `a == b` means "the same object".

## Fields: read and write directly

Every field of a game object is a property you can read and write, and the
write goes straight into the game's memory:

```csharp
Player p = Main.player[Main.myPlayer];
p.statLife = p.statLifeMax2;     // full health
p.position.X += 16;              // one tile to the right (yes, straight into the struct)
p.moveSpeed *= 1.5f;
```

`p.position.X += 16` works because `position` gives you the game's own
`Vector2`, not a copy.

## Properties and methods

They look exactly like the game's C#:

```csharp
Vector2 center = p.Center;               // a property
int buffs      = p.CountBuffs();         // a method
p.AddBuff(BuffID.Swiftness, 600, false); // with arguments
```

Methods with default values in the game have them in C# too:
`WorldGen.InWorld(100, 100)` is `InWorld(100, 100, fluff: 0)`.

## Arrays

The game's arrays (`Main.npc`, `Main.player`, `p.inventory`, the `Sets`
tables) are `GameArray<T>`: index them like any array.

```csharp
int enemies = 0;
for (int i = 0; i < Main.npc.Length; i++)
{
    NPC npc = Main.npc[i];
    if (npc.active && !npc.townNPC) enemies++;
}
Main.NewText($"{enemies} enemies around", 255, 240, 120);

bool deprecated = ItemID.Sets.Deprecated[5];     // a bool[] in the game
```

The index is checked: outside the array you get `IndexOutOfRangeException`,
not a crash.

## Statics and IDs

```csharp
Main.dayTime = true;            // static fields: read and write
Main.time = 0;                  // with dayTime, the start of the day (4:30 AM)
int me = Main.myPlayer;         // static property
int id = ItemID.Minishark;      // constants (98), same names as the game
```

## Strings

`string` parameters and returns convert on their own:

```csharp
string? name = Lang.GetItemNameValue(ItemID.Minishark);   // "Minishark" (in the game's language)
p.name = "Bunny";                                          // string fields too
Main.NewText("Hello!", 255, 255, 255);
```

## Inheritance and casting

`Player` and `NPC` are both `Entity` in the game, and the handles know it:

```csharp
Entity e = p;                      // Player → Entity: automatic
Player again = (Player)e;          // Entity → Player: explicit, not checked
GameObject any = p;                // anything → GameObject
bool isNpc = NPC.Is(any);          // a real type check, asks the game
```

## Keeping an object between frames

A handle does **not** keep its object alive: the game's garbage collector
does not see C# memory. Inside one call (a hook, a frame) that never matters.
To keep an object for later, wrap it in a `GameHandle`:

```csharp
static GameHandle? target;

target = new GameHandle(npc.Pointer);        // now the game keeps it alive
// ...frames later...
var still = new NPC(target.Target);
target.Dispose();                            // let it go when done
```

Indexes (`Main.npc[i]`) and IDs need none of this: store the number.

## Talking to the player

```csharp
Log.Info("goes to the mod log");
Log.Warn("a warning");
Log.Error("an error: also opens the error panel");
Main.NewText("goes to the in-game chat", 255, 240, 120);
```

## What is in `BunnyLoader.dll` today

Every member and hook exists for these game types (and their nested types):

`Main`, `Entity`, `Player`, `Item`, `NPC`, `Projectile`, `WorldGen`,
`WorldItem`, `Dust`, `Collision`, `Utils`, `Lang`, `ItemID`, `NPCID`,
`ProjectileID`, `BuffID`, `DustID`, and from XNA `Vector2`, `Vector3`,
`Vector4`, `Rectangle`, `Point`, `Color`, `Matrix`, `Quaternion`, `Plane`,
`MathHelper`.

Any other game type that appears in their signatures (`ItemVariant`,
`IEntitySource`, `SpriteBatch`...) exists as a handle, so you can pass it
around, but without its own members yet. The list grows with each Bunny
Loader version.

Next: the tabs. [5. C# × JS × tModLoader](05-comparacao.md).
