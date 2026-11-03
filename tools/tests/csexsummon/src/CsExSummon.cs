using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExSummon;

public sealed class CsExSummonMod : Mod
{
    const int End = 1300;

    internal static readonly Dictionary<int, int> Hits = new();
    static int frame, pass, fail, pressed, slime = -1, peakMinion, peakSentry, peakPet, peakLight;
    static bool use;
    static Vector2 aim;

    static ILog Log => ModContent.GetInstance<CsExSummonMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexsummon {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static int ProjectileType(string name) => ModContent.Find<ModProjectile>("ExampleMod/" + name).Type;

    static int BuffType(string name) => ModContent.Find<ModBuff>("ExampleMod/" + name).Type;

    static int Owned(int type)
    {
        int count = 0;
        for (int k = 0; k < Main.maxProjectiles; k++)
        {
            Projectile p = Main.projectile[k];
            if (p.active && p.owner == Main.myPlayer && p.type == type) count++;
        }
        return count;
    }

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (!Main.gameMenu && self.whoAmI == Main.myPlayer && frame > 0)
        {
            self.controlUseItem = use;
            self.releaseUseItem = !use || frame == pressed;
            if (aim != Vector2.Zero)
            {
                Main.mouseX = (int)(aim.X - Main.screenPosition.X);
                Main.mouseY = (int)(aim.Y - Main.screenPosition.Y);
            }
        }
        orig(self);
    }

    static void Use(Player player, string item)
    {
        player.inventory[player.selectedItem].SetDefaults(ItemType(item), default);
        player.direction = 1;
        use = true;
        pressed = frame;
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
        if (frame == End) Log.Info($"csexsummon END pass={pass} fail={fail}");
    }

    static void KeepSlime(Player player)
    {
        Vector2 at = player.Center + new Vector2(200, -8);
        if (slime < 0 || !Main.npc[slime].active || Main.npc[slime].type != NPCID.BlueSlime)
        {
            slime = NPC.NewNPC(player.GetSource_FromThis(), (int)at.X, (int)at.Y + 24, NPCID.BlueSlime);
            Main.npc[slime].lifeMax = 100000;
            Main.npc[slime].damage = 0;
        }
        NPC n = Main.npc[slime];
        n.Center = at;
        n.velocity = Vector2.Zero;
        n.life = n.lifeMax;
    }

