# Weapon Out Lite

Port for **Bunny Loader** of *Weapon Out Lite* by **Flashkirby** (tModLoader,
MIT license). The item you hold shows on your character while you are not
using it: swords at the waist, axes and rifles on the back, bows on the
shoulder, books and staves in the off hand, magic foci floating. After you
attack (or get hurt), your character stays a few seconds in a **combat pose**
made for that kind of weapon, then puts the item away with an animation.

Other players with the mod see your item too.

## How it works

Every item falls into a **group** (melee weapon, spear, yoyo, bow, handgun,
automatic gun, staff, book, potion, large item, giant item...) by its sprite
size, use style and damage class, and every group has its own **pose** (58 to
choose from, from "Back" to "Combat: bolt-action rifle").

## Options

In **Mod Config** (pause menu):

- turn the mod on, show the held item, combat pose duration, combat pose always
  on or when hurt;
- the pose of each group (items, tools, melee, ranged, magic and giant items);
- item physics, the put-away animation, prefix scaling, the sizes that split
  one-handed from two-handed weapons, and the giant item size;
- the item in the player select menu, small yoyos and large spears (projectile
  sprites);
- the **next group** and **clear override** buttons for the held item.

## /wo command

The inventory eye button from PC became the `/wo` chat command:

| Command | What it does |
|---|---|
| `/wo` | shows or hides the held item |
| `/wo next` | moves the held item to the next group |
| `/wo pose <name>` | forces a pose on the held item (`/wo pose FloatingBack`) |
| `/wo clear` | removes the held item's override |
| `/wo reset` | resets all overrides |
| `/wo info` | the group and pose of the held item |
| `/wo poses` | the pose names |

Overrides are saved in `Android/data/com.bunnyloader/mod_data/<uid>/ItemOverrides.json`
(the item by its game name, like `EmpressBlade`, or `mod/Item`).

## For other mods

The original API is still there through `Call`, from `PostSetupContent` on:

```js
const wo = ModLoader.GetMod('weaponoutlite');
wo.Call('RegisterSpear', ModContent.ItemType(MySpear));           // or RegisterBow, RegisterGun, RegisterStaff...
wo.Call('RegisterItemHoldPose', ModContent.ItemType(MyItem), 'FloatingOffHandAimed');
wo.Call('HidePlayerHeldItem', player.whoAmI);                      // hides it this frame
```

`RegisterCustomItemStyle` with `RegisterCustomDrawDepth`,
`RegisterCustomUpdateIdleBodyFrame` and `RegisterCustomPreDrawData` draw an item
your own way; in `RegisterCustomPreDrawData`, `data` is an object with
`position`, `origin`, `rotation`, `texture`, `sourceRect`, `color`, `scale` and
`effect` (and `move(x, y)`), like the original `DrawData`.

## Differences from the original

- The animated previews of the PC config screen are not here: poses are picked
  by name.
- Detection of backwards arrows from other mods and the experimental swing
  effects are left out.
- While sleeping in a bed the item is not shown.
- The integrations with PC mods (Calamity, Thorium, Overhaul...) do not apply.

Available in English, Portuguese, Spanish, French, German, Italian, Polish,
Russian, Chinese (Simplified and Traditional), Japanese and Korean.
