using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExPlayer;

public sealed class CsExPlayerMod : Mod
{
    const int End = 900;

    static int frame, pass, fail, pressed, wingsAdded = -1, rockets;
    static bool use;

    static ILog Log => ModContent.GetInstance<CsExPlayerMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexplayer {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static int BuffType(string name) => ModContent.Find<ModBuff>("ExampleMod/" + name).Type;

    static ModPlayer Part(Player player, string name) => player.GetModPlayer(ModContent.Find<ModPlayer>("ExampleMod/" + name));

    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (!Main.gameMenu && self.whoAmI == Main.myPlayer && frame > 0)
        {
            self.controlUseItem = use;
            self.releaseUseItem = !use || frame == pressed;
            Main.mouseX = Main.screenWidth / 2 + 300;
            Main.mouseY = Main.screenHeight / 2 - 60;
        }
        orig(self);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        orig(self, i);
        if (i != Main.myPlayer || frame > End) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == End) Log.Info($"csexplayer END pass={pass} fail={fail}");
    }

    static void Equip(Player player, int slot, string name) => player.armor[slot].SetDefaults(ItemType(name), default);

    static void Step(Player player)
    {
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 27000;
                Statics(player);
                On_PlayerDrawLayers.DrawPlayer_09_Wings += (orig, info) =>
                {
                    int start = info.DrawDataCacheCount;
                    orig(info);
                    if (info.drawPlayer.whoAmI == Main.myPlayer && frame > 60) wingsAdded = info.DrawDataCacheCount - start;
                };
                break;
            case 60:
                Equip(player, 3, "ExampleBoots");
                Equip(player, 4, "ExampleResourceAccessory");
                Equip(player, 5, "ExampleShield");
                Equip(player, 6, "WaspNest");
                Equip(player, 7, "ExampleCustomDrawWings");
                break;
            case 120:
                Accessories(player);
                Log.Info("csexplayer SHOT accessories");
                break;
            case 200:
                player.armor[3].TurnToAir();
                player.armor[5].TurnToAir();
                player.armor[6].TurnToAir();
                player.armor[7].TurnToAir();
                player.inventory[player.selectedItem].SetDefaults(ItemType("ExampleCustomResourceWeapon"), default);
                break;
            case 240:
                rockets = 0;
                Log.Info("csexplayer SHOT resourcebar");
                use = true;
                pressed = frame;
                break;
            case 280:
                use = false;
                Resource(player);
                break;
            case 300:
                Pickup(player);
                break;
            case 400:
                PickupDone(player);
                break;
            case 420:
                player.armor[4].TurnToAir();
                Equip(player, 5, "ExampleCostume");
                player.armor[13].SetDefaults(ItemType("ExampleBeard"), default);
                break;
            case 500:
                Costume(player);
                Log.Info("csexplayer SHOT costume");
                break;
        }
        if (frame >= 240 && frame <= 280)
        {
            int count = 0;
            for (int k = 0; k < Main.maxProjectiles; k++)
                if (Main.projectile[k].active && Main.projectile[k].owner == Main.myPlayer && Main.projectile[k].type == ProjectileID.VortexBeaterRocket) count++;
            rockets = Math.Max(rockets, count);
        }
    }

    static void Accessories(Player player)
    {
        Item boots = player.armor[3];
        Report("the boots equip with their shoe slot", player.shoe == boots.shoeSlot && boots.shoeSlot > 0, $"shoe={player.shoe} slot={boots.shoeSlot}");
        Report("the boots give the rocket boots, water walk, no fall damage and flames", player.rocketBoots == 2 && player.waterWalk && player.noFallDmg && player.hellfireTreads && player.fireWalk, $"rocket={player.rocketBoots}");
        ModPlayer resource = Part(player, "ExampleResourcePlayer");
        Report("the resource accessory raises the maximum, the regen and the magnet", Get<int>(resource, "exampleResourceMax2") == 200 && Get<float>(resource, "exampleResourceRegenRate") == 6f && Get<bool>(resource, "exampleResourceMagnet"),
            $"max={Get<int>(resource, "exampleResourceMax2")} regen={Get<float>(resource, "exampleResourceRegenRate")}");
        ModPlayer dash = Part(player, "ExampleDashPlayer");
        Report("the shield turns the dash on", Get<bool>(dash, "DashAccessoryEquipped"), "");
        Set(dash, "DashDir", 2);
        Set(dash, "DashDelay", 0);
        player.velocity = Vector2.Zero;
        dash.PreUpdateMovement();
        Report("the dash pushes the player and shows the shadow", player.velocity.X == 10f && player.eocDash > 0 && Get<int>(dash, "DashDelay") == 49, $"vx={player.velocity.X} eoc={player.eocDash} delay={Get<int>(dash, "DashDelay")}");
        player.velocity = Vector2.Zero;
        Report("the wasp nest gives strong bees and its back slot", player.strongBees && player.back == player.armor[6].backSlot && player.armor[6].backSlot > 0, $"back={player.back}");
        var bees = new HashSet<int>();
        for (int k = 0; k < 300; k++) bees.Add(player.beeType());
        Report("with the wasp nest, some bees are beenades", bees.Contains(ProjectileID.Beenade) && bees.Contains(ProjectileID.GiantBee), string.Join(",", bees));
        Report("the wings equip", player.wings == player.armor[7].wingSlot && player.armor[7].wingSlot > 0, $"wings={player.wings}");
        Report("ModifyEquipTextureDraw draws the wing and two glows", wingsAdded == 3, $"added={wingsAdded}");
    }

    static void Resource(Player player)
    {
        Report("the resource weapon fires", rockets > 0, $"rockets={rockets}");
        ModItem weapon = player.inventory[player.selectedItem].ModItem!;
        ModPlayer resource = Part(player, "ExampleResourcePlayer");
        Set(resource, "exampleResourceCurrent", 100);
        Report("the weapon can be used with enough resource", weapon.CanUseItem(player), "");
        weapon.UseItem(player);
        Report("using it spends 5", Get<int>(resource, "exampleResourceCurrent") == 95, $"current={Get<int>(resource, "exampleResourceCurrent")}");
        Set(resource, "exampleResourceCurrent", 2);
        Report("without enough resource it cannot be used", !weapon.CanUseItem(player), "");
        Item tip = new Item(weapon.Type);
        var lines = new List<TooltipLine>();
        tip.ModItem!.ModifyTooltips(lines);
        Report("the tooltip says the cost", lines.Count == 1 && lines[0].Text.Contains('5'), lines.Count > 0 ? lines[0].Text : "");
    }

    static int pickupIndex;
    static int beforePickup;

    static void Pickup(Player player)
    {
        ModPlayer resource = Part(player, "ExampleResourcePlayer");
        Set(resource, "exampleResourceCurrent", 0);
        beforePickup = player.CountItem(ItemType("ExampleResourcePickup"), 9999);
        pickupIndex = Item.NewItem(player.GetSource_FromThis(), player.getRect(), ItemType("ExampleResourcePickup"));
    }

    static void PickupDone(Player player)
    {
        Report("the pickup leaves the ground and stays out of the inventory", !Main.item[pickupIndex].active && player.CountItem(ItemType("ExampleResourcePickup"), 9999) == beforePickup, $"active={Main.item[pickupIndex].active}");
    }

    static void Costume(Player player)
    {
        ModPlayer costume = Part(player, "ExampleCostumePlayer");
        ModItem item = ModContent.Find<ModItem>("ExampleMod/ExampleCostume");
        Mod mod = item.Mod;
        int head = EquipLoader.GetEquipSlot(mod, "ExampleCostume", EquipType.Head);
        int body = EquipLoader.GetEquipSlot(mod, "ExampleCostume", EquipType.Body);
        Report("the costume registers its two head, body and legs sets", head > 0 && body > 0 && EquipLoader.GetEquipSlot(mod, "ExampleCostume", EquipType.Legs) > 0 && EquipLoader.GetEquipSlot(mod, "BlockyAlt", EquipType.Head) > 0, $"head={head} body={body}");
        Report("the costume's head hides the player's and the body hides the skin", !ArmorIDs.Head.Sets.DrawHead[head] && ArmorIDs.Body.Sets.HidesTopSkin[body] && ArmorIDs.Body.Sets.HidesArms[body], "");
        Report("near a town NPC the costume gives the Blocky buff", player.townNPCs >= 1 && player.HasBuff(BuffType("Blocky")) && Get<bool>(costume, "BlockyPower"), $"town={player.townNPCs}");
        Report("FrameEffects dresses the player", player.head == head && player.body == body, $"head={player.head} body={player.body}");
        Report("the Blocky buff raises the jump", player.jumpSpeedBoost >= 4.8f, $"jump={player.jumpSpeedBoost}");
        Item beard = player.armor[13];
        Report("the beard in a vanity slot shows", beard.beardSlot > 0 && player.beard == beard.beardSlot, $"beard={player.beard} slot={beard.beardSlot}");
    }

    static void Statics(Player player)
    {
        Report("the Example Mod is loaded", ModLoader.TryGetMod("ExampleMod", out _), "");
        var missing = new List<string>();
        foreach (string n in new[] { "ExampleBoots", "ExampleResourceAccessory", "ExampleCustomResourceWeapon", "ExampleResourcePickup", "ExampleCostume", "ExampleBeard", "ExampleCustomDrawWings", "WaspNest", "ExampleShield" })
            if (!ModContent.TryFind<ModItem>("ExampleMod/" + n, out _)) missing.Add(n);
        foreach (string n in new[] { "ExampleResourcePlayer", "ExampleDashPlayer", "ExampleCostumePlayer", "WaspNestPlayer" })
            if (!ModContent.TryFind<ModPlayer>("ExampleMod/" + n, out _)) missing.Add(n);
        if (!ModContent.TryFind<ModBuff>("ExampleMod/Blocky", out _)) missing.Add("Blocky");
        Report("every block 4 item, player and buff is registered", missing.Count == 0, string.Join(",", missing));
        string boots = ModContent.Find<ModItem>("ExampleMod/ExampleBoots").Tooltip.Value ?? "";
        Report("the boots' tooltip fills its numbers", boots.Contains('8') && boots.Contains("2 ") && !boots.Contains("{0}") && !boots.Contains("{1}"), boots.Split('\n')[1]);
        string resource = ModContent.Find<ModItem>("ExampleMod/ExampleResourceAccessory").Tooltip.Value ?? "";
        Report("the resource accessory's tooltip fills its number", resource.Contains("100") && !resource.Contains("{0}"), resource.Replace('\n', '|'));
        string wasp = ModContent.Find<ModItem>("ExampleMod/WaspNest").Tooltip.Value ?? "";
        Report("the wasp nest's tooltip takes the game's Hive Backpack line", !wasp.Contains("{$") && wasp.Contains('\n'), wasp.Replace('\n', '|'));
        Item nest = new Item(ItemType("WaspNest"));
        Report("the wasp nest cannot go with the Hive Pack", !nest.ModItem!.CanAccessoryBeEquippedWith(nest, new Item(ItemID.HiveBackpack), player) && nest.ModItem.CanAccessoryBeEquippedWith(nest, new Item(ItemID.Shackle), player), "");
        Item beard = new Item(ItemType("ExampleBeard"));
        Report("the beard uses the hair color", beard.beardSlot > 0 && ArmorIDs.Beard.Sets.UseHairColor[beard.beardSlot], $"slot={beard.beardSlot}");
    }
}
