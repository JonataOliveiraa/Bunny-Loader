using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExCodex;

public sealed class CsExCodexMod : Mod
{
    static readonly string[] Items = { "AbsorbTeamDamageAccessory", "CameraModifierShowcase", "ExampleAdvancedFlail", "ExampleArrow", "ExampleBugNet", "ExampleCloneWeapon", "ExampleCustomAmmo", "ExampleCustomAmmoGun", "ExampleCustomSwingSword", "ExampleCustomUseStyleWeapon", "ExampleGolfClub", "ExampleGravityDebuffBullet", "ExampleHeldProjectileWeapon", "ExampleHood", "ExampleInfoAccessory", "ExampleInteractableProjectileItem", "ExampleJavelin", "ExampleJoustingLance", "ExampleMultiExtraJumpAccessory", "ExamplePiercingProjectileItem", "ExamplePotionDelayAccessory", "ExampleRocket", "ExampleShootingSword", "ExampleSpecialGlideWings", "ExampleSpecificAmmoGun", "ExampleStaff", "ExampleTallHelmet", "ExampleWhipAdvanced", "HitModifiersShowcase", "RubyEarrings", "SapphireEarrings", "SimpleAccessory" };
    static readonly string[] Projectiles = { "ActiveSoundShowcaseProjectile", "ExampleAdvancedFlailProjectile", "ExampleArrowProjectile", "ExampleCloneProjectile", "ExampleCustomSwingProjectile", "ExampleExplosive", "ExampleGravityDebuffBullet", "ExampleGrenadeProjectile", "ExampleHeldProjectileWeaponProjectile", "ExampleHomingProjectile", "ExampleInteractableProjectile", "ExampleJavelinProjectile", "ExampleJoustingLanceProjectile", "ExamplePaperAirplaneProjectile", "ExamplePiercingProjectile", "ExampleProximityMineProjectile", "ExampleRocketProjectile", "ExampleSnowmanRocketProjectile", "ExampleWhipProjectileAdvanced" };
    static readonly string[] Buffs = { "AbsorbTeamDamageBuff", "AnimatedBuff", "ExampleCrowdControlledDebuff", "ExampleDodgeBuff", "ExampleGravityDebuff", "ExampleJavelinDebuff", "ExampleWeaponImbue", "ExampleWhipAdvancedPlayerBuff" };
    static readonly HashSet<string> MustShoot = new() { "ExampleAdvancedFlail", "ExampleCloneWeapon", "ExampleCustomAmmoGun", "ExampleCustomSwingSword", "ExampleHeldProjectileWeapon", "ExampleInteractableProjectileItem", "ExampleJavelin", "ExampleJoustingLance", "ExamplePiercingProjectileItem", "ExampleShootingSword", "ExampleStaff", "ExampleWhipAdvanced" };

    static int frame, pass, fail, pressed, stepEnd, peak, watched = -1, animation, uses, maxActive, gravityNpc = -1, gravityType, gravityStart;
    static float fallFull, fallLight;
    static bool use, finished;
    static string current = "";
    static Action<Player>? closing;
    static readonly HashSet<float> seen = new();
    static readonly Queue<(string Name, int Length, Action<Player> Begin, Action<Player>? End)> Steps = new();

