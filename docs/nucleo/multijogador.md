# Joining a server: syncing mods and textures

The Host screen (Multiplayer > Host > the world) has two Bunny Loader options,
after the Password, in a scrolling list (`HostSettingsMenu`). Both start on
and apply while the game is open:

| Option | On | Off |
|---|---|---|
| Mod sync | Anyone joining with different mods is refused and offered a sync. | Joins as always (with different mods, it may freeze while loading the world). |
| Allow textures | Anyone joining can have texture packs enabled. | Anyone with a texture pack enabled is refused, with the reason. |

## The handshake

1. The client, before asking for the world (message 6), introduces itself:
   the loaded mods, in load order (uid, id, name, version), and how many
   texture packs it has enabled (`ServerSyncLoader`).
2. The introduction goes in a **message 68**, the client UUID one:
   `Main.clientUUID` becomes `bunnyloader-sync:` + JSON only while sending.
   The server refuses, with "invalid operation in this state", any message
   above 12 from someone still joining, except 16, 38, 42, 50, 68, 93, 147,
   161 and 164 (checked in mobile's `MessageBuffer.ProcessData`). The mod
   packets' 82 does not work here.
3. The host reads the 68 in a hook on `MessageBuffer.ProcessData`, installed
   only while hosting, and does not pass it on: the client's real UUID, which
   arrives in another 68, stays intact.
4. The host decides:
   - textures forbidden and the client has some: `NetMessage.BootPlayer` with
     the reason;
   - sync on and different mods (set, **order** or version): it sends its list
     (the `sync-plan` mod packet) and refuses. Order matters because it is what
     gives item, NPC and projectile numbers. The list already comes out sorted
     by dependencies, and the client's core, when reordering
     (`mods/LoadOrder.cpp`), arrives at the same order: a list that already
     respects the dependencies does not change. The version has to be
     provided and match exactly: two empty versions are not considered
     compatible. `1.1.0-beta` also differs from `1.1.0`.
5. The client receives the list and calls `bl.__offerServerSync(json)`, which
   takes to Kotlin (`dev.bunnyloader.game.ServerSync.offer`) the list, the
   address, the port, the password, the character file and the enabled texture
   packs.

The core reads the metadata from `manifest.json` or, in its absence,
`mod.json`, with the same priority as the launcher. That also applies to the
version sent in the introduction; accepting `mod.json` only in the launcher
left the version empty in the game.

## The sync (ServerSync.kt)

Each mod becomes an action:

- the server's, installed at its version: goes in;
- the server's, in the store at its version: downloaded from the store;
- the server's, neither: the "Server mods" screen lists what to install, and
  stops there;
- the player's, which the server does not have: left out this time.

The **host's exact version** applies, even when it is older than the
client's. If the store only has another version and the required one is not
installed, joining stays blocked. With no version provided by the host, the
package is also marked missing. The planning check is in `ServerModSync`.

The screen is the game's (`ServerSyncMenu`, in place of the disconnect
screen): it reads the progress from `bl.__serverSyncState()` and answers with
`bl.__serverSyncApply()` or `bl.__serverSyncCancel()`. Each mod shows up with
its action on the right (Download, Turn on, Turn off, Missing) and, while
downloading, with the percentage.

On accepting, it downloads, writes `files/server_session.json` (the server's
mods in its order, plus the player's texture packs, and the server) and
restarts through `RestartActivity`. On opening, `GameActivity` reads and
deletes the file (`takeSession`): the mods go to the core in place of the
player's selection, and `NativeConfig`'s `join*` fields make the title screen
pick the character and connect (`joinServer`, in `boot/QuickStart.cpp`,
without depending on Quick start). It applies to a single launch: the next
one is the player's.

Before replacing an installation, the downloaded ZIP's manifest must have the
expected uid and version, be a mod and use a supported format. That
validation is passed to the importer and runs before deleting the installed
package. After the downloads, the manifests on disk are read again: the
session is only written if all of them still have the exact version and a
valid entry file.

## What is not there yet

- A mod outside the store does not come from the host over the network: the
  player installs it by hand.
- The Host screen options are not saved between game launches.
- Without sync, different mods can still freeze the join.
- The introduction compares uid, version and order; it does not compare the
  content by hash. Two different packages published with the same version are
  not told apart by this protocol.

The regression tests, including two Android instances in multiplayer, are in
[tools/tests/serversync](../../tools/tests/serversync/README.md).
