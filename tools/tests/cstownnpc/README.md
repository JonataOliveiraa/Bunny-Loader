# Town NPC integration checks

Build with `bash tools/tests/build-cs.sh cstownnpc`. Run with `DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh cstownnpc` while the emulator window is visible. The pack uses the native town selection, housing condition, name, dialogue, shop, shopping-price, attack and weapon-drawing paths. `python tools/tests/cstownnpc/check-native.py` checks the ARM64 anchors against the integrated game image.

The test restores room bounds, closes the conversation and removes its temporary NPCs. Its global disables natural monster spawns. It exercises both legacy buttons and the local tModLoader NPCInteraction contract. Shop networking requires a coordinated two-device window and is not covered by this standalone pack.