    static ILog Log => ModContent.GetInstance<CsExCodexMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexcodex {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (!Main.gameMenu && self.whoAmI == Main.myPlayer && frame > 0)
        {
            self.controlUseItem = use;
            self.releaseUseItem = !use || frame == pressed;
            Main.mouseX = Main.screenWidth / 2 + 200;
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
        if (i != Main.myPlayer || finished) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"step {current}", false, e.ToString()); }
    }

    static int Count(int type)
    {
        int count = 0;
        for (int k = 0; k < Main.maxProjectiles; k++)
        {
            Projectile p = Main.projectile[k];
            if (p.active && (type < 0 || p.type == type)) count++;
        }
        return count;
    }

    static void Clear()
    {
        for (int k = 0; k < Main.maxProjectiles; k++)
            if (Main.projectile[k].active) Main.projectile[k].active = false;
    }

    static void Shot(string name) => Steps.Enqueue(($"shot {name}", 15, _ => Log.Info($"csexcodex SHOT {name}"), null));

    static void UseStep(string name, Item sample)
    {
        int type = sample.type, shoot = sample.shoot;
        Steps.Enqueue(($"use {name}", name == "CameraModifierShowcase" ? 330 : 60, p =>
        {
            p.inventory[p.selectedItem].SetDefaults(type, default);
            p.itemAnimation = 0;
            p.itemTime = 0;
            p.channel = false;
            p.direction = 1;
            use = true;
            pressed = frame;
            watched = shoot;
            peak = 0;
            animation = 0;
            uses = 0;
        }, p =>
        {
            use = false;
            p.itemAnimation = 0;
            p.itemTime = 0;
            p.channel = false;
            if (name == "ExampleSpecificAmmoGun") Report("ItemUsesThisAnimation counts the three shots", uses == 3, $"uses={uses}");
            Report($"use {name}", animation > 0 && (!MustShoot.Contains(name) || peak > 0), $"animation={animation} shoot={shoot} peak={peak}");
            watched = -1;
            Clear();
        }));
        Steps.Enqueue(("rest", 10, _ => { }, null));
    }

    static void Build(Player player)
    {
        foreach (string name in Items)
        {
            Item sample = new Item(ItemType(name));
            if (sample.Name.Length == 0 || sample.Name.Contains("Mods.ExampleMod")) Report($"item {name} has a name", false, sample.Name);
            int type = sample.type;
            if (sample.useStyle > 0) UseStep(name, sample);
            else if (sample.accessory)
                Steps.Enqueue(($"equip {name}", 15, p => p.armor[3].SetDefaults(type, default), p =>
                {
                    Report($"equip {name}", p.armor[3].type == type);
                    p.armor[3].TurnToAir();
                }));
            else if (sample.headSlot >= 0)
                Steps.Enqueue(($"wear {name}", 15, p => p.armor[0].SetDefaults(type, default), p =>
                {
                    Report($"wear {name}", p.head == sample.headSlot, $"head={p.head}");
                    p.armor[0].TurnToAir();
                }));
            else Report($"item {name} is ammo", sample.ammo > 0, $"ammo={sample.ammo}");
        }
        foreach (string name in Buffs)
        {
            int type = ModContent.Find<ModBuff>("ExampleMod/" + name).Type;
            Steps.Enqueue(($"buff {name}", 20, p => p.AddBuff(type, 15), p => Report($"buff {name}", true)));
        }
        foreach (string name in Projectiles)
        {
            int type = ModContent.Find<ModProjectile>("ExampleMod/" + name).Type;
            Steps.Enqueue(($"projectile {name}", 30, p =>
            {
                int index = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(120, -120), new Vector2(3, -1), type, 10, 1, Main.myPlayer);
                Report($"projectile {name} spawned", index >= 0 && index < Main.maxProjectiles && Main.projectile[index].type == type, $"index={index}");
            }, _ => Clear()));
        }
        Steps.Enqueue(("equipped look", 30, p =>
        {
            p.armor[0].SetDefaults(ItemType("ExampleHood"), default);
            p.armor[3].SetDefaults(ItemType("RubyEarrings"), default);
            p.armor[4].SetDefaults(ItemType("ExampleInfoAccessory"), default);
            p.armor[5].SetDefaults(ItemType("ExampleSpecialGlideWings"), default);
        }, null));
        Shot("equipped");
        Steps.Enqueue(("unequip", 5, p =>
        {
            p.armor[0].TurnToAir();
            p.armor[3].TurnToAir();
            p.armor[4].TurnToAir();
            p.armor[5].TurnToAir();
        }, null));
        Steps.Enqueue(("explosive", 340, p =>
        {
            maxActive = 0;
            int type = ModContent.Find<ModProjectile>("ExampleMod/ExampleExplosive").Type;
            int index = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(160, -140), new Vector2(1, 0), type, 10, 1, Main.myPlayer);
            Report("the explosive uses the explosive AI", Main.projectile[index].aiStyle == 16, $"aiStyle={Main.projectile[index].aiStyle}");
        }, _ =>
        {
            Report("the explosive leaves five children", maxActive >= 5, $"maxActive={maxActive}");
            Clear();
        }));
        Steps.Enqueue(("crowd control", 50, p => p.AddBuff(ModContent.Find<ModBuff>("ExampleMod/ExampleCrowdControlledDebuff").Type, 25), null));
        Steps.Enqueue(("gravity", 360, p =>
        {
            gravityType = ModContent.Find<ModBuff>("ExampleMod/ExampleGravityDebuff").Type;
            gravityNpc = -1;
            for (int k = 0; k < Main.maxNPCs; k++)
                if (Main.npc[k].active && Main.npc[k].townNPC) gravityNpc = k;
            if (gravityNpc < 0) gravityNpc = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + 200, (int)p.Center.Y - 200, NPCID.Guide);
            Main.npc[gravityNpc].AddBuff(gravityType, 300);
            seen.Clear();
            gravityStart = frame;
        }, _ =>
        {
            Report("the debuff changes the multiplier as the counter grows", seen.Contains(0.5403f) && seen.Contains(-0.4161f), $"values={string.Join(",", seen)}");
            Report("the multiplier never accumulates", seen.Count <= 5, $"values={seen.Count}");
            NPC npc = Main.npc[gravityNpc];
            Report("the multiplier returns to one after the debuff", npc.active && npc.FindBuffIndex(gravityType) < 0 && npc.GravityMultiplier.Value == 1f, $"value={npc.GravityMultiplier.Value} active={npc.active} buff={npc.FindBuffIndex(gravityType)}");
            Report("a falling NPC gains less speed under the multiplier", fallFull > 0.05f && fallLight > 0.05f && fallLight / fallFull < 0.75f, $"full={fallFull} light={fallLight}");
            gravityNpc = -1;
        }));
    }

    static void Step(Player player)
    {
        player.statMana = player.statManaMax2;
        player.statLife = player.statLifeMax2;
        if (frame == 30)
        {
            Main.dayTime = true;
            Main.time = 27000;
            int slot = 54;
            foreach (string ammo in new[] { "ExampleArrow", "ExampleGravityDebuffBullet", "ExampleCustomAmmo", "ExampleRocket" })
            {
                player.inventory[slot].SetDefaults(ItemType(ammo), default);
                player.inventory[slot].stack = 999;
                slot++;
            }
            int custom = ItemType("ExampleCustomUseStyleWeapon");
            Report("the item set factory covers mod items", ItemID.Sets.Factory._size > custom, $"size={ItemID.Sets.Factory._size} item={custom}");
            int[] fresh = ItemID.Sets.Factory.CreateIntSet(0, custom, -6);
            Report("a new item set reaches mod items", fresh.Length > custom && fresh[custom] == -6, $"length={fresh.Length}");
            Report("the custom use style is past the game's styles", new Item(custom).useStyle >= ItemUseStyleID.Count, $"useStyle={new Item(custom).useStyle}");
            Build(player);
            Log.Info($"csexcodex steps={Steps.Count}");
        }
        if (frame < 40) return;
        if (watched > 0) peak = Math.Max(peak, Count(watched));
        animation = Math.Max(animation, player.itemAnimation);
        uses = Math.Max(uses, player.ItemUsesThisAnimation);
        maxActive = Math.Max(maxActive, Count(-1));
        if (current == "crowd control" && frame == stepEnd - 35) Report("crowd control holds during the debuff", player.CCed);
        if (current == "crowd control" && frame == stepEnd - 2) Report("crowd control ends with the debuff", !player.CCed);
        if (gravityNpc >= 0)
        {
            NPC npc = Main.npc[gravityNpc];
            seen.Add(MathF.Round(npc.GravityMultiplier.Value, 4));
            int age = frame - gravityStart;
            if (age == 40 || age == 140)
            {
                npc.position.Y -= 400;
                npc.velocity.Y = 0;
            }
            if (age == 46) fallFull = npc.velocity.Y;
            if (age == 146) fallLight = npc.velocity.Y;
            if (frame % 60 == 0) Log.Info($"csexcodex gravity f={frame} value={npc.GravityMultiplier.Value} velY={npc.velocity.Y} buff={npc.FindBuffIndex(gravityType)}");
        }
        if (frame < stepEnd) return;
        closing?.Invoke(player);
        closing = null;
        if (Steps.Count == 0)
        {
            finished = true;
            Log.Info($"csexcodex END pass={pass} fail={fail}");
            return;
        }
        var step = Steps.Dequeue();
        current = step.Name;
        stepEnd = frame + step.Length;
        closing = step.End;
        step.Begin(player);
    }
}
