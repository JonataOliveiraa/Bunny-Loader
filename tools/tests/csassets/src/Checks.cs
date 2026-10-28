using System;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace CsAssets;

public static class Checks
{
    static ILog Log => ModContent.GetInstance<CsAssetsMod>().Logger;

    static int pass, fail;

    public static void OnGameThread()
    {
        Asset<Texture2D> gem = ModContent.Request<Texture2D>("CsAssets/Content/Items/AssetGem");
        Texture2D texture = gem.Value;
        Report("mod texture loads", gem.IsLoaded && !texture.IsNull && texture.Width == 26 && texture.Height == 26,
            $"loaded={gem.IsLoaded} size={(texture.IsNull ? "null" : $"{texture.Width}x{texture.Height}")}");

        Asset<Texture2D> vanilla = ModContent.Request<Texture2D>("Terraria/Images/Item_1");
        Texture2D pickaxe = vanilla.Value;
        Report("vanilla texture loads", vanilla.IsLoaded && !pickaxe.IsNull && pickaxe.Width > 1,
            $"size={(pickaxe.IsNull ? "null" : $"{pickaxe.Width}x{pickaxe.Height}")}");
        Report("vanilla HasAsset", ModContent.HasAsset("Terraria/Images/Item_1"), "");
        Throws<InvalidOperationException>("missing vanilla texture", () => _ = ModContent.Request<Texture2D>("Terraria/Images/NoSuchImage").Value);
        Log.Info($"csassets END pass={pass} fail={fail}");
    }

    public static void Throws<T>(string name, Action body) where T : Exception
    {
        try
        {
            body();
            Report(name, false, "no exception");
        }
        catch (T e)
        {
            Report(name, true, e.Message);
        }
        catch (Exception e)
        {
            Report(name, false, $"{e.GetType().Name}: {e.Message}");
        }
    }

    public static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csassets {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