    static void Step(Player player)
    {
        player.statMana = player.statManaMax2;
        if (frame >= 100 && frame < 700) KeepSlime(player);
        int minion = ProjectileType("ExampleSimpleMinion"), sentry = ProjectileType("ExampleSentry");
        peakMinion = Math.Max(peakMinion, Owned(minion));
        peakSentry = Math.Max(peakSentry, Owned(sentry));
        peakPet = Math.Max(peakPet, Owned(ProjectileType("ExamplePetProjectile")));
        peakLight = Math.Max(peakLight, Owned(ProjectileType("ExampleLightPetProjectile")));
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 27000;
                Statics(player);
                break;
            case 100:
                aim = player.Center + new Vector2(120, -60);
                Hits.Clear();
                Use(player, "ExampleSimpleMinionItem");
                break;
            case 110:
                use = false;
                break;
            case 300:
            {
                int buff = BuffType("ExampleSimpleMinionBuff");
                Report("the minion staff summons the minion and its buff", peakMinion == 1 && player.HasBuff(buff), $"peak={peakMinion}");
                bool handler = BuffID.Sets.BuffTextHandlers.TryGetValue(buff, out var text);
                Report("the minion buff counts minions in its text", handler && !string.IsNullOrEmpty(text.HandleBuffText()), $"text='{(handler ? text.HandleBuffText() : "")}'");
                Report("the minion attacks the slime", Hits.GetValueOrDefault(minion) > 0, $"hits={Hits.GetValueOrDefault(minion)}");
                Log.Info("csexsummon SHOT minion");
                break;
            }
            case 320:
                aim = player.Center + new Vector2(80, -40);
                Hits.Clear();
                Use(player, "ExampleSentryItem");
                break;
            case 330:
                use = false;
                break;
            case 560:
            {
                int shot = ProjectileType("ExampleSentryShot");
                Report("the sentry staff places the sentry", peakSentry == 1 && player.maxTurrets >= 1, $"peak={peakSentry}");
                Report("the sentry shoots the slime", Hits.GetValueOrDefault(shot) > 0, $"hits={Hits.GetValueOrDefault(shot)}");
                Log.Info("csexsummon SHOT sentry");
                break;
            }
            case 700:
                for (int k = 0; k < Main.maxProjectiles; k++)
                    if (Main.projectile[k].active && Main.projectile[k].owner == Main.myPlayer && (Main.projectile[k].type == minion || Main.projectile[k].type == sentry)) Main.projectile[k].Kill();
                player.ClearBuff(BuffType("ExampleSimpleMinionBuff"));
                aim = Vector2.Zero;
                Use(player, "ExamplePetItem");
                break;
            case 710:
                use = false;
                break;
            case 800:
                Report("the pet item calls the paper airplane", peakPet == 1 && player.HasBuff(BuffType("ExamplePetBuff")), $"peak={peakPet}");
                player.inventory[player.selectedItem].SetDefaults(ItemType("ExampleLightPetItem"), default);
                break;
            case 810:
                Use(player, "ExampleLightPetItem");
                break;
            case 820:
                use = false;
                break;
            case 900:
                Report("the light pet item calls the annoying light", peakLight == 1 && player.HasBuff(BuffType("ExampleLightPetBuff")), $"peak={peakLight}");
                Log.Info("csexsummon SHOT pets");
                break;
            case 950:
            {
                int mount = ModContent.Find<ModMount>("ExampleMod/ExampleMinecartMount").Type;
                player.mount.SetMount(mount, player);
                Report("the minecart mounts as a cart", player.mount.Active && player.mount.Type == mount && player.mount.Cart, $"active={player.mount.Active} cart={player.mount.Cart}");
                break;
            }
            case 1000:
            {
                Log.Info("csexsummon SHOT minecart");
                int mount = ModContent.Find<ModMount>("ExampleMod/ExampleMinecartMount").Type;
                Mount.MountDelegatesData delegations = Mount.mounts[mount].delegations;
                int before = ActiveDusts();
                for (int k = 0; k < 20; k++) delegations.MinecartDust?.Invoke(player.Bottom);
                int after = ActiveDusts();
                var savedDust = delegations.MinecartDust;
                Vector2 seenDust = default;
                delegations.MinecartDust = at => seenDust = at;
                delegations.MinecartDust?.Invoke(new Vector2(11, 22));
                Report("a C# method works as the minecart's dust", seenDust == new Vector2(11, 22), $"seen={seenDust.X},{seenDust.Y}");
                delegations.MinecartDust = savedDust;
                var savedSound = delegations.MinecartBumperSound;
                string soundArgs = "";
                delegations.MinecartBumperSound = (who, at, w, h) => soundArgs = $"{who.whoAmI} {at.X},{at.Y} {w} {h}";
                delegations.MinecartBumperSound?.Invoke(player, new Vector2(33, 44), 5, 6);
                Report("a C# method works as the minecart's sound", soundArgs == $"{player.whoAmI} 33,44 5 6", $"args={soundArgs}");
                delegations.MinecartBumperSound = savedSound;
                Report("the minecart's dust is the Meowmere sparks", delegations.MinecartDust != null && after > before, $"before={before} after={after}");
                Report("the minecart's sounds are the fart cart's", delegations.MinecartLandingSound != null && delegations.MinecartBumperSound != null, "");
                delegations.MinecartLandingSound?.Invoke(player, player.position, player.width, player.height);
                break;
            }
            case 1100:
                player.mount.Dismount(player);
                break;
        }
    }

    static int ActiveDusts()
    {
        int count = 0;
        for (int k = 0; k < Main.maxDust; k++)
            if (Main.dust[k].active) count++;
        return count;
    }

    static void Statics(Player player)
    {
        Report("the Example Mod is loaded", ModLoader.TryGetMod("ExampleMod", out _), "");
        var missing = new List<string>();
        foreach (string n in new[] { "ExampleSimpleMinionItem", "ExampleSentryItem", "ExamplePetItem", "ExampleLightPetItem", "ExampleMinecart" })
            if (!ModContent.TryFind<ModItem>("ExampleMod/" + n, out _)) missing.Add(n);
        foreach (string n in new[] { "ExampleSimpleMinion", "ExampleSentry", "ExampleSentryShot", "ExamplePetProjectile", "ExampleLightPetProjectile" })
            if (!ModContent.TryFind<ModProjectile>("ExampleMod/" + n, out _)) missing.Add(n);
        foreach (string n in new[] { "ExampleSimpleMinionBuff", "ExamplePetBuff", "ExampleLightPetBuff", "ExampleMinecartBuff" })
            if (!ModContent.TryFind<ModBuff>("ExampleMod/" + n, out _)) missing.Add(n);
        if (!ModContent.TryFind<ModMount>("ExampleMod/ExampleMinecartMount", out _)) missing.Add("ExampleMinecartMount");
        Report("every block 3 item, projectile, buff and mount is registered", missing.Count == 0, string.Join(",", missing));

        int sentry = ProjectileType("ExampleSentry");
        Report("ContentSamples.ProjectilesByType gives the sentry's sample", ContentSamples.ProjectilesByType[sentry].height == 30 && ContentSamples.ProjectilesByType[sentry].sentry, $"height={ContentSamples.ProjectilesByType[sentry].height}");
        int pet = ProjectileType("ExamplePetProjectile");
        Report("the pet has its character preview animation", !ProjectileID.Sets.CharacterPreviewAnimations[pet].IsNull && Main.projPet[pet], "");
        Report("the light pet is a light pet", ProjectileID.Sets.LightPet[ProjectileType("ExampleLightPetProjectile")], "");
        int mount = ModContent.Find<ModMount>("ExampleMod/ExampleMinecartMount").Type;
        int cartBuff = BuffType("ExampleMinecartBuff");
        Report("the minecart is a cart with its buff", MountID.Sets.Cart[mount] && BuffID.Sets.MountType[cartBuff] == mount && Mount.mounts[mount].buff == cartBuff, $"buff={Mount.mounts[mount].buff} expected={cartBuff}");

        string petName = Lang.GetBuffName(BuffType("ExamplePetBuff")) ?? "";
        string itemName = Lang.GetItemNameValue(ItemType("ExamplePetItem")) ?? "";
        string tooltip = ModContent.Find<ModItem>("ExampleMod/ExamplePetItem").Tooltip.Value ?? "";
        Report("{$...} references inside the mod resolve, with and without Mods.ExampleMod", petName.Length > 0 && !petName.Contains("{$") && itemName == petName && tooltip.Contains(petName) && !tooltip.Contains("{$"), $"name='{petName}' item='{itemName}' tooltip='{tooltip.Replace('\n', '|')}'");
        string description = Lang.GetBuffDescription(BuffType("ExamplePetBuff")) ?? "";
        Report("a one-line ''' text reads as the text inside", description.StartsWith('"') && !description.Contains("'''"), $"description='{description}'");
        string cartName = Lang.GetBuffName(cartBuff) ?? "";
        Report("the minecart buff takes the item's name", cartName == Lang.GetItemNameValue(ItemType("ExampleMinecart")), $"buff='{cartName}'");
    }
}

public sealed class HitCounter : GlobalNPC
{
    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        CsExSummonMod.Hits[projectile.type] = CsExSummonMod.Hits.GetValueOrDefault(projectile.type) + 1;
    }
}
