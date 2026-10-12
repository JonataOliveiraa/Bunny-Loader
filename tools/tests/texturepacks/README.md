# Texture loader tests

The native test compiles the production `TexturePacks.cpp` against IL2CPP/Unity simulations:

```bash
python3 tools/tests/texturepacks/run.py
```

Requires Linux/WSL and g++. It checks the tables, priority, fallback for an invalid file, full paths and uppercase extensions, symlink isolation, restoring value/source/state and that the GC handles are released. It also simulates a future load, a repeated request and a call outside the game thread.

`device/` tests the first in-game step. `named-device/` tests the individual assets, other tables and subfolders. These fixtures are test mods; the texture packs they use need no scripts.

For `named-device/`, provide `content/expectations.json`:

```json
{
  "name": "my-test",
  "rows": [
    {"path": "Images/Inventory_Back", "field": "InventoryBack", "width": 52, "height": 52},
    {"path": "Images/Background_325", "field": "Background", "index": 325, "width": 1024, "height": 546},
    {"path": "Images/UI/Minimap/Default/MinimapFrame", "width": 256, "height": 264}
  ]
}
```

A row without `field` creates an empty Asset<Texture2D>, explicitly calls `.ctor(string name)` and checks the future load. `Asset.new()` does not initialize the name. The test waits 120 game updates before checking the assets. `vanilla: true` checks that the replacements were disabled; `world: true` asks for the inventory to be opened in the copy of the world.

The validation of the local ports (2026-10-04, 2,091 checks on the device) is in the mods repository: `ports/textures/VALIDATION.md` in
[Bunny-Loader-Mods](https://github.com/JonataOliveiraa/Bunny-Loader-Mods).
