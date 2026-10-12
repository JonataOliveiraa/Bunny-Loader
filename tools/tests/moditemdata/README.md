# ModItem SaveData/LoadData regression

This suite checks the loader's mechanism. The test item has an owner, a
counter and nested data; there are also classes with inheritance, an empty tag
and callbacks that throw. It does not depend on the Valentine Ring's
implementation.

## Tests without a device

At the repository root:

```powershell
node tools/tests/moditemdata/check.mjs
```

The runtime loads the project's real JS files and uses the real `ModItem`
registration. Only the calls into Terraria/IL2CPP are simulated. The cases
include 200 deterministic round trips of varied data, missing values,
inheritance, independent instances, JSON validation, exceptions, recovery,
clones, package order, relaying, disconnecting and mobile drops.

The C++ test directly includes the header used in production, without a copy
of the format's implementation:

```powershell
wsl.exe -d Ubuntu -- bash -lc 'cd "/mnt/c/Scripts/Bunny Loader" && g++ -std=c++20 -Wall -Wextra -Werror -fsanitize=address,undefined -Iapp/src/main/cpp tools/tests/moditemdata/format.cpp -o /tmp/bunny-moditemdata-format && /tmp/bunny-moditemdata-format'
```

It can also be built in another environment with C++20, adjusting the paths.
It checks old and new files, long lines, Unicode, containers, prefixes,
invalid numbers, streams with errors and unexpected delimiters.

## Integration in Android Terraria

Requires a built debug APK, ADB, a rooted device, and an existing local
character and world. Close the game before `prepare`. The runner installs the
APK, backs up the preferences, enables only this fixture and starts **copies**
of the saves named `BL_ItemData_Test`. The slots changed in the native round
are restored before returning to the game loop.

```powershell
python tools/tests/moditemdata/device.py prepare --device 127.0.0.1:16384 --role native
python tools/tests/moditemdata/device.py launch --device 127.0.0.1:16384 --role native
# After loading and the FIM line in the log:
python tools/tests/moditemdata/device.py collect --device 127.0.0.1:16384 --role native
```

`collect` fails if there is no completion with checks run and zero failures.
The logs live in `build/moditemdata/<device>/`. The round calls the real
`SavePlayer`/`LoadPlayer`, writes a separate character
`BL_ItemData_Probe.plr`, checks all 15 containers and the callbacks'
failures. For chests, it calls the real `SaveChests` and `FixAgainstExploits`:
the vanilla stream is in memory, and the companion `.wld.bl` save goes to
disk. It is not a test of closing/reopening the process while the world is
being saved.

## Multiplayer between two instances

After saving the native logs, close the test game. Prepare both sides:

```powershell
python tools/tests/moditemdata/device.py prepare --device 127.0.0.1:16384 --role host
python tools/tests/moditemdata/device.py prepare --device 127.0.0.1:16416 --role client
adb -s 127.0.0.1:16384 forward tcp:7777 tcp:7777
python tools/tests/moditemdata/device.py launch --device 127.0.0.1:16384 --role host
# Wait for the host to log running=true.
python tools/tests/moditemdata/device.py launch --device 127.0.0.1:16416 --role client
# After the FIM completions on both sides:
python tools/tests/moditemdata/device.py collect --device 127.0.0.1:16384 --role host
python tools/tests/moditemdata/device.py collect --device 127.0.0.1:16416 --role client
```

The host uses the native `GUIMultiplayerHost.HostServer` entry. The client
connects to `10.0.2.2:7777`, the NAT host of the emulators used in this round.
Adjust that address in `content/multiplayer.js` for other devices.

The fixture's packets coordinate the steps, without sending the item's owner
or state. Those fields go exclusively through the loader's hooks. The client
drops through the mobile UI; the host picks up with `Player.PickupItem`, drops
through the selected item and the client picks up again. A change only to the
inventory counter must be detected and reach the server. Then, a chest item
goes back and forth through native message 32. The fixture prepares an empty
chest on the client before sending: the vanilla protocol ignores message 32
when the chest does not exist on that client yet.

## Restoring the environment

Run when done, including after a failure:

```powershell
python tools/tests/moditemdata/device.py restore --device 127.0.0.1:16384
python tools/tests/moditemdata/device.py restore --device 127.0.0.1:16416
adb -s 127.0.0.1:16384 forward --remove tcp:7777
```

`restore` closes the test game, restores the previous preferences and removes
only this fixture's folder, after checking its path. The save copies and the
logs remain for diagnosis. The installed APK remains the tested build.

Run results: [RESULTADOS.md](RESULTADOS.md).
