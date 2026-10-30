using System;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CsPlayerDraw;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPlayerDrawMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 180) return;
        done = true;
        foreach (Action step in new Action[] { Order, Layers, Colors, Camera, Zoom })
        {
            try { step(); }
            catch (Exception e) { Report(step.Method.Name, false, e.ToString()); }
        }
        Log.Info($"csplayerdraw END pass={pass} fail={fail}");
    }

    static int IndexOf(PlayerDrawLayer layer) => PlayerDrawLayerLoader.DrawOrder.ToList().IndexOf(layer);

    static void Order()
    {
        PlayerDrawLayer between = ModContent.GetInstance<BetweenLayer>(), moved = ModContent.GetInstance<MovedLayer>();
        int at = IndexOf(between);
        Report("a Between layer sits between its two layers", at > IndexOf(PlayerDrawLayers.Torso) && at < IndexOf(PlayerDrawLayers.OffhandAcc),
            $"torso={IndexOf(PlayerDrawLayers.Torso)} layer={at} offhand={IndexOf(PlayerDrawLayers.OffhandAcc)}");
        Report("a parent layer gets its child", PlayerDrawLayers.Head.ChildrenAfter.Contains(ModContent.GetInstance<ChildLayer>()) &&
            PlayerDrawLayers.Skin.ChildrenBefore.Contains(ModContent.GetInstance<HiddenLayer>()), $"head={PlayerDrawLayers.Head.ChildrenAfter.Count}");
        int movedAt = IndexOf(moved);
        Report("ModifyDrawLayerOrdering moves a layer", Counts.Of(nameof(DrawingPlayer.ModifyDrawLayerOrdering)) >= 1 &&
            movedAt > IndexOf(PlayerDrawLayers.Wings) && movedAt < IndexOf(PlayerDrawLayers.HairBack),
            $"wings={IndexOf(PlayerDrawLayers.Wings)} moved={movedAt} hair={IndexOf(PlayerDrawLayers.HairBack)}");
        Report("the order keeps every game layer", IndexOf(PlayerDrawLayers.FirstVanillaLayer) == 0,
            $"order={PlayerDrawLayerLoader.DrawOrder.Count}");
    }

    static void Layers()
    {
        int drawn = Counts.Of(nameof(BetweenLayer));
        Report("mod layers draw every frame", drawn >= 100 && Counts.Of(nameof(MovedLayer)) >= 100 && Counts.Of(nameof(ChildLayer)) >= 100,
            $"between={drawn} moved={Counts.Of(nameof(MovedLayer))} child={Counts.Of(nameof(ChildLayer))}");
        Report("game layers still draw before ours", BetweenLayer.CacheBefore > 0, $"cache={BetweenLayer.CacheBefore}");
        Report("TextureAssets.Item gives the game texture", BetweenLayer.TextureWidth == 16 && Terraria.GameContent.TextureAssets.Item[1].Size().X > 0, $"width={BetweenLayer.TextureWidth}");
        Report("DrawDataCache.Add grows the cache", BetweenLayer.CacheAfter == BetweenLayer.CacheBefore + 1, $"{BetweenLayer.CacheBefore}->{BetweenLayer.CacheAfter}");
        Report("HideDrawLayers hides a layer", Counts.Of(nameof(DrawingPlayer.HideDrawLayers)) >= 100 && Counts.Of(nameof(HiddenLayer)) == 0,
            $"hide={Counts.Of(nameof(DrawingPlayer.HideDrawLayers))} hidden={Counts.Of(nameof(HiddenLayer))}");
        Report("TransformDrawData sees the finished cache", Counts.Of(nameof(DrawingPlayer.TransformDrawData)) >= 100 && DrawingPlayer.TransformCache > BetweenLayer.CacheBefore,
            $"calls={Counts.Of(nameof(DrawingPlayer.TransformDrawData))} cache={DrawingPlayer.TransformCache}");
    }

    static void Colors()
    {
        Report("DrawEffects tints the colors before ModifyDrawInfo", Counts.Of(nameof(DrawingPlayer.DrawEffects)) >= 100 && Counts.Of(nameof(DrawingPlayer.ModifyDrawInfo)) >= 100 &&
            DrawingPlayer.ShirtRed == 0 && DrawingPlayer.PantsRed == 0,
            $"effects={Counts.Of(nameof(DrawingPlayer.DrawEffects))} info={Counts.Of(nameof(DrawingPlayer.ModifyDrawInfo))} shirt={DrawingPlayer.ShirtRed} pants={DrawingPlayer.PantsRed}");
    }

    static void Camera()
    {
        Report("DrawPlayer gets the camera", Counts.Of(nameof(DrawingPlayer.DrawPlayer)) >= 100 && DrawingPlayer.CameraSeen, $"calls={Counts.Of(nameof(DrawingPlayer.DrawPlayer))}");
        Report("ModifyScreenPosition runs every frame", Counts.Of(nameof(DrawingPlayer.ModifyScreenPosition)) >= 100, $"calls={Counts.Of(nameof(DrawingPlayer.ModifyScreenPosition))}");
    }

    static void Zoom()
    {
        Vector2 old = Main.GameViewMatrix.Zoom;
        float x = old.X, y = old.Y;
        DrawingPlayer.ZoomFactor = 1.5f;
        Main.GameViewMatrix.Zoom = new Vector2(1.2f, 1.2f);
        float zoomed = Main.GameViewMatrix.Zoom.X;
        DrawingPlayer.ZoomFactor = 1f;
        Main.GameViewMatrix.Zoom = new Vector2(x, y);
        Report("ModifyZoom scales the game zoom", Math.Abs(zoomed - 1.8f) < 0.001f, $"zoom={zoomed} restored={Main.GameViewMatrix.Zoom.X}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csplayerdraw {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
