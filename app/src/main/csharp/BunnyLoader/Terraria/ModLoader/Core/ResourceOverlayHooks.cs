using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent.UI.ResourceSets;

namespace Terraria.ModLoader.Core;

internal static unsafe class ResourceOverlayHooks
{
    [StructLayout(LayoutKind.Sequential)]
    struct DrawArgs
    {
        public IntPtr Texture;
        public float X, Y;
        public int HasSource;
        public int SourceX, SourceY, SourceWidth, SourceHeight;
        public uint Color;
        public float Rotation;
        public float OriginX, OriginY;
        public float ScaleX, ScaleY;
        public int Effects;
    }

    static PlayerStatsSnapshot snapshot;
    static int resourceNumber;
    static bool display;
    static bool drawLifeText = true;
    static ResourceOverlayDrawContext current;

    internal static void Install(Type type)
    {
        bool Has(string name) => ItemHooks.Overrides(type, typeof(ModResourceOverlay), name);
        if (Has(nameof(ModResourceOverlay.PreDrawResource)) || Has(nameof(ModResourceOverlay.PostDrawResource)))
            ItemHooks.Once("ResourceDraws", () =>
            {
                if (Host.Native()->OnResourceDraw(&ResourceDraw) == 0)
                {
                    Log.Error("resource overlay: the SpriteBatch hooks are not installed; PreDrawResource and PostDrawResource do not run");
                    return;
                }
                WantSnapshot();
                On_GUIHearts.HeartDraw += HeartDraw;
                On_GUIMana.StarDraw += StarDraw;
            });
        if (Has(nameof(ModResourceOverlay.PreDrawResourceDisplay)) || Has(nameof(ModResourceOverlay.PostDrawResourceDisplay)))
            ItemHooks.Once("ResourceDisplay", () =>
            {
                display = true;
                WantSnapshot();
                On_GUIHearts.DrawLifeString += DrawLifeString;
            });
    }

    static void WantSnapshot() => ItemHooks.Once("ResourceSnapshot", () =>
    {
        On_GUIHearts.Draw += HeartsDraw;
        On_GUIMana.Draw += ManaDraw;
    });

    static IPlayerResourcesDisplaySet DisplaySet => Main.ActivePlayerResourcesSet;

    static void Display(bool life, Action draw)
    {
        snapshot = new PlayerStatsSnapshot(Main.LocalPlayer);
        if (!display)
        {
            draw();
            return;
        }
        Color textColor = Color.White;
        bool go = ResourceOverlayLoader.PreDrawResourceDisplay(snapshot, DisplaySet, life, ref textColor, out bool drawText);
        if (life) drawLifeText = drawText;
        try
        {
            if (go) draw();
        }
        finally { drawLifeText = true; }
        ResourceOverlayLoader.PostDrawResourceDisplay(snapshot, DisplaySet, life, textColor, drawText);
    }

    static void HeartsDraw(On_GUIHearts.orig_Draw orig, GUIHearts self) => Display(true, () => orig(self));

    static void ManaDraw(On_GUIMana.orig_Draw orig, GUIMana self) => Display(false, () => orig(self));

    static void DrawLifeString(On_GUIHearts.orig_DrawLifeString orig, GUIHearts self)
    {
        if (drawLifeText) orig(self);
    }

    static void Around(int index, Action draw)
    {
        resourceNumber = index;
        Host.Native()->ResourceDrawActive(1);
        try { draw(); }
        finally { Host.Native()->ResourceDrawActive(0); }
    }

    static void HeartDraw(On_GUIHearts.orig_HeartDraw orig, GUIHearts self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale) =>
        Around(index, () => orig(self, gridLayout, index, position, scale));

    static void StarDraw(On_GUIMana.orig_StarDraw orig, GUIMana self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale) =>
        Around(index, () => orig(self, gridLayout, index, position, scale));

    [UnmanagedCallersOnly]
    static int ResourceDraw(int phase, IntPtr spriteBatch, IntPtr argsPointer)
    {
        try
        {
            if (phase != 0)
            {
                ResourceOverlayLoader.PostDrawResource(current);
                return 0;
            }
            DrawArgs* args = (DrawArgs*)argsPointer;
            uint packed = args->Color;
            current = new ResourceOverlayDrawContext(snapshot, DisplaySet, resourceNumber, Asset<Texture2D>.ForTexture(args->Texture))
            {
                SpriteBatch = new SpriteBatch(spriteBatch),
            };
            current.position = new Vector2(args->X, args->Y);
            current.source = args->HasSource != 0 ? new Rectangle(args->SourceX, args->SourceY, args->SourceWidth, args->SourceHeight) : null;
            current.color = Unsafe.As<uint, Color>(ref packed);
            current.rotation = args->Rotation;
            current.origin = new Vector2(args->OriginX, args->OriginY);
            current.scale = new Vector2(args->ScaleX, args->ScaleY);
            current.effects = (SpriteEffects)args->Effects;
            return ResourceOverlayLoader.PreDrawResource(current) ? 1 : 0;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ResourceOverlay", e);
            return phase == 0 ? 1 : 0;
        }
    }
}
