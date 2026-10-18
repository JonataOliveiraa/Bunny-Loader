# Dust, UI and resources in multiplayer

Two real game processes, a phone hosting and playing (`netMode` 3) and a
remote client (`netMode` 1), both with this test and the Example Mod enabled.
The test checks, on both sides:

- the mod dust types are the same on both devices;
- an `ExampleSentryShot` fired by either player reaches the other device and
  spawns `Sparkle` there;
- `ExampleStatIncreasePlayer` (life fruits and mana crystals) reaches the
  other side when the client joins (`SyncPlayer`) and after each change
  (`SendClientChanges`, from the client and from the host), and the other side
  computes the same max life and mana;
- the healing number of `ExampleResourcePlayer` (`ExampleResourceEffect`)
  shows on the other device;
- on the client, the `/coins` screen counts coins dropped by the host, a touch
  on the panel does not use the held weapon, the X closes it, and the
  resource weapon spends the resource and its rocket reaches the host.

The setup is the same as [mpmodplayer](../mpmodplayer/README.md): 1600 × 900
screens, the first character and world in the list, the server
`10.0.2.2:7777` saved on the client.

```bash
export BL_HOST=127.0.0.1:16384
export BL_CLIENT=127.0.0.1:16416
bash tools/tests/mpexample/run.sh apk    # installs out/bunny-loader.apk on both
bash tools/tests/mpexample/run.sh cleanup
```

Without `apk`, it uses the APK already installed. The script answers the
test's `TAP`/`UISCALE`/`SHOT` requests on each device, stops when both log
`FIM`, and exits with 0 only when both log `FIM: tudo ok`. Logs and
screenshots go to `build/mpexample/`. `cleanup` restores the mod list and
settings it changed and removes the test pack.
