using System;
using CsVanillaTex.Content;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsVanillaTex;

public static class Checks
{
    static int pass, fail, frames, holdout = -1, slime = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsVanillaTexMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        holdout = slime = -1;
        done = false;
        Counts.Calls.Clear();
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 30) Spawn(p);
            if (frames == 150) Log.Info("csvanillatex shot");
            if (frames != 240) return;
            done = true;
            Run(p);
        }
        catch (Exception e)
        {
            done = true;
            Report("run", false, e.ToString());
        }
        if (done) Log.Info($"csvanillatex END pass={pass} fail={fail}");
    }

    static void Spawn(Player p)
    {
        holdout = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(60f, -40f), Vector2.Zero, ModContent.ProjectileType<VanillaPrismHoldout>(), 0, 0f, p.whoAmI);
        Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(-60f, -40f), Vector2.Zero, ModContent.ProjectileType<OwnBolt>(), 0, 0f, p.whoAmI);
        Item.NewItem(X: (int)p.Center.X - 160, Y: (int)p.Center.Y, Width: 1, Height: 1, Type: ModContent.ItemType<VanillaPrism>(), Stack: 1, noBroadcast: false, pfix: 0, ownership: default);
        Item.NewItem(X: (int)p.Center.X - 220, Y: (int)p.Center.Y, Width: 1, Height: 1, Type: ModContent.ItemType<MissingGameItem>(), Stack: 1, noBroadcast: false, pfix: 0, ownership: default);
        Item.NewItem(X: (int)p.Center.X - 280, Y: (int)p.Center.Y, Width: 1, Height: 1, Type: ModContent.ItemType<MissingFileItem>(), Stack: 1, noBroadcast: false, pfix: 0, ownership: default);
        slime = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + 120, (int)p.Center.Y - 40, ModContent.NPCType<VanillaSlime>());
        p.AddBuff(ModContent.BuffType<VanillaBuff>(), 3600);
    }

    static IntPtr Game(string name) => ModContent.Request<Texture2D>(name, AssetRequestMode.ImmediateLoad).Value.Pointer;

    static void Run(Player p)
    {
        int item = ModContent.ItemType<VanillaPrism>();
        Texture2D own = TextureAssets.Item[item].Value;
        IntPtr prism = Game("Terraria/Images/Item_" + ItemID.LastPrism);
        Report("ModItem with a game texture uses the game's image", own.Pointer == prism && own.Width > 1 && own.Height > 1,
            $"type={item} same={own.Pointer == prism} size={own.Width}x{own.Height}");
        Report("the game-texture item draws in the world", Counts.Of("ItemDrawInWorld") >= 60, $"draws={Counts.Of("ItemDrawInWorld")}");

        int projectile = ModContent.ProjectileType<VanillaPrismHoldout>();
        Texture2D shot = TextureAssets.Projectile[projectile].Value;
        IntPtr beam = Game("Terraria/Images/Projectile_" + ProjectileID.LastPrism);
        Report("ModProjectile with a game texture uses the game's image", shot.Pointer == beam && shot.Height > 5 && Main.projFrames[projectile] == 5,
            $"type={projectile} same={shot.Pointer == beam} size={shot.Width}x{shot.Height} frames={Main.projFrames[projectile]}");
        Report("the game-texture projectile draws", Counts.Of("ProjectilePreDraw") >= 5 && Main.projectile[holdout].active && Main.projectile[holdout].type == projectile,
            $"draws={Counts.Of("ProjectilePreDraw")} active={Main.projectile[holdout].active}");

        int buff = ModContent.BuffType<VanillaBuff>();
        bool sameBuff = TextureAssets.Buff[buff].Pointer == TextureAssets.Buff[BuffID.Ironskin].Pointer;
        Report("ModBuff with a game texture uses the game's image", sameBuff && p.HasBuff(buff), $"type={buff} same={sameBuff} has={p.HasBuff(buff)}");

        int npc = ModContent.NPCType<VanillaSlime>();
        IntPtr blue = Game("Terraria/Images/NPC_" + NPCID.BlueSlime);
        Report("ModNPC with a game texture uses the game's image", TextureAssets.Npc[npc].Value.Pointer == blue && slime >= 0 && Main.npc[slime].type == npc,
            $"type={npc} same={TextureAssets.Npc[npc].Value.Pointer == blue} slime={slime}");

        Texture2D missingGame = TextureAssets.Item[ModContent.ItemType<MissingGameItem>()].Value;
        Texture2D missingFile = TextureAssets.Item[ModContent.ItemType<MissingFileItem>()].Value;
        Report("a missing texture draws the placeholder instead of failing the mod",
            missingGame.Pointer == missingFile.Pointer && missingGame.Width == 24 && missingGame.Height == 34 && ModContent.ProjectileType<OwnBolt>() > 0,
            $"same={missingGame.Pointer == missingFile.Pointer} size={missingGame.Width}x{missingGame.Height}");
        Texture2D bolt = TextureAssets.Projectile[ModContent.ProjectileType<OwnBolt>()].Value;
        Report("a mod PNG still loads next to them", bolt.Width == 12 && bolt.Height == 12, $"size={bolt.Width}x{bolt.Height}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csvanillatex {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
