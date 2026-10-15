# What each class has today

This reference describes the contracts, fields and shortcuts of the mod
classes. The [catalog of methods by class](metodos.md) is the complete index of
the public JavaScript declarations, including signatures, inheritance and
aliases. It is also available as an [Excel spreadsheet](metodos.xlsx) and a
[CSV](metodos.csv). The guides explain how to use them; this page is for
looking things up.

For each method you write, the table says **when it runs** and **which game
method is behind it**, with the native filter, if any. That is what decides the
cost (see the [cost guide](../mods/03-custo-e-desempenho.md)):

- **`type` filter**: the hook only enters JS for **mod** content; vanilla
  content passes straight through, almost for free;
- **no filter**: enters JS on every call, and the class checks the type in
  there;
- **native**: not a JS hook; the C++ core calls the method.

Content hooks are installed on demand, according to the overridden methods.
There is shared infrastructure and permanent hooks, like
`Player.GetHairSettings`; that is why the number of methods available in the
API does not match the number of JavaScript entries per frame.

Every class is **global**: no `import`. Every class exported by the entry
file or by a file in `Content/` and `Common/` registers itself
(`static Autoload = false` leaves it out, like a
[base class](../mods/04-conteudo-novo.md#your-mods-base-classes-tmodloaders-abstract-ones)
others extend); each one's `register` is for registering by hand, in the mod's
`Load()`.

| Class | What for | Guide |
|---|---|---|
| [`ModItem`](#moditem) | A new item. | [5](../mods/05-itens.md) |
| [`ModPrefix`](#modprefix) | A new prefix (modifier). | [5](../mods/05-itens.md#prefixes) |
| [`ModProjectile`](#modprojectile) | A new projectile. | [6](../mods/06-projeteis.md) |
| [`ModNPC`](#modnpc) | A new NPC, boss or town NPC. | [7](../mods/07-npcs.md) |
| [`ModPlayer`](#modplayer) | Per-player data and behavior. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModBuff`](#modbuff) | A new buff or debuff. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModMount`](#modmount) | A new mount (car, minecart). | — |
| [`ModCommand`](#modcommand) | A chat command (`/name args`). | — |
| [`ModTouchButton`](#modtouchbutton) | A button of your own in the game's touch controls (tModLoader's `ModKeybind`). | [18](../mods/18-botoes-de-toque.md) |
| [`ModHair`](#modhair) | A new hairstyle (character creation and the Stylist). | — |
| [`ModCloud`](#modcloud) | A new cloud in the sky (common or rare). | — |
| [`ModEmoteBubble`](#modemotebubble) | A new emote (bubble and emote menu). | — |
| [`ModAchievement`](#modachievement) | A new achievement. | — |
| [`ModRarity`](#modrarity) | A new rarity (the item name's color). | [5](../mods/05-itens.md#mod-rarity) |
| [`ModTile`](#modtile) | A new block, or furniture with `TileObjectData`. | [9](../mods/09-blocos.md) |
| [`ModWall`](#modwall) | A new wall. | [9](../mods/09-blocos.md#walls-modwall) |
| [`GlobalItem`, `GlobalNPC`, `GlobalProjectile`](#globalitem-globalnpc-and-globalprojectile) | Changing the game's items, NPCs and projectiles. | [12](../mods/12-globais-e-mundo.md) |
| [`GlobalLoot`](#globalloot) | Drops that apply to every NPC. | [12](../mods/12-globais-e-mundo.md#drops) |
| [`ModSystem`](#modsystem) | What belongs to the whole mod; the world cycle and the data saved in it. | [12](../mods/12-globais-e-mundo.md#modsystem-the-world) |
| [`ModBiome`, `ModSceneEffect`](#modbiome-and-modsceneeffect) | Mod biome and scene effect (the music, the backgrounds, the water and the map background by priority). | — |
| [`ModSurfaceBackgroundStyle`, `ModUndergroundBackgroundStyle`](#mod-backgrounds) | Surface and underground backgrounds, with the `BackgroundTextureLoader` textures. | — |
| [`ModWaterStyle`, `ModWaterfallStyle`](#mod-water) | A biome's water and waterfall. | — |
| [`ModMenu`](#modmenu) | Title screen theme: logo, sun, moon and background. | — |
| [`TagCompound`](#tagcompound) | The data the mod saves (world, player). | [12](../mods/12-globais-e-mundo.md#data-saved-with-the-world) |
| [`ModPacket`, `NetWriter`, `NetReader`](#network) | Mod data over the network. | [12](../mods/12-globais-e-mundo.md#network) |
| [`Mod`, `ModLoader`](#mod-and-modloader) | The mod itself; talking between mods. | [11](../mods/11-conversa-entre-mods.md) |
| [`ModContent`](#modcontent) | Type, template and texture by name or by class. | [4](../mods/04-conteudo-novo.md#modcontent) |
| [`ModConfig`](#modconfig) | The mod's persistent options, changed in the pause menu and in the main menu's Settings. | [14](../mods/14-opcoes-do-mod.md) |
| [`DamageClass`, `StatModifier`, `StatInheritanceData`](#damageclass) | Damage classes, stat modifiers and bonus inheritance. | [15](../mods/15-classes-de-dano.md) |
| [`HitModifiers`, `HitInfo`, `HurtModifiers`, `HurtInfo`](#hits) | The hit on an NPC or a player, before (`ModifyHit*`) and after (`OnHit*`). | [16](../mods/16-golpes.md) |
| [`ModScreenEffect`, `ScreenEffects`](#screen-effects) | A screen effect (GLSL post-processing between the world and the interface) and the manager of the live ones; `ScreenPresets`, `ScreenGraphics` and `ScreenUtils` are its tools. | [17](../mods/17-efeitos-de-tela.md) |
| [`ModRecipe`](#modrecipe) | Recipes and recipe groups. | [5](../mods/05-itens.md#recipes) |
| [`NPCLoot`, `NPCSpawnInfo`, `NPCShop`, `NPCHappiness`, `ModGore`](#npc-helpers) | Drops, spawning, shop, happiness, gore. | [7](../mods/07-npcs.md) |
| [`TooltipLine`, `DrawableTooltipLine`](#tooltipline) | A tooltip line, and the line while drawing. | [5](../mods/05-itens.md#tooltip) |
| [`ModLocalization`](#modlocalization) | Localized texts. | [4](../mods/04-conteudo-novo.md#localization) |
| [`SoundStyle`, `SoundEngine`, `MusicLoader`](#sound-and-music) | Sound and music. | [10](../mods/10-sons-e-musica.md) |
| [Helpers](#helpers) | `Vector2`, `Color`, `Rand`, `Ref`... | [4](../mods/04-conteudo-novo.md#helpers) |

---

## ModItem

A new item. The class is the **template**; each game item of that type gets
its own instance, with `this.Item` pointing to it.

### Fields

| Field | Type | What for |
|---|---|---|
| `Item` | game `Item` | This instance's item. On the template, `undefined`. |
| `Type` | number | This item's type (`ItemID`), after `register`. |
| `Mod` | `Mod` | The mod that registered it. |
| `Texture` | text | The path in the mod, without `.png` (also `get Texture()`). Default: the class's file (`Content/Items/X.js` -> `Content/Items/X`). In older mods, the mirror in `Assets/Textures/`. |
| `DisplayName` | text or `{ culture: text }` | The name. Empty: `ItemName.<Class>` in `Localization/*.json`, and without that the class name. |
| `Tooltip` | text or `{ culture: text }` | The description, lines split by `\n`. Empty: `ItemTooltip.<Class>`. |
| `TooltipLines` | array of text | The lines, for `ModifyTooltipLines` to change. |
| `HideFromModMenu` | `boolean` | `true`: left out of the Mod Menu. |
| `Item.ResearchUnlockCount` | number | How many Journey mode asks for to research it (per type). Mod items start at 1; `0` takes the item out of research. Set it in `SetStaticDefaults` (`this.Item.ResearchUnlockCount = 3`); it also works on any item, the game's included. |

### Methods you write

| Method | When it runs | Behind it |
|---|---|---|
| `SetStaticDefaults()` | Once, with the type already in the game's tables. | native |
| `PostStaticDefaults()` | Right after `SetStaticDefaults`. | native |
| `SetDefaults(item)` | Every item of this type that spawns. | native (`Item.SetDefaults`) |
| `PostSetDefaults(item)` | Right after `SetDefaults`. | native |
| `Clone(newItem)` | The game copied the item (`Item.Clone`); return the copy's instance. | `Item.Clone`, `type` filter |
| `SaveData(tag)` | Write the instance's data into the `TagCompound`; the return value is ignored. Also the network fallback. | C++: character save; `ChestItem.SetToItem` conversion |
| `LoadData(tag)` | Read the instance's data; old tags may be empty. | C++: character load; `ChestItem.ExpandItem`; sync |
| `NetSend(writer)` | Write data with `NetWriter`. Takes precedence over the automatic `SaveData` sending. | Messages 5, 21, 32 and 90 |
| `NetReceive(reader)` | Read in the same order `NetSend` wrote. | `MessageBuffer.ProcessData`, before the server relays |
| `AddRecipeGroups()` | Once, before any recipe from any mod. | content ready |
| `AddRecipes()` | Once, with the game's recipes ready. | content ready |
| `PostSetupContent()` | Once, with all mod content in the game. | content ready |
| `ModifyTooltipLines()` | Once per language, with `this.TooltipLines` filled in. | on `register` |
| `ModifyTooltips(item, tooltips)` | Every time the tooltip shows up, and in the crafting guide popup (without color). | `Main.MouseText_DrawItemTooltip` (JS port, only for the item that asks; + `DrawString` with `whileIn`); `GetLinesInfo` with `whileIn` in `GUICraftGuidePopup.UpdateText` |
| `PreDrawTooltip(item, lines, x, y)` | Before the lines; `x`, `y` are `Ref`s; `false` does not draw the lines. | same |
| `PostDrawTooltip(item, lines)` | After all the lines (`DrawableTooltipLine`). | same |
| `PreDrawTooltipLine(item, line, yOffset)` | Before each line; `yOffset` (`Ref`) adds after each following line; `false` does not draw the line. | same |
| `PostDrawTooltipLine(item, line)` | After each line (including one not drawn). | same |
| `OnCraft(item, player, recipe)` | When crafting the item from the crafting menu. | `Main.CraftItem_GrantItem`, `type` filter |
| `CanUseItem(item, player)` | Before use; `false` prevents it. | `Player.ItemCheck_CheckCanUse_Inner`, `type` filter |
| `UseItem(item, player)` | On the frame the use starts. `true`: the item counts as used (use time applied, like tModLoader's `ApplyItemTime`), and a consumable is spent. | `Player.ItemCheck_StartActualUse`, `type` filter; the time, right after `ItemCheck_OwnerOnlyCode` |
| `CanRightClick(item)` | `true`: the item opens in the inventory (bag, crate); goes into `ItemID.Sets.OpenableBag`. On touch, it gets the "Open" button and the use buttons are locked, as with the game's bags. | `ItemSlot.TryOpenContainer_GrantItems`, `GUIPageOptions.CanBeOpened`, `VirtualControllerInputState.GetItemCategory`, `type` filter |
| `RightClick(item, player)` | On opening, before `ItemLoot`. | same |
| `ModifyItemLoot(itemLoot)` | Once, on the first opening: what comes out of the item (`itemLoot.Add(rule)`). The game's rules and `ItemDropRule.CoinsBasedOnNPCValue(npc)` / `ItemDropRule.Coins(value)`; what would drop at the NPC goes to the player (`QuickSpawnItem`). | same |
| `ConsumeItem(item, player)` | `false`: opening does not spend the item. | same |
| `IsQuestFish()` | `true`: an Angler quest fish (the same as `ItemID.Sets.IsQuestFish[this.Type] = true` in `SetStaticDefaults`). Goes at the end of `Main.anglerQuestItemNetIDs`. | in `Ready` |
| `IsAnglerQuestAvailable()` | `false`: the daily roll does not land on it (it rolls again). | `Main.AnglerQuestSwap` |
| `AnglerQuestChat(description, catchLocation)` | The Angler's line (`Ref`): `description.value` and `catchLocation.value` (shown in parentheses below). Without it, the item's name. | `Lang.AnglerQuestChat` |
| `HoldItem(item, player)` | Every frame with the item in hand. | `Player.ItemCheck_ApplyUseStyle`/`ApplyHoldStyle`, `type` filter |
| `UseStyle(item, player, mountOffset, frame)` | Every frame of use, after the game's style. | same |
| `HoldStyle(item, player, mountOffset, frame)` | Every frame while held, after the game's style. | same |
| `UseAnimation(item, player)` | Before the native calculation of the animation duration. | `Player.ApplyItemAnimation`, per-type marks and a point shared with ModPlayer |
| `UseItemFrame(item, player)` | After the native frame, during the use animation. | `Player.PlayerFrame`, native filter by the selected item |
| `HoldItemFrame(item, player)` | After the native frame, idle and with the item visually allowed. | same |
| `GetHealLife(item, player, quickHeal, healValue)` | Life restored (`Ref`), with a positive base heal; also in QuickHeal's selection. | `Player.ApplyLifeAndOrMana` and `QuickHeal_GetItemToUse_TryChoosingItem`, per-type marks |
| `GetHealMana(item, player, quickHeal, healValue)` | Mana restored (`Ref`), with a positive base heal. | `Player.ApplyLifeAndOrMana`, per-type marks |
| `ModifyPotionDelay(item, player, baseDelay)` | Delay computed by the mobile game (`Ref`), before the veto and the application. | `Player.ApplyPotionDelay`, `AddBuff` with `whileIn` |
| `ApplyPotionDelay(item, player, potionDelay)` | `false`: prevents the counter and the potion sickness buff; keeps healing and consumption. | same, shared with ModPlayer |
| `NeedsAmmo(item, player)` | `false`: can shoot with the temporary default ammo when there is no candidate. | `Player.HasAmmo` and `PickAmmo`, per-type marks |
| `CanChooseAmmo(item, ammo, player)` | Weapon: `false` rejects; `true` allows another category; `null` keeps the native category. | `Player.PickAmmo_IterateRange`, per-type marks |
| `CanBeChosenAsAmmo(item, weapon, player)` | Ammo: combines into the selection; any veto wins. | same, including for vanilla weapons |
| `CanConsumeAmmo(item, ammo, player)` | Weapon: `false` keeps the stack without preventing the shot. | `Player.PickAmmo`, shared with ModPlayer |
| `CanBeConsumedAsAmmo(item, weapon, player)` | Ammo: `false` keeps the stack. | same |
| `OnConsumeAmmo(item, ammo, player)` | Weapon: actual consumption, with the stack already reduced and before clearing the type. | same |
| `OnConsumedAsAmmo(item, weapon, player)` | Ammo: actual consumption, including the last unit. | same |
| `PickAmmo(item, weapon, player, type, speed, damage, knockback)` | Ammo: three `Ref<number>`s and the `StatModifier` of mobile's total damage. | after the native `Player.PickAmmo`, before it returns |
| `HoldoutOffset(item, player)` | Return `{ X, Y }`: shifts the weapon in hand. | same |
| `CanShoot(item, player)` | `false`: uses, but does not shoot. | `Player.ItemCheck_Shoot`, `type` filter |
| `ModifyShootStats(item, player, stats)` | Before each projectile: `stats.position`, `velocity`, `type`, `damage`, `knockBack`. | same + `Projectile.NewProjectile` (no filter; only acts during the shot) |
| `Shoot(item, player, position, velocity, type, damage, knockBack)` | `false`: the game's projectile does not spawn. | same |
| `ModifyWeaponDamage(item, player, damage)` | `StatModifier`; also accepts a numeric return. | `Player.GetWeaponDamage`, per-type marks |
| `ModifyWeaponCrit(item, player, crit)` | `crit` is a `Ref<number>`. | `Player.GetWeaponCrit`, per-type marks |
| `ModifyWeaponKnockback(item, player, knockback)` | `knockback` is a `StatModifier`. | `Player.GetWeaponKnockback`, per-type marks |
| `ModifyItemScale(item, player, scale)` | `scale` is a `Ref<number>` of the multiplier, including the native glove. | `Player.GetAdjustedItemScale` and `ItemCheck_GetMeleeHitbox`, per-type marks |
| `CanHitNPC(item, player, target)` | `false` vetoes; `true` allows eligibility; `null` keeps the game's. | `Player.ProcessHitAgainstNPC` and `CanNPCBeHitByPlayerOrPlayerProjectile` |
| `ModifyHitNPC(item, player, target, modifiers)` | `modifiers` is a `HitModifiers` ([Hits](../mods/16-golpes.md)). | `NPC.StrikeNPC`, item context |
| `CanHitPvp(item, player, target)` | On the attacker; `false` prevents the hit before status and sending. | `Player.CanHit`, during `ItemCheck_MeleeHitPVP` |
| `ModifyHitPvp(item, player, target, modifiers)` | On the victim; `modifiers` is a `HurtModifiers` ([Hits](../mods/16-golpes.md)). | `Player.Hurt`, including when received over the network |
| `OnHitPvp(item, player, target, hurtInfo)` | On the victim, only after positive damage, with the `HurtInfo` of the applied result. | `Player.Hurt`'s return, including when received over the network |
| `CanMeleeAttackCollideWithNPC(item, player, hitbox, target)` | `false` vetoes; `true` allows the collision; `null` keeps the game's. | `Player.ProcessHitAgainstNPC`, `Rectangle.Intersects` with a flag and `whileIn` |
| `MeleeEffects(item, player, hitbox)` | After the native use visuals. | `Player.ItemCheck_EmitUseVisuals`, per-type marks |
| `UseItemHitbox(item, player, hitbox, noHitbox)` | `Ref<Rectangle>` and `Ref<boolean>` after the native hitbox. | `Player.ItemCheck_GetMeleeHitbox`, per-type marks |
| `OnHitNPC(item, player, target, hit, damageDone)` | After the melee hit; `hit` is a `HitInfo`. | `NPC.StrikeNPC` (CombatLoader), `type` filter |
| `UpdateEquip(item, player)` | Every frame, equipped (armor or accessory). | `Player.ApplyEquipFunctional`, `GrantArmorBenefits`, `type` filter |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Every frame, accessory equipped (also in the vanity slot). | `Player.ApplyEquipFunctional`, `ApplyEquipVanity`, `type` filter |
| `IsArmorSet(head, body, legs)` | For each worn mod piece: `true` = complete set. | `Player.UpdateArmorSets`, no filter (resets `setBonus` first) |
| `UpdateArmorSet(item, player)` | The set bonus; the text goes in `player.setBonus`. | same |
| `VerticalWingSpeeds(item, player, falling, rising, maxCan, maxAscent, constant)` | This item's wings in the air (five `Ref`s). | `Player.WingMovement`, filter `wingsLogic` ≥ the game's `Count` |
| `HorizontalWingSpeeds(item, player, speed, acceleration)` | Running in the air with these wings (two `Ref`s). | after `Player.WingAirLogicTweaks`, `wingsLogic` filter |
| `WingUpdate(player, inUse)` | `true`: the mod animates the wings (the game's `WingFrame` does not run). | `Player.WingFrame`, `wings` filter |
| `IsVanitySet(head, body, legs)` | The drawn SLOTS; `true` = vanity set (default: the `IsArmorSet` of those slots' items). | through each slot's texture |
| `PreUpdateVanitySet(player)`, `UpdateVanitySet(player)` | Before and after `FrameEffects`, with the vanity set. | after `Player.PlayerFrame` |
| `ArmorSetShadows(player)` | The set's shadows (`armorEffectDraw*`). | after `Player.SetArmorEffectVisuals` |
| `SetMatch(male, equipSlot, robes)` | This piece's drawn slot (`Ref`); the robe sets `robes` and the legs. | after `Player.SetMatch` |
| `EquipFrameEffects(player, type)` | Every frame, with the item's texture worn. | after `Player.PlayerFrame` |
| `UpdateVanity(item, player)` | Accessory in the vanity slot. | `Player.ApplyEquipVanity` |
| `Load()` | On registration (`AddEquipTexture` goes here). | registration |
| `UpdateInventory(item, player)` | Every frame, in the inventory. | `Player.UpdateEquips`, no filter (only walks the player's mod items) |
| `GetAlpha(item, lightColor)` | Return the drawing `Color`. On the ground `item` is the `WorldItem` (with `Center`); in the inventory, the `Item`, without a position. | `Item.GetAlpha`, `type` filter |
| `PostUpdate(item)` | Every frame, with the item on the ground (`item` is the `WorldItem`). Light, dust. | `WorldItem.UpdateItem`, `inner.type` filter |
| `PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI)` | `item` is a `WorldItem`; rotation and scale are `Ref`s. `false` skips the vanilla drawing. | internal step of `Main.DrawItem`, `inner.type` filter |
| `PostDrawInWorld(item, spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI)` | After drawing, even with a veto; rotation and scale are numbers. | final step of `Main.DrawItem` |
| `PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale)` | Receives the frame and scale computed by the game; `false` skips the vanilla drawing. | internal step of `ItemSlot.DrawItemIcon`, `type` filter |
| `PostDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale)` | After drawing, even with a veto. | final step of `ItemSlot.DrawItemIcon` |
| `ModifyFishingLine(item, bobber, lineOriginOffset, lineColor)` | Rod in hand, for each bobber: where the line comes from and its color (two `Ref`s). | `Main.DrawProj_FishingLine`, no filter |
| `CanPickup(worldItem, player)` | `false`: the player neither picks up nor pulls the item from the ground. | `Player.CanAcceptItemIntoInventory(Item)`, which `GrabItems` and the reservation (`WorldItem.FindOwner`, through `CanPullItem`) call; `type` filter |
| `OnPickup(worldItem, player)` | On pickup, on the picker's device; `false`: the item disappears without entering the inventory (heart, orb). | `Player.PickupItem`, `inner.type` filter |
| `ItemSpace(item, player)` | `true`: picks it up even with a full inventory (together with `OnPickup`, which consumes it). | `Player.ItemSpace(Item, ref status)` (`GrabItems`) and `Player.ItemSpace(Item)` (the reservation), `type` filter |
| `GrabRange(worldItem, player, grabRange)` | The pickup range in pixels (`Ref`, over the game's). | `Player.GetItemGrabRange(WorldItem)` (the reservation) and, inside `GrabItems`, `CanAcceptItemIntoInventory`: on mobile the range math is baked into `GrabItems`, so a larger range pulls from outside it and a smaller one refuses the item (touching it still picks it up) |
| `GrabStyle(worldItem, player)` | `true`: the mod moves the item to the player (change `velocity`) and the game does not pull it. | `Player.PullItem_Common`/`PullItem_Pickup`, `inner.type` filter |
| `MeleePrefix(item)`, `WeaponPrefix(item)`, `RangedPrefix(item)`, `MagicPrefix(item)`, `SummonPrefix(item)` | The item's prefix categories (default: `melee` without `noUseGraphic`, `melee` with it, `ranged`, `magic`, `summon`). Only applies to items with damage that are neither ammo nor consumables. Read once per type. | `Item.GetRollablePrefixes`, no filter |
| `ChoosePrefix(item, rand)` | A prefix forced when rolling (> 0), or -1. | `Item.RollAPrefix`, no filter |
| `PrefixChance(item, pre, rand)` | `false` prevents, `true` forces a prefix; `null` = the game's (`pre`: -1 crafting/chest, -2 reforge). | `Item.Prefix`, no filter |
| `AllowPrefix(item, pre)` | `false` removes that prefix from the options. | `Item.GetRollablePrefixes` |
| `ApplyPrefix(item, pre)` | After the prefix's stats. | `Item.Prefix`, no filter |

The full contracts for the modifiers, the order between classes and the PvP
limits are in [ModItem combat](moditem-combate.md). Selection, consumption
and the mobile adaptations are in [ModItem use and ammo](moditem-uso-municao.md).

### Shortcuts

| | |
|---|---|
| `CloneDefaults(type)` | Copies a vanilla item's values. |
| `SetWeaponValues(damage, knockback, crit)` | |
| `SetDefaultWeaponStyle(useTime, autoReuse)` | `useTime`, `useAnimation`, `autoReuse` and the matching `useStyle`. |
| `SetShopValues(rarity, price)` | |
| `DefaultToPlaceableTile(tileType, style)` | An item that places a block. |
| `DefaultToFood(buff, time, drink, animation)` | Food. |
| `DefaultToWhip(proj, damage, knockback, speed, animation)` | Whip. |
| `DefaultToSpear(proj, speed, animation)` | Spear. |
| `DefaultToGolfBall(proj)` | Golf ball. |
| `SetItemAnimation(frames, ticks, pingPong)` | Animated item (vertical strip). In `SetStaticDefaults`. |
| `CreateRecipe(amount)` | A `ModRecipe` that gives this item. |
| `CreateRecipeGroup(types)` | A group named after the first item. |
| `SetWingStats(time, speed, acceleration, hover, hoverSpeed, hoverAcceleration)` | The item's wings' WingStats. In `SetStaticDefaults`. |
| `static AutoloadEquip = [EquipType.Head, ...]` | Only these worn textures by name; `[]` none. |

### Statics

| | |
|---|---|
| `ModItem.register(Class)` | Registers by hand (an exported class already registers itself); returns the type. |
| `ModItem.getTypeByName('Class')` | The type of an item from **this** mod; -1 if there is none. |
| `ModItem.getModItem(type)`, `ModItem.getByName('Class')` | The template. |
| `ModItem.isModType(type)`, `ModItem.isModItem(item)` | Is it mod content? |
| `ModItem.CommonMaxStack` | 9999. |
| `ModItem.sellPrice(pl, gold, silver, copper)`, `buyPrice(...)` | Price in copper. |

### Not yet

From tModLoader's `ModItem`, among others: `DrawArmorColor`/`ArmorArmGlowMask`,
`WingGlidingSpeeds`, `ModifyEquipTextureDraw`. Custom draw layers
([`PlayerDrawLayer`](playerdrawlayers.md)) can already be registered and
drawn.

### Worn textures: EquipLoader and EquipTexture

| | |
|---|---|
| `<Texture>_<type>.png` | The texture on the body (`EquipType`: `Head`, `Body`, `Legs`, `HandsOn`, `HandsOff`, `Back`, `Front`, `Shoes`, `Waist`, `Wings`, `Shield`, `Neck`, `Face`, `Beard`, `Balloon`). |
| `EquipLoader.AddEquipTexture([mod,] texture, type, item, name, equipTexture)` | Registers by hand, in `Load()`. |
| `EquipLoader.GetEquipSlot([mod,] name, type)` | The slot; -1 if there is none. From `SetStaticDefaults` on. |
| `EquipLoader.GetEquipTexture(type, slot)` | The slot's `EquipTexture`. |
| `class X extends EquipTexture` | `FrameEffects`, `IsVanitySet`, `PreUpdateVanitySet`, `UpdateVanitySet`, `ArmorSetShadows`, `SetMatch`, `VerticalWingSpeeds`, `HorizontalWingSpeeds`, `WingUpdate` for a single texture (default: the owner item's). |
| `ArmorIDs.Head.Sets.DrawHead`, `ArmorIDs.Body.Sets.HidesTopSkin`/`HidesBottomSkin`/`HidesHands`/`HidesArms`, `ArmorIDs.Legs.Sets.HidesTopSkin`/`HidesBottomSkin` | tModLoader's (they do not exist in this game), applied when drawing. |

Hair is decided by the Sets in `Terraria.ID`. The loader replaces
`Player.GetHairSettings` with a lookup of these tables:

| Set | Effect |
|---|---|
| `ArmorIDs.Head.Sets.DrawFullHair[headSlot]` | Allows drawing the full hair with that equipment. |
| `ArmorIDs.Head.Sets.DrawHatHair[headSlot]` | Allows drawing the hat hair. |
| `ArmorIDs.Face.Sets.PreventHairDraw[faceSlot]` | Hides the hair because of the face equipment. |
| `ArmorIDs.Head.Sets.DrawsBackHairWithoutHeadgear[headSlot]` | Sets the matching back hair flag. |
| `HairID.Sets.DrawBackHair[hair]` | Sets whether the hairstyle draws hair behind. |

For a hat that keeps the hat hair visible, set the already registered slot in
`SetStaticDefaults`:

```js
SetStaticDefaults() {
    Terraria.ID.ArmorIDs.Head.Sets.DrawHatHair[this.Item.headSlot] = true;
}
```

The hiding rule also considers `player.faceHead >= 0` with
`player.head !== 0`. The [hair and hat tests](../../tools/tests/hairsettings/check.mjs)
check how the flags combine.

---

## ModPrefix

A new prefix (modifier), like tModLoader's `ModPrefix`. One instance per
prefix; `Type` is the number the item stores in `item.prefix` (after the
game's 98, up to 255: the game stores the prefix in 1 byte). Guide:
[5 · Prefixes](../mods/05-itens.md#prefixes).

### Fields

| Field | Type | What it is |
|---|---|---|
| `Type` | number | The prefix (read-only). |
| `Mod`, `Name`, `FullName` | | The mod, the class name, `'<mod id>/<Class>'`. |
| `DisplayName` | text or `{ culture: text }` | The name. Empty: `PrefixName.<Class>` (or `Prefixes.<Class>.DisplayName`, as in tModLoader) in `Localization/*.json`, and without that the class name. |
| `Category` | `PrefixCategory` | Which items can get it (default `Custom`). Write it as a getter: `get Category() { return PrefixCategory.AnyWeapon; }`. |

`PrefixCategory`: `Melee`, `Ranged`, `Magic`, `Summon`, `AnyWeapon`,
`Accessory`, `Custom` (does not roll on its own: only through the item's
`ChoosePrefix` or `item.Prefix(type)`).

### Methods you write

| Method | When it runs | Behind it |
|---|---|---|
| `SetStaticDefaults()` | Once, with content ready and the tables grown (`PrefixID.Sets.ReducedNaturalChance[this.Type]`). | content ready |
| `RollChance(item)` | The weight in the roll (each vanilla prefix weighs 1). | `Item.RollAPrefix`, no filter |
| `CanRoll(item)` | `false`: this item cannot get it (default: `RollChance > 0`). | `Item.GetRollablePrefixes`, `CanRollPrefix` |
| `SetStats(damageMult, knockbackMult, useTimeMult, scaleMult, shootSpeedMult, manaMult, critBonus, tagDamage, armorPenetration)` | The stats, as `Ref`s (`damageMult.value *= 1.2`). With a single parameter, it receives `{ damage, knockBack, speed, size, shootSpeed, mana, crit, tagDamage, armorPenetration }` (TL's ExMod form). The game applies and checks them, like its own: if some stat does not actually change on the item, the prefix does not take. | `Item.TryGetPrefixStatMultipliersForItem`, filter: mod prefix |
| `AllStatChangesHaveEffectOn(item)` | `false`: does not take on this item (for stats that are not the game's). | same |
| `ModifyValue(valueMult)` | The price (`Ref`); the rarity goes up or down with it. | same |
| `Apply(item)` | After the stats: whatever else the prefix changes on the item. | `Item.Prefix`, no filter |
| `ApplyAccessoryEffects(player)` | Every frame, with the accessory equipped. | `Player.GrantPrefixBenefits`, filter: mod prefix |
| `GetTooltipLines(item)` | Extra lines (an array of `TooltipLine`; `IsModifier = true` paints it green). The game already writes the damage, speed, crit etc. ones. | `Main.MouseText_DrawItemTooltip_GetLinesInfo` |

### Statics and PrefixLoader

| | |
|---|---|
| `ModPrefix.register(Class)` | Registers by hand; returns the type. |
| `ModPrefix.getTypeByName('Class')`, `getModPrefix(type)`, `isModType(type)` | |
| `PrefixLoader.GetPrefix(type)` | The `ModPrefix`, or `undefined` (a vanilla prefix). |
| `PrefixLoader.GetPrefixesInCategory(category)`, `PrefixLoader.PrefixCount` | |
| `PrefixLoader.Categories(item)`, `PrefixLoader.VanillaPrefixes(category)` | The item's categories; a category's vanilla prefixes. |

### Saving

The `.plr`/`.wld` store the prefix in 1 byte, and a mod prefix's number
depends on the installed mods and the load order. That is why it also goes
**by name** into `<character>.plr.bl` and `<world>.wld.bl` (inventory,
equipment, storage, loadouts, trash and chests), and comes back right with
other mods before it. Without the mod, the item has no prefix and the name
stays stored as long as the item does not move; with the mod back, the prefix
comes back. Mannequins, hat racks, weapon racks and items on the ground only
keep the number.

### Not yet

`ModItem.ReforgePrice`/`CanReforge`/`PreReforge`/`PostReforge`, tModLoader's
"unloaded" prefix (`UnloadedPrefix`). Mobile's reforge window
(`GUIReforgePopup`) builds its own lines separately and was not checked:
`GetTooltipLines` may not show up there (it does in the inventory).

---

## ModProjectile

A new projectile. Like the item: a template and one instance per projectile
(`this.Projectile`).

### Fields

| Field | Type | What for |
|---|---|---|
| `Projectile` | game `Projectile` | This instance's projectile. |
| `Type`, `Mod` | | The type and the mod. |
| `Texture` | text | The path in the mod, without `.png`; default, next to the class's file. |
| `DisplayName` | text or `{ culture: text }` | Empty: `ProjectileName.<Class>`. |
| `AIType` | number | Uses this vanilla projectile's AI (the type is swapped only during the AI). |

### Methods you write

| Method | When it runs | Behind it |
|---|---|---|
| `SetStaticDefaults()` | Once. `Main.projFrames[this.Type] = n` here counts as the texture's frames. | native |
| `SetDefaults(proj)`, `PostSetDefaults(proj)` | Every projectile of this type that spawns. | native (`Projectile.SetDefaults`) |
| `PostStaticDefaults()`, `PostSetupContent()` | Once. | native / content ready |
| `Clone(newProjectile)` | A copy of the instance. | |
| `OnSpawn(proj)` | Once, on the first frame of life. | `Projectile.AI`, `type` filter |
| `PreAI(proj)` | Before the AI; `false` skips the game's AI and `AI`. | same |
| `AI(proj)` | Every frame. | same |
| `PostAI(proj)` | After the AI. | same |
| `OnTileCollide(proj, oldVelocity)` | Hit a tile and was about to die; `false` keeps it alive. | `Projectile.HandleMovement` + `Kill`, `type` filter |
| `TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac)` | Four `Ref`s: collision size, falling through platforms and center. `false` skips collision for this movement. | `Projectile.HandleMovement`/`GetCollisionParams`, filter by type and override |
| `PreKill(proj, timeLeft)` | Before dying; `false` removes the game's effects. | `Projectile.Kill`, `type` filter |
| `OnKill(proj, timeLeft)` | On death. | same |
| `CanHitNPC(proj, target)` | `false` vetoes, `true` forces, `null` leaves it to the game (player projectiles). | `Player.CanNPCBeHitByPlayerOrPlayerProjectile` (CombatLoader) |
| `ModifyHitNPC(proj, target, modifiers)` | `modifiers` is a `HitModifiers` ([Hits](../mods/16-golpes.md)). | `Projectile.Damage_PVE` + `NPC.StrikeNPC` |
| `OnHitNPC(proj, target, hit, damageDone)` | After the hit; `hit` is a `HitInfo`. | same |
| `CanHitPlayer(proj, target)`, `CanHitPvp(proj, target)` | `false` vetoes the hit on the player (hostile / PvP). | `Player.Hurt` (CombatLoader) |
| `ModifyHitPlayer(proj, target, modifiers)` | `modifiers` is a `HurtModifiers`. | same |
| `OnHitPlayer(proj, target, info)` | After the damage; `info` is a `HurtInfo`. | same |
| `Colliding(proj, projHitbox, targetHitbox)` | `true`/`false` decides the hit; `undefined`, the game's. | `Projectile.Colliding`, `type` filter |
| `CanDamage(proj)` | `false`: deals no damage. | `Projectile.Damage`, `type` filter |
| `MinionContactDamage(proj)` | `true`: a minion or pet hurts on contact. | same |
| `ModifyDamageHitbox(proj, hitbox)` | Change the damage area's `Rectangle`. | `Projectile.Damage_GetHitbox`, `type` filter |
| `CanCutTiles(proj)`, `CutTiles(proj)` | Cutting grass and cobwebs. | `Projectile.CanCutTiles`/`CutTiles`, `type` filter |
| `GetAlpha(proj, lightColor)` | The final color; `undefined`, the game's. | `Projectile.GetAlpha`, `type` filter |
| `PreDrawExtras(proj)` | Before `PreDraw`; `false` removes what the game draws before the sprite: the hook's chain, the fishing line, the yoyo string, the flail's chain. A hook with its own chain draws it here. | `Main.DrawProjDirect` (on mobile the chain is inside it: up to the first draw with the projectile's texture, the others do not come out) |
| `PreDraw(proj, lightColor)` | Before drawing; `false` does not draw the game's sprite (the `PreDrawExtras` extras remain). | `Main.DrawProjDirect`, `type` filter |
| `PostDraw(proj, lightColor)` | After drawing, even with `PreDraw` `false` (as in tModLoader). | same |
| `CanUseGrapple(player, type)` | On the **template**, before launching the hook; `false` prevents it. | `Player.FireGrapple`, filter by `item.shoot` |
| `UseGrapple(player, type)` | On the template: return the type to launch. | same |
| `GrappleCanLatchOnTo(proj, player, tile)` | `true`/`false`: latches onto this tile. | `Projectile.AI_007_GrapplingHooks_CanTileBeLatchedOnTo`, `type` filter |
| `GrappleRange(proj)` | Hook (aiStyle 7): the distance in pixels at which it returns. Default 300, as in tModLoader; with a vanilla hook's `AIType`, that hook's range applies. | before `AI_007_GrapplingHooks`, in the mod projectiles' AI hook |
| `NumGrappleHooks(proj, player, numHooks)` | How many stay latched (`Ref`; the game gives a mod hook 3). Above that, the oldest lets go. | same: during the AI, the other hooks are left out of the game's count, and the mod's limit applies on latching (`bl.projectiles.latchedHooks`) |
| `GrappleRetreatSpeed(proj, player, speed)` | The return speed (`Ref`; the game's: 11). | after `AI_007_GrapplingHooks` |
| `GrapplePullSpeed(proj, player, speed)` | The speed at which the latched hook pulls the player (`Ref`; the game's: 11). The first latched hook's applies. | `Player.GetGrapplingForces`, no filter (only runs with a latched hook) |
| `GrappleTargetPoint(proj, player, grappleX, grappleY)` | The point the hook pulls toward (`Ref`): the sum of the centers of the hooks latched up to this one, as in tModLoader. | same |
| `ShouldUpdatePosition(proj)` | `false`: the game does not add velocity to position (the mod sets the position: held projectile, laser). | `Projectile.UpdatePosition`, `type` filter |

Always installed, for every mod projectile: `originalDamage` (the minion and
sentry base damage) comes from the item that created the projectile
(`Projectile.ApplyStatsFromSource`).

`OnTileCollide` returning `false` only keeps the projectile for the current
impact. It still dies when it expires or runs out of penetration on an NPC,
going through `PreKill` and `OnKill`. A veto in `PreKill` removes the
projectile and ends the flow before `OnKill`. See the
[death cycle regression](../../tools/tests/projectilekill/check.mjs).

`SendExtraAI(writer)` and `ReceiveExtraAI(reader)` go along with the
projectile's sync. Use the loader's readers and writers described in
[Network](#network).

### Shortcuts and statics

| | |
|---|---|
| `CloneDefaults(type)` | Copies a vanilla projectile's values. |
| `DefaultToSpear()`, `DefaultToYoyo()`, `DefaultToFlail()`, `DefaultToWhip()`, `DefaultToDrillOrChainsaw()`, `DefaultToKite()` | The game's defaults for each family of held projectiles. |
| `ModProjectile.register`, `getTypeByName`, `getModProjectile`, `getByName`, `isModType`, `isModProjectile` | As for items. |

### Not yet

`FlailStats`; the grapple methods on `GlobalProjectile`.

---

## ModNPC

A new NPC: enemy, boss or town NPC. A template and one instance per NPC
(`this.NPC`).

### Fields

| Field | Type | What for |
|---|---|---|
| `NPC`, `Type`, `Mod` | | This instance's NPC, the type, the mod. |
| `Texture` | text | The path in the mod, without `.png`; default, next to the class's file. A vertical strip of frames. |
| `DisplayName` | text or `{ culture: text }` | Empty: `NPCName.<Class>`. |
| `AIType` | number | Vanilla type used only during the game's AI; `0` turns it off. Set `npc.aiStyle` separately. Applies in `SetDefaults` and can change per instance. |
| `AnimationType` | number | Animates like this vanilla NPC (0 = no). Applies in `SetDefaults`. |
| `Banner`, `BannerItem` | number | The banner ID (the representative NPC's type) and the item unlocked by BannerSystem. `0` turns it off. See [banners](../mods/07-npcs.md#enemy-banners). |
| `HideFromBestiary` | `boolean` | No Bestiary entry. |
| `BestiaryIcon` | text | A still image for the Bestiary entry (list and portrait), instead of the animated NPC. Default: `<Texture>_BestiaryIcon`; without the file, the NPC. `NPCBestiaryDrawModifiers`'s `CustomTexturePath` with a mod path takes priority. |
| `BossHeadTexture` | text | Boss icon (boss bar and map). Default: `<Texture>_Head_Boss`; without the file, no big bar and no icon. Other icons: `Mod.AddBossHeadTexture`. |
| `HideFromModMenu` | `boolean` | Left out of the Mod Menu. |
| `HeadTexture`, `ShimmerHeadTexture` | text | Town NPC: the head on the map. Default: `<Texture>_Head`, `<Texture>_Shimmer_Head`. |
| `Music` | number | The music while it is near the screen (`MusicLoader.GetMusicSlot` or `MusicID`); -1 = the game's. |
| `SceneEffectPriority` | number | Between two NPCs with music, the higher wins. |
| `Happiness` | `NPCHappiness` | The town NPC's preferences (in `SetStaticDefaults`). |

### Methods you write

| Method | When it runs | Behind it |
|---|---|---|
| `SetStaticDefaults()` | Once. `Main.npcFrameCount[this.Type] = n`: the texture's frames. | native |
| `SetDefaults(npc)` | Every NPC of this type that spawns. The Expert/Master scaling comes after. | native (`NPC.SetDefaults`) |
| `PostStaticDefaults()`, `PostSetDefaults(npc)`, `PostSetupContent()` | | native / content ready |
| `Clone(newNPC)` | A copy of the instance. | |
| `CloneDefaults(type)` | Copies classic defaults from a vanilla or mod NPC; keeps type, netID, ModNPC, globals and the players' participation. The world difficulty is applied afterwards. | `NPC.SetDefaults` with copy parameters |
| `ApplyBuffImmunity(npc)` | After `SetDefaults`: `npc.buffImmune[id] = true`. | native |
| `ModifyNPCLoot(npcLoot)` | Once: the drops. | native |
| `SetBestiary(database, bestiaryEntry)` | Once: the Bestiary entry. | content ready |
| `HitEffect(npc, hitDirection, damage)` | On each hit, after the game's effect. | native (`NPC.HitEffect`), only if written |
| `OnSpawn(npc, source)` | Once after spawning; receives the native source. | `NPC.NewNPC`, filter on the Type argument and by override |
| `ResetEffects(npc)` | After the buff flags are reset, before reapplying effects and running the AI. | entry of `NPC.UpdateNPC_BuffSetFlags`, filter by type and override |
| `CanBeHitByItem(npc, player, item)` | `false` vetoes; `true` allows it in the eligibility test; `null`/`undefined` keeps the game's. A ModPlayer veto wins. | shared flow of `Player.ProcessHitAgainstNPC` and `CanNPCBeHitByPlayerOrPlayerProjectile` |
| `ModifyHitByItem(npc, player, item, modifiers)` | Before the item hit; `HitModifiers`. | `NPC.StrikeNPC` (CombatLoader) |
| `CanBeHitByProjectile(npc, projectile)` | `false` vetoes, `true` forces, `null` leaves it to the game (player projectiles). | `Player.CanNPCBeHitByPlayerOrPlayerProjectile` |
| `ModifyHitByProjectile(npc, projectile, modifiers)` | Before the projectile hit; `HitModifiers`. | `NPC.StrikeNPC` (CombatLoader) |
| `ModifyIncomingHit(npc, modifiers)` | Last, on every hit (also with no source). Not reapplied on `fromNet`. | same |
| `CanHitNPC(npc, target)`, `CanBeHitByNPC(npc, attacker)` | NPC against NPC; `false` vetoes. | `NPC.BeHurtByOtherNPC` |
| `ModifyHitNPC(npc, target, modifiers)`, `OnHitNPC(npc, target, hit)` | Its hit on another NPC. | `NPC.BeHurtByOtherNPC` + `StrikeNPC` |
| `OnHitByItem(npc, player, item, hit, damageDone)` | After actual item damage, once per hit. | shared flow of `NPC.StrikeNPC` |
| `OnHitByProjectile(npc, projectile, hit, damageDone)` | After actual projectile damage, including with no player owner. | `Projectile.Damage_PVE` and shared flow of `NPC.StrikeNPC` |
| `CanHitPlayer(npc, player, cooldownSlot)` | `false` vetoes damage; `cooldownSlot.value` changes the immunity slot. | shared flow of `Player.Hurt` |
| `ModifyHitPlayer(npc, player, modifiers)` | Changes the damage parameters mobile supports before `Hurt`. | same |
| `OnHitPlayer(npc, player, hurtInfo)` | After actual damage, receives the hit's data and the slot used. | same |
| `CheckDead(npc)` | With the NPC active and life ≤ 0: `false` prevents death; restore life to avoid another check. | `NPC.checkDead`, filter by type and override |
| `ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment)` | After the per-player scaling. Receives the count used by the NPC, the balance computed by the game and an adjustment of 0.85 in Master or 1 in the other modes. | `NPC.ScaleStats_ByPlayerCount`, filter by type and override |
| `DrawEffects(npc, drawColor)` | `Ref<Color>` of the center's lighting; runs before `PreDraw`. The changed color goes through the game's buff tint. | both overloads of `Main.DrawNPCDirect`, filter by NPC and override |
| `PreDraw(npc, spriteBatch, screenPos, drawColor)` | `false` skips the native drawing. | same |
| `PostDraw(npc, spriteBatch, screenPos, drawColor)` | After drawing, even when `PreDraw` vetoes. | same |
| `DrawBehind(npc, index)` | After the draw cache; add the index to the `Main.instance.DrawCacheNPCs*` lists. | `Main.CacheNPCDraws`, only tracked entities with an override |
| `GetAlpha(npc, drawColor)` | A `Color` replaces the result, including alpha 0; `null`/`undefined` uses the game's. | `NPC.GetAlpha`, filter by type and override |
| `BossHeadSlot(npc, index)`, `BossHeadRotation(npc, rotation)`, `BossHeadSpriteEffects(npc, spriteEffects)` | Receive a `Ref` initialized with the game's result. Index -1 hides the icon. | `NPC.GetBossHead*` getters, filter by type and override |
| `PreAI(npc)`, `AI(npc)`, `PostAI(npc)` | Every frame. `PreAI` → `false` skips the game's AI. | `NPC.AI`, `type` filter |
| `FindFrame(npc, frameHeight)` | Custom animation: change `npc.frame`. | `NPC.FindFrame`, `type` filter |
| `CheckActive(npc)` | `false`: does not despawn when far away. | `NPC.CheckActive`, `type` filter |
| `PreKill(npc)`, `OnKill(npc)` | On death. `PreKill` → `false` cancels the drop. | `NPC.NPCLoot`, `type` filter |
| `SpawnChance(spawnInfo)` | On each natural spawn: return the weight (the game's weighs 1; 0 = does not spawn). Single player or server only. | `NPC.Spawner.SpawnAnNPC`, no filter |
| `SpawnNPC(tileX, tileY)` | Picked: how it spawns, at the spawn tile; returns the index. Default: on top of the tile (`tileX * 16 + 8`, `tileY * 16`), as in tModLoader. | same |
| `SpawnModBiomes` | In `SetDefaults`: the `ModBiome`s where it spawns (classes, instances or `Type`): their name, icon and background in the Bestiary entry, and the filter. | when building the Bestiary |
| **Town NPC** | | |
| `CanTownNPCSpawn(numTownNPCs)` | Every so often, with none of this one in the world: `true` and it moves in. | `Main.UpdateTime_SpawnTownNPCs`, no filter |
| `CheckConditions(left, right, top, bottom)` | Does the room suit it? | `WorldGen.CheckSpecialTownNPCSpawningConditions` |
| `SetNPCNameList()` | Its first names; one is picked. | `NPC.getNewNPCName` |
| `GetChat(npc)` | What it says when you chat. | `NPC.GetChat`, `type` filter |
| `CanChat(npc)` | Decides whether it can chat, including outside AI 7; an undefined return keeps the game's. | `NPC.get_CanTalk` and `get_CanBeTalkedTo`, filter by type and override |
| `TownNPCProfile()` | Once after content loads: return a native `ITownNPCProfile`; `null` keeps the automatic appearance. | registration in `TownNPCProfiles.Instance`, no per-frame hook |
| `ModifyActiveShop(npc, shopName, items)` | When filling a registered shop of this NPC in a chat, after `NPCShop` builds the items. Receives the shop's local name and the native item array. | `InventoryStorage.SetupShop` |
| `ModifyNPCHappiness(npc, player, primaryPlayerBiome, shopHelper, nearbyNPCsByType)` | After the native preferences, before clamping the price. Uses ExMod's alternative; the method is disabled in tModLoader stable. | `AllPersonalitiesModifier.ModifyShopPrice`, no native filter, only price calculations |
| `SetChatButtons(npc, buttons)` | The chat buttons (up to two on mobile). | `GUINPCDialogue.SetupButtonText` |
| `OnChatButtonClicked(npc, firstButton)` | A button was tapped; return a shop's name to open it. | `GUINPCDialogue.Option1Clicked`/`Option2Clicked` |
| `AddShops()` | Once: the shops (`NPCShop`). | content ready |
| `TownNPCAttackProj(npc, attack)` | The attack's projectile (`projType`, `attackDelay`). | `NPC.AI`, `type` filter |
| `TownNPCAttackStrength(npc, attack)` | `damage`, `knockback`. | same |
| `TownNPCAttackProjSpeed(npc, attack)` | `speed`, `gravityCorrection`, `randomOffset`. | same |
| `NPCHeadSlot()` | (to read) the head's index, -1 without a head. | |

The music (`Music`) is decided by the `Main.UpdateAudio*` hooks, installed
once; see [guide 10](../mods/10-sons-e-musica.md).

Hits on an NPC receive a `HitModifiers` and those on the player a
`HurtModifiers`, with tModLoader 1.4.4's fields; the `OnHit`s receive a
`HitInfo` or a `HurtInfo`. The older names (`damage`, `knockBack`,
`hitDirection`, `crit`) still work. See [Hits](../mods/16-golpes.md).

The methods without a complete contract reachable on mobile were dropped; the
reasons and the tests are in
[ModNPC: contracts and validation](../../tools/tests/modnpchooks/README.md).

In the happiness callback, the primary biome follows ExMod's IDs: 0 forest,
1 underground, 2 snow, 3 desert, 4 jungle, 5 ocean, 6 hallow, 7 mushroom,
8 dungeon, 9 corruption and 10 crimson. `shopHelper` is native and accepts its
preference methods; the neighbors array also comes from the native
`HelperInfo`.

### Statics

| | |
|---|---|
| `ModNPC.register`, `getTypeByName`, `getModNPC`, `getByName`, `isModType`, `isModNPC` | As for items. |
| `ModNPC.NPCValue(pl, gold, silver, copper)` | The money it drops. |

### Not yet

`UpdateLifeRegen`, `ModifyDeathMessage`, `DrawHealthBar`, treasure bags, the
boss-defeated flag, `CanGoToStatue` and town NPC attacks beyond the
projectile.

---

## ModBossBar

A mod NPC's big bar (`ModNPC.BossBar = ModContent.GetInstance(Bar)`, in
`SetDefaults`). It also gives the bar to an NPC that is not a boss and has no
boss icon. It is drawn like the game's bars on mobile (`GUIBossBar.Draw`: the
panel under the minimap, with the icon, life and shield), in the same place
and style as the Eye of Cthulhu's. See [guide 7](../mods/07-npcs.md#custom-bar-modbossbar).

| | | |
|---|---|---|
| `GetIconTexture(iconFrame)` | The icon (`Asset<Texture2D>` or `Texture2D`); `iconFrame` (`Ref`) crops it. `null`: the game's "?". | |
| `ModifyInfo(info, life, lifeMax, shield, shieldMax)` | `null`: the NPC's life; `true`: the values given (`Ref`); `false`: no bar (the game looks for another). `info.npcIndexToAimAt` changes the NPC. | `BigProgressBarSystem.Update` (in `Main.UpdateUIStates`) |
| `PreDraw(spriteBatch, npc, drawParams)`, `PostDraw(...)` | `drawParams`: tModLoader's `BossBarDrawParams` (see [`GlobalBossBar`](#globalbossbar)); `false` removes the game's drawing. | `BigProgressBarSystem.Draw` → `GUIBossBar.Draw` |
| `Life`, `LifeMax`, `Shield`, `ShieldMax` | The last frame's values. | |
| `SetStaticDefaults()` | On registration. | |

No `ModBossBarStyle`. Every bar, the game's included, also goes through the
[`GlobalBossBar`](#globalbossbar)s: theirs first, then the bar's own.

## ModPlayer

Each player's data and behavior. **One instance per player per class**,
created the first time someone asks for it. The methods receive the player
(`player`), which is the same as `this.Player`.

### Fields

| Field | What for |
|---|---|
| `Player` | This instance's player. |
| `CumulativeHealth`, `CumulativeMana` | In `ModifyMaxStats`: added to max life and mana. |
| `WeaponDamage` | In `ModifyWeaponDamage`: the damage (an alternative to returning it). |

### Methods you write, in frame order

| Method | When it runs | Behind it |
|---|---|---|
| `PreUpdate(player)` | Start of the player's frame. | `Player.Update` |
| `ResetEffects(player)` | Right after the game resets the effects. | `Player.ResetEffects` |
| `ModifyMaxStats(player)` | After `ResetEffects`. | same |
| `PreUpdateBuffs(player)`, `PostUpdateBuffs(player)` | Around the buffs. | `Player.UpdateBuffs` |
| `UpdateEquips(player)`, `PostUpdateEquips(player)` | After the equipment. | `Player.UpdateEquips` |
| `UpdateBadLifeRegen(player)`, `UpdateLifeRegen(player)` | Before and after life regeneration. | `Player.UpdateLifeRegen` |
| `UpdateManaRegen(player)` | After mana regeneration. | `Player.UpdateManaRegen` |
| `PreModifyLuck(player, luck)`, `ModifyLuck(player, luck)` | Luck, `luck.value`; `PreModifyLuck` returning false skips the game's. | `Player.RecalculateLuck` |
| `UpdateMovement(player)` | Custom movement (dash), near the end of the frame. | `Player.BordersMovement` |
| `FrameEffects(player)` | After the game assembles what gets drawn: switching `player.head`/`body`/`legs` changes the drawing. | after `Player.PlayerFrame` |
| `PostUpdate(player)` | End of the frame. | `Player.Update` |
| `UpdateDead(player)` | Every frame while dead. | `Player.UpdateDead` |

### Others

| Method | When it runs | Behind it |
|---|---|---|
| `Initialize()` | Once, when the instance is created. | |
| `OnEnterWorld(player)` | Entered the world. | `Player.Hooks.EnterWorld` |
| `OnRespawn(player)` | Came back to life. | `Player.Spawn` |
| `CanUseItem(player, item)` | `false` prevents use. | `Player.ItemCheck_CheckCanUse_Inner` |
| `ModifyWeaponDamage(player, item, damage)` | Return the new damage. | `Player.GetWeaponDamage` |
| `ImmuneTo(player, source, cooldown, dodgeable)` | `true`: the hit does not happen. | `Player.Hurt` |
| `FreeDodge(player, source, damage, ...)` | `true`: dodges. | same |
| `ModifyHurt(player, modifiers)` | Before the hit: `modifiers.damage`, `hitDirection`, `quiet`, `crit`, `dodgeable`. | same |
| `OnHurt(player, source, damage, ...)` | After the hit. | same |
| `PostHurt(player, source, damage, ...)` | After the hit, if the player survived. | same |
| `PreKill(player, source, damage, direction, pvp)` | `false` prevents death. | `Player.KillMe` |
| `Kill(player, source, damage, direction, pvp)` | Died. | same |
| `CatchFish(attempt, itemDrop, npcSpawn, sonar, sonarPosition)` | Caught a fish, after the game's roll: `itemDrop.value` and `npcSpawn.value` (`Ref`, 0 = nothing) decide what comes out; `attempt` has `questFish`, `common`/`uncommon`/`rare`..., `inLava`... `sonar`/`sonarPosition` do nothing yet. | `Projectile.FishingCheck_RollItemDrop` |
| `SaveData(data)` | On each save: put what to remember in `data`. | `Player.InternalSavePlayerFile` |
| `LoadData(data)` | When the character loads. | `Player.LoadPlayer` |

`ModPlayer`'s hooks are shared and installed according to the overrides.
Update hooks can run every tick per player; the combat, item and drawing ones
follow their respective events. Callback lists are cached and the frequent
dispatches avoid temporary arrays. The cost depends on the active callbacks
and on what each mod runs. See the [review and the measurements](../../tools/tests/modplayerperf/RESULTADOS.md).

### Finding the instance

| | |
|---|---|
| `player.GetModPlayer(Class)`, `GetModPlayer('Class')` or `GetModPlayer('mod/Class')` | That player's instance. By name: your mod's; if the name belongs to another mod and more than one mod has it, `undefined` and a warning in the log. Two mods can have a `ModPlayer` with the same name. |
| `Class.get(player)` | The same. |
| `AddDrawData(drawInfo, drawData)` | A ModPlayer static: writes into the next active slot and increments DrawDataCacheCount after the write. Returns true on insertion and false for a full cache, an invalid counter or a missing cache/data. |
| `ModPlayer.getByName('Class')` | The **local** player's (for UI). |
| `ModPlayer.register(Class)` | Registers. |

### Combat, items, drawing, inventory and network

The [full reference of the ModPlayer hooks](modplayer-hooks.md) describes the
additional hooks, the references, the return values, the jump and layer
descriptors, and the adaptations to native Terraria.

The test with a real host and client had 102 checks with no failures,
including disconnecting and reconnecting. That run does not cover every hook
nor a dedicated server. See the [multiplayer results](../../tools/tests/mpmodplayer/RESULTADOS.md)
and the [script with the coverage limits](../../tools/tests/mpmodplayer/README.md).

---

## ModBuff

A new buff or debuff. **One instance per type** (a buff is not an entity): the
methods receive the player or the NPC and the buff's position in its list.

### Fields

| Field | What for |
|---|---|
| `Type`, `Mod` | |
| `Texture` | 32x32; the path in the mod, without `.png` (default: next to the class's file). |
| `DisplayName`, `Description` | Text or `{ culture: text }`. Empty: `BuffName.<Class>`, `BuffDescription.<Class>`. |
| `HideFromModMenu` | Left out of the Mod Menu. |

### Methods you write

| Method | When it runs | Behind it |
|---|---|---|
| `SetStaticDefaults()` | Once: `Main.debuff[this.Type]`, `buffNoSave`, `BuffID.Sets...`. | native |
| `PostStaticDefaults()`, `PostSetupContent()` | | |
| `ModifyDisplayName()`, `ModifyDescription()` | Once per language: change `this.DisplayName`/`this.Description`. | on `register` |
| `UpdatePlayer(player, buffIndex)` | Every frame, active on the player. | `Player.UpdateBuffs` |
| `UpdateNPC(npc, buffIndex)` | Every frame, active on the NPC. | `NPC.UpdateNPC_BuffSetFlags` |
| `ApplyPlayer(player, time)`, `ApplyNPC(npc, time)` | When it is added. | `Player.AddBuff_ActuallyTryToAddTheBuff`, `NPC.AddBuff` |
| `ReApplyPlayer(player, time, i)`, `ReApplyNPC(npc, time, i)` | When it is added again; `false` prevents renewing the time. | `Player.AddBuff_TryUpdatingExistingBuffTime`, `NPC.AddBuff` |
| `CanRemove(player, time, i, debuff)` | On tapping the icon: `true`/`false` decides; `null`, the game. | `GUIBuffs.RemoveBuff` |
| `OnRemove(player, time, i)` | After being removed by the tap. | same |
| `ModifyBuffText(buffName, tip, rare)` | The buff's name and tip in the bar (three `Ref`s; `rare` does not change the color on mobile). | `Lang.GetBuffName` (drawn by `GUIBuffs.Draw`) and `Main.GetBuffTooltip` (`GUIBuffs.ChangeBuffString`), `type` filter |
| `PreDraw(spriteBatch, buffIndex, drawParams)`, `PostDraw(...)` | The icon in the buff bar. `drawParams`: see [`GlobalBuff`](#globalbuff); `false` hides the icon. | `GUIBuffs.ItemDraw` |

### Statics

`ModBuff.register`, `getTypeByName`, `getModBuff`, `isModType`.

### Not yet

`RightClick` (`CanRemove` does the same).

---

## ModMount

A new mount, like tModLoader's `ModMount`. **One instance per type**: each
player's state lives in their `Mount` (`player.mount`). The type is assigned on
registration, before the buff and the item that ask for it
(`ModContent.MountType`); `MountData` and `SetStaticDefaults` come with the
game ready, when the buff and dust types exist too.

### Fields

| Field | What for |
|---|---|
| `Type`, `Mod` | |
| `MountData` | The game's `Mount.MountData`: speed, jump, frames, offsets, `buff`, `spawnDust`, textures. Change it in `SetStaticDefaults`. |
| `Texture` | The base for the layers: `<Texture>_Back`, `_BackGlow`, `_BackExtra`, `_BackExtraGlow`, `_Front`, `_FrontGlow`, `_FrontExtra`, `_FrontExtraGlow` (`MountTextureType`), only the ones that exist. Default: the mirror of the class's file (`Content/Mounts/X.js` → `Assets/Textures/Mounts/X_Back.png`). |

### Methods you write

tModLoader's `ref`s arrive as `Ref`s (`.value`).

| Method | When it runs | Behind it |
|---|---|---|
| `SetStaticDefaults()` | Once, with the textures already in `MountData`. | in `Ready` |
| `SetMount(player, skipDust)` | On mounting, after `FinalizeMountData`; `skipDust.value = true` skips the game's dust. | `Mount.SetMount`/`DoSpawnDust` |
| `Dismount(player, skipDust)` | On dismounting, before the game. | `Mount.Dismount` |
| `UpdateEffects(player)` | Every frame while mounted, before the game's effects. | `Mount.UpdateEffects` |
| `UpdateFrame(player, state, velocity)` | The animation; `false` skips the game's. | `Mount.UpdateFrame` |
| `JumpHeight(player, jumpHeight, xVelocity)`, `JumpSpeed(player, jumpSpeed, xVelocity)` | The jump, with the game's value in the `Ref`. | `Mount.JumpHeight`/`JumpSpeed` |
| `UseAbility(player, mouse, toggleOn)`, `AimAbility(player, mouse)` | The mount's ability. | `Mount.UseAbility`/`AimAbility` |
| `Draw(playerDrawData, drawType, player, texture, glowTexture, drawPosition, frame, drawColor, glowColor, rotation, spriteEffects, drawOrigin, drawScale, shadow)` | Once per layer with a texture (0 Back, 1 BackExtra, 2 Front, 3 FrontExtra). The `Ref`s carry what the game would draw; changed, it draws with the new ones. `false`: the layer is not drawn. | `Mount.Draw` |

### Statics

| | |
|---|---|
| `ModMount.GetSpecificData(player)`, `SetSpecificData(player, value)` | The player's data on this mount (tModLoader's `_mountSpecificData`, which in C# is `object`). Cleared on mounting another one and on dismounting. |
| `ModMount.NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect)` | A `DrawData`. |
| `ModMount.AddDrawData(playerDrawData, data)` | tModLoader's `playerDrawData.Add`, inside `Draw`. |

A buff with `BuffID.Sets.MountType[this.Type] = type` mounts the player on its
own (the game does that in `UpdateBuffs`) and has no time on screen and is not
saved. The item points to the mount in `Item.mountType`.

### Differences from tModLoader

- `MountData.MinecartUpgrade*` does not exist on mobile: the minecart upgrade
  kit uses the game's values.
- `DelegateMethods.Minecart.SparksMeow` and the Meowmere sounds are private on
  mobile: copy the `delegations` from a vanilla minecart
  (`Terraria.Mount.mounts[MountID.MeowmereMinecart].delegations`).
- `ModDust` does not exist yet: `spawnDust` with vanilla dust.

---

## ModCommand

A chat command, like tModLoader's `ModCommand`. One instance per command. The
typed text goes through the game's processor; what it does not know arrives
with the slash, and the mod's runs in its place.

| | |
|---|---|
| `Command` | The name, without the slash (getter). |
| `Aliases` | Other names (from ExMod): `get Aliases() { return ['heal']; }`. |
| `Type` | `CommandType.Chat` (on the device of whoever typed it), `Server` (on the server, requested by a player), `World` (in single player, in the chat; in multiplayer, on the server). `Console` does not exist on mobile. |
| `Usage`, `Description` | Default: `Commands.<Class>.Usage`/`.Description` from `Localization` (`/help` shows them). |
| `IsCaseSensitive` | `false`: the text arrives in lowercase. |
| `Action(caller, input, args)` | The command. `caller.Player`, `caller.CommandType`, `caller.Reply(text, color)`. `throw new UsageException(text, color)` replies with the error; `return false` (from ExMod) replies with the `Usage`. |

`/mod:name` picks the mod when two have the same name. The game's `/help`
lists the mod ones at the end.

---

## ModTouchButton

A mod action in the game's touch controls, placed by the player in the
controls editor like Jump or Heal: tModLoader's `ModKeybind` (which is an
alias of this class). Created by
`TouchButtonLoader.RegisterButton(mod, name, options)` in `Load()`, or by
`KeybindLoader.RegisterKeybind(mod, name, defaultKey)` (the key is ignored).

| | |
|---|---|
| `JustPressed`, `Current`, `JustReleased`, `Old` | The state in the current update. Read them in `ModPlayer.ProcessTriggers`. |
| `RetroCurrent` | `Current`, false with the chat, a conversation or a sign open. |
| `IsBound` | Whether it is in the layout in use. |
| `Mod`, `Name`, `FullName` | `FullName` is `'modid/Name'`, the saved name. |
| `DisplayName` | `{ Key, Value }`, from `TouchButtons.<Name>.DisplayName` (or `Keybinds.`). |
| `GetAssignedKeys()` | The button's name when it is in the layout, or `[]`. |

The options (`icon`, `label`, `visible`, `enabled`, `onPress`) and what the
player sees are in [guide 18](../mods/18-botoes-de-toque.md). Cost: while some
mod has a button, the controls' `Update` and `Draw` enter JS every frame; the
drawing and editor hooks only enter for mod actions (native filter).

---

## ModRarity

A rarity, like tModLoader's `ModRarity`. The type comes after the game's 12
(`ModContent.RarityType(Class)`); it goes in `item.rare`.

| | |
|---|---|
| `get RarityColor()` | The name's color (read on every draw: it can flash). |
| `GetPrefixedRarity(offset, valueMult)` | The rarity when a prefix moves the item up (1, 2) or down (-1, -2); default `this.Type`. |
| `SetStaticDefaults()` | Once, with the game ready. |

Hooks: `ItemRarity.Initialize` (the color table), `Item.GetPopupRarityColor`,
`Item.Prefix` (`rare` filter) and `Main.MouseTextInner` (the item on the
ground). `ModContent.GetModRarity(type)` returns the instance.

---

## ModHair

A hairstyle, like tModLoader's `ModHair`. The type comes after the game's 228.

| | |
|---|---|
| `Texture`, `AltTexture` | The hairstyle's and the one with a hat (`<Texture>_Alt`; without it, the same). Default: next to the file (`Content/Hairs/X.js` → `Content/Hairs/X.png`). |
| `AvailableDuringCharacterCreation` | In character creation (getter; default `true`). |
| `GetUnlockConditions()` | At the Stylist when all of them hold: objects with `IsMet()` or functions. |
| `IsUnlocked(inCreation, atStylist)` | Decides everything on its own (from ExMod), if written. |

Saving: the game's file stores hair 0 and the mod one goes by name into
`<character>.plr.bl.json` (`bunny:hair`); without the mod, the character opens
with hair 0. `Terraria.ID.HairID.Sets.DrawBackHair[this.Type] = true` in
`SetStaticDefaults()` allows drawing the hair behind the player. tModLoader's
`RandomizedCharacterCreationGender` does not exist on mobile.

---

## ModCloud

A cloud, like tModLoader's `ModCloud`. The type comes after `CloudID.Count`.

| | |
|---|---|
| `Texture` | Default: next to the file (`Content/Clouds/X.js` → `Content/Clouds/X.png`). |
| `RareCloud` | `true`: rolled among the rare ones (getter). |
| `SpawnChance()` | The weight: common ones against the game's 22 (weight 1 each), rare ones against the game's 18 rare ones. The common ones' is reread every 60 attempts at a new cloud (about one second: the game tries one per frame), not on all of them; the rare ones', on every roll. |
| `OnSpawn(cloud)` | The cloud spawned (the game's `Cloud`: `spriteDir`, `scale`...). |

`CloudLoader.AddCloudFromTexture('Clouds/X', weight, rare)`, in `Mod.Load`,
creates a texture-only cloud. Every PNG in a `Clouds/` folder of the mod with
no cloud of the same name becomes a common cloud of weight 1 on its own.
tModLoader's `Draw` does not exist yet.

---

## ModEmoteBubble

> **Disabled for now** (`EmoteBubbleLoader.Enabled = false`): on the user's
> phone mod emotes did not work. The class is ignored, with no type and no
> hook; the text below applies when it comes back.

An emote, like tModLoader's `ModEmoteBubble`. The type comes after the game's
151. The texture has 34 x 28 frames side by side (two, by default), only the
icon: the white bubble comes from the game's sheet. During each method,
`this.EmoteBubble` is the bubble.

| | |
|---|---|
| `AddToCategory(EmoteID.Category.X)` | In the emote menu (`General`, `RockPaperScissors`, `Items`, `BiomesAndEvents`, `Town`, `CrittersAndMonsters`, `Dangers`). In `SetStaticDefaults`. |
| `IsUnlocked()` | In the menu only if unlocked. |
| `OnSpawn()` | The bubble spawned (`EmoteBubble.NewBubble`). |
| `UpdateFrame()` | `false`: the animation belongs to the mod (`this.EmoteBubble.frame`, `frameCounter`). |
| `GetFrame()` | The frame in the texture; `null`, the game's (half the texture). |
| `GetFrameInEmoteMenu(frame, frameCounter)` | The frame in the menu (the menu animation has two). |
| `PreDraw(sb, texture, position, frame, origin, effects)`, `PostDraw(...)` | Around drawing the bubble; `false` does not draw it. |

Multiplayer: an emote the player sends is refused by the game above 150
(msg 120); the mod one does not reach the others.

---

## ModAchievement

An achievement, like tModLoader's `ModAchievement`, in the game's manager
(`Main.Achievements`), named `<mod>/<Class>`. Progress is kept in the game's
achievements file.

| | |
|---|---|
| `Texture` | A 592 x 64 sheet: the unlocked icon at x = 0 and the locked one at x = 528 (where the mobile menu looks). tModLoader's (130 x 64) does not work. |
| `Index` | The position in the sheet (columns of 66). |
| `Category` | `Terraria.Achievements.AchievementCategory.Slayer` (default), `Collector`, `Explorer`, `Challenger`. |
| `Hidden` | The name and description become "???" in the menu until it is completed. |
| `SetStaticDefaults()` | The conditions: `AddCondition(key)`, `AddIntCondition(key, max)`, `AddFloatCondition`, `AddItemCraftCondition(id or [ids])`, `AddItemPickupCondition`, `AddNPCKilledCondition`, `AddTileDestroyedCondition([ids])`. They return the game's condition (`.Value` on the numeric ones, `.Complete()`). |
| `OnCompleted(achievement)` | Completed. |
| `OnNPCKilled(player, npcId)`, `OnItemPickup(player, type, count)`, `OnItemCraft(type, count)`, `OnTileDestroyed(player, tile)` | The game's notifications, while not completed (from ExMod; tModLoader uses `AchievementsHelper`'s events). |

Name and description: `Achievements.<Class>.FriendlyName` (or `.Name`) and
`.Description` from `Localization`. tModLoader's menu positions
(`GetDefaultPosition`) do not exist.

---

## ModTile

A new block (dirt, stone, ore) or a multi-cell object with `TileObjectData`
(furniture: sink, chair, door, chest, bed...). One instance per type; the
methods receive the position `(i, j)` in tiles, and tModLoader's `ref`
parameters arrive as `Ref`s (`.value`). The full list, with what each piece of
Example Mod furniture uses, is in [guide 9](../mods/09-blocos.md).

### Fields

| Field | What for |
|---|---|
| `Type`, `Mod` | |
| `Texture`, `HighlightTexture` | The frame sheet; the outline (`HasOutlines`). |
| `DustType`, `HitSound` | Dust and sound when hitting. |
| `MinPick`, `MineResist` | Minimum pickaxe; each hit's damage is divided by it. |
| `ItemDrop` | The item that drops; `undefined` = the mod item that places the tile (by style). |
| `AdjTiles` | Crafting stations it also counts as. |
| `AnimationFrameHeight` | The height of one animation frame. |
| `CacheDrawData` | `false`: `SetDrawPositions`/`AnimateIndividualTile`/`SetSpriteEffects` without the cache, on every draw (animation by a counter other than the type's `Main.tileFrame`). |

### Methods you write, and where they go in

| Methods | Behind it |
|---|---|
| `SetStaticDefaults`, `PostSetDefaults`, `PostSetupContent` | native, content ready |
| `CanKillTile`, `KillTile`, `NumDust`, `CreateDust`, `KillSound`, `CanDrop`, `GetItemDrops`, `KillMultiTile` | `WorldGen.KillTile*`, `TileFrameImportant`; tile filter |
| `TileFrame` | `WorldGen.TileFrame`; `tile.frame` mark |
| `ModifyLight` | `Lighting.LightTiles` (every 3 frames) + batched `Lighting.AddLight`; `tile.light` mark |
| `AnimateTile` | `Main.AnimateTiles` (only if some tile uses it) |
| `SetDrawPositions`, `AnimateIndividualTile`, `SetSpriteEffects` | the native drawing (`GetTileDrawData`); `tile.drawdata` mark |
| `PreDraw`, `PostDraw`, `DrawEffects`, `SpecialDraw`, `EmitParticles` | a pass after `TileDrawing.PostDrawTiles`; `tile.draw` mark |
| `RightClick`, `MouseOver`, `MouseOverFar` | `Player.TileInteractions*`; tile filter |
| `HitWire` | `Wiring.HitWireSingle`; `tile.wire` mark |
| `RandomUpdate` | `WorldGen.UpdateWorld_*Tile`; `tile.random` mark |
| `NearbyEffects` | `SceneMetrics.Scan` |
| `PlaceInWorld` | `TileObjectData.CallPostPlacementPlayerHook` and `WorldGen.PlaceTile` (when placing) |
| `Slope` | `WorldGen.SlopeTile`/`PoundTile`; `tile.slope` mark |
| `ModifySittingTargetInfo`, `ModifySleepingTargetInfo` | `PlayerSittingHelper`/`PlayerSleepingHelper` |
| `IsLockedChest`, `UnlockChest`, `LockChest` | `Chest.IsLocked`/`Unlock`/`Lock`; `tile.chest` mark |

### To call

| | |
|---|---|
| `AddMapEntry(color, name)` | The color and the name on the map; each call is one more option. |
| `RegisterItemDrop(item, ...styles)` | The item that drops. |
| `CreateMapEntryName()` | The class name. |
| `ModTile.register`, `getTypeByName`, `getModTile`, `isModType` | |
| `bl.tiles.typeAt(x, y)` | The type of the active tile at a position (-1 if there is none). |
| `MusicLoader.AddMusicBox(mod, slot, item, tile)` | A mod music box. |

### Not yet

`ModPalmTree`/`ModCactus`, `ModifyFrameMerge`/`PostTileFrame`, `SwitchTiles`,
`HasSmartInteract`, `GetTorchLuck`; village NPCs do not open mod doors.

---

## ModBannerTile

A base for 1x3 enemy banners, derived from `ModTile`. Set up an item with
`DefaultToPlaceableTile(tileType, style)` and assign that item to
`ModNPC.BannerItem`. The tile automatically identifies the banner through the
item that places its style and activates `SceneMetrics.NPCBannerBuff` in the
buff area. The default is `KillsToBanner = 50`, with `BannerStrength`
enabled.

`SetStaticDefaults` sets the object up for ceilings and platforms. If you
override it, call `super.SetStaticDefaults()`. `NearbyEffects` applies the
bonus; when overriding it, call the base method. `PreDraw` queues the sway
through the native `MultiTileVine` renderer; `SetDrawPositions` adjusts the
vertical offset. When overriding those methods, keep the base call to keep the
effect.

Complete example: [Enemy banners](../mods/07-npcs.md#enemy-banners).

## ModWall

A new wall. See [guide 9](../mods/09-blocos.md#walls-modwall).

| | | |
|---|---|---|
| `Type`, `DustType`, `HitSound`, `ItemDrop` | Fields. Without `ItemDrop`, the mod item whose `createWall` equals the type. | |
| `SetStaticDefaults()` | With the type already in the tables (`Main.wallHouse`, `WallID.Sets`). | on installation |
| `KillWall(i, j, fail)`, `NumDust(i, j, fail, num)` | On hitting or breaking (`fail`, `num`: `Ref`). | `WorldGen.KillWall`, filter by the wall at `(i, j)` |
| `CreateDust(i, j, type)` | Each dust particle. | `WorldGen.KillWall_MakeWallDust` |
| `KillSound(i, j, fail)` | `false` silences it. | `WorldGen.KillWall_PlaySounds` |
| `Drop(i, j, type)` | The item that drops; `false` nothing. | `WorldGen.KillWall_DropItems`/`_GetItemDrops` |
| `ModifyLight(i, j, r, g, b)` | The wall's light (`Ref`), near the screen. | `Lighting.LightTiles` + `Lighting.AddLight`, `wall.light` mark |
| `AnimateWall(frame, frameCounter)` | The frame (`Main.wallFrame`), per type, every frame. | `Main.DoUpdate_AnimateWalls` |
| `RandomUpdate(i, j)` | Random update. | `WorldGen.UpdateWorld_*Tile`, filter by the wall, `wall.random` mark |
| `AddMapEntry(color, name)` | The color and the name on the map. | `MapHelper.wallLookup` |
| `WallFrame(i, j, randomizeFrame, style, frameNumber)` | The border's frame; `false` keeps the previous one. | `Framing.WallFrame`, filter by the wall, `wall.frame` mark |
| `ModWall.isModType`, `getTypeByName`, `getModWall` | As for tiles. | |
| `item.DefaultToPlaceableWall(type)` (`ModItem`) | The item that places the wall. | |

## ModTree

A new tree: the game's tile 5, decided by the ground under the trunk. See
[guide 9](../mods/09-blocos.md#trees-modtree).

| | | |
|---|---|---|
| `GrowsOnTileId` | Its grounds (in `SetStaticDefaults`). | |
| `SetStaticDefaults()` | When content loads, on the game thread (it can request textures). | |
| `GetTexture()`, `GetTopTextures()`, `GetBranchTextures()` | The trunk (a 176-wide strip), the tops (3 of 80x80) and the branches (2x3 of 40x40). The default: `<Class>.png`, `_Tops`, `_Branches`. | native: `TileDrawing.DrawTiles`'s `SpriteBatch.Draw` and `WorldGen.GetCommonTreeFoliageData` |
| `CountsAsTreeType` | The `TreeTypes` the game sees; the default is `TreeTypes.Custom`. | `WorldGen.GetTreeType` |
| `SaplingGrowthType(style)` | The sapling an acorn becomes on its ground (`style`: `Ref`). | `TileObject.CanPlace` |
| `DropWood()`, `CanDropAcorn()` | The wood when cutting; the acorn from the top. | `WorldGen.KillTile_GetTreeDrops` |
| `TreeLeaf()` | The leaf gore; `-1` none. | `WorldGen.GetTreeLeaf` |
| `CreateDust()` | The dust when hitting. | `WorldGen.KillTile_MakeTileDust` |
| `Shake(x, y, createLeaves)` | On shaking (the top at `(x, y)`); `false` removes the game's drops. | `WorldGen.ShakeTree` |
| `ModTile.GrowSapling(i, j, underground, treeHeightAddon, ignoreWalls)` | On the mod sapling (`TileID.Sets.TreeSapling`): the default makes the tree grow. | `WorldGen.AttemptToGrowTreeFromSapling` |
| `TreeTypes` | The game's enum plus `Custom`. | |

There is no `SetTreeFoliageSettings` and no paint on mod trees.

## ModPylon and TEModdedPylon

A new teleportation pylon: a `ModTile` (3x4, like the game's) with a
`TEModdedPylon` (the mod's class can be empty). `PylonType` comes after the
game's (`TeleportPylonType.Count`, 11). See
[guide 9](../mods/09-blocos.md#pylons-modpylon).

| | | |
|---|---|---|
| `PylonType` | The type in the pylon system (11, 12...). | |
| `CanPlacePylon()` | The default: only one per type in the world. | `TileObject.CanPlace` |
| `ValidTeleportCheck_NPCCount(pylonInfo, defaultNecessaryNPCCount)` | NPCs around (the destination and the pylon you leave from). The default is the game's (2 town NPCs). | `TeleportPylonsSystem.DoesPositionHaveEnoughNPCs` inside `HandleTeleportRequest` |
| `ValidTeleportCheck_BiomeRequirements(pylonInfo, sceneData)` | The biome around the destination pylon (`SceneMetrics` scanned there); `false` refuses with the game's message. | before `TeleportPylonsSystem.HandleTeleportRequest`. Mobile's `DoesPylonAcceptTeleportation` receives `player` with no value (the game's compiler does not pass it) and cannot be hooked |
| `DrawMapIcon(context, mouseOverText, pylonInfo, isNearPylon, drawColor, deselectedScale, selectedScale)` | The icon on the map. The default: `<Texture>_MapIcon.png` (without it, the vanilla pylon's) and a tap teleports. | `TeleportPylonsMapLayer.Draw` |
| `DefaultDrawMapIcon(context, mapIcon, drawCenter, drawColor, deselectedScale, selectedScale)`, `DefaultMapClickHandle(mouseOver, pylonInfo, hoveringTextKey, mouseOverText)` | tModLoader's helpers (drawing; name and tap). | |
| `DefaultDrawPylonCrystal(spriteBatch, i, j, crystal, crystalHighlight, crystalOffset, shadowColor, dustColor, dustChance, frames)` | The floating crystal, in `SpecialDraw` (without the smart cursor outline). | |
| `RightClick`, `HasSmartInteract`, `DrawEffects` | The default: tapping opens the map; `SpecialDraw` through the top-left cell. | |
| `TEModdedPylon`: `IsTileValidForEntity`, `TryGetModPylon()`, `Hook_AfterPlacement` | The entity at the top-left corner; placing and removing rebuild the game's list. | `TeleportPylonsSystem.UpdatePylonsListAndBroadcastChanges` |

There is no `ValidTeleportCheck_DestinationPostCheck`,
`ValidTeleportCheck_NearbyPostCheck`, `ModifyTeleportationPosition`,
`GetNPCShopEntry` or `TileID.Sets.CountsAsPylon`.

## ModTileEntity

Data and code attached to a placed block (the barrel that fills in the rain,
the machine that counts time). The template (`ModContent.GetInstance(Class)`)
places, finds and removes; each placed object has its own instance. See
[guide 9](../mods/09-blocos.md#tile-entities-modtileentity).

The game does not know about them: they live in Bunny Loader, saved in
`<world>.wld.bl.json` and sent over the mod network. The `ID` starts at
`0x40000000`, far from the game's.

| | | |
|---|---|---|
| `Type`, `ID`, `Position` (`X`, `Y`, `ToWorldCoordinates(x = 8, y = 8)`, `ToVector2()`), `Mod`, `Name`, `FullName` | Fields. `Position` is the object's top-left corner. | |
| `RequiresUpdates` | `false` takes it out of the update loop (the default is "overrode `Update`"). | |
| `IsTileValidForEntity(x, y)` | **Required**: is the block at `(x, y)` still this entity's? One that fails is discarded. | when the world loads, on placement by the server |
| `Update()`, `PreGlobalUpdate()`, `PostGlobalUpdate()` | On every world update, on the server or in single player (never on the client). | `TileEntity.PerformUpdates` |
| `SaveData(tag)`, `LoadData(tag)` | `TagCompound`. | `WorldFile.InternalSaveWorld`/`LoadWorld` |
| `NetSend(writer)`, `NetReceive(reader)` | What goes from the server to the clients. To send again: `NetMessage.SendData(86, -1, -1, null, this.ID, ...)` (tModLoader's `TileEntitySharing`). | whoever joins receives the ones in their world section (`NetMessage.SendSection`) |
| `OnNetPlace()` | On the server, placed at a client's request, before going to everyone. | |
| `OnKill()` | Only through `Kill(i, j)`. | |
| `Load()` | On registration. | |
| `Place(i, j)`, `Kill(i, j)`, `Find(i, j)` | On the template: places it (returns the `ID`), removes it, and the `ID` of the one of this type at `(i, j)` (or `-1`). | |
| `Generic_HookPostPlaceMyPlayer` | For `TileObjectData.newTile.HookPostPlaceMyPlayer`: the entity is created at the top-left corner of what the player placed (on the client, the server places and sends it). | `TileObjectData.CallPostPlacementPlayerHook` |
| `PlacementHook(processedCoordinates)`, `Hook_AfterPlacement(i, j, type, style, direction, alternate)` | The same with a custom hook. | |
| `ModTileEntity.TryGet(i, j, Class)` | The entity of the object at `(i, j)` (any cell) if it is of that class; otherwise `null`. | |
| `ModTileEntity.ByID`, `ModTileEntity.ByPosition`, `ModTileEntity.CountInWorld()` | The ones that exist (`Map`). | |

---

## GlobalItem, GlobalNPC and GlobalProjectile

Code that runs for **vanilla** entities (and mod ones). A Global method applies
to every type: its hook enters JS for every item, NPC or projectile that goes
through there, and `AppliesToEntity` picks in there (see the cost in
[guide 12](../mods/12-globais-e-mundo.md#the-cost)). A method no Global writes
installs no hook.

`X.register(Class)` (by hand; exporting already registers) returns the
template (the one from `ModContent.GetInstance`).

### On all of them

| Member | What for |
|---|---|
| `AppliesToEntity(entity, lateInstantiation)` | `false`: the Global's methods do not run for it. `lateInstantiation` is `false` on the `ModifyNPCLoot` sample. |
| `InstancePerEntity` | `true` (field or getter): each entity gets its own copy, created in `SetDefaults`. |
| `Clone(from, to)` | Returns a copy of the global instance's fields. In the item flow, it is called by `Item.Clone`. |
| `NewInstance(target)` | A new entity's copy. Default: the template's fields. |
| `SetStaticDefaults()`, `AddRecipeGroups()`, `AddRecipes()`, `PostSetupContent()` | Once, with content ready. |
| `this.Mod` | The `Mod` that registered it. |

On the entity: `item.GetGlobalItem(Class)` (or `'Name'`) returns its instance
and throws if the Global does not apply; `item.TryGetGlobalItem(Class, ref)`
returns `true`/`false`. `npc.GetGlobalNPC` and `proj.GetGlobalProjectile`, the
same.

### GlobalItem

| Method | When it runs | Game method |
|---|---|---|
| `SetDefaults(item)` | The item spawns or changes type. | `Item.SetDefaults` |
| `CanUseItem(item, player)` | Before use; `false` prevents it. | `Player.ItemCheck_CheckCanUse_Inner` |
| `UseItem(item, player)` | The use starts; `true` as in `ModItem`. | `Player.ItemCheck_StartActualUse` |
| `UseStyle`, `HoldStyle(item, player, mountOffset, heldItemFrame)`, `HoldItem(item, player)` | Every frame with the item in hand. | `Player.ItemCheck_ApplyUseStyle`/`ApplyHoldStyle` |
| `ModifyWeaponDamage(item, player, damage)` | The weapon's damage; return the new one. | `Player.GetWeaponDamage` |
| `CanShoot(item, player)` | Before the shot; `false` does not shoot. | `Player.ItemCheck_Shoot` |
| `ModifyShootStats(item, player, stats)` | `stats = { position, velocity, type, damage, knockBack }`. | the shot's `Projectile.NewProjectile` |
| `Shoot(item, player, position, velocity, type, damage, knockBack, source)` | `false`: the game's projectile does not come out. | same |
| `CanHitNPC`, `ModifyHitNPC`, `OnHitNPC(item, player, target, hit, damageDone)`, `CanMeleeAttackCollideWithNPC` | The melee hit, as in `ModItem` ([Hits](../mods/16-golpes.md)). | `Player.ProcessHitAgainstNPC` + `NPC.StrikeNPC` |
| `CanHitPvp`, `ModifyHitPvp`, `OnHitPvp` | The PvP melee hit. | `Player.ItemCheck_MeleeHitPVP` + `Hurt` |
| `PreDrawInWorld`, `PostDrawInWorld` | Same arguments as ModItem. Local Pre and Post run before the globals; every applicable Pre runs even after a veto. | internal steps of `Main.DrawItem` |
| `PreDrawInInventory`, `PostDrawInInventory` | Same arguments as ModItem. The global Pre runs before the local one; the local Post runs before the global. Post remains after a veto. | internal steps of `ItemSlot.DrawItemIcon` |
| `ModifyWeaponCrit`, `ModifyWeaponKnockback`, `ModifyItemScale`, `UseItemHitbox`, `MeleeEffects`, `UseAnimation`, `UseItemFrame`, `HoldItemFrame` | As in `ModItem`, after it, for any item. | `ModItem`'s |
| `NeedsAmmo`, `CanChooseAmmo`, `CanBeChosenAsAmmo`, `CanConsumeAmmo`, `CanBeConsumedAsAmmo`, `OnConsumeAmmo`, `OnConsumedAsAmmo`, `PickAmmo(weapon, ammo, player, type, speed, damage, knockback)` | Ammo; a `false` vetoes, in the `null` ones a `true` forces. | same |
| `GetHealLife`, `GetHealMana`, `ModifyPotionDelay`, `ApplyPotionDelay` | Healing and potion delay. | same |
| `UpdateInventory(item, player)` | Every frame, for the 58 inventory slots. | `Player.UpdateEquips` |
| `UpdateEquip(item, player)` | Equipped (accessory or armor). | `Player.ApplyEquipFunctional`, `GrantArmorBenefits` |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Accessory equipped (also vanity). | `Player.ApplyEquipFunctional`/`ApplyEquipVanity` |
| `OnCraft(item, player, recipe)` | Crafted in a recipe. | `Main.CraftItem_GrantItem` |
| `ModifyTooltips(item, tooltips)` | The tooltip; after `ModItem`. | `Main.MouseText_DrawItemTooltip` (JS port) |
| `PreDrawTooltip`, `PostDrawTooltip`, `PreDrawTooltipLine`, `PostDrawTooltipLine` | `ModItem`'s, with `item` first, for any item; after `ModItem`. | same |
| `IsArmorSet(head, body, legs)` → name, `UpdateArmorSet(player, name)` | A set of any items (`''` = none). | `Player.UpdateArmorSets` |
| `IsVanitySet(head, body, legs)` → name, `PreUpdateVanitySet`/`UpdateVanitySet`/`ArmorSetShadows(player, name)` | Vanity by the drawn slots. | `Player.PlayerFrame`, `SetArmorEffectVisuals` |
| `SetMatch(armorSlot, type, male, equipSlot, robes)` | A part's drawn slot (0 head, 1 body, 2 legs). | `Player.SetMatch` |
| `VerticalWingSpeeds(item, player, ...)`, `HorizontalWingSpeeds(item, player, speed, acceleration)`, `WingUpdate(wings, player, inUse)` | Wings, including vanilla ones. | `Player.WingMovement`, `WingAirLogicTweaks`, `WingFrame` |
| `ChoosePrefix(item, rand)`, `PrefixChance(item, pre, rand)`, `AllowPrefix(item, pre)`, `ApplyPrefix(item, pre)` | `ModItem`'s prefix ones, for any item (the global comes first in `ChoosePrefix`). | `Item.RollAPrefix`, `Prefix`, `GetRollablePrefixes` |
| `CanPickup`, `OnPickup(worldItem, player)`, `ItemSpace(item, player)`, `GrabRange(worldItem, player, grabRange)`, `GrabStyle(worldItem, player)` | Picking up from the ground, as in `ModItem`, for any item. The Globals come before `ModItem` (in `GrabRange`, after), as in tModLoader. No native filter: every item in range enters JS. | `ModItem`'s |

### GlobalNPC

| Method | When it runs | Game method |
|---|---|---|
| `SetDefaults(npc)` | The NPC spawns (also on the client, when it arrives over the network). | `NPC.SetDefaults` |
| `OnSpawn(npc, source)` | Created by `NewNPC` (on the server or in single player). | `NPC.NewNPC` |
| `ResetEffects(npc)` | Start of the NPC's update. | `NPC.UpdateNPC_BuffSetFlags` |
| `PreAI(npc)`, `AI(npc)`, `PostAI(npc)` | Every frame. `PreAI` `false` skips the game's AI and the others' `AI`. | `NPC.AI` |
| `HitEffect(npc, hitDirection, damage)` | Took a hit (blood, gore). | `NPC.HitEffect` |
| `CanBeHitByItem`, `ModifyHitByItem`, `OnHitByItem(npc, player, item, hit, damageDone)` | An item hit, as in `ModNPC` ([Hits](../mods/16-golpes.md)). | `NPC.StrikeNPC` (CombatLoader) |
| `CanBeHitByProjectile`, `ModifyHitByProjectile`, `OnHitByProjectile(npc, projectile, hit, damageDone)` | A projectile hit. | same |
| `ModifyIncomingHit(npc, modifiers)` | Last, on every hit. | same |
| `CanHitPlayer`, `ModifyHitPlayer`, `OnHitPlayer(npc, target, hurtInfo)` | Its hit on the player. | `Player.Hurt` |
| `CanHitNPC`, `CanBeHitByNPC`, `ModifyHitNPC`, `OnHitNPC(npc, target, hit)` | NPC against NPC. | `NPC.BeHurtByOtherNPC` |
| `FindFrame(npc, frameHeight)` | After the game's (or the `ModNPC`'s) frame. | `NPC.FindFrame` |
| `CheckActive(npc)`, `CheckDead(npc)` | `false`: does not despawn far away / does not die at 0 life. | `NPC.CheckActive`, `NPC.checkDead` |
| `CanChat(npc)` | `false` prevents, `true` allows, `null` leaves it to the game. | `NPC.CanTalk`/`CanBeTalkedTo` |
| `PreDraw`, `PostDraw`, `DrawEffects`, `GetAlpha` | Drawing (the Globals before the `ModNPC` in `PreDraw`). | `Main.DrawNPCDirect`, `NPC.GetAlpha` |
| `BossHeadSlot`, `BossHeadRotation`, `BossHeadSpriteEffects` | `Ref` of the boss icon. | `NPC.GetBossHead*` |
| `ApplyDifficultyAndPlayerScaling`, `ModifyNPCHappiness` | As in `ModNPC`, for any NPC. | `ModNPC`'s |
| `ModifyShop(shop)` | Once per shop (vanilla or mod), on the first opening: `shop.Add` goes after its items. | `InventoryStorage.SetupShop` |
| `SetupShop(type, shop, nextSlot)` | The old format: `shop.item`, the free slot in `nextSlot` (`Ref`). | same |
| `ModifyActiveShop(npc, shopName, items)` | On every opening, with the items ready. | same |
| `SetupTravelShop(shop, nextSlot)` | The Traveling Merchant's shop (`Main.travelShop`). | `InventoryStorage.SetupTravelShop` |
| `PreKill(npc)`, `OnKill(npc)` | Death with drops, only on the server or in single player. `PreKill` `false`: no drop and no `OnKill`. | `NPC.NPCLoot` |
| `GetChat(npc, chat)` | The line; `chat` is a `Ref`. | `NPC.GetChat` |
| `NetSend(npc, writer)`, `NetReceive(npc, reader)` | Network: along with each NPC the server syncs. | `NetMessage.SendData` (23) |
| `ModifyNPCLoot(npc, npcLoot)` | Once per NPC type, with the game's sample. | native (when loading finishes) |
| `ModifyGlobalLoot(globalLoot)` | Once. | same |
| `EditSpawnFlags(spawnInfo)` | Before the rate, the area and the point: the player's fields in `spawnInfo` (`noWorms`, `invaders`, `ZoneCorrupt`...). The point does not exist yet (`SpawnTileX` = -1). | `NPC.Spawner.SetSpawnFlags` |
| `EditSpawnRate(player, spawnRate, maxSpawns)` | On each spawn attempt for the player. `Ref`: lower `spawnRate` = more spawns; `maxSpawns`, how many enemies nearby. | `NPC.Spawner.GetSpawnRate` |
| `EditSpawnRange(player, spawnRangeX, spawnRangeY, safeRangeX, safeRangeY)` | `Ref`, in tiles: how far out it spawns and the minimum distance from the player. | `NPC.Spawner.GetSpawnArea` |
| `EditSpawnInfo(spawnInfo)` | With the point chosen, before the roll: the `spawnInfo` fields (`waterTile`, `nearGranite`...) change what the game and `SpawnChance` read. | `NPC.Spawner.SetSpawnFlagsForChosenTile` |
| `EditSpawnPool(pool, spawnInfo)` | The roll (`SpawnPool`): `pool[type] = weight`; `0` is the game's spawn. | `NPC.Spawner.SpawnAnNPC` |
| `SpawnNPC(npc, tileX, tileY)` | A rolled NPC that is not the game's spawned (`npc`, the index). | same |

Natural spawning only runs in single player or on the server; `player` and
`spawnInfo.Player` are the target player (in multiplayer, it is not
`Main.LocalPlayer`).

```js
export class MoreEnemiesInTheBiome extends GlobalNPC {
    EditSpawnRate(player, spawnRate, maxSpawns) {
        if (player.InModBiome(MyBiome)) {
            spawnRate.value = Math.floor(spawnRate.value * 0.5);   // twice the spawns
            maxSpawns.value = Math.floor(maxSpawns.value * 1.5);
        }
    }
    EditSpawnPool(pool, spawnInfo) {
        if (spawnInfo.Player.InModBiome(MyBiome)) pool[0] = 0.25;   // fewer vanilla enemies
    }
}
```

### GlobalProjectile

| Method | When it runs | Game method |
|---|---|---|
| `SetDefaults(projectile)` | The projectile spawns (also on the other side of the network). | `Projectile.SetDefaults` |
| `OnSpawn(projectile, source)` | Created by `NewProjectile`, on whoever created it. | `Projectile.NewProjectile` |
| `PreAI`, `AI`, `PostAI(projectile)` | Every frame. | `Projectile.AI` |
| `PreKill(projectile, timeLeft)` | Before death. `false` removes the projectile without the native effect and ends the flow before `OnKill`. | `Projectile.Kill` |
| `OnKill(projectile, timeLeft)` | After the native death, when `PreKill` did not veto. | `Projectile.Kill` |
| `CanHitNPC`, `ModifyHitNPC`, `OnHitNPC(projectile, target, hit, damageDone)` | The hit on an NPC, as in `ModProjectile` ([Hits](../mods/16-golpes.md)). | `Projectile.Damage_PVE` + `NPC.StrikeNPC` |
| `CanHitPlayer`, `CanHitPvp`, `ModifyHitPlayer`, `OnHitPlayer(projectile, target, info)` | The hit on a player (hostile / PvP). | `Player.Hurt` |
| `PreDraw(projectile, lightColor)` | Color in a `Ref<Color>`; `false` cancels the sprite. Every Global runs before the local method, keeping extras like chains. | `Main.DrawProjDirect` |
| `PostDraw(projectile, lightColor)` | Direct color; runs after the local method, even with a draw veto. | `Main.DrawProjDirect` |
| `GetAlpha(projectile, lightColor)` | The first global `Color` decides; `undefined`/`null` asks the local method and the game. | `Projectile.GetAlpha` |
| `Colliding(projectile, projHitbox, targetHitbox)` | The first global boolean decides; `undefined`/`null` asks the local method and the game. | `Projectile.Colliding`, `Projectile.Damage` |
| `CanDamage(projectile)` | A global `false` vetoes; a global `true` wins over the local method. Native conditions still apply. | `Projectile.Damage` |
| `ModifyDamageHitbox(projectile, hitbox)` | `Ref<Rectangle>`, after the local method. | `Projectile.Damage_GetHitbox` |
| `OnTileCollide(projectile, oldVelocity)` | `false` prevents death by impact; time and NPC still call `OnKill`. Every Global runs before the local method. | `Projectile.HandleMovement`, `UpdatePosition`, `Kill` |
| `TileCollideStyle(projectile, width, height, fallThrough, hitboxCenterFrac)` | Four `Ref`s, after the local method; the first `false` skips collision for this movement. | `Projectile.HandleMovement`, `GetCollisionParams` |
| `MinionContactDamage(projectile)` | `true` allows a pet's contact damage. | `Projectile.Damage` |
| `CanCutTiles(projectile)` | The first global boolean decides; `false` prevents default and custom cutting. | `Projectile.CanCutTiles` |
| `CutTiles(projectile)` | Globals, the local method and the default cutting, when allowed. | `Projectile.CutTiles` |
| `CanUseGrapple(type, player)` | On the template, filtered by a sample of the type; the last global boolean answer decides. | `Player.FireGrapple` |
| `UseGrapple(player, type)` | Type in a `Ref<number>`, after the local method, for every template that implements the method. | `Player.FireGrapple` |
| `GrappleCanLatchOnTo(projectile, player, tile)` | Receives the native tile; a global `false` vetoes, a global `true` allows. | `Projectile.AI_007_GrapplingHooks_CanTileBeLatchedOnTo` |
| `ShouldUpdatePosition(projectile)` | `false`: the game does not add velocity to position. After the local method; one `false` is enough. | `Projectile.UpdatePosition` |
| `NetSend(projectile, writer)`, `NetReceive(projectile, reader)` | Network: along with each synced projectile, from whoever controls it. | `NetMessage.SendData` (27) |

The [catalog](metodos.md#globalprojectile) includes the full signatures and the
methods inherited from `GlobalType`. The
[GlobalProjectile validation](../../tools/tests/globalprojectilehooks/RESULTADOS.md)
records 59 JavaScript checks and 38 in-game single player checks, with no
failures. The optional returns, the order with `ModProjectile` and
`TileCollideStyle`'s four parameters are detailed in the
[test contract](../../tools/tests/globalprojectilehooks/README.md).

### Not yet

`GlobalItem`'s `NetSend`/`NetReceive`, per-entity `SaveData`/`LoadData`,
`GlobalWall`; on `GlobalNPC`, `UpdateLifeRegen`, the town NPC attacks and the
chat buttons. For tiles, buffs, boss bars and recipes, see
[the type-wide Globals](#globaltile-globalbuff-globalbossbar-and-globalrecipe).

---

## GlobalLoot

The drop rules that apply to **every** NPC (the game's `_globalEntries`), and
what `ModifyGlobalLoot` receives.

| Member | What for |
|---|---|
| `Add(rule)`, `Remove(rule)` | Adds or removes a global rule. |
| `Get()` | The global rules, in an array. |
| `RemoveWhere(rule => ...)` | Removes the matching ones; returns how many. |

ExMod's way: `class X extends GlobalLoot { ModifyGlobalLoot() {...} }` and
`GlobalLoot.register(X)`. Inside, `this.RegisterToNPC(id, rule)`,
`this.RemoveFromNPC(id, rule)` and `this.GetRulesForNPCID(id)` (the game's
`List`) change an NPC's table, and the Bestiary follows.

`NPCLoot` (the one from `ModifyNPCLoot`) has `Add`, `Remove`, `Get()` and
`RemoveWhere`, the same.

---

## GlobalTile, GlobalBuff, GlobalBossBar and GlobalRecipe

Code for **every** tile, buff, boss bar or recipe, the game's included. There
is no per-entity copy: one instance per class, and the methods receive the
type. A method no Global writes installs no hook. `SetStaticDefaults()` and
`PostSetupContent()` run once, with content ready. See
[guide 12](../mods/12-globais-e-mundo.md#tiles-buffs-boss-bars-and-recipes).

### GlobalTile

`ref` parameters arrive as `Ref`. They have no native type filter: the
per-frame-per-cell methods (drawing, light, `NearbyEffects`, `TileFrame`) were
left out, since on mobile they would enter JS for every tile on screen.

| Method | When it runs | Behind it |
|---|---|---|
| `SetStaticDefaults()` | With `TileObjectData` unlocked: changing a vanilla tile's shape or anchors works here. | |
| `PostSetupTileMerge()` | Once, after setup (`Main.tileMerge`). | |
| `CanKillTile(i, j, type, blockDamaged)` | `false`: the tile does not break. | `WorldGen.CanKillTile` |
| `KillTile(i, j, type, fail, effectOnly, noItem)` | Before breaking (the three are `Ref`). | `WorldGen.KillTile` |
| `NumDust(i, j, type, fail, num)` | How many particles (`num`, `Ref`; only lowers it). | `WorldGen.KillTile` |
| `CreateDust(i, j, type, dustType)` | Each particle, after the game picks it: `dustType` (`Ref`) swaps it; `false` removes it. | `WorldGen.KillTile_MakeTileDust` |
| `KillSound(i, j, type, fail)` | `false`: no sound. | `WorldGen.KillTile_PlaySounds` |
| `CanDrop(i, j, type)`, `Drop(i, j, type)` | `false`: the block drops nothing; `Drop` comes after its items. Vanilla multi-cell objects drop through their own code. | `WorldGen.KillTile_DropItems` |
| `CanPlace(i, j, type)` | `false`: the player does not place it. | `Player.PlaceThing_Tiles_BlockPlacementForAssortedThings` |
| `PlaceInWorld(i, j, type, item)` | After the player places it. | `TileObjectData.CallPostPlacementPlayerHook`, `WorldGen.PlaceTile` |
| `CanExplode(i, j, type)` | `false`: a projectile's explosion spares it. | `Projectile.CanExplodeTile` |
| `Slope(i, j, type)` | `false`: the player's hammer leaves it as it is. | `WorldGen.SlopeTile`/`PoundTile` inside `ItemCheck_UseMiningTools_TryPoundingTile` |
| `RandomUpdate(i, j, type, underground)` | Each tile the world update picks (hundreds per frame). | `WorldGen.UpdateWorld_OvergroundTile`/`UndergroundTile` |
| `PreHitWire(i, j, type)`, `HitWire(i, j, type)` | A wire signal; `false` stops it (the game's and `HitWire`). | `Wiring.HitWireSingle` |
| `RightClick(i, j, type)`, `MouseOver(...)`, `MouseOverFar(...)` | The tap on a tile, the tile under the cursor (near and far). | `Player.TileInteractionsUse`/`MouseOver`/`CheckLongDistance` |
| `AnimateTile()` | Once per frame. | `Main.AnimateTiles` |
| `AdjTiles(type)` | The types a tile of `type` counts as near the player (crafting stations). Asked once per type. On mobile the game only checks with the inventory open. | `Player.AdjTiles` |
| `PreShakeTree(x, y, treeType)`, `ShakeTree(x, y, treeType)` | Shaking a tree (`TreeTypes`): `PreShakeTree` before the drops; `ShakeTree` returning `true` means it dropped the main item. | `WorldGen.ShakeTree` |

### GlobalBuff

| Method | When it runs | Behind it |
|---|---|---|
| `UpdatePlayer(type, player, buffIndex)`, `UpdateNPC(type, npc, buffIndex)` | Every frame, each active buff (tModLoader's two `Update`). `buffIndex` is `Ref`: whoever removes the buff lowers it by 1. | `Player.UpdateBuffs`, `NPC.UpdateNPC_BuffSetFlags` |
| `ReApplyPlayer(type, player, time, buffIndex)`, `ReApplyNPC(...)` | The buff was there and got another `AddBuff`. `true`: the game does not renew the time (as in tModLoader; `ModBuff`'s is the other way round). | `Player.AddBuff_TryUpdatingExistingBuffTime`, `NPC.AddBuff` |
| `ModifyBuffText(type, buffName, tip, rare)` | After the `ModBuff`'s own. | `Lang.GetBuffName`, `Main.GetBuffTooltip` |
| `PreDraw(spriteBatch, type, buffIndex, drawParams)`, `PostDraw(...)` | The icon. `drawParams`: `{ Texture, Position, TextPosition, SourceRectangle, MouseRectangle, DrawColor }`; what changes applies to the drawing. `false` hides it; `PostDraw` runs anyway. | `GUIBuffs.ItemDraw` |
| `RightClick(type, buffIndex)` | The tap that cancels the buff; `false` keeps it. | `GUIBuffs.RemoveBuff` |

### GlobalBossBar

| Method | When it runs |
|---|---|
| `PreDraw(spriteBatch, npc, drawParams)` | Before each bar. `false`: no bar (`PostDraw` runs anyway). |
| `PostDraw(spriteBatch, npc, drawParams)` | After. |

`drawParams` is tModLoader's `BossBarDrawParams`: `BarTexture`, `BarCenter`,
`BarColor`, `IconTexture`, `IconFrame`, `IconColor`, `Life`, `LifeMax`,
`Shield`, `ShieldMax`, `IconScale`, `ShowText`, `TextOffset`. Every bar on
mobile, the game's and the `ModBossBar`s, follows the mobile layout (under the
minimap, `GUIBossBar.Draw`): `Life`, `LifeMax`, `Shield`, `ShieldMax`,
`IconTexture`, `IconFrame`, `BarColor`, `IconColor` and `ShowText` apply;
`BarCenter` says where the bar is but changing it does not move it,
`IconScale` and `TextOffset` do not apply, and `BarTexture` is `null` (there
is no PC `UI_BossBar` frame).

### GlobalRecipe

tModLoader 1.4 dropped this class in favor of each recipe's conditions and
callbacks (which [`ModRecipe`](#modrecipe) has); the Bunny Loader keeps its
three hooks, for every recipe.

| Method | When it runs | Behind it |
|---|---|---|
| `RecipeAvailable(recipe)` | `false`: the recipe leaves the crafting menu (the Guide still shows it). | `Recipe.FindRecipes` |
| `ConsumeItem(recipe, type, amount)` | Each ingredient on each craft: `amount` (`Ref`) is how much is spent, already with the Alchemy Table discount. Never more than the player has. | `Recipe.GetIngredientCraftingDiscount` |
| `OnCraft(item, recipe)` | The crafted item, before it goes to the cursor or the inventory. | `Main.CraftItem_GrantItem` |

---

## ModSystem

What belongs to the whole mod: recipes, the world cycle and the data saved in
it. Each world method only gets a hook if some `ModSystem` wrote it.

| Method | When it runs | Game method |
|---|---|---|
| `OnModLoad()` | On `register`. | — |
| `AddRecipeGroups()` | Once, before any recipe. | native |
| `AddRecipes()`, `PostAddRecipes()` | Once, with the game's recipes ready. | native |
| `PostSetupContent()` | Once, with all mod content in the game. | native |
| `ClearWorld()` | On entering any world (and before generating one). | `WorldGen.clearWorld` |
| `OnWorldLoad()` | The world opened; on the client, when it arrives from the server. | `WorldFile.LoadWorld`; on the client, `WorldGen.clearWorld` |
| `LoadWorldData(tag)` | After `OnWorldLoad`, with the world's data. Only on the server or in single player. | same |
| `PostWorldLoad()` | After the data. | same |
| `SaveWorldData(tag)` | On each world save. Only on the server or in single player. | `WorldFile.InternalSaveWorld` |
| `PreSaveAndQuit()` | On leaving, before saving. | `WorldGen.SaveAndQuit` |
| `OnWorldUnload()` | After leaving. | `WorldGen.SaveAndQuitCallBack` |
| `PreUpdateWorld()`, `PostUpdateWorld()` | Every frame. Only on the server or in single player. | `WorldGen.UpdateWorld` |
| `PreUpdateTime()`, `PostUpdateTime()` | Every frame. Only on the server or in single player. | `Main.UpdateTime` |
| `PostUpdateEverything()` | Every frame, on everyone. | `Main.DoUpdateInWorld` |
| `ResetNearbyTileEffects()` | Before the game counts the blocks around the local player (every 5 frames), and also on leaving the world and loading another (the previous count does not apply to the new one). | `SceneMetrics.Reset`, `WorldGen.SaveAndQuit`, `WorldGen.clearWorld` |
| `TileCountsAvailable(tileCounts)` | With the count ready: `tileCounts[type]` is how many blocks of that type are around. Valid during the call: keep the number, not the array. Only the local player's scan (the pylons' and the camera's do not call it). | `SceneMetrics.AggregateTileCounts` |
| `NetSend(writer)`, `NetReceive(reader)` | Network: the server sends it along with the world data (on joining and on each sync); the client reads. | `NetMessage.SendData` (7) |
| `OnModUnload()` | Once on a normal exit or on the JS engine's shutdown. | `Main.QuitGame`, runtime shutdown |
| `OnLocalizationsLoaded()` | After applying the mods' texts, initially and on each language change. | `LanguageManager.LoadFilesForCulture`, deferred initial application |
| `ResizeArrays()` | Content ready, before the setup phases. | Ready's resize phase |
| `PostSetupRecipes()` | After every recipe, post-hooks and caches. | Ready's finish phase |
| `PreWorldGen()` | Before running the generation list already built by mobile. | `WorldGenerator.GenerateWorld` |
| `ModifyWorldGenTasks(tasks)` | A JS array of GenPass: insert, remove, reorder, change weight and disable. | same |
| `PostWorldGen()` | After a completed generation; cancelling does not call it. | same |
| `ModifyHardmodeTasks(tasks)` | A JS array; a single native Hardmode Conversion step. | `WorldGen.initializeHardMode` |
| `UpdateUI(gameTime)` | After the in-world UI update, even while paused. | `Main.UpdateUIStates` |
| `PostUpdateInput()` | After input processing. | `Main.DoUpdate_HandleInput` |
| `PreUpdateEntities()` | Before the entity groups. | entry of `Main.DoUpdateInWorld` |
| `PreUpdatePlayers()`, `PostUpdatePlayers()` | Once at the edges of the player group. | internal points of `Main.DoUpdateInWorld` |
| `PreUpdateNPCs()`, `PostUpdateNPCs()` | Once at the edges of the NPC group. | same |
| `PreUpdateGores()`, `PostUpdateGores()` | Once at the edges of the gore group. | same |
| `PreUpdateProjectiles()`, `PostUpdateProjectiles()` | Once at the edges of the projectile group. | `Main.PreUpdateAllProjectiles`, internal point of `Main.DoUpdateInWorld` |
| `PreUpdateItems()`, `PostUpdateItems()` | Once at the edges of the WorldItem group. | internal points of `Main.DoUpdateInWorld` |
| `PreUpdateDusts()`, `PostUpdateDusts()` | Before and after updating particles. | `Dust.UpdateDust` |
| `PreUpdateInvasions()`, `PostUpdateInvasions()` | Before and after the invasion routine. | `Main.UpdateInvasion` |
| `ModifyTimeRate(timeRate, tileUpdateRate, eventUpdateRate)` | Three independent Refs; double clock, tiles and events with fractional accumulation. | `Main.UpdateTimeRate`, internal point of `Main.UpdateTime` |
| `ModifySunLightColor(tileColor, backgroundColor)` | Two color Refs after the native calculation. | `Main.ApplyColorOfTheSkiesToTiles` |
| `ModifyLightingBrightness(scale)` | A brightness scale Ref after the native calculation. | `Lighting.UpdateGlobalBrightness` |
| `ModifyScreenPosition()` | After the camera and ModPlayer, inside the world; change Main.screenPosition. | shared hook of `Main.DoDraw_UpdateCameraPosition` |
| `ModifyTransformMatrix(transform)` | A SpriteViewMatrix Ref after the zoom, before the matrix is read. | internal point of `Main.DoDraw` |
| `SaveWorldHeader(tag)` | Header in `.wld.bl.header.json`, separate from the full data. | `WorldFile.InternalSaveWorld` |
| `CanWorldBePlayed(playerData, worldData)` | false blocks the character and world combination. | `GUIWorldSelectMenu.CanWorldBePlayed`, `PlayWorldCheck`, `WorldFile.LoadWorld` |
| `WorldCanBePlayedRejectionMessage(playerData, worldData)` | The message from the first system that blocked. | the loader's rejection flow |
| `HijackGetData(messageType, reader, playerNumber)` | Type and reader Refs; any true cancels the packet. | `MessageBuffer.ProcessData` |
| `HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7)` | Any true cancels sending and the entity envelope. | shared `NetMessage.SendData` pipeline |

The data goes to `<world>.wld.bl.json`, next to the `.wld`, one entry per
`ModSystem` (the key is the mod uid and the class name).
`ModSystem.register(Class)` returns the instance (the one from
`ModContent.GetInstance`).

The generation, header, rate and network contracts are in
[ModSystem: generation and mobile integration](modsystem-hooks.md).
`RequiresScreenTarget` was dropped for lack of a compatible capture path in
the mobile renderer. `ModifyInterfaceLayers` remains unimplemented.

---

## ModBiome and ModSceneEffect

A **biome** says, every frame, whether the player is in it. The count of
blocks around comes through `ModSystem.TileCountsAvailable`, as in tModLoader:

```js
export class BiomeCount extends ModSystem {
    blocks = 0;
    TileCountsAvailable(tileCounts) {
        this.blocks = tileCounts[ModContent.TileType('ExampleTile')];
    }
}

export class MyBiome extends ModBiome {
    SetStaticDefaults() {
        this.Music = MusicLoader.GetMusicSlot('Music/MyMusic');
    }
    IsBiomeActive(player) {
        return ModContent.GetInstance(BiomeCount).blocks >= 40 && player.ZoneOverworldHeight;
    }
    OnEnter(player) {}
}
```

| `ModBiome` | |
|---|---|
| `IsBiomeActive(player)` | Every frame, after the game updates its zones. An error counts as `false` in this evaluation. |
| `OnEnter(player)`, `OnLeave(player)` | Once, on the change. When leaving the world inside the biome, `OnLeave` comes on exit. |
| `OnInBiome(player)` | Every frame inside, including the entry frame. |
| `player.InModBiome(Class)` | Whether the player is in the biome. Accepts the class, the instance (`ModContent.GetInstance`) or the `Type`. |
| `Type` | The position among the biomes (the same class in two mods is two biomes). |
| `DisplayName`, `TownNPCDialogueName` | The names, as in tModLoader: `Biomes.<Class>.DisplayName` and `.TownNPCDialogueName` in the mod's `Localization/<culture>.json` (keys `Mods.<id>.Biomes.<Class>...`). Without them, the class name split into words ("My Biome") and "the My Biome". A `DisplayName` written on the class (text or `{ culture: text }`) wins over the file. After registration, both are `{ Key, Value }`. |
| `BestiaryIcon`, `BackgroundPath` | Paths in the mod: the class's + `_Icon` (30 x 30, the filter icon) and + `_Background` (115 x 65, the portrait background in the Bestiary), as in tModLoader. Without the file, or at another size, the game's. The map background can reuse `BackgroundPath`. |
| `BackgroundColor` | The portrait background color in the Bestiary (`Color`), or `null`. |
| In the Bestiary | The biome shows up in the entry of each NPC that has it in `SpawnModBiomes`, and as a filter (if some NPC uses it). |
| In happiness | `this.Happiness.SetBiomeAffection(MyBiome, AffectionLevel.Love)` on a town NPC: its price and lines change with the player in the biome (the line uses `TownNPCDialogueName`). |
| Defaults | `Priority` `BiomeLow` and `Music` `0` (silence), as in tModLoader: with no music of your own, declare `Music = -1`. |

A **scene effect** (`ModSceneEffect`) is the same without the flags:
`IsSceneEffectActive(player)` says whether it is active. Among the active
effects (biomes too), the one with the highest `Priority` +
`GetWeight(player)` (0 to 1) gives the scene's music, in
`player.CurrentSceneEffect.music`. `SpecialVisuals(player, isActive)` runs
every frame for all of them, active or not.

`Music` and `Priority` can come as a field (`this.Music = ...` in
`SetStaticDefaults`) or as a getter (`get Music() { ... }`, as in tModLoader).

The scene's music plays by the rules of [sound and music](#sound-and-music).

The game only evaluates the local player's biomes, every frame (also while
dead). Teleporting and respawning reevaluate right away. In **multiplayer**,
each device sends its own player's flags when they change, and whoever joins
receives everyone's. So another player's `InModBiome` works on any device,
including the server, where spawning runs (`SpawnChance` with
`info.Player.InModBiome(...)`). For a remote player there is no
`OnEnter`/`OnInBiome`/`OnLeave`, as in tModLoader.

The scene effect also picks the background and the water:
`SurfaceBackgroundStyle`, `UndergroundBackgroundStyle` and `WaterStyle` return
the style's instance (see [mod backgrounds](#mod-backgrounds) and
[mod water](#mod-water)), or `null`.

And the **full-screen map** background, as in tModLoader:

```js
export class MyBiome extends ModBiome {
    get MapBackground() { return this.BackgroundPath; }   // or 'Biomes/MyMapBackground'
    // ...
}
```

| `ModSceneEffect` (map) | |
|---|---|
| `MapBackground` | The path of a mod texture, or `null`. It covers the map screen instead of the game's background. The path is read on every draw (it can change); the texture loads the first time. |
| `MapBackgroundFullbright` | `true`: always white. The default (`false`) is the sky color with the map screen at the surface and white below it. |
| `MapBackgroundColor(color)` | `Ref` (`.value`, a `Color`): the final color, after the two above. |

**Not yet**: the biome's torch and campfire (`BiomeTorchItemType`,
`BiomeCampfireItemType`).

### Mod backgrounds

As in tModLoader. Every PNG in a `Backgrounds/` folder of the mod (in the
Example Mod, `Assets/Textures/Backgrounds`) gets a texture number after the
game's 344 (`TextureAssets.Background`, `Main.backgroundWidth`/`Height` grow):

| `BackgroundTextureLoader` | |
|---|---|
| `GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Name')` | The number. Also `GetBackgroundSlot('Assets/...')` (the calling mod's) and `GetBackgroundSlot('examplemod/Assets/...')` (the mod's id or class name in front). Throws if it does not exist. |
| `TryGetBackgroundSlot(path, ref)` | `true` and the number in `ref.value`, or `false`. |
| `AddBackgroundTexture(mod, path)` | A PNG outside a `Backgrounds/` folder; only while the mod loads. |

```js
export class MyBackground extends ModSurfaceBackgroundStyle {
    ChooseFarTexture() { return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Far'); }
    ChooseMiddleTexture() { return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Middle'); }
    ChooseCloseTexture(scale, parallax, a, b) { return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Close'); }
}

export class MyUndergroundBackground extends ModUndergroundBackgroundStyle {
    FillTextureArray(slots) {
        for (let i = 0; i < 4; i++) slots[i] = BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Cavern' + i);
    }
}

export class MyBiome extends ModBiome {
    get SurfaceBackgroundStyle() { return ModContent.GetInstance(MyBackground); }
    get UndergroundBackgroundStyle() { return ModContent.GetInstance(MyUndergroundBackground); }
    // ...
}
```

| `ModSurfaceBackgroundStyle` | |
|---|---|
| `Slot` | The style's number, after the game's 16 (`Main.bgStyle` becomes it). |
| `ChooseFarTexture()`, `ChooseMiddleTexture()` | The far texture (behind the middle mountains) and the middle one (behind the background trees), or `-1`. Every frame: it can animate. |
| `ChooseCloseTexture(scale, parallax, a, b)` | The front one, or `-1`. All four are `Ref`s (`.value`): scale 1.25, parallax 0.37, height `a` 1800 and `b` 1750, as in tModLoader. |
| `PreDrawCloseBackground(spriteBatch)` | `false` does not draw the front one. |
| `ModifyFarFades(fades, transitionSpeed)` | Each style's transparency (`fades[i]`), every frame with this style on screen. The default raises this one's and lowers the others. |

| `ModUndergroundBackgroundStyle` | |
|---|---|
| `Slot` | The style's number, after the game's 22. |
| `FillTextureArray(slots)` | `slots[0]` the edge with the sky (160x16), `[1]` the dirt (160x96), `[2]` the edge between dirt and stone (160x16), `[3]` the stone (160x96). `[4]`, the passage to the underworld, comes with the common cavern's. |

The background goes in by the scene's `Priority`, on tModLoader's tiers. On
the surface, `BiomeLow` only replaces the forest, `BiomeMedium` also the jungle
and the snow, and `BiomeHigh` replaces everything. Underground, `BiomeLow` only
replaces the common cavern, `BiomeMedium` also the snow and the jungle, and
`BiomeHigh` replaces everything. The switch has the game's transition, and on
leaving the scene the background goes back to the game's.

With the background turned off in the options, the underground uses the
game's old drawing and keeps the game's style. The title screen background
comes from [`ModMenu`](#modmenu). **Not yet**: `GlobalBackgroundStyle`.

### Mod water

As in tModLoader. The texture comes from the class's file path
(`Content/Biomes/MyWater.js` -> `Content/Biomes/MyWater.png`, the water
surface, 48x1360 like the game's), plus `MyWater_Block.png` (the block,
306x16) and `MyWater_Slope.png` (the slope, 72x16; without it, the block). The
waterfall is a 512x40 PNG named after its class.

```js
export class MyWaterfall extends ModWaterfallStyle {
    AddLight(i, j) { Terraria.Lighting['void AddLight(int i, int j, float r, float g, float b)'](i, j, 0.5, 0.5, 0.5); }
}

export class MyWater extends ModWaterStyle {
    ChooseWaterfallStyle() { return ModContent.GetInstance(MyWaterfall).Slot; }
    GetSplashDust() { return Terraria.ID.DustID.BlueCrystalShard; }
    GetDropletGore() { return ModGore.getTypeByName('MyDroplet'); }
}

export class MyBiome extends ModBiome {
    get WaterStyle() { return ModContent.GetInstance(MyWater); }
    // ...
}
```

| `ModWaterStyle` | |
|---|---|
| `Slot` | The style's number, after the game's 15 (`Main.waterStyle` becomes it). |
| `ChooseWaterfallStyle()` | A `ModWaterfallStyle`'s `Slot`, or a vanilla waterfall (0 is the forest's). |
| `GetSplashDust()` | The splash dust (whoever falls into the water). Default: the game's. |
| `GetDropletGore()` | The droplet that drips from the block. A mod one (in a `Gores/` folder) behaves like the game's water droplet. |
| `LightColorMultiplier(r, g, b)` | `Ref` (`.value`): how much light passes through the water. `1, 1, 1` loses no light. |
| `GetRainVariant()` | The rain variant: the texture column ÷ 4 (in the game's, 0 to 2 is the forest's). The default is the game's 0-to-2 roll, at no cost; overriding costs one JS entry per new drop. |
| `GetRainTexture()` | The rain texture: the path of a PNG in the mod (4 px columns, 40 high, like the game's), or `null` (the game's, the default). |
| `BiomeHairColor()` | The biome hair dye color with this water (a `Color`). Default: the forest's. |

| `ModWaterfallStyle` | |
|---|---|
| `Slot` | The number, after the game's 28. |
| `AddLight(i, j)` | Light in the waterfall, every 3 tiles of the fall (mobile bakes the vanilla waterfalls' light and color into the drawing; the mod one runs after it). |
| `ColorMultiplier(r, g, b, a)` | Each piece's color: `r`, `g`, `b` are `Ref`s (`.value`, 0 to 255: the spot's light times the opacity) and `a`, the opacity. For colors that change over time. |

The water goes in with `Priority` `BiomeLow` or higher and only where the game
would use the common background's, as in tModLoader: an active water fountain,
the blood moon and the game's biome backgrounds (jungle, desert, snow...) win.
The switch has the game's fade, and on leaving the water goes back to the
game's.

---

## ModMenu

A title screen theme, as in tModLoader: the logo, the sun, the moon, the
background and the music while the game is in the menus. The player picks the
theme in **Settings > General > Menu Theme** (in the main menu, right below
Mod Configuration): each tap on the button moves to the next available theme,
with the game's ("Terraria") in the rotation. The choice is stored in
`BunnyLoader.menu.json`, in the saves folder. With no choice, or with the
chosen theme unavailable (mod disabled, `IsAvailable` false), the last loaded
mod's applies; with none at all, the game's title. In tModLoader the switch is
through the text at the bottom of the title screen.

```js
export class MyTheme extends ModMenu {
    get DisplayName() { return 'My Theme'; }
    get SunTexture() { return 'Assets/Textures/Menu/MySun'; }
    get MoonTexture() { return 'Assets/Textures/Menu/MyMoon'; }
    get MenuBackgroundStyle() { return ModContent.GetInstance(MyBackground); }
    SetStaticDefaults() {
        this.Music = MusicLoader.GetMusicSlot('Music/MyTitle');
    }
    PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor) {
        drawColor.value = Color.new(255, 120, 120, 255);
        return true;
    }
}
```

| `ModMenu` | |
|---|---|
| `Logo` | The logo texture: the path in the mod (`'Assets/Textures/Menu/Logo'`, or `'othermod/...'`) or an `Asset` (`ModContent.Request`). `null` (the default): the game's logo, the day and night ones. |
| `SunTexture`, `MoonTexture` | The sun and the moon, the same way. The moon is a single frame (no phases). `null`: the game's. |
| `Music` | The menus' music: `MusicLoader.GetMusicSlot(...)` or a vanilla `MusicID`. `-1` (the default): the game's. The game's stays during world generation with a special seed and in the credits, as in tModLoader. Get the slot once (`SetStaticDefaults`), not in a `get`: the `get` is read every frame. |
| `MenuBackgroundStyle` | A `ModSurfaceBackgroundStyle` (`ModContent.GetInstance(Class)`), or `null`: the game's background. |
| `DisplayName` | The name on the Menu Theme button. `null` (the default): the mod's name. A long name is cut with "...". |
| `IsAvailable` | `false` takes the theme out of the rotation (if chosen, the automatic one applies). It can be `get IsAvailable()`, for a theme that only applies on certain dates. |
| `IsSelected` | Whether it is the theme in effect now (read-only). |
| `OnSelected()`, `OnDeselected()` | When the theme takes effect (on game launch, already in the menus, or on tapping Menu Theme) and when it stops. |
| `Update(isOnTitleScreen)` | Every frame in the menus; `isOnTitleScreen` on the title screen (`menuMode` 0). |
| `PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | Before the logo. The last four are `Ref`s (`.value`): the center, rotation, scale and color of the game's logo on this frame. `false` does not draw the logo. |
| `PostDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | After the logo, with the values it was drawn with. |

The values can be fields (`this.Logo = ...`) or `get`ters (as in tModLoader),
and are read every frame. The theme's background stays out during world
generation with a special seed, as in tModLoader. On mobile, the theme's logo,
sun and moon only go into the title screen drawing; in the world everything
stays as in the game. **Differences from tModLoader**: without tModLoader's
logo, the default is the game's, and there are none of the old game's
ready-made themes (1.3.5.3, Bigger and Boulder). There is no theme
`UserInterface` either.

---

The new update, generation, drawing, content cycle and network hooks are
described in [ModSystem: contracts and limitations](modsystem-hooks.md).
`RequiresScreenTarget` was dropped: tModLoader's screen capture does not exist
in mobile's Unity drawing path.

## GenPass and PassLegacy

`GenPass` represents a step of the mobile generator. Extend the class and
implement `ApplyPass(progress, configuration)`, or use
`new PassLegacy(name, function, weight)`. Name and weight must be valid; zero
weight is allowed. `Name`, `Weight` and `Enabled` reflect the native step;
`Disable()` and `Enable()` control execution.
`Apply(progress, configuration)` respects `Enabled`. The constructor uses
weight 1 by default.

`ModifyWorldGenTasks(tasks)` receives a JavaScript array of steps, including
the native ones. Use `findIndex`, `splice`, `push` and `Disable()`; changes are
applied to the game's list before generating. Read the
[example and the Hardmode adaptation](modsystem-hooks.md).

## TagCompound

The data a mod saves: the `tag` of `SaveWorldData`/`LoadWorldData` and of
`ModPlayer`'s `SaveData`/`LoadData`. Write to it like an object
(`tag.key = value`); it goes to disk as JSON.

| Member | What for |
|---|---|
| `ContainsKey(key)` | Was the key saved? |
| `Get(key, default)` | The value, or the default. |
| `GetBool`, `GetInt`, `GetFloat`, `GetString`, `GetList`, `GetCompound(key)` | The value already converted (false, 0, `''`, `[]` if missing). |
| `Set(key, v)`, `Add(key, v)`, `Remove(key)` | Writing and deleting. |
| `Count` | How many keys. |
| `TagCompound.from(object)` | A `TagCompound` with the object's fields. |

---

## Network

The `writer` and `reader` of `NetSend`/`NetReceive`, and the `Mod`'s packet.
See [guide 12](../mods/12-globais-e-mundo.md#network).

| Member | What for |
|---|---|
| `writer.Write(value)` | Stores a number, text, boolean, array, plain object or `Vector2`. The typed names (`WriteInt32`...) do the same. |
| `writer.WriteFlags(a, b, ...)`, `writer.WriteVector2(v)` | Several booleans; a vector. |
| `reader.Read()` | The next value, as it was written. |
| `reader.ReadInt32()`, `ReadByte`, `ReadSingle`, `ReadDouble`, `ReadBoolean`, `ReadString` | The next one, converted. |
| `reader.ReadFlags()`, `reader.ReadVector2()` | An array of booleans; a `Vector2`. |
| `reader.HasMore` | Are there more values? |
| `mod.GetPacket()` | A `ModPacket` (a `writer` with `Send`). |
| `packet.Send(toClient = -1, ignoreClient = -1)` | From the client: to the server. From the server: to one client, or to everyone except one. |
| `HandlePacket(reader, whoAmI)` | On `Mod`: the same mod's packet, from the other side. `whoAmI`: the client, or `256` (the server). |

On `ModNPC` and `ModProjectile`: `SendExtraAI(writer)` and
`ReceiveExtraAI(reader)`, along with the entity's sync.

---

## Mod and ModLoader

`Mod` is the mod itself: the entry file's `export default` class, required and
one per package. `ModLoader` finds the others.

| `Mod` | |
|---|---|
| `id` | The manifest `id` (tModLoader's `Name`). |
| `uuid`, `name`, `version` | From the manifest. |
| `path`, `root` | `main.js`'s folder (`content/`) and the package's. |
| `dataDirectory` | `Android/data/com.bunnyloader/mod_data/<uid>`. |
| `Load()` | During loading, after the content registration. |
| `AddRecipeGroups()`, `AddRecipes()`, `PostSetupContent()` | As in `ModSystem`. |
| `GetContent(Base)` | This mod's templates that extend `Base`, in registration order: `this.GetContent(ModItem).map((i) => i.Type)` gives the types of every item. Does not apply to `ModPlayer` (one per player: `player.GetModPlayer`). |
| `AddBossHeadTexture(path, type = -1)` | Registers one more boss icon (the 2nd phase, for example) and returns the slot, for `BossHeadSlot`. With `type`, it becomes that NPC's default icon. -1 without the file. Call it during registration (`Load`, `SetStaticDefaults`). |
| `Call(...args)` | When another mod calls. |
| `export default class X extends Mod` | Required, in the entry file; one per package. Bunny Loader creates it (it is the package's `bl.mod`). |

| `ModLoader` | |
|---|---|
| `TryGetMod(id, ref)` | `true` and the `Mod` in `ref.value`; or `false` and `null`. |
| `GetMod(id)` | The `Mod`; throws if it is not there. |
| `HasMod(id)` | Is it installed (and did it load)? |
| `Mods` | All of them, in load order. |

`bl.mod` is the `Mod` of whoever asks.

---

## ModContent

tModLoader's `ModContent`: the type, the template and the textures of a mod
content, by **class**, by **name** or by `'mod/Name'`.

| | |
|---|---|
| `ItemType(x)`, `ProjectileType(x)`, `NPCType(x)`, `BuffType(x)`, `TileType(x)`, `PrefixType(x)`, `MountType(x)`, `RarityType(x)` | The type. `x` is the class, the name (`'ExampleBobber'`) or `'othermod/Name'`. Not found: 0. |
| `GetInstance(Class)` | The template (the `register` instance). |
| `GetContent(Base)` | The templates from every mod that extend `Base` (`ModItem`, `ModNPC`, `ModBuff`, `ModSystem`...), in registration order. |
| `Find(ModItem, 'mod/Name')` | The template by name; throws if missing. |
| `TryFind(ModItem, 'mod/Name', ref)` | The same, in `ref.value`; returns `true`/`false`. |
| `GetModItem(type)`, `GetModProjectile(type)`, `GetModNPC(type)`, `GetModBuff(type)`, `GetModTile(type)`, `GetModPrefix(type)`, `GetModMount(type)`, `GetModRarity(type)` | The template by type. |
| `Request(path)` | The game's `Asset<Texture2D>`, loaded once (`.Value` is the texture). Game thread. |
| `Texture(path)` | The `Texture2D` (`Request`'s `.Value`). |
| `HasAsset(path)` | Does the texture exist? |
| `GetModBossHeadSlot(path)` | The slot of a registered boss icon (a boss's `_Head_Boss` or an `AddBossHeadTexture`), or -1. |
| `SoundStyle(path, options)` | The same as `new SoundStyle(...)`. |

By bare name, your mod's applies; if there is none, the only mod with content
of that name (two: ask with `'mod/Name'`).

---

## ModRecipe

| | |
|---|---|
| `new ModRecipe()` | A recipe (inside `AddRecipes`). |
| `SetResult(type, n)` | What it gives. |
| `AddIngredient(type, n)` | Up to 15. |
| `AddRecipeGroup(group, n)` | Accepts any item in the group (object, a vanilla name like `'IronBar'`, or one created by a mod). |
| `AddTile(type)` | The station (one per recipe). |
| `SetProperty(name, value)` | `needWater`, `needLava`, `needHoney`, `needSnowBiome`, `needGraveyardBiome`, `needTorchGodsFavor`, `needMechdusa`, `notDecraftable`, `crimson`, `corruption`, `alchemy`. |
| `AddCustomShimmerResult(type, n)` | What comes out in Shimmer. |
| `AddCondition(condition)`, `AddCondition(description, () => bool)` | A [`Condition`](#condition): outside it, the recipe leaves the crafting menu. |
| `RemoveCondition(condition)` | Takes one out (`true` if it was there). |
| `AddConsumeIngredientCallback((recipe, type, amount, isDecrafting) => ...)` | How much of each ingredient is spent (`amount` is `Ref`; `0`: not spent). `AddConsumeItemCallback` is the old name. |
| `AddOnCraftCallback((recipe, item, consumedItems, destinationStack) => ...)` | After crafting. On mobile `consumedItems` and `destinationStack` arrive `null`. |
| `Register()` | Puts it into the game. |
| `ModRecipe.CreateRecipeGroup(name, types)` | A new group (in `AddRecipeGroups`). |
| `ModRecipe.GetGroup(x)` | A group by object, name or number. |

**Not yet**: shimmer conditions (`AddDecraftCondition`), `Clone`,
`RemoveIngredient` and showing the conditions in the crafting menu.

### Condition

tModLoader's `Condition`: `new Condition(description, () => bool)`.
`description` is a text key (`'Conditions.LowHealth'` of the mod itself, a
full `Mods....` key, one of the game's) or the text itself. `IsMet()`,
`Description` (the text in the current language) and `DescriptionKey`.

The ready ones: `NearWater`, `NearLava`, `NearHoney`, `TimeDay`, `TimeNight`,
the `In<Biome>` ones (`InDesert`, `InBeach`, `InSnow`, `InJungle`, `InHallow`,
`InCorrupt`, `InCrimson`, `InDungeon`, `InGraveyard`...), the heights
(`InSkyHeight`, `InUnderworldHeight`...), `InClassicMode`, `InExpertMode`,
`InMasterMode`, `InJourneyMode`, `Hardmode`, `PreHardmode`, `SmashedShadowOrb`,
`CrimsonWorld`, `CorruptWorld` and the special seeds (`DrunkWorld`,
`RemixWorld`, `NotTheBeesWorld`, `ForTheWorthyWorld`, `TenthAnniversaryWorld`,
`DontStarveWorld`, `NoTrapsWorld`, `ZenithWorld`). `NearShimmer` does not exist
on mobile.

---

## NPC helpers

| Class | |
|---|---|
| `NPCLoot` | Received in `ModifyNPCLoot`: `npcLoot.Add(rule)`, with the game's rules (`ItemDropRule...`). |
| `ItemLoot` | Received in `ModItem`'s `ModifyItemLoot`: `itemLoot.Add(rule)`, `Remove`, `Get()`, `RemoveWhere`. Kept by Bunny Loader (the game has no per-item drops). |
| `NPCSpawnInfo` | Received in `SpawnChance`, `EditSpawnPool` and `EditSpawnInfo`, like tModLoader's `NPC.Spawner`: `SpawnTileX`, `SpawnTileY`, `GroundTileY`, `SpawnTileType` and `SpawnWallType` (the ground's block and wall), `SafeRangeX`, `Player` (the target player), `Spawner` (the game's) and its fields, which can be read and written (`waterTile`, `nearGranite`, `nearMarble`, `spawnSpider`, `ZoneCorrupt`...); height (`Sky`, `Surface`, `Underground`, `Cavern`, `Underworld`, `AboveSurface`, `BelowSurface`); time and events (`Day`, `Night`, `Rain`, `SlimeRain`, `BloodMoon`, `SolarEclipse`, `PumpkinMoon`, `FrostMoon`, `AnyEvent`, `Invasion`, `AnyTower`); world (`HardMode`, `Expert`, `Master`); biome (`Corruption`, `Crimson`, `Hallow` and the `Underground...` ones, `Snow`, `Ice`, `Jungle`, `UndergroundJungle`, `Mushroom`, `SurfaceMushroom`, `Ocean`, `Desert`, `DesertCave`, `Meteor`, `Marble`, `Granite`, `Graveyard`, `Dungeon`, `Lihzahrd`); `CommonEnemy`. |
| `SpawnPool` | `EditSpawnPool`'s roll: `pool[type] = weight`, `delete pool[type]`, or `Add`, `Remove`, `ContainsKey`, `Clear`, `Keys`, `Count`. `0` is the game's spawn (weight 1). With a total of 0, nothing spawns. |
| `NPCShop` | `new NPCShop(npcType, 'Shop').Add(item, { condition, price, currency }).Register()`; `NPCShop.get(type, name)`, `shop.Open()`. |
| `NPCHappiness` | `this.Happiness.SetNPCAffection(npc, level)`, `.SetBiomeAffection('Desert', level)` (vanilla) or `.SetBiomeAffection(MyBiome, level)` (a `ModBiome` class), with `AffectionLevel.Love`, `Like`, `Dislike`, `Hate`. |
| `SpawnCondition` | The game's spawn conditions as objects, as in tModLoader: `SpawnCondition.OverworldNightMonster.Chance` is the share (0 to 1) of the game's roll that would land there at the current spawn point; `.Active` says whether it was evaluated. For `SpawnChance`: `return SpawnCondition.OverworldDaySlime.Chance * 0.1`. Evaluated once per roll, only if read. |
| `ModGore` | `ModGore.getTypeByName('Name')`: the `Name.png` gore from a `Gores/` folder in the mod. |

---

## TooltipLine

| | |
|---|---|
| `new TooltipLine(name, text)` | (also `new TooltipLine(mod, name, text)`, as in tModLoader). |
| `Mod`, `Name`, `Text` | The line's mod (`'Terraria'` on the game's), the name (tModLoader's: `ItemName`, `Damage`, `Tooltip0`..., also in `TooltipNames`: `TooltipNames.Damage`, `TooltipNames.Tooltip(0)`) and the text. |
| `OverrideColor` | A `Color` for the whole line. |
| `IsModifier`, `IsModifierBad` | A prefix line: good, or bad. |
| `OneDropLogo` | `true`: the line is the One Drop logo. |
| `TooltipLine.colorTag(text, color)` | `'[c/RRGGBB:text]'` to paint a span. |
| `Visible`, `Hide()` | `Hide()` removes the line from the tooltip. |

`DrawableTooltipLine` (in `Pre/PostDrawTooltipLine` and `PostDrawTooltip`):
everything from `TooltipLine`, plus

| | |
|---|---|
| `Index` | The line's position. |
| `X`, `Y`, `OriginalX`, `OriginalY` | Where the line is drawn (change `X`, `Y` in `PreDrawTooltipLine`). |
| `Color` | The final color, with the tooltip's transparency. |
| `Font`, `Rotation`, `Origin` | How the text is drawn. |
| `BaseScale` | The scale; already including mobile's tooltip scale (`Settings.Tooltips.Scale`). |
| `Spread` | The distance of the four shadows (2). |

---

## ModLocalization

| | |
|---|---|
Every key in `Localization/<culture>.json` is already in the game as
`Mods.<mod id>.<Section>.<Key>` (at any depth), and follows language changes.

The texts go in after the original `LanguageManager.LoadFilesForCulture`,
before the language events and the native reference processing. Names,
descriptions, tooltips and config labels follow the change; `{$key}`
references to the game are resolved in the loaded culture. A string returned by
`Translate` or `GetTextValue` and kept by the mod keeps its value: look it up
when using it, or keep `GetText`'s `LocalizedText` and read `.Value`. Circular
references end at the full name of the key that closes the cycle.

| | |
|---|---|
| `ModLocalization.Translate('Section.Key')` | The **text** in the game's language (as in TL); without a text, the path itself. |
| `ModLocalization.TryTranslate('Section.Key')` | The same, with `''` when there is no text. |
| (where it looks) | In the calling mod's `Localization/*.json`. Called from outside a mod (the Editor console), the full key `Mods.<id>.Section.Key` finds its mod, and the short one works if only one mod has it. `{0}` is not replaced: use `.replace('{0}', x)`. |
| `ModLocalization.GetTextValue(key)` | The mod's text or, without it, the game's. |
| `ModLocalization.GetText(key)` | The mod's or the game's `LocalizedText`. |
| `ModLocalization.Key('Section.Key')` | The **key** `Mods.<id>.Section.Key` (what the Bestiary and currencies ask for). |
| `ModLocalization.Exists(key)` | Whether the mod or the game has the text. |
| `ModLocalization.Register(key, text)` | A text (or `{ culture: text }`) under any key. |
| `ModLocalization.ActiveCultureName` | `'pt-BR'`, `'en-US'`... |

---

## Sound and music

| | |
|---|---|
| `new SoundStyle(path, { Volume, Pitch, PitchVariance, MaxInstances, SoundLimitBehavior })` | A mod sound, for `UseSound`, `HitSound`, `DeathSound`. |
| `SoundEngine.PlaySound(style, position?)` | Mod sound: an asynchronous request identifier (0 = refused). Acceptance does not confirm playback. |
| `SoundEngine.GetSoundState(handle)` | -1 failed; 0 finished/unknown; 1 pending; 2 started (estimated end); 3 paused; 4 cancelled; 5 expired in the queue. History limited to the 256 most recent requests, keeping the active ones. |
| `SoundEngine.FindActiveSound(style)`, `SoundEngine.StopSound(n)` | The newest one started, or 0 if pending/paused/inactive; cancels a pending request or stops a started one. |
| `SoundLimitBehavior.ReplaceOldest`, `.IgnoreNew` | |
| `MusicLoader.GetMusicSlot(path)` (or `(mod, path)`) | A mod track's number (0 = does not exist). |
| `MusicLoader.MusicExists(path)`, `MusicLoader.IsMusicPlaying(slot)`, `MusicLoader.MusicCount` | |
| `SceneEffectPriority.None` ... `BossHigh` | The music priority of `ModNPC` and `ModSceneEffect`/`ModBiome`. |

**Who plays.** A music box (mod or vanilla) beats everything. Then, among the
mod candidates (a `ModNPC` with `Music` near the screen and the local player's
scene), the one with the highest priority; the scene only replaces the NPC's
if its own is higher. It beats the game's music if the priority reaches that
music's tier, as in tModLoader:

| The game music's tier | To beat it |
|---|---|
| credits | nothing beats it |
| torch god's challenge, Moon Lord | `BossHigh` |
| Plantera, martian invasion, celestial pillars | `BossMedium` |
| the other bosses | `BossLow` |
| events (pirates, goblins, Old One's Army, moons, rainbow stone) | `Event` |
| eclipse, slime rain, shimmer, town, sandstorm, underworld, space | `Environment` |
| temple, dungeon, mushroom, corruption, crimson | `BiomeHigh` |
| meteor, graveyard, desert, jungle, snow | `BiomeMedium` |
| the rest (surface, underground, ocean, rain) | `BiomeLow` |

It also applies in Otherworld. `Priority` `None` does not play.

**Not yet**: `Variants`, `IsLooped`.

---

## ModConfig

Per-mod options, declared in `static Options`. Automatic registration creates
an instance that can be obtained with `ModContent.GetInstance(Class)`.
`static VisibleInWorld = false` keeps the config only in the main menu's Mod
Configuration (a world generation one, for example); the default is `true`.
The [options guide](../mods/14-opcoes-do-mod.md) describes the menu and
persistence. The [full signatures](metodos.md#modconfig) come from the
implementation.

| Method | Contract |
|---|---|
| `OnLoaded()` | After the configuration loads. |
| `OnChanged(key)` | When an option changes in the applied configuration. |
| `OnApply()` | Notification that the options were applied. |
| `ResetToDefaults()` | Asks the loader to restore the default values. |
| `SetOption(key, value)` | Changes an option through the configuration API. |
| `ModConfig.Header(extra)`, `Toggle(defaultValue, extra)`, `Range(defaultValue, options)` | Title, toggle and slider descriptors. |
| `ModConfig.Radio(defaultValue, choices, extra)`, `Dropdown(...)`, `Cycle(...)` | Selection descriptors. |
| `ModConfig.Color(defaultValue, extra)`, `Button(action, extra)`, `Link(url, extra)` | Color, action and address descriptors. |
| `ModConfig.register(Class)` | Registers a configuration class by hand. |

## Hits

`HitModifiers` (the hit on an NPC, in the `ModifyHit*`s and in
`ModifyIncomingHit`), `HitInfo` (the result, in the `OnHit*`s),
`HurtModifiers` (the hit on the player: `ModifyHurt`, `ModifyHitByNPC`,
`ModifyHitPlayer`, `ModifyHitPvp`) and `HurtInfo` (the result). The fields,
the math and the call order are in [guide 16](../mods/16-golpes.md).

| | |
|---|---|
| `HitModifiers` | `SourceDamage`, `FinalDamage`, `CritDamage`, `NonCritDamage`, `Knockback`, `Defense` (`StatModifier`); `FlatBonusDamage`, `ScalingBonusDamage`, `TargetDamageMultiplier`, `ArmorPenetration`, `ScalingArmorPenetration`, `DefenseEffectiveness` (numbers); `SetMaxDamage`, `SetCrit`, `DisableCrit`, `DisableKnockback`, `SetInstantKill`, `HideCombatText`, `GetDamage`, `ToHitInfo`. |
| `HitInfo` | `Damage`, `SourceDamage`, `Crit`, `Knockback`, `HitDirection`, `InstantKill`, `HideCombatText`, `DamageType`. |
| `HurtModifiers` | `SourceDamage`, `FinalDamage`, `Knockback` (`StatModifier`); `IncomingDamageMultiplier`, `ArmorPenetration`, `ScalingArmorPenetration`; `DamageSource`, `PvP`, `CooldownCounter`, `Dodgeable`, `HitDirectionOverride`; `SetMaxDamage`, `Cancel`, `DisableDust`, `DisableSound`. |
| `HurtInfo` | `Damage`, `SourceDamage`, `DamageSource`, `PvP`, `CooldownCounter`, `Dodgeable`, `HitDirection`, `Knockback`, `Crit`, `Quiet`, `Cancelled`. |

## DamageClass

A damage class defines bonus, effect and prefix inheritance. It can be used in
`item.DamageType` and `projectile.DamageType`. The adapted native instances are
reached through `DamageClass.Melee`, `Magic`, `Ranged`, `Summon` and the other
getters listed in the [catalog](metodos.md#damageclass).

| Method | Contract |
|---|---|
| `GetModifierInheritance(damageClass)` | Returns `StatInheritanceData`; by default, it inherits all of `Generic`'s bonuses and none of the others'. |
| `GetEffectInheritance(damageClass)` | Defines whether it inherits the other class's effects; default `false`. |
| `GetPrefixInheritance(damageClass)` | Defines prefix inheritance; the default delegates to `GetEffectInheritance`. |
| `SetStaticDefaults()`, `SetDefaultStats(player)` | Initializing the type and the player's stats. |
| `UseStandardCritCalcs` | A getter defaulting to `true`. |
| `ShowStatTooltipLine(player, lineName)` | Controls the stat line in the tooltip; default `true`. |
| `CountsAsClass(damageClass)`, `GetsPrefixesFor(damageClass)` | Query the relations between registered classes. |
| `DamageClass.register(Class)` | Registers by hand and returns the type. |

`StatModifier` applies `(value + Base) * Additive * Multiplicative + Flat`.
Its fields are mutable in JavaScript. `ApplyTo`, `CombineWith`, `Scale`,
`Undo`, `Clone` and `Equals` are in the [StatModifier catalog](metodos.md#statmodifier).
`StatInheritanceData` accepts five factors or an object with those factors;
`Full` and `None` return new instances. See the
[damage classes guide](../mods/15-classes-de-dano.md) to combine these stats.

## Screen effects

A screen effect redraws part of what is already on screen with a GLSL
shader, between the world and the interface. Everything is in
[guide 17](../mods/17-efeitos-de-tela.md); this is the summary.

**`ModScreenEffect`** (extend it; in `Content/` it registers on its own, and
its shader is the `.frag` next to the `.js` or `static Source`):

| | |
|---|---|
| `Center`, `Follow`, `Radius`, `Intensity`, `Color` | Where (world), what it follows, size (world pixels), strength, color (`uColor`). |
| `Duration`, `FadeIn`, `FadeOut`, `Age`, `Progress`, `TimeLeft`, `Opacity` | Time in ticks. |
| `Key`, `Layer`, `Order`, `Fullscreen`, `CaptureMargin`, `Mask`, `Uniforms`, `Shader` | Renewal by key, World/Screen layer, draw order, area, extra pixels read, where it applies, values for the shader, the shader. |
| `SetDefaults()`, `OnSpawn()`, `Update()`, `ShouldDraw()`, `DrawDistortion(d)`, `Draw(cmd)`, `OnKill()` | The life cycle. |
| `Kill()`, `Tween(property, to, ticks, easing, options)`, `Active` | |
| `static Spawn(center, options)` | `center`: a position, an entity (followed) or `null` (whole screen); options in camelCase. |

**`ScreenEffects`**: `Spawn(Class, center, options)`, `Find(key)`,
`Kill(key)`, `KillAll()`, `Active`, `Classes`, `Register(Class)`, `Layer`,
`Enabled` (the player's "Screen Effects" setting), `Debug`, `HotReload`,
`Reload()`.

**Tools**: `ScreenPresets` (18 ready-made effects, extendable classes),
`ScreenGraphics` (`Shader`, `Texture`, `Passes`, `Sources`, `BlendMode`,
`Array`) and `ScreenUtils` (`Space`, `Easing`, `Mask`, `Tween`, `Curve`,
`ColorMath`). In `Draw(cmd)`: `cmd.DrawShader`, `cmd.Target`, `cmd.Blit`; in
`DrawDistortion(d)`: `d.Radial`, `d.Swirl`, `d.Ring`, `d.Haze`, `d.Push`.

## Helpers

Globals, with tModLoader's names:

| | |
|---|---|
| `Vector2` | `new(x, y)`, `Add`, `Subtract`, `Multiply`, `Divide`, `Length`, `Distance`, `Normalize`, `SafeNormalize`, `DirectionTo`, `RotatedBy`, `RotatedByRandom`, `ToRotation`, `ToRotationVector2`, `Lerp`, `Dot`, `ToTileCoordinates`, `Clone`. |
| `MathHelper` | `Pi`, `TwoPi`, `PiOver2`, `ToRadians`, `ToDegrees`, `Clamp`, `Lerp`, `SmoothStep`, `WrapAngle`. |
| `Rand` | `Next(max)`, `Next(min, max)`, `NextFloat()`, `NextFloat(max)`, `NextBool(oneIn)`, `NextChance(p)`, `NextSign()`, `NextFromList(list)`, `NextVector2Circular(rx, ry)`, `NextVector2Unit()`. |
| `Color` | `new(r, g, b, a)`, `Color.White`, `Color.SkyBlue`... (always a copy), `Multiply`, `Lerp`, `ToVector3`. |
| `Rectangle` | `new(x, y, w, h)`, `Size`, `Center`, `Contains`, `Intersects`. |
| `Ref` | `new Ref(value)`: a `ref`/`out` parameter, `.value`. |
| `ItemRarityID`, `ProjAIStyleID`, `NPCAIStyleID` | The numbers this Terraria does not ship. |
| `Terraria.ID.DustID` | With the names tModLoader adds (`DustID.PinkFairy`...). |
| `ProjAI` | Legacy: `proj.ai[0]` already works directly. |

---

## What does not exist yet

tModLoader classes with no equivalent today. You can get the effect with
direct hooks ([guide 1](../mods/01-hooks-do-zero.md)), but with no shortcut:

- `GlobalTile`, `GlobalBuff`, `GlobalWall`;
- `ModDust`;
- custom UI (`UIState`, `ModifyInterfaceLayers`);
- `ModTree`, `ModPalmTree`, `ModCactus` and pylons (`ModPylon`).
