# 11. Talking between mods

A mod can find another installed mod and call what it offers. That is how, on
PC, Wikithis receives each mod's wiki address, and a boss checklist mod learns
the other mods' bosses. The approach is tModLoader's: `ModLoader.TryGetMod`
finds the other mod and `Call` calls what it offers.

Prerequisite: [guide 4](04-conteudo-novo.md) and the `ModSystem` from [guide 5](05-itens.md#modsystem).

## Calling another mod

```js
// content/Common/Systems/WikiCompat.js
export class WikiCompat extends ModSystem {
    PostSetupContent() {
        const wikithis = new Ref();
        if (ModLoader.TryGetMod('wikithis', wikithis)) {
            wikithis.value.Call('AddModURL', bl.mod, 'https://examplemod.wiki.gg/wiki/{}');
        }
    }
}
```

- The name is the other mod's **manifest `id`** (not its `name`). The `uid`
  works too.
- `TryGetMod(id, ref)` returns `true` and puts the `Mod` in `ref.value`, or
  returns `false` and puts `null`. It is C#'s `out Mod`, with a
  [`Ref`](02-ref-e-out.md).
- Without the other mod installed, the `if` is skipped and nothing happens.
  That keeps compatibility optional: someone without Wikithis plays the same.

For a mod yours cannot work without, `ModLoader.GetMod(id)` returns the `Mod`
directly and throws if it is not there. `ModLoader.HasMod(id)` only answers
whether it is. `ModLoader.Mods` is the list of all of them, in load order.

## Offering a `Call`

`Call` lives on the mod class, the `export default` of the entry file (every
mod has one, and only one):

```js
// content/main.js
const urls = new Map();

export default class Wikithis extends Mod {
    Call(command, ...args) {
        switch (command) {
            case 'AddModURL': {
                const [mod, domain] = args;
                if (!(mod instanceof Mod) || typeof domain !== 'string') {
                    throw new Error('AddModURL: expects (Mod, "https://...{}")');
                }
                urls.set(mod.id, domain);
                return true;
            }
            case 'GetURL':
                return urls.get(args[0]);
        }
        return undefined;
    }
}
```

- The convention, inherited from tModLoader, is for the first argument to name
  the command.
- Every value goes through **as is**: number, text, object, function, class,
  the `Mod` itself. All mods run in the same JS engine, so `Call` is a plain
  function call. In tModLoader everything becomes `object[]` and the other
  side converts; here an object arrives as the same object, and a received
  function can be called.
- An error thrown inside `Call` reaches the caller, as in tModLoader. Throw
  with a message that says what was missing.
- An unknown command returns `undefined`. So does a mod that does not define
  `Call`: it is tModLoader's `return null`.

Inside `Call`, `bl.mod` is the **called** mod, and relative paths
(`bl.loadTexture`, `bl.file`) are relative to its folder. The caller arrives
in the arguments, if it sends itself, as in `AddModURL` above.

## When to call: in `PostSetupContent`

Mods load in the order of the launcher's Packages tab, which each player
changes, so you cannot know whether the other mod runs before or after yours
(unless it is in your [`dependencies`](#dependencies)). That is why:

- the `Mod` of every installed mod **exists from the start**: `TryGetMod`
  finds the other one even if it has not loaded yet, and the object is the
  same afterwards (you can keep it);
- `Call` is only guaranteed **from `PostSetupContent` on**, when every mod has
  loaded. During loading (at the top of a file or in `Load()`), calling a mod
  that has not loaded yet throws `Mod 'x' ainda nao carregou: chame o Call a
  partir do PostSetupContent` ("has not loaded yet: call it from
  PostSetupContent"), instead of silently answering nothing.

`PostSetupContent` can be a `ModSystem`'s (as above) or your `Mod`'s.

## Dependencies

A mod yours cannot work without goes in the manifest's `dependencies`, by `id`
or by `uid`:

```json
"dependencies": ["wikithis", "7c2e9d41-5b3a-4f8e-9a61-2d0c4e8b7f15"]
```

- Each dependency **loads before** your mod, whatever the Packages tab order.
  The rest keeps the launcher's order. With that, a dependency's `Call`
  already works while your mod loads (at the top of files and in `Load()`).
- If a dependency is not enabled, your mod **does not load**, and the error
  panel says which one was missing (`requires 'wikithis', which is not
  enabled`). The same applies when the dependency fails to load and when two
  mods depend on each other (`dependency cycle: a -> b -> a`). The other mods
  load normally.
- By `id`, the dependency has to be unique among the enabled mods; if two use
  the same `id`, ask by `uid`.
- Optional compatibility does not go here: use `TryGetMod` in
  `PostSetupContent`, as above.

## The `Mod`

The `export default` class of the entry file receives, like `ModSystem`:

| | |
|---|---|
| `Load()` | During loading, after the top level of the files and the content registration. |
| `AddRecipeGroups()`, `AddRecipes()` | Together with `ModSystem`'s. |
| `PostSetupContent()` | With every mod's content ready. |
| `Call(...args)` | When another mod calls. |

And the package data, to read:

| | |
|---|---|
| `id` | The manifest `id`: the name other mods find this one by. |
| `uuid` | The manifest `uid`. |
| `name`, `version` | From the manifest, as text (`"1.2.0"`). |
| `path`, `root` | `main.js`'s folder and the package's. |
| `dataDirectory` | `Android/data/com.bunnyloader/mod_data/<uid>`. |

`bl.mod` is the `Mod` of the mod asking. At the top of the files, before Bunny
Loader creates your class, it is a plain `Mod`, with the same data; from
`Load()` on, it is the same object, now of your class (and its `this`).

## Two mods with the same `id`

The `id` is a nickname, and the site does not guarantee it is unique. If two
installed mods have the same one, `TryGetMod` by `id` does not pick: it
returns `false` and the log says to ask by `uid`, which is unique.

## Differences from tModLoader

| tModLoader | Bunny Loader |
|---|---|
| `ModLoader.TryGetMod("X", out Mod m)` | `ModLoader.TryGetMod('x', ref)`, with `ref.value` |
| `Mod.Name` | `mod.id` (the manifest `id`) |
| `Mod.DisplayName` | `mod.name` |
| `Mod.Version` (`Version` type) | `mod.version` (text) |
| `public override object Call(params object[] args)` | `Call(...args)`, values as they are |
| `public class X : Mod` | `export default class X extends Mod` in the entry file |
| `this` (your `Mod`) | `this` in the mod class; `bl.mod` in any file |

| `modReferences = ModA, ModB@1.0` in `build.txt` | `"dependencies": ["moda", "modb"]` in the manifest (no minimum version) |

There is no `Unload` and no `weakReferences`/`sortBefore`/`sortAfter`.

## Quick reference

| | |
|---|---|
| `ModLoader.TryGetMod(id, ref)` | `true` and the `Mod` in `ref.value`; or `false` and `null` |
| `ModLoader.GetMod(id)` | the `Mod`; throws if it is not there |
| `ModLoader.HasMod(id)` | is it installed (and did it not fail to load)? |
| `ModLoader.Mods` | all of them, in load order |
| `export default class X extends Mod { Call(...args) {} }` | offering a `Call` (the mod class, one per package) |
| `mod.Call(...)` | calling; from `PostSetupContent` on (a dependency's, already during loading) |
| `"dependencies": ["id"]` | load after that mod; without it, do not load |
| `bl.mod` | the `Mod` of whoever asks |

The `tools/tests/crossmod` test (paired with `tools/tests/crossmodtarget`)
covers every case in this guide: the mod that has not loaded yet, the missing
one, the same object before and after loading, an object and a function
through `Call`, an error propagating, `bl.mod` inside `Call` and the call in
the opposite direction, during loading.
