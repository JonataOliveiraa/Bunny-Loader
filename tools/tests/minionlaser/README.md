# Mod lasers and the cost of projectile combat

Origin: Consolaria's EyeOfEternity minion (GST378's port) tanked the FPS when
shooting the EternalLaser, which clones the EyeLaser and uses
`AIType = ProjectileID.PurpleLaser`. The suspect was `AIType`; it was not.

The test fires lasers at a fixed rate (one every 2 frames, 120 frames of
life: ~60 in the air) and measures `DoUpdate`/`DoDraw` and the hooks
(`bl.hookStats`) in six phases of 300 frames: nothing, the game's PurpleLaser
and EyeLaser, the EternalLaser with and without `AIType`, and 10 minions
shooting at slimes. It needs Consolaria enabled
(`ModContent.ProjectileType('consolaria/EternalLaser')`).

## What it was

With any projectile hit hook (a projectile's `OnHitNPC`, a ModPlayer's
`OnHitNPCWithProj`), `CombatLoader` put a JS hook on `Projectile.Damage_PVE`,
which the game calls for every friendly projectile on every update (the laser
has 2 to 4 extra updates per frame), and that hook turned on
`CanNPCBeHitByPlayerOrPlayerProjectile` in JS for each NPC. On MuMu, with 60
lasers and ~12 NPCs: 2,800 JS entries per frame.

Now `Damage_PVE` only has the native scope (`scope()`), `StrikeNPC` finds the
projectile through `scopeSelf()` and the projectile's `CanNPCBeHit...` only
enters JS for those with a projectile `CanHitNPC` or `CanBeHitByProjectile`.

| Phase (MuMu, 2026-10-09) | DoUpdate before | after |
|---|---:|---:|
| Nothing | 4.4 ms | 4.3 ms |
| The game's PurpleLaser | 68.3 ms | 6.5 ms |
| The game's EyeLaser | 39.4 ms | 5.9 ms |
| EternalLaser with AIType | 48.4 ms | 9.0 ms |
| EternalLaser without AIType | 31.4 ms | 7.8 ms |
| 10 minions shooting | — | 8.2 ms |

## Running

With Consolaria and this package in `bunny_packs`, enter the world; the test
logs `minionlaser ...` and ends with `minionlaser FIM`.
