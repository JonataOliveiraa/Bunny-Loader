# NPC spawning integration checks

Build with `bash tools/tests/build-cs.sh csnpcspawn`. Run with `NOENV=1 DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh csnpcspawn`. Keep the emulator window visible so the game advances. The environment pack suppresses both native spawners, so it must remain disabled here.

The test performs 256 native spawn attempts with 3:1 weights, 32 replacement-pool attempts and four empty-pool attempts. It validates the native and compatibility contexts, native rate/range edits, custom creation, global notification order, negative vanilla net IDs, the native zero entry and invalid weights. It restores world time/layers and removes the NPCs it creates. The tight same-frame loop releases the native two-tick slot protection only for those removed test NPCs.
