# Contributing

Thanks for helping. Bug reports, mods, documentation fixes and code are all
welcome.

## Before you start

- Read [LICENSING.md](LICENSING.md). Bunny Loader is source-available under the
  PolyForm Noncommercial License 1.0.0, not under an OSI license.
- The game binaries and data are not in the repository, and never will be.
  Do not commit Terraria files (`.so`, `global-metadata.dat`, `data.unity3d`,
  dumps from `refs/`), the Unity classes, the Andy font, keystores or tokens.
  `.gitignore` already blocks the usual paths.
- The guides live in the [wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki), in English: making mods, the API
  reference, and, under [Contributing](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Contributing), how the native
  core, the C# runtime and the tests work. Only collaborators edit the wiki;
  to fix a page, open an issue with the page and the change.
- Mods do not go in this repository, apart from the samples in `samples/`.
  Share yours as a package
  ([Sharing your mod](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Sharing-Your-Mod)).
- Code style: identifiers in English; comments in Portuguese (pt-BR); app
  text through the translation files in `app/src/main/assets/localization/`
  ([Translating the app](https://github.com/JonataOliveiraa/Bunny-Loader/wiki/Translating-the-App)); logs and messages shown
  to modders in English. Commit messages in English.

## Contributor License Agreement (CLA)

By opening a pull request, you agree to the terms below for everything you
submit to this project (code, documentation, art and anything else, together
"your contribution").

1. **You can grant it.** Your contribution is your own original work, or you
   have the right to submit it under these terms. If your employer has rights
   over your work, you have their permission.
2. **License to the project.** You grant JonataOliveiraa, the maintainer of
   Bunny Loader, and anyone who receives the project from them, a perpetual,
   worldwide, non-exclusive, royalty-free, irrevocable license to use,
   reproduce, modify, distribute, sublicense and relicense your contribution,
   under the project's current license or any other license, including
   commercial licenses granted to third parties.
3. **Patents.** You grant the same parties a perpetual, worldwide,
   non-exclusive, royalty-free, irrevocable patent license for any of your
   patent claims that your contribution would infringe, to make, use, sell and
   distribute it as part of the project.
4. **You keep your copyright.** This agreement does not transfer ownership.
   You may use your own contribution in any way you like.
5. **Third-party material.** If part of your contribution comes from someone
   else, say so in the pull request, with its source and license. MIT, BSD and
   Apache-2.0 code is fine when the notice comes along; GPL code cannot be
   accepted.
6. **No warranty.** Your contribution is provided "as is".

Why a CLA: the project's license may change in the future (for example, to a
more permissive one), and the maintainer may grant commercial exceptions. Without
the agreement, every such change would need the consent of every contributor.

The pull request template has a checkbox for this. A pull request without it
checked will not be merged.

## Pull requests

- One topic per pull request.
- Say what you tested and where: MuMu, a real phone (model and Android
  version), and the game version.
- Keep generated files out unless the change is about them.
