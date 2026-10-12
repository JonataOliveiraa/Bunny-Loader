# The game freezing when entering the world (map save with mod tiles)

Round of 2026-09-30, on MuMu. Sometimes, right after entering the world, the
game froze on the "Drawing the map: 100%" screen: not a single extra frame,
every thread sleeping (on a futex), no crash. It showed up about one round in
three with several test mods together (`tilename`, `modfurniture`, `prefix`,
`boss`); alone, never.

## How it was found

- `debuggerd`'s stacks are useless on MuMu (the ARM code is translated): both
  `UnityMain` threads show up only as `<unknown>`.
- The log always stopped at the same point: the player save that runs right
  after entering (`saveToonWhilePlaying`) starts writing the map, our scan of
  the mod cells finishes, and nothing else. The game thread, on frame 1 or 2.
- A new log in the JS engine (`JsSuspend`'s wait over 5 s says who holds the
  engine) did not fire: it was not the engine.
- [`tools/bench/repeat.sh`](../../tools/bench/repeat.sh) repeats the round
  until it freezes and keeps the logcat.

## The cause

The hook on `MapHelper.InternalSaveMapCompressed` (the `.map` cannot carry a
mod tile index, see [`ModTileMap.cpp`](../../app/src/main/cpp/content/tiles/ModTileMap.cpp))
took the map's `LockObject` and held it during the whole save, so the game
thread would not write a mod cell into an already cleaned chunk.

But the game's save does not use `LockObject`: it locks **each chunk**
(`lock(Main.Map.Chunks[i])`), compresses and releases. With ours on the
outside, the save thread took `LockObject` → chunk; the game thread, touching
the map right after entering (`modfurniture` builds a house on frame 1), took
both in the opposite order. Each one waiting for the other.

Before finding this, a first fix (releasing `LockObject` before
`FileUtilities.WriteAllBytes`, where the game writes the file) did not solve
it: the inversion happened earlier, during compression.

## The fix

No lock of our own, chunk by chunk, like the game:

1. before the save, the chunks with mod cells are kept loaded and dirty (with
   each one's lock, the same as the game's), so the game compresses them
   again;
2. a hook on `WorldMapChunk.SaveCompressed`, only on the save thread and
   already inside the chunk's lock, removes the mod cells, compresses and puts
   them back (and marks the chunk dirty, so the next unload compresses it with
   them).

Checked: `tilemap` in the three rounds (the `.map` without the cells, the
`.map.bl` with them, and they come back) and 12 rounds of the mix that froze,
without freezing.

## For the next one

A hook that takes a game lock has to take **the same** one the game takes at
that point, and not a bigger one on the outside: the game thread's lock order
is the game's, and an extra lock around it inverts that order.
