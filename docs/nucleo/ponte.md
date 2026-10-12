# The JS ↔ game bridge

> Part of the core documentation ([overview](README.md)). The mod author's
> side is in [guide 1](../mods/01-hooks-do-zero.md) and in the
> [bridge reference](../referencia/ponte-e-bl.md).

## What the bridge does

A mod writes `Terraria.Main.player[0].statLife = 400`. None of that exists in
JavaScript: `Terraria` is not a JS object, `Main` is a compiled C# class,
`player` is an array on the IL2CPP heap and `statLife` is an `int` at a fixed
offset inside a `Player` object. The bridge transforms each piece of that
path:

1. **name → game thing**: `Terraria.Main` becomes the `Il2CppClass*` class,
   `player` becomes the static field, `statLife` becomes an instance field
   with an offset and a type;
2. **memory → JS value**: the `int` at the offset becomes a JS number; a game
   object becomes a *wrapper*; a `Vector2` becomes a view;
3. **JS value → memory**: `400` becomes an `int` written at the right offset.

Everything by **name**, resolved at runtime through the IL2CPP API
(`il2cpp_class_from_name`, `il2cpp_class_get_field_from_name`...). No offset
or address is hard-coded: a new game version changes all of them, and the
bridge keeps finding things by name.

## The objects the mod sees

[`Bindings.cpp`](../../app/src/main/cpp/script/bridge/Bindings.cpp) defines
six kinds of JS object, each with a QuickJS exotic *getter/setter* (the
function that answers **any** property name):

| JS object | Example | What it wraps |
|---|---|---|
| **Namespace** | `Terraria`, `Terraria.ID` | Just a text prefix. |
| **GameClass** | `Terraria.Item` | A class: static fields and properties, methods, `.new()`, `.makeGeneric()`. |
| **GameObject** | `Main.player[0]` | A game object: instance fields and properties, methods. |
| **GameArray** | `Main.player` | A game array: `[i]`, `.length`, `.cloneResized(n)`. |
| **GameStruct** | `player.position` | A view (or copy) of a struct by value. |
| **GameMethod** | `Item['void SetDefaults(int Type, ItemVariant variant)']` | A method: callable and hookable (`.hook`). |

### Lazy namespaces

`Terraria.ID.ItemID` works without any declaration. At boot, the bridge scans
the game's classes **once**, only to learn which namespace **roots** exist
(`Terraria`, `System`, `Microsoft`, `ReLogic`...), and publishes a global for
each. The rest of the tree is lazy: `Terraria.ID` is an object with the prefix
`"Terraria.ID"`; accessing `.ItemID` tries to resolve `Terraria.ID.ItemID` as
a class and, if it is not a class, returns another namespace with the extended
prefix. Materializing the whole tree would mean scanning tens of thousands of
classes.

Classes **without a namespace** (the mobile UI: `GUIBuffs`,
`GUIInstance`...) become lazy globals: the getter finds the class on first
access and replaces itself with the value. A name that already exists
(`Math`, `bl`) stays as it is.

`bl.classOf(ns, name)` resolves directly, for the rare case where the tree
does not reach (a compiler-generated name, for example).

## Resolving a name: the member cache

`self.statLife` in a `Player.Update` hook runs every frame. Resolving the name
from scratch every time (turning the atom into text, scanning `Player`'s
hundreds of fields with `strcmp`, walking up the class hierarchy, asking for
the type) is expensive. [`Members.cpp`](../../app/src/main/cpp/script/bridge/Members.cpp)
resolves **once** per `(class, name)` and stores:

- the key is the **`JSAtom`**, not the text: QuickJS has already interned the
  name, and comparing an integer is what makes the hot path cheap;
- the result says what the name is: a field (offset and type), a C# property
  (`get_X`/`set_X`), a method by signature, a uniquely named method, a nested
  class, or a name from the JS prototype itself (`new`, `hook`, `toString`),
  which stays with JS;
- there are three spaces: **static** (`Terraria.Main.x`), **instance**
  (`player.x`, offset from the start of the object) and **struct**
  (`player.position.X`, offset from the start of the struct data).

