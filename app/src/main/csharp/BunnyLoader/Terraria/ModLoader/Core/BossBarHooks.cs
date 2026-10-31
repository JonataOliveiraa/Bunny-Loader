using System;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;

namespace Terraria.ModLoader.Core;

internal static class BossBarHooks
{
    const int ScanEvery = 30;

    struct Look
    {
        public Color? BarColor, IconColor;
        public IntPtr Icon;
        public bool ShowText;
    }

    static ModBossBar? bar;
    static int index = -1, tracked = -1, scanAge;
    static bool drawing, looking, looksHooked;
    static Look look;
    static Panel_Layout panel;

    internal static void Install(bool globals)
    {
        ItemHooks.Once("BossBarSystem", () =>
        {
            On_BigProgressBarSystem.Update += Update;
            On_BigProgressBarSystem.Draw += Draw;
        });
        if (globals) ItemHooks.Once("BossBarGlobals", () => On_GUIBossBar.Draw += GuiDraw);
    }

    static ModBossBar? Of(NPC npc) => npc.active ? BossBarLoader.Get(npc) : null;

    static bool Collect(ModBossBar candidate, int at, bool showText)
    {
        var info = new BigProgressBarInfo { npcIndexToAimAt = at, showText = showText };
        bool valid;
        try { valid = candidate.ValidateAndCollectNecessaryInfo(ref info); }
        catch (Exception e)
        {
            ItemHooks.Report($"{candidate.FullName}.ModifyInfo", e);
            valid = false;
        }
        if (!valid) return false;
        bar = candidate;
        index = info.npcIndexToAimAt;
        return true;
    }

    static void Update(On_BigProgressBarSystem.orig_Update orig, BigProgressBarSystem self)
    {
        orig(self);
        bar = null;
        index = -1;
        try
        {
            if (!self._currentBar.IsNull)
            {
                tracked = -1;
                int at = self._info.npcIndexToAimAt;
                ModBossBar? own = (uint)at < 200u ? Of(Main.npc[at]) : null;
                if (own != null && !Collect(own, at, self._info.showText)) self._currentBar = default;
                return;
            }
            if (tracked >= 0)
            {
                ModBossBar? own = Of(Main.npc[tracked]);
                if (own != null && Collect(own, tracked, self._info.showText)) return;
                tracked = -1;
            }
            if (++scanAge < ScanEvery || !BossBarLoader.AnyAssigned) return;
            scanAge = 0;
            int found = Find();
            if (found >= 0 && Of(Main.npc[found]) is ModBossBar candidate && Collect(candidate, found, self._info.showText)) tracked = found;
        }
        catch (Exception e) { ItemHooks.Report("ModBossBar update", e); }
    }

