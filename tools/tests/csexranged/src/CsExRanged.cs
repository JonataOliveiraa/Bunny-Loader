using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExRanged;

public sealed class CsExRangedMod : Mod
{
    const int End = 900;

    static int frame, pass, fail, pressed, peakMeow, peakAnimated, golf = -1;
    static bool use;
    static readonly HashSet<int> Frames = new();
    static readonly HashSet<int> Alphas = new();
    static float golfStartY;

    static ILog Log => ModContent.GetInstance<CsExRangedMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexranged {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static int ProjectileType(string name) => ModContent.Find<ModProjectile>("ExampleMod/" + name).Type;

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
            Main.mouseX = Main.screenWidth / 2 + 300;
            Main.mouseY = Main.screenHeight / 2 - 40;
        }
        orig(self);
    }

    static void Press()
    {
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
        if (frame == End) Log.Info($"csexranged END pass={pass} fail={fail}");
    }

    static void Hold(Player player, int type)
    {
        player.inventory[player.selectedItem].SetDefaults(type, default);
        player.direction = 1;
    }

    static void Step(Player player)
    {
        player.statMana = player.statManaMax2;
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 27000;
                Statics(player);
                break;
            case 100:
                Hold(player, ItemType("ExampleRocketLauncher"));
                player.inventory[54].SetDefaults(ItemID.RocketIII, default);
                player.inventory[54].stack = 99;
                Press();
                break;
            case 160:
                use = false;
                Report("the rocket launcher fires Rocket III as Meowmere", peakMeow > 0, $"peak={peakMeow}");
                player.inventory[54].TurnToAir();
                break;
            case 200:
                Hold(player, ItemType("ExampleAdvancedAnimatedProjectileItem"));
                Press();
                break;
            case 240:
                Log.Info("csexranged SHOT animated");
                break;
            case 300:
                use = false;
                Report("the animated item shoots its projectile", peakAnimated > 0, $"peak={peakAnimated}");
                Report("the projectile goes through its four frames", Frames.Count == 4, $"frames={string.Join(",", Frames.OrderBy(f => f))}");
                Report("the projectile fades in", Alphas.Contains(100) && Alphas.Any(a => a > 100), $"alphas={Alphas.Count}");
                break;
            case 400:
            {
                int type = ProjectileType("ExampleGolfBallProjectile");
                golf = Projectile.NewProjectile(player.GetSource_FromThis(), player.Center + new Vector2(60, -120), new Vector2(2, 0), type, 0, 0, Main.myPlayer);
                golfStartY = Main.projectile[golf].position.Y;
                break;
            }
            case 460:
                Log.Info("csexranged SHOT golf");
                break;
            case 520:
            {
                Projectile ball = Main.projectile[golf];
                Report("the golf ball falls and lands under the golf AI", ball.active && ball.type == ProjectileType("ExampleGolfBallProjectile") && ball.position.Y > golfStartY + 40, $"active={ball.active} dy={ball.position.Y - golfStartY}");
                ball.Kill();
                break;
            }
        }
        if (frame > 100 && frame <= 160) peakMeow = Math.Max(peakMeow, Owned(ProjectileID.Meowmere));
        if (frame > 200 && frame <= 300)
        {
            int animated = ProjectileType("ExampleAdvancedAnimatedProjectile");
            peakAnimated = Math.Max(peakAnimated, Owned(animated));
            for (int k = 0; k < Main.maxProjectiles; k++)
            {
                Projectile p = Main.projectile[k];
                if (!p.active || p.type != animated) continue;
                Frames.Add(p.frame);
                Alphas.Add(p.alpha);
            }
        }
    }

    static void Statics(Player player)
    {
        Report("the Example Mod is loaded", ModLoader.TryGetMod("ExampleMod", out _), "");
        var missing = new List<string>();
        foreach (string name in new[] { "ExampleRocketLauncher", "ExampleMultiplePrefixCategoryWeapon", "ExampleGolfBall", "ExampleAdvancedAnimatedProjectileItem" })
            if (!ModContent.TryFind<ModItem>("ExampleMod/" + name, out _)) missing.Add(name);
        foreach (string name in new[] { "ExampleGolfBallProjectile", "ExampleAdvancedAnimatedProjectile" })
            if (!ModContent.TryFind<ModProjectile>("ExampleMod/" + name, out _)) missing.Add(name);
        Report("every block 2 item and projectile is registered", missing.Count == 0, string.Join(",", missing));

        int launcher = ItemType("ExampleRocketLauncher");
        var matches = AmmoID.Sets.SpecificLauncherAmmoProjectileMatches;
        Report("the launcher falls back to the Rocket Launcher", AmmoID.Sets.SpecificLauncherAmmoProjectileFallback[launcher] == ItemID.RocketLauncher && AmmoID.Sets.SpecificLauncherAmmoProjectileFallback[ItemID.RocketLauncher] == -1, "");
        Report("the launcher's own matches are there", matches.TryGetValue(launcher, out var own) && own.Count == 1 && own[ItemID.RocketIII] == ProjectileID.Meowmere, "");
        bool vanilla = matches.TryGetValue(ItemID.RocketLauncher, out var rocketLauncher);
        Report("the game's Rocket Launcher matches read back", vanilla && rocketLauncher.Count >= 4 && rocketLauncher.TryGetValue(ItemID.RocketI, out int rocket) && rocket == ProjectileID.RocketI, $"count={(vanilla ? rocketLauncher.Count : -1)}");

        Item item = new Item(launcher);
        player.inventory[54].SetDefaults(ItemID.RocketI, default);
        player.inventory[54].stack = 10;
        int projectile = item.shoot, damage = item.damage;
        float speed = item.shootSpeed, knockBack = item.knockBack;
        bool canShoot = false;
        player.PickAmmo(item, ref projectile, ref speed, ref canShoot, ref damage, ref knockBack, out int ammo, true);
        Report("Rocket I comes from the fallback launcher", canShoot && ammo == ItemID.RocketI && projectile == ProjectileID.RocketI, $"canShoot={canShoot} proj={projectile} ammo={ammo}");
        player.inventory[54].SetDefaults(ItemID.RocketIII, default);
        player.inventory[54].stack = 10;
        projectile = item.shoot;
        canShoot = false;
        player.PickAmmo(item, ref projectile, ref speed, ref canShoot, ref damage, ref knockBack, out ammo, true);
        Report("Rocket III uses the launcher's own match", canShoot && projectile == ProjectileID.Meowmere, $"canShoot={canShoot} proj={projectile}");
        player.inventory[54].TurnToAir();
        Report("the launcher holds out with its offset", item.ModItem!.HoldoutOffset() == new Vector2(-8f, 2f) && item.useAmmo == AmmoID.Rocket && item.ranged, "");

        Item multi = new Item(ItemType("ExampleMultiplePrefixCategoryWeapon"));
        var rolled = new HashSet<int>();
        for (int k = 0; k < 400; k++)
        {
            multi.ResetPrefix();
            multi.Prefix(-1);
            rolled.Add(multi.prefix);
        }
        int[] melee = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, PrefixID.Legendary };
        int[] magic = { 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 52, PrefixID.Mythical };
        int[] ranged = { 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 58, PrefixID.Unreal };
        Report("reforging rolls melee and magic prefixes and no ranged ones",
            rolled.Overlaps(melee) && rolled.Overlaps(magic) && !rolled.Overlaps(ranged), $"rolled={string.Join(",", rolled.OrderBy(p => p))}");

        Item ball = new Item(ItemType("ExampleGolfBall"));
        ContentSamples.CreativeHelper.ItemGroup group = ContentSamples.CreativeHelper.GetItemGroup(ball, out _);
        Report("the golf ball shoots its ball and sorts with golf in research", ball.shoot == ProjectileType("ExampleGolfBallProjectile") && ProjectileID.Sets.IsAGolfBall[ball.shoot] && group == ContentSamples.CreativeHelper.ItemGroup.Golf, $"shoot={ball.shoot} group={group}");
    }
}
