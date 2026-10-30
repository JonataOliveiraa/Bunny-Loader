# NPC AI and lifecycle checks

Build the pack with `bash tools/tests/build-cs.sh csnpcai`, then run it alone
with `DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh csnpcai`. The runner
enables csenv and disables QuickJS. Enter BagTest and QA_B1_Sonda manually if
the device's menu order differs from the runner's saved coordinates.

The default mode checks mod/global ordering and vetoes, borrowed type and
frame restoration, reset timing, death/despawn guards, spawn source and AI
arguments, hit metadata, real DOT/healing, nested DOT contexts, difficulty
and player scaling, vanilla defaults, negative variants, immunity and warm
managed allocation. It then watches the ported Flutter Slime for 240 frames.
Both modes also check thread-static `Main.rand` reads/writes, the AI style
name dictionary and lazy binding after a direct entity type change.
Hostile world fixtures use `townNPC = true`; multiple controlled fixtures
also have zero damage and no slime AI style.

For the native filter mode, create an empty `content/native-only.flag`
before running the same command, and remove it afterward. Both GlobalNPC
classes disable autoloading in this mode. It checks that unmarked AI and
frame calls leave a directly assigned mod type unbound, that requesting
ModNPC binds it once, and that AIType/AnimationType opt in to the native
filters. It also checks lifecycle order and allocation without globals,
followed by the same visible example.

The screenshot marker is `csnpcai Flutter Slime visible for screenshot`.
Success ends with `csnpcai END pass=N fail=0`. This pack does not claim a
physical-phone frame-time measurement or a multiplayer result.