    static int Find()
    {
        Player me = Main.LocalPlayer;
        Vector2 center = me.Center, screen = Main.screenPosition;
        float left = screen.X - 5000f, right = screen.X + Main.screenWidth + 5000f, top = screen.Y - 5000f, bottom = screen.Y + Main.screenHeight + 5000f;
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < 200; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active) continue;
            Vector2 position = npc.position;
            if (position.X + npc.width < left || position.X > right || position.Y + npc.height < top || position.Y > bottom) continue;
            if (BossBarLoader.Get(npc) == null) continue;
            float distance = Vector2.Distance(npc.Center, center);
            if (distance >= bestDistance) continue;
            best = i;
            bestDistance = distance;
        }
        return best;
    }

    static void Draw(On_BigProgressBarSystem.orig_Draw orig, BigProgressBarSystem self, SpriteBatch spriteBatch)
    {
        ModBossBar? own = bar;
        if (own == null || (uint)index >= 200u)
        {
            orig(self, spriteBatch);
            return;
        }
        try { DrawModBar(own, Main.npc[index], spriteBatch); }
        catch (Exception e) { ItemHooks.Report($"{own.FullName} draw", e); }
    }

    static void DrawModBar(ModBossBar own, NPC npc, SpriteBatch spriteBatch)
    {
        GUIBossBar gui = GUIInstance.Active.IsNull ? default : GUIInstance.Active.GUIBossBar;
        if (gui.IsNull) return;
        Rectangle? frame = null;
        Texture2D icon = default;
        try { icon = own.GetIconTexture(ref frame)?.Value ?? default; }
        catch (Exception e) { ItemHooks.Report($"{own.FullName}.GetIconTexture", e); }
        if (icon.IsNull) icon = TextureAssets.NpcHead[0].Value;
        BossBarDrawParams drawParams = Params(icon, frame ?? new Rectangle(0, 0, icon.Width, icon.Height), own.Life, own.LifeMax, own.Shield, own.ShieldMax);
        BossBarDrawParams before = drawParams;
        if (BossBarLoader.PreDraw(spriteBatch, npc, own, ref drawParams))
        {
            Begin(in before, in drawParams);
            try
            {
                bool shielded = drawParams.ShieldMax > 0f;
                gui.Draw(drawParams.Life, drawParams.LifeMax, drawParams.IconTexture, drawParams.IconFrame,
                    shielded ? drawParams.Shield : -1f, shielded ? drawParams.ShieldMax : 1f);
            }
            finally { End(); }
        }
        BossBarLoader.PostDraw(spriteBatch, npc, own, drawParams);
    }

    static void GuiDraw(On_GUIBossBar.orig_Draw orig, GUIBossBar self, float life, float lifeMax, Texture2D bossIconTexture, Rectangle bossIconFrame,
        float shield, float shieldMax)
    {
        BigProgressBarSystem system = drawing || BossBarLoader.Globals.Count == 0 ? default : Main.BigBossProgressBar;
        int at = system.IsNull ? -1 : system._info.npcIndexToAimAt;
        if ((uint)at >= 200u)
        {
            orig(self, life, lifeMax, bossIconTexture, bossIconFrame, shield, shieldMax);
            return;
        }
        NPC npc = Main.npc[at];
        SpriteBatch spriteBatch = Main.spriteBatch;
        BossBarDrawParams drawParams = Params(bossIconTexture, bossIconFrame, life, lifeMax, shield, shieldMax);
        BossBarDrawParams before = drawParams;
        if (BossBarLoader.PreDraw(spriteBatch, npc, null, ref drawParams))
        {
            Begin(in before, in drawParams);
            try { orig(self, drawParams.Life, drawParams.LifeMax, drawParams.IconTexture, drawParams.IconFrame, drawParams.Shield, drawParams.ShieldMax); }
            finally { End(); }
        }
        BossBarLoader.PostDraw(spriteBatch, npc, null, drawParams);
    }

    static BossBarDrawParams Params(Texture2D icon, Rectangle frame, float life, float lifeMax, float shield, float shieldMax) =>
        new(default, Center(), icon, frame, Color.White, life, lifeMax, Color.White, shield, shieldMax);

    static Vector2 Center()
    {
        if (panel.IsNull) return new Vector2(Main.screenWidth * 0.5f, Main.screenHeight - 50f);
        Rectangle region = GUIPanel.Region(panel);
        return new Vector2(region.X + region.Width / 2f, region.Y + region.Height / 2f);
    }

    static void Begin(in BossBarDrawParams before, in BossBarDrawParams after)
    {
        drawing = true;
        bool barColor = !after.BarColor.Equals(before.BarColor), iconColor = !after.IconColor.Equals(before.IconColor), text = after.ShowText != before.ShowText;
        looking = barColor || iconColor || text;
        if (looking)
        {
            look = new Look
            {
                BarColor = barColor ? after.BarColor : null,
                IconColor = iconColor ? after.IconColor : null,
                Icon = after.IconTexture.Pointer,
                ShowText = after.ShowText,
            };
            if (!looksHooked)
            {
                looksHooked = true;
                On_GUIPanel.DrawBacking += DrawBacking;
                On_GUITransactionButton.Draw_TransactionButton_Layout_Texture2D_Rectangle_string_bool_reffloat_Nullable_bool_bool_bool_bool += DrawIcon;
                On_GUIString.Draw_String_Layout_StringBuilder += DrawText;
                On_GUIString.Draw_String_Layout_StringBuilder_Color += DrawTextColored;
            }
        }
        if (panel.IsNull) On_GUIPanel.Region += Region;
    }

    static void End()
    {
        drawing = looking = false;
        if (!panel.IsNull) On_GUIPanel.Region -= Region;
    }

    static Rectangle Region(On_GUIPanel.orig_Region orig, Panel_Layout layout)
    {
        if (drawing && panel.IsNull) panel = layout;
        return orig(layout);
    }

    static void DrawBacking(On_GUIPanel.orig_DrawBacking orig, Vector2 position, Vector2 size, Texture2D texture, Color color, GameNullable<Rectangle> sourceFrame)
    {
        if (looking && look.BarColor is Color tint) color = Tint(color, tint);
        orig(position, size, texture, color, sourceFrame);
    }

    static GUITransactionButton.InputState DrawIcon(On_GUITransactionButton.orig_Draw_TransactionButton_Layout_Texture2D_Rectangle_string_bool_reffloat_Nullable_bool_bool_bool_bool orig,
        TransactionButton_Layout layout, Texture2D itemTexture, Rectangle srcRect, string? label, bool disabled, ref float scale, GameNullable<Color> overloadedItemColour,
        bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)
    {
        if (looking && look.IconColor is Color tint && itemTexture.Pointer == look.Icon) overloadedItemColour = new GameNullable<Color>(tint);
        return orig(layout, itemTexture, srcRect, label, disabled, ref scale, overloadedItemColour, forcedPressed, hasControllerFocus, forceOver, disablePressedState);
    }

    static void DrawText(On_GUIString.orig_Draw_String_Layout_StringBuilder orig, String_Layout layout, GameObject value)
    {
        if (looking && !look.ShowText) return;
        orig(layout, value);
    }

    static void DrawTextColored(On_GUIString.orig_Draw_String_Layout_StringBuilder_Color orig, String_Layout layout, GameObject value, Color overloadedColour)
    {
        if (looking && !look.ShowText) return;
        orig(layout, value, overloadedColour);
    }

    static Color Tint(Color color, Color by) =>
        new(color.R * by.R / 255, color.G * by.G / 255, color.B * by.B / 255, color.A * by.A / 255);
}
