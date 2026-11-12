# Joining a server: rules, syncing mods and textures

The Host screen (Multiplayer > Host > the world) has Bunny Loader options,
after the Password, in a scrolling list (`HostSettingsMenu`). They start on
and apply while the game is open:

| Option | On | Off |
|---|---|---|
| Mod sync | Anyone joining with different mods is refused and offered a sync. | Anyone joining with different mods is refused, with the list of differences. |
| Allow textures | Anyone joining can have texture packs enabled. | Anyone with a texture pack enabled is refused, with the reason. |
| Allow cheat menu | Anyone joining can use the cheat menu. | The cheat menu of anyone joining is turned off while on this server. |

## Bunny Loader only joins Bunny Loader servers

Always on, in the core (`content/common/ModNet.cpp`). Before asking for the
world (message 6), the client introduces itself in a **message 68**, the
client UUID one: `Main.clientUUID` becomes `bunnyloader-cs:` + the
introduction only while sending. The server refuses, with "invalid operation in
this state", any message above 12 from someone still joining, except 16, 38,
42, 50, 68, 93, 147, 161 and 164 (checked in mobile's
`MessageBuffer.ProcessData`), so the mod packets' 82 does not work here. The
host reads the 68 in a hook on `MessageBuffer.ProcessData` and does not pass it
on: the client's real UUID, which arrives in another 68, stays intact.

The same rule applies to the C# side: a client sends its mod packets only from
`Player.Hooks.EnterWorld`, right after the message 12 that moves it to state
10 (`ModPlayer.SyncPlayer`, the mod accessory slots). The slots used to follow
the client's message 4, which goes out while joining, and the server kicked
the client.

Every Bunny Loader server answers the introduction with a **welcome** (a C#
net packet, kind 200, carrying the host rules: bit 0 is the cheat menu). If the
world data (message 7) arrives without a welcome, the server is vanilla (or an
older Bunny Loader): the core turns that message 7 into a message 2 (kick) with
"Bunny Loader only joins Bunny Loader servers.", and the client leaves. With
the welcome and the cheat menu forbidden, `cheatsBlocked()` turns the cheat
menu off: the button is hidden (`nCheatsBlocked` in the CheatBridge), its
native calls do nothing and the powers are reset.

For tests, `adb shell setprop debug.bl.nowelcome 1` on the host makes it skip
the welcome, acting as a vanilla server.

## The introduction and the host's decision

The introduction is JSON: `{"mods":[...],"textures":N}`, the loaded packs in
load order (uid, id, name, version, from the core's `PackInfo`) and how many
texture packs are enabled. The core hands it to the C# listener
(`NetHelloListen`, `Terraria/ModLoader/Core/ServerSyncHooks.cs`), which
decides:

- textures forbidden and the client has some: `NetMessage.BootPlayer` with the
  reason;
- same mods (set, **order** and version): the client goes in;
- different mods and sync on: the host sends its list (C# packet kind 11) and
  refuses; with sync off, it refuses with the list of what is missing and what
  is extra.

Order matters because it is what gives item, NPC and projectile numbers. The
list already comes out sorted by dependencies, and the client's core, when
reordering (`mods/LoadOrder.cpp`), arrives at the same order: a list that
already respects the dependencies does not change. The version has to be
provided and match exactly: two empty versions are not considered compatible.
`1.1.0-beta` also differs from `1.1.0`.

The client receives the list and offers the sync through
`NativeApi.ServerSync`, which takes to Kotlin
(`dev.bunnyloader.game.ServerSync.offer`) the list, the address, the port, the
password, the character file and the enabled texture packs, and opens the
"Server mods" screen (`ServerSyncMenu.cs`).

The core reads the metadata from `manifest.json` or, in its absence,
`mod.json`, with the same priority as the launcher.

## The sync (ServerSync.kt)

Each mod becomes an action:

- the server's, installed at its version: goes in;
- the server's, in the store at its version: downloaded from the store (the
  online catalog is off for now, `RemoteCatalog.ENABLED`, so nothing is
  downloaded);
- the server's, neither: the "Server mods" screen lists what to install, and
  stops there;
- the player's, which the server does not have: left out this time.

The **host's exact version** applies, even when it is older than the
client's. With no version provided by the host, the package is also marked
missing. The planning check is in `ServerModSync`.

The screen is the game's, in place of the disconnect screen: it reads the
progress from `ServerSync.state` and answers with `apply` or `cancel`. Each mod
shows up with its action on the right (Download, Turn on, Turn off, Missing)
and, while downloading, with the percentage.

On accepting, it downloads, writes `files/server_session.json` (the server's
mods in its order, plus the player's texture packs, and the server) and
restarts through `RestartActivity`. On opening, `GameActivity` reads and
deletes the file (`takeSession`): the mods go to the core in place of the
player's selection, and `NativeConfig`'s `join*` fields make the title screen
pick the character and connect (`joinServer`, in `boot/QuickStart.cpp`,
without depending on Quick start). It applies to a single launch: the next
one is the player's.

Before replacing an installation, the downloaded ZIP's manifest must have the
expected uid and version, be a mod and use a supported format. After the
downloads, the manifests on disk are read again: the session is only written if
all of them still have the exact version and a valid entry file.

## Texts

The in-game texts of these screens come from
`app/src/main/assets/localization/game/<culture>.json`, in the game's
language: the launcher packs them into `NativeConfig.gameTexts` and the core
registers each key in Terraria's localization (`BunnyLoader.<Key>`; keys with a
dot, like `tModLoader.ModConfigAccepted`, as they are).

## What is not there yet

- A mod outside the store does not come from the host over the network: the
  player installs it by hand.
- The Host screen options are not saved between game launches.
- The introduction compares uid, version and order; it does not compare the
  content by hash. Two different packages published with the same version are
  not told apart by this protocol.
