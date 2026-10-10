# Licensing

Bunny Loader is **source-available for noncommercial use**. You may use,
study, modify and share it for any noncommercial purpose. It is not "open
source" in the OSI sense, which requires allowing commercial use.

## What applies to each part

| Part | License |
|---|---|
| Everything in this repository except the rows below: the app, the native core, the JS mod runtime, the tools and the documentation | [PolyForm Noncommercial 1.0.0](LICENSE.md) |
| `samples/` | MIT, each with its own `license.md`. `ExampleMod` is a port of tModLoader's ExampleMod and keeps its MIT license |
| Code examples in the documentation (`docs/`) | You may copy them into your mod under the MIT license (the same text as `samples/HelloMod/license.md`) |
| Code adapted from tModLoader | tModLoader Team's MIT license still applies to the parts that came from there; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) |
| Libraries (QuickJS-ng, ShadowHook, AndroidX...) | Their own licenses, listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) |
| Terraria (game binaries, game data and the art cropped for the app screens), the Andy font and the Unity player classes | **Not licensed here.** They belong to their owners; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) |

## Mods are separate works

A mod that interacts with Bunny Loader **only through its public API** (the
`bl` and `Terraria` objects, the mod classes such as `ModItem` and `ModNPC`,
hooks, and the `.bl` package format) is not considered a work based on Bunny
Loader for the purposes of this license. The mod's author picks its license,
and writing, publishing and distributing a mod needs nothing beyond it.

This clause does not cover copying parts of Bunny Loader itself into a mod,
except for `samples/` and the code examples in `docs/`, which are already MIT.

The official catalog (the **Explorar** tab) only lists free mods.

## What counts as commercial use

The [license text](LICENSE.md) is what decides. The lists below are the
project's reading of it, to settle the common questions.

**Needs written permission:**

- selling the APK, a version of it, or access to it;
- a "Pro", "Premium" or paid-feature version;
- ads in the app, or a download gated behind an ad-supported link shortener or
  a rewarded "task";
- subscriptions, Patreon or any support that unlocks builds, features or early
  access;
- publishing it in a store as a paid app or with in-app purchases;
- bundling Bunny Loader into a product or service that is sold.

**Allowed:**

- using it and playing with it;
- studying the code, making a hobby fork, and sharing changes for
  noncommercial purposes;
- writing and publishing free mods;
- recording, streaming and monetizing videos of gameplay with Bunny Loader.
  This is an explicit permission from the licensor.

Not sure, or want a commercial license? Open an
[issue](https://github.com/JonataOliveiraa/Bunny-Loader/issues) first.

## Name and logo

The license covers the code, not the brand. A fork needs a different name, a
different icon and a different `applicationId`; the rules are in
[TRADEMARKS.md](TRADEMARKS.md).

## Contributions

Contributors accept the terms in [CONTRIBUTING.md](CONTRIBUTING.md), including
the Contributor License Agreement (CLA).
