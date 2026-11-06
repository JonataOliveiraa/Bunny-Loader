# API reference

Every public class a mod can use in Bunny Loader, member by member, side by side
with tModLoader. Use the [guides](../README.md) to learn how something works and
this reference to look up what exists.

## How to read the tables

| Column | Meaning |
|---|---|
| **Member** | The signature, with short type names |
| **Kind** | `hook` is a `virtual` you override. Otherwise `method`, `property`, `field`, `event`, `const`, `constructor`, `indexer`, with `static` when it applies |
| **tModLoader** | `same`: same name and parameters, in tModLoader or in the PC game it runs on. `differs`: same name, other signature, which the row shows. **only here**: exists only in Bunny Loader. **not here**: exists in tModLoader, a mod that uses it does not compile here. `XNA` and `ReLogic`: from a library tModLoader also uses, not compared |
| **What it does** | What the member is for. **Phone:** marks a behavior different from the PC |

The game's own members, such as `Item.damage` or `Main.dayTime`, come from the
mobile game and are not listed: only what Bunny Loader adds to those classes is.
The `On_` classes are generated for each game method, as
[guide 2](../02-hooks.md) explains.

These pages are generated from the code by `tools/docs/ApiReference`. Descriptions
of members that tModLoader also has come from its documentation (MIT license); the
rest are in `tools/docs/ApiReference/notes.txt`.

The `tModLoader` column looks at signatures only. A member can be `same` and still
act differently on the phone, and an `only here` member can be a wrapper around
something the PC game has under another shape, such as `GameList` for a `List<T>`.
