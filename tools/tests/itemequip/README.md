# ModItem state when moving equipment

The fixture calls the mobile UI's native transfer methods and checks the
owner, the counter, mutable data and the `ModItem.Item` link. The item
implements SaveData/LoadData and NetSend/NetReceive, but the tests change its
state in memory without saving before the transfer.

Native cases:

- Dragging (`ItemSlot.LeftClick`) into an empty accessory slot.
- Swapping between two different accessory types, with different owners.
- Returning the excess to the inventory while dragging. This case forces a
  stack of two accessories to exercise the partial transfer branch; it does
  not represent the Valentine Ring's normal configuration.
- `ItemSlot.PlaceItem` into an empty slot, with a custom Clone and independent
  data.
- Twenty consecutive transfers, without mixing owners.
- A partial transfer with `keepStack`, keeping source and destination.
- `SetDefaults` outside the transfer still resets the state.

Use the manifest and the `content` folder as a test mod, enabled on its own on
copies of a character and a world. The round runs once after entering the
world and restores the changed slots and UI fields before the next frame. Look
for `itemequip FIM checks=7 falhas=0` in the log. The fixture tests native
methods; it does not automate physical gestures on the screen.

On October 8, 2026, on MuMu with game 301720, the same round showed four
failures on the previous APK and zero on the APK built with the fix. The
simple drag and the full swap already passed before; the PlaceItem path
recreated the item only by type and lost the mod's state. Local logs:
`build/itemequip/before.log`, `after.log`, `unit.log` and `gradle.log`.

Complementary JS tests:

```powershell
node tools/tests/moditemdata/check.mjs
```

The suite's 67 cases passed. The added coverage includes items without
persistence hooks, a custom Clone, nested transfers, a native exception,
creating an item inside Clone, cached data and an earlier LoadData failure.
That JS suite's native calls are simulated; this folder's fixture validates
the transfer path inside the game.
