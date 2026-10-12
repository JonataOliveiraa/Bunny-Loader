# _sprites/ — game sprites, by ID

Extracted from an image dump of Terraria 1.4.5 (items 6145 to 6195 and buffs
389 to 400 from mobile 1.4.5.8) and renamed to the **ID**, which is the key the
cheat menu has at hand:

```
item/98.png     Item_98.png    -> Minishark
npc/50.png      NPC_50.png     -> King Slime
buff/3.png      Buff_3.png     -> Regeneration
```

`item/` has 6134 files, `npc/` 697 and `buff/` 400 — 4 MB together. Each one's
name does NOT come from here: it comes from the game itself, through
Localization (`Lang.GetItemNameValue`), so it comes out in the device's
language and never goes stale.

Why it exists: the game's textures live only on the GPU. Measured on the
device — the item atlas is 2048x2048 with `isReadable = 0`,
`GetRawImageDataSize()` zero and `GetWritableImageData()` null, and the
workarounds (`Graphics.Blit`, `ReadPixels`, `GetNativeTexturePtr`) were
stripped from this binary by IL2CPP. There is no pixel to read at runtime;
either the GAME draws it, or the image comes from outside. This folder is the
"outside".

## This folder does not go into git

It is Terraria art. It is in `.gitignore`, like `_icons/` — only this README is
versioned. Gradle copies the content to `assets/sprites/` in the APK.
