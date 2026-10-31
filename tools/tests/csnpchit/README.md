# NPC item hit checks

Build with `bash tools/tests/build-cs.sh csnpchit` and run alone with
`DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh csnpchit`. Enter BagTest and
QA_B1_Sonda, and dismiss the first-run touch control dialog if necessary.
The runner enables csenv and disables QuickJS.

Checks cover NPC/global nullable decisions, collision overrides, the selected
local CombinedHooks order and first-non-null source priority, incoming and
item-specific damage modifiers, full hit-effect metadata, raw message 28
arguments, overkill, immortal targets, nested raw strikes, combat text,
vanilla targets, the ported defense debuff and PartyZombie magic resistance.
Warm candidate and accepted-hit loops measure managed allocations.

Create an empty `content/npc-only.flag` for the installation check, then remove
it after the run. It disables the item/global-item/player combat fixtures and
uses a vanilla sword, so the NPC overrides must install the existing hit path
themselves. It runs the NPC-side checks and examples without the source-order
cases that require those fixtures.

The log includes a characterization of the existing primitive mobile strike's
odd critical-damage quantization. It is not counted as a completed precision
feature. This pack observes raw packet arguments in single player; it does
not claim a two-device network result.

Success ends with `csnpchit END pass=N fail=0`.
