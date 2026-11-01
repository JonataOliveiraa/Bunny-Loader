using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsClouds;

public sealed class TestCloud : ModCloud
{
    public static float Weight;
    public static bool Keep = true, Extra = true, Modify = true, IndexOk = true;
    public static DrawData Last;
    public static int Spawns, Draws, FrameDraws;

    public override string Texture => "Clouds/TestCloud";

    public override float SpawnChance() => Weight;

    public override void OnSpawn(Cloud cloud)
    {
        Spawns++;
        cloud.spriteDir = SpriteEffects.None;
    }

    public override bool Draw(List<DrawData> drawDataCache, Cloud cloud, int cloudIndex, ref DrawData drawData)
    {
        Draws++;
        FrameDraws++;
        if (cloudIndex < 0 || Main.cloud[cloudIndex].Pointer != cloud.Pointer) IndexOk = false;
        Last = drawData;
        if (Keep && Modify) drawData.color *= 0.9f;
        if (Extra)
        {
            var copy = drawData;
            copy.color *= 0.5f;
            drawDataCache.Add(copy);
        }
        return Keep;
    }
}

public sealed class RareTestCloud : ModCloud
{
    public static float Weight;
    public static int Spawns;

    public override string Texture => "Clouds/RareTestCloud";

    public override bool RareCloud => true;

    public override float SpawnChance() => Weight;

    public override void OnSpawn(Cloud cloud) => Spawns++;
}

public sealed class ManualClouds : ICustomAutoload
{
    public static void Autoload(Mod mod) => CloudLoader.AddCloudFromTexture(mod, "CsClouds/Clouds/ManualCloud", spawnChance: 0.5f, rareCloud: true);
}

public sealed class CsCloudsMod : Mod
{
    static int frames, pass, fail, textureDraws, drawsAtStart;
    static bool done, inCloud, watching;

