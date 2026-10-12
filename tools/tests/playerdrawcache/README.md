# The player's DrawData cache

The fixture compares three ways of inserting a marker in ModifyDrawInfo: writing into the next slot without increasing DrawDataCacheCount, updating the array and the counter by hand, and using ModPlayer.AddDrawData. The marker is produced with the native MagicPixel; the fixture does not call DrawData.Draw.

The diagnostic hook observes the cache after DrawPlayer_RenderAllLayers. The case without the counter has to disappear from the active cache; the manual and helper cases have to survive the layers and transformations. The magenta marker stays visible after the end for visual checking, until the process is closed.

The marker sits near the center of the screen so it stays visible with the game's zoom. Being in the cache does not replace checking the image. See the [results and the limits of the reproduction](RESULTADOS.md).

The fixture also checks the prefix inserted by the helper with Skin after Head, an order that is already satisfied and a cyclic order. Extra markers inside Skin and Head let you check the effective order of the segments in the cache. The final line requires draws observed in each case and no loss of the marker. The cycle must produce the ignored-order warning and keep the original drawing.

The [multiplayer fixture](../mpmodplayer/README.md) repeats the three modes for the local and the remote player in both processes, including reordering and reconnecting. Its [results](../mpmodplayer/RESULTADOS-DRAW-MODITEM.md) record 28 passing DrawDataCache checks and a visual check on both screens.

With a test Android instance connected and the APK installed:

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture playerdrawcache --run-id my-run
```

Start the game from the launcher. The fixture only runs for the local player and draws without shadows, between updates 60 and 260. Read the log of the com.bunnyloader:game process: the playerdrawcache FIM line must end in passou=true, with lost=0, failed=0 and reordered/unchanged/cyclic greater than zero. The number of observed draws varies with the frame rate.

After collecting the log and the screenshot:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture playerdrawcache --run-id my-run
```

The script uses the manifest's UID, keeps the session's own preferences and checks the path before removing the fixture. Use the same run-id in the attempts and in the cleanup. The test does not change the inventory or the character's data.
