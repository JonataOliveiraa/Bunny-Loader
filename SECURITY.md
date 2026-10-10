# Security policy

Bunny Loader downloads mods from its catalog and runs their JavaScript inside
the game, with access to the game's code. A flaw in the loader, in the package
format or in the catalog can reach every player, so please report it privately.

## How to report

Use GitHub's private vulnerability reporting:
[Report a vulnerability](https://github.com/JonataOliveiraa/Bunny-Loader/security/advisories/new).
Do not open a public issue for security problems.

Include what you found, the steps to reproduce it, and the app and game versions.
You should get an answer within 7 days.

## In scope

- Catalog downloads: integrity checks, package validation, path traversal when
  unpacking a `.bl`.
- Anything that lets a mod or a package escape what the loader allows, or run
  without the player installing it.
- Leaked credentials or signing material in the repository or in a release.
- Multiplayer messages that crash or take over another player's game.

## Out of scope

- A mod doing what mods are allowed to do. Mods run with the same power as the
  game; install only mods you trust.
- Bugs in Terraria itself.
- Unofficial builds and forks. Check the certificate fingerprint in the README
  before installing an APK.