    static ILog Log => ModContent.GetInstance<CsCloudsMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_NextHorizonRenderer.DrawCloud += (orig, self, alpha, cloud, pass, y) =>
        {
            bool outer = inCloud;
            inCloud = true;
            try { orig(self, alpha, cloud, pass, y); }
            finally { inCloud = outer; }
        };
    }

    static void Count(Texture2D texture)
    {
        if (inCloud && texture.Width == 186 && texture.Height == 65) textureDraws++;
    }

    static int vanillaMatch, vanillaMiss;
    static string vanillaWhere = "", vanillaDetail = "";

    static void Vanilla(string overload, Texture2D texture, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Vector2 origin, float scale)
    {
        if (!inCloud || !comparing || texture.Width != 186 || texture.Height != 65) return;
        DrawData d = TestCloud.Last;
        var a = position - origin * scale;
        var e = d.position - d.origin * d.scale.X;
        if (Math.Abs(a.X - e.X) < 1.5f && Math.Abs(a.Y - e.Y) < 1.5f && Math.Abs(scale - d.scale.X) < 0.001f) vanillaMatch++;
        else
        {
            vanillaMiss++;
            vanillaDetail = $"game {a.X:0.0},{a.Y:0.0} x{scale:0.00} mine {e.X:0.0},{e.Y:0.0} x{d.scale.X:0.00}";
        }
        if (!vanillaWhere.Contains(overload)) vanillaWhere += overload + " ";
    }

    static bool comparing;

    static void Watch()
    {
        if (watching) return;
        watching = true;
        On_SpriteBatch_Cloud.Draw_Texture2D_Vector2_Nullable_Color_float_Vector2_Vector2_SpriteEffects_float += (orig, self, texture, position, source, color, rotation, origin, scale, effects, depth) =>
        {
            Count(texture);
            Vanilla("VecV", texture, position, origin, scale.X);
            orig(self, texture, position, source, color, rotation, origin, scale, effects, depth);
        };
        On_SpriteBatch_Cloud.Draw_Texture2D_Vector2_Nullable_Color_float_Vector2_float_SpriteEffects_float += (orig, self, texture, position, source, color, rotation, origin, scale, effects, depth) =>
        {
            Vanilla("VecF", texture, position, origin, scale);
            orig(self, texture, position, source, color, rotation, origin, scale, effects, depth);
        };
        On_SpriteBatch_Cloud.Draw_Texture2D_Vector2_Nullable_refVertexColors_float_Vector2_Vector2_SpriteEffects_float += (orig, self, texture, position, source, ref colors, rotation, origin, scale, effects, depth) =>
        {
            Vanilla("VertV", texture, position, origin, scale.X);
            orig(self, texture, position, source, ref colors, rotation, origin, scale, effects, depth);
        };
        On_SpriteBatch_Cloud.Draw_Texture2D_Vector2_Nullable_refVertexColors_float_Vector2_float_SpriteEffects_float += (orig, self, texture, position, source, ref colors, rotation, origin, scale, effects, depth) =>
        {
            Vanilla("VertF", texture, position, origin, scale);
            orig(self, texture, position, source, ref colors, rotation, origin, scale, effects, depth);
        };
        On_SpriteBatch_Cloud.Draw_Texture2D_refVector2_refRectangle_refColor_float_Vector2_float_SpriteEffects += (orig, self, texture, ref position, ref source, ref color, rotation, origin, scale, effects) =>
        {
            Vanilla("RefF", texture, position, origin, scale);
            orig(self, texture, ref position, ref source, ref color, rotation, origin, scale, effects);
        };
        On_SpriteBatch_Cloud.Draw_Texture2D_Vector2_refRectangle_Color_float_Vector2_float_SpriteEffects_float += (orig, self, texture, position, ref source, color, rotation, origin, scale, effects, depth) =>
        {
            Vanilla("RectF", texture, position, origin, scale);
            orig(self, texture, position, ref source, color, rotation, origin, scale, effects, depth);
        };
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(self, ++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csclouds END pass={pass} fail={fail}");
    }

    static void Step(Player p, int f)
    {
        if (f == 1)
        {
            Main.dayTime = true;
            Main.time = 27000;
        }
        switch (f)
        {
            case 10:
                Registration();
                break;
            case 20:
                Rolls();
                break;
            case 30:
                Spawning();
                break;
            case 96:
                Watch();
                TestCloud.Modify = false;
                TestCloud.Extra = false;
                comparing = true;
                break;
            case 99:
                comparing = false;
                Report("the cloud's draw data matches the game's own draw", vanillaMatch > 0 && vanillaMiss == 0, $"match={vanillaMatch} miss={vanillaMiss} via {vanillaWhere}{vanillaDetail}");
                TestCloud.Modify = true;
                TestCloud.Extra = true;
                break;
            case 100:
                textureDraws = 0;
                drawsAtStart = TestCloud.Draws;
                break;
            case 200:
                {
                    int draws = TestCloud.Draws - drawsAtStart;
                    Report("Draw runs for the mod clouds on screen with their index", draws > 0 && TestCloud.IndexOk, $"draws={draws} indexOk={TestCloud.IndexOk}");
                    Report("Draw: the extra draw data and the changed cloud are drawn on the cloud batch", draws > 0 && textureDraws == 2 * draws, $"texture draws={textureDraws} for {draws}");
                    TestCloud.Keep = false;
                    TestCloud.Extra = false;
                    textureDraws = 0;
                    drawsAtStart = TestCloud.Draws;
                    break;
                }
            case 260:
                {
                    int draws = TestCloud.Draws - drawsAtStart;
                    Report("Draw returning false draws nothing", draws > 0 && textureDraws == 0, $"texture draws={textureDraws} for {draws}");
                    TestCloud.Weight = RareTestCloud.Weight = 0;
                    done = true;
                    break;
                }
        }
    }
    static void Registration()
    {
        var mod = ModContent.GetInstance<CsCloudsMod>();
        ModCloud[] mine = mod.GetContent<ModCloud>().ToArray();
        string names = string.Join(",", mine.Select(c => $"{c.Name}:{c.Type}").OrderBy(n => n));
        Report("four clouds: two classes, one from ICustomAutoload and one autoloaded, without duplicates",
            mine.Length == 4 && mod.TryFind<ModCloud>("ManualCloud", out _) && mod.TryFind<ModCloud>("AutoCloud", out _), names);
        int test = ModContent.GetInstance<TestCloud>().Type;
        int max = mine.Max(c => c.Type);
        Report("types after the game's", mine.All(c => c.Type >= CloudID.Count) && CloudLoader.CloudCount == max + 1, $"count={CloudID.Count} {names} total={CloudLoader.CloudCount}");
        Report("cloud textures grew and hold the mod's", TextureAssets.Cloud.Length > max && TextureAssets.Cloud[test].Value.Width == 186, $"{TextureAssets.Cloud.Length}");
        var masks = TextureMaskManager.CloudMasks;
        Report("horizon masks grew with the texture size", masks.Length > max && masks[test].Pointer != IntPtr.Zero && masks[test].srcWidth == 186 && masks[test].srcHeight == 65,
            $"{masks.Length} {(masks.Length > test ? masks[test].srcWidth : -1)}");
        mod.TryFind("ManualCloud", out ModCloud manual);
        mod.TryFind("AutoCloud", out ModCloud auto);
        Report("AddCloudFromTexture keeps the chance and the pool; autoload is common with chance 1",
            manual.RareCloud && manual.SpawnChance() == 0.5f && !auto.RareCloud && auto.SpawnChance() == 1f && manual is SimpleModCloud,
            $"manual {manual.RareCloud} {manual.SpawnChance()} auto {auto.RareCloud} {auto.SpawnChance()}");
    }

    static void Rolls()
    {
        int test = ModContent.GetInstance<TestCloud>().Type, rare = ModContent.GetInstance<RareTestCloud>().Type;
        int vanilla = 0;
        for (int k = 0; k < 200; k++)
            if (CloudLoader.ChooseCloud(22, false) == 0) vanilla++;
        Report("weight 0: the game's clouds and the autoloaded one share the roll", vanilla > 150 && vanilla < 200, $"{vanilla}/200 vanilla");
        TestCloud.Weight = 1e6f;
        RareTestCloud.Weight = 1e6f;
        int commons = 0, rares = 0, rolled = 0;
        for (int k = 0; k < 100; k++)
        {
            if (CloudLoader.ChooseCloud(22, false) == test) commons++;
            if (CloudLoader.ChooseCloud(18, true) == rare) rares++;
            if (Cloud.RollRareCloud() == rare) rolled++;
        }
        Report("a heavy weight wins the common and the rare rolls, RollRareCloud too", commons > 95 && rares > 95 && rolled > 95, $"common={commons} rare={rares} rolled={rolled}");
    }

    static void Spawning()
    {
        TestCloud.Spawns = RareTestCloud.Spawns = 0;
        var clouds = Main.cloud;
        for (int k = 0; k < clouds.Length; k++) clouds[k].active = false;
        for (int k = 0; k < 30; k++) Cloud.addCloud();
        int test = ModContent.GetInstance<TestCloud>().Type, rare = ModContent.GetInstance<RareTestCloud>().Type;
        int tests = 0, rares = 0, sized = 0, flipped = 0;
        for (int k = 0; k < clouds.Length; k++)
        {
            Cloud c = clouds[k];
            if (!c.active) continue;
            if (c.type == test)
            {
                tests++;
                if (c.width == (int)(186 * c.scale)) sized++;
                if (c.spriteDir != SpriteEffects.None) flipped++;
            }
            if (c.type == rare) rares++;
        }
        Report("addCloud spawns the mod clouds with their size and OnSpawn", tests > 0 && sized == tests && flipped == 0 && TestCloud.Spawns == tests && RareTestCloud.Spawns == rares,
            $"test={tests} sized={sized} flipped={flipped} spawns={TestCloud.Spawns} rare={rares}/{RareTestCloud.Spawns}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csclouds {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
