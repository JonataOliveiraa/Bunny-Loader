using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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

    const int PhasePre = 1;
    const int PhasePost = 2;
    const int PhasesKnown = 8;

    static PlayerStatsSnapshot snapshot;
    static int resourceNumber;
    static bool display;
    static bool drawLifeText = true;
    static ResourceOverlayDrawContext current;
    static bool pending;
    static int phases;

    internal static void Install(Type type)
    {
        bool Has(string name) => ItemHooks.Overrides(type, typeof(ModResourceOverlay), name);
        bool pre = Has(nameof(ModResourceOverlay.PreDrawResource)), post = Has(nameof(ModResourceOverlay.PostDrawResource));
        if (pre || post)
        {
            Volatile.Write(ref phases, Volatile.Read(ref phases) | (pre ? PhasePre : 0) | (post ? PhasePost : 0));
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
        }
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

    static bool BeforeDisplay(bool life, out Color textColor, out bool drawText)
    {
        snapshot = new PlayerStatsSnapshot(Main.LocalPlayer);
        textColor = Color.White;
        drawText = true;
        if (!display) return true;
        bool go = ResourceOverlayLoader.PreDrawResourceDisplay(snapshot, DisplaySet, life, ref textColor, out drawText);
        if (life) drawLifeText = drawText;
        return go;
    }

    static void HeartsDraw(On_GUIHearts.orig_Draw orig, GUIHearts self)
    {
        bool go = BeforeDisplay(true, out Color textColor, out bool drawText);
        try
        {
            if (go) orig(self);
        }
        finally { drawLifeText = true; }
        if (display) ResourceOverlayLoader.PostDrawResourceDisplay(snapshot, DisplaySet, true, textColor, drawText);
    }

    static void ManaDraw(On_GUIMana.orig_Draw orig, GUIMana self)
    {
        bool go = BeforeDisplay(false, out Color textColor, out bool drawText);
        try
        {
            if (go) orig(self);
        }
        finally { drawLifeText = true; }
        if (display) ResourceOverlayLoader.PostDrawResourceDisplay(snapshot, DisplaySet, false, textColor, drawText);
    }

    static void DrawLifeString(On_GUIHearts.orig_DrawLifeString orig, GUIHearts self)
    {
        if (drawLifeText) orig(self);
    }

    static void HeartDraw(On_GUIHearts.orig_HeartDraw orig, GUIHearts self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        resourceNumber = index;
        Host.Native()->ResourceDrawActive(1);
        try { orig(self, gridLayout, index, position, scale); }
        finally { Host.Native()->ResourceDrawActive(0); }
    }

    static void StarDraw(On_GUIMana.orig_StarDraw orig, GUIMana self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        resourceNumber = index;
        Host.Native()->ResourceDrawActive(1);
        try { orig(self, gridLayout, index, position, scale); }
        finally { Host.Native()->ResourceDrawActive(0); }
    }

    static ResourceOverlayDrawContext Context(IntPtr spriteBatch, DrawArgs* args)
    {
        uint packed = args->Color;
        var context = new ResourceOverlayDrawContext(snapshot, DisplaySet, resourceNumber, Asset<Texture2D>.ForTexture(args->Texture))
        {
            SpriteBatch = new SpriteBatch(spriteBatch),
        };
        context.position = new Vector2(args->X, args->Y);
        context.source = args->HasSource != 0 ? new Rectangle(args->SourceX, args->SourceY, args->SourceWidth, args->SourceHeight) : null;
        context.color = Unsafe.As<uint, Color>(ref packed);
        context.rotation = args->Rotation;
        context.origin = new Vector2(args->OriginX, args->OriginY);
        context.scale = new Vector2(args->ScaleX, args->ScaleY);
        context.effects = (SpriteEffects)args->Effects;
        return context;
    }

    [UnmanagedCallersOnly]
    static int ResourceDraw(int phase, IntPtr spriteBatch, IntPtr argsPointer)
    {
        int known = Volatile.Read(ref phases) << 1 | PhasesKnown;
        try
        {
            if (phase != 0)
            {
                if (!pending) current = Context(spriteBatch, (DrawArgs*)argsPointer);
                pending = false;
                ResourceOverlayLoader.PostDrawResource(current);
                return known;
            }
            current = Context(spriteBatch, (DrawArgs*)argsPointer);
            pending = true;
            return (ResourceOverlayLoader.PreDrawResource(current) ? 1 : 0) | known;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ResourceOverlay", e);
            return phase == 0 ? 1 : 0;
        }
    }
}