A name with `(` is a **signature**: `'void SetDefaults(int Type, ItemVariant variant)'`
is matched against the class's methods (with C# type names, and accepting
`float?`/`Nullable<float>`/``Nullable`1``, `out int`, `ref Vector2`). A bare
method name works if there is **only one** method with that name; with more
than one, the bridge **refuses** and lists the overloads in the message.
Silently picking one once produced an exception every frame.

The **parameter names** are also checked, exactly
(`il2cpp_method_get_param_name`), in
[`Signature.cpp`](../../app/src/main/cpp/il2cpp/Signature.cpp). They do not
break ties, because two overloads never have the same types. They are there to
refuse a signature written differently from the game's: `drawInfo` where the
game says `drawinfo`, or just the types. The method that almost matched comes
back in the message (`o parametro 1 se chama 'drawinfo', nao 'drawInfo'`,
"parameter 1 is named ..., not ...", plus the game's signature). In the
internal C++ lookups, which only receive `nullptr`, the reason goes to the
log: it is what shows up when a game update renames a parameter.

### The name that does not exist

When the cache finds nothing (no field, no property, no method, none of the
fields and methods mods add with `bl.defineField`/`defineMethod`, none of the
static extras from `ModHelpers.js`), the read **throws**
`Member with name 'x' is not found in Class`, and so does the write. The
suggestion (`Did you mean 'whoAmI'?`) comes from a scan of the class: the same
name in a different case, or up to two letters swapped. That only runs on the
error path; the success path is still a hash lookup.

Three exceptions stay `undefined`, because it is the **engine** asking, not
the mod: symbols (`Symbol.toPrimitive` in an addition with text,
`Symbol.iterator` in a `for...of`), `toJSON` (`JSON.stringify`) and `then`
(an `await` or `Promise.resolve`). The cache marks those names (`quiet`) the
first time.

`in` has its own answer (the exotic's `has_property`): true for whatever a read
would find. Without it, `'statLife' in player` only looked at the JS prototype
and gave `false`. It is the way to ask without an error: `'Item' in source`.

C# runs a class's static constructor on the first access to one of its
statics; IL2CPP's `field_static_get_value` does not. Without care,
`AmmoID.Sets.IsArrow` read before the game used any ammo came back `null`. The
bridge calls `il2cpp_runtime_class_init` before the first use of each static
member, once.

## Reading and writing: the type system

[`Value.cpp`](../../app/src/main/cpp/script/bridge/Value.cpp) is the **only**
place that touches the game's memory to read or write a value. Before, each
contact point (instance field, static, array element, argument, return value,
hook argument) had its own type `if`, and the six copies diverged: an enum
worked as an argument and blew up as a field.

Each thing's type becomes a `TypeDesc`, resolved once:

| `Prim` | In JS | Detail |
|---|---|---|
| `Bool`, `I8`...`U64`, `Char` | number (or `boolean`) | Writing checks the type: text where a number is expected is a `TypeError`, never a silent 0. |
| `F32`, `F64` | number | |
| enum | number | `prim` is already the underlying type. |
| `String` | text | Converted from/to .NET's UTF-16. |
| `Object` | wrapper (`GameObject`), or `null` | See identity, below. |
| `Array` | `GameArray` | A JS array is also accepted where the game expects `int[]`, `string[]`...: the bridge builds one on the spot. |
| `Struct` | view or copy (`GameStruct`) | See structs, below. |
| `Nullable<T>` | `null` or the `T` itself | Never the `{hasValue, value}` struct. |
| `ref`/`out` | `Ref` | See [`ref` and `out`](../mods/02-ref-e-out.md). |

### Structs: view or copy

A `Vector2` inside an object (`npc.position`) is a **view**: a JS object that
keeps the owner and the offset, and reads and writes straight into the
owner's memory. `npc.position.X = 100` moves the NPC. The view keeps the owner
alive while it exists.

Two exceptions follow C#'s value semantics: the struct a **method returns**
and the one that arrives as a **hook argument** are **copies**. Writing to
them does not change the game.

A **static** struct field is a special case: IL2CPP only hands out a copy of
the statics block, never the address. The view keeps the `FieldInfo` and
**writes the value back on every write**; without that,
`Main.screenPosition.X = 0` would do nothing.

A struct created with `.new()` (`Vector2.new()`, `NetPacket.new()`) is
something else: a **boxed** object, with the object header before the data.
Fields are read by the object's offset, which already counts the header. But
the struct's methods and properties want the **data** as `this`: the bridge
passes the address right after the header. The property used to pass the box,
and `packet.Length` read the high bytes of the class pointer; when reading the
`Writer`, the game crashed.

Views come from a *pool* (`StructRef`), reused: creating one per read of
`player.position` was measured at ~330 ns and dropped to ~170 with the pool.

Structs with identical numbered fields (`proj.ai` is a `Float_FixedArray_3`,
with `_0`, `_1`, `_2`) get an **indexer**: `proj.ai[0]` reads the field
directly, with bounds (`RangeError` outside them). Without that shape, the
indexer falls back to the game's `get_Item`/`set_Item`.

## Calling a game method

[`Invoke.cpp`](../../app/src/main/cpp/script/bridge/Invoke.cpp) has two
paths:

1. **Direct call** (the normal one): it fills the registers using the same ABI
   plan the hook uses ([hooks](hooks.md#the-abi-plan)) and jumps to
   `methodPointer`, the same address the game uses. On a hooked method, the
   hook answers, as with a call from the game.
2. **`il2cpp_runtime_invoke`**: the runtime's generic path. It builds a vector
   of pointers to the arguments and **boxes the return value** (one collector
   allocation per call, even to return an `int`). It is only used when the
   plan does not fit (too many arguments for the registers, a large return
   struct).

IL2CPP's hidden argument (the `const MethodInfo*` at the end) is supplied by
the bridge, which is the caller here.

### Game exceptions

A C# exception becomes an `InternalError` in JS, with the game's text:

```
InternalError: 'PlaceObject' lancou excecao no jogo: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values.
```

On the `runtime_invoke` path, the runtime itself returns the exception. On the
direct call, we have to catch it, and a C++ `catch` **does not work**: in
IL2CPP, a C# exception is a `throw Il2CppExceptionWrapper` from libil2cpp's
`__cxa_throw`, which brings **libgcc**'s unwinder; libbunny brings **LLVM
libunwind** (static, from the NDK). The `catch` calls libbunny's personality,
which hands libgcc's context to its libunwind, which reads it as a different
structure and jumps into garbage. That is how it was until 2026-10: the
`catch (...)` in `Abi.cpp` crashed the game with SIGSEGV (on the emulator and
on the phone), and the exception never reached JS. (The fix below was written
on 2026-09-27, but stayed out of Git in a worktree and only landed in `main`
on 2026-10-10.)

[`GuardedCall`](../../app/src/main/cpp/il2cpp/GuardedCall.h) depends on no
unwinder at all. An assembly trampoline
([`GuardedCall.S`](../../app/src/main/cpp/il2cpp/GuardedCall.S)) saves the
callee-saved registers, writes a marker into its own frame and makes the call.
Its `.eh_frame` declares a personality of **our own**, which never reads the
context: in the search phase it answers "the handler is this one"; in the
cleanup phase (the game frames below have already run their `finally`s), it
walks up the stack to the marker, copies the C# exception pointer, frees the
C++ exception as one would a foreign one (`exception_cleanup`) and jumps back
to the trampoline, which returns 1. `callRaw` and `MapReveal` go through it; no
file is compiled with `-fexceptions` anymore. That also applies to frames that
only **pass through** a game exception, like a native hook in the middle of
the stack (`TexturePacks.cpp`): with `-fexceptions`, a pending destructor
there would call the same personality; without it, the frame is just passed
through.

Cost measured on MuMu: ~15–25 ns per direct call (`methodCached` 120 → 140
ns), the trampoline's `stp`/`ldp`. The first version found the marker through
a `thread_local`, which with minSdk 24 is emulated and cost ~50 ns; the marker
on the stack is only searched for when something throws. In the 2026-10-10
round: 200 exceptions in a row in 86 ms, and 10k `InWorld` in 5 ms.

The text comes from `il2cpp_format_exception`. To find the C# exception
inside the C++ one without RTTI, the bridge reads the `type_info` from the
`__cxa_exception` header, whose position changes with the libc++abi version
(−88 in libil2cpp's, which is NDK r21's; −80 in newer ones): each candidate is
only read if `dladdr` says it points into a library. Test:
[`tools/tests/gameexceptions`](../../tools/tests/gameexceptions).

A `GameMethod` is created once per `MethodInfo` and reused:
`obj['signature']` every frame returns the same object (before, building a new
one cost ~290 ns per access).

## Game objects held by JS

IL2CPP's garbage collector (Boehm, non-incremental in this Terraria) scans the
stack and the game's roots, not QuickJS's memory. An object that **only the
mod** holds (an `Item.new()` kept in a variable) would be collected, and the
pointer would dangle.

### The anchor: `Roots`

[`Roots.cpp`](../../app/src/main/cpp/script/bridge/Roots.cpp) keeps blocks of
4096 pointers allocated with `il2cpp_gc_alloc_fixed`: memory the collector
**scans as a root**, but does not collect. Anchoring an object is writing the
pointer into a free slot; releasing it is zeroing the slot. Before, it was one
`gchandle` per object (a lock and a runtime table for every object that
entered and left JS). Without the fixed memory API (another runtime version),
it falls back to `gchandle`.

### Identity: `WrapperMap`

`Main.player[0]` read twice returns the **same** JS object, and
`Main.player[0] === self` is `true`, as in C#.
[`WrapperMap.h`](../../app/src/main/cpp/script/bridge/WrapperMap.h) is a
pointer → wrapper map with open addressing (linear probing, backward-shift
deletion), with no allocation per entry.

- The map does **not** hold the wrapper. JS keeps it alive; when the last
  reference goes, QuickJS (which counts references) finalizes it **right
  away**, and the finalizer removes the entry and releases the anchor. The map
  never returns a dead wrapper.
- The key is stable because Boehm **does not move** objects, and the anchor
  prevents the address from being collected and reused while the entry
  exists.
- The `Pinned` records (one per wrapper) come from a free list, reused.

The map has its own *fuzz* test against `std::unordered_map`
([`tools/tests/wrappermap`](../../tools/tests/wrappermap), 9 million
operations, with a negative control).

QuickJS freeing right away is one of the reasons not to switch engines: V8
only frees wrappers when its collector runs, and the anchors would pile up
between collections (see [history](../historico/AVALIACAO-PONTE-E-CRASH.md#would-v8-be-worse)).

## Fields and methods the mod adds to the game

In IL2CPP an object's size and each field's position are baked into the
binary, and all compiled code reads by fixed position: a real field cannot be
added to `Item`.
[`ExtraFields.cpp`](../../app/src/main/cpp/script/bridge/ExtraFields.cpp)
keeps the mod fields (`bl.defineField(Terraria.Item, 'ModItem')`) in a side
table, indexed by the object, with a **weak reference**
(`il2cpp_gchandle_new_weakref`): the table does not hold the object, and when
the collector reclaims it the entry is discarded. The weak reference tells the
object apart from a new one born at the same address.

That is how `item.ModItem`, `proj.ModProjectile`, `npc.ModNPC` and
`player.ModPlayers` exist. `bl.defineMethod` does the same with methods
(`player.GetModPlayer(...)`). A name the game class already has wins over the
mod's.

## `Ref`: `ref` and `out`

[`Ref.cpp`](../../app/src/main/cpp/script/bridge/Ref.cpp): an object with
`.value`. In a hook, the `Ref` arrives **bound** to the address of the
caller's variable (reading reads the variable, writing changes what the game
will use) and **comes loose** when the callback returns (it keeps the last
value and forgets the address, which was on the stack). In a call from the
mod, a loose `Ref` becomes a temporary variable with its value and receives
back what the method left. A `Ref` still bound, passed on from inside a hook,
goes as the original address. A struct by `ref` always comes out as a copy.
The full usage is in [guide 2](../mods/02-ref-e-out.md).

## Generics and arrays

- `List.makeGeneric(Vector2)` goes through .NET itself
  (`Type.MakeGenericType`). It only works for combinations the game already
  uses: IL2CPP only compiles the code of the generic instances that exist in
  the game. For the others, the runtime throws and the message says which.
- `arr.cloneResized(n)` creates a new array of the same type, copies what fits
  and leaves the rest zeroed. It is the tool for anyone who needs to grow a
  game table (`Main.recipe = Main.recipe.cloneResized(4000)`).
- An array index is read straight from QuickJS's **integer atom** (bit 31 plus
  the value), with the encoding checked once against `JS_NewAtomUInt32`; the
  element type is kept in the array's wrapper. The index stride is the
  element size: a pointer (8 bytes) in a `Player[]`, the whole struct in a
  `Vector2[]`.

## Where each cost number comes from

The bridge's measurements, step by step, are in the
[optimization history](../historico/PONTE-OTIMIZACAO.md); the practical
summary for mod authors is in the [cost guide](../mods/03-custo-e-desempenho.md).
