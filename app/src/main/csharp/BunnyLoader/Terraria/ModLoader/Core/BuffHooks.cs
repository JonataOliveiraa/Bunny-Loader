using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

internal static unsafe class BuffHooks
{
    [StructLayout(LayoutKind.Sequential)]
    struct IconLook
    {
        public byte Active;
        public IntPtr Texture;
        public IntPtr NewTexture;
        public float Dx, Dy, Tx, Ty;
        public byte HasSource;
        public int SourceX, SourceY, SourceWidth, SourceHeight;
        public byte HasColor;
        public uint Color;
        public int Applied;
    }

    static IconLook* look;

    static readonly ConcurrentDictionary<(Type, string), bool> Overridden = new();

    static bool texting;

    internal static void Install(Type type, Type baseType)
    {
        bool Has(string name) => ItemHooks.Overrides(type, baseType, name);
        if (Has(nameof(ModBuff.Update)))
            ItemHooks.Once("BuffUpdate", () =>
            {
                On_Player.UpdateBuffs += UpdateBuffs;
                On_NPC.UpdateNPC_BuffSetFlags += UpdateNPCBuffs;
            });
        if (Has(nameof(ModBuff.ReApply)))
            ItemHooks.Once("BuffReApply", () =>
            {
                On_Player.AddBuff_TryUpdatingExistingBuffTime += TryUpdatingExistingBuffTime;
                On_NPC.AddBuff += AddNPCBuff;
            });
        if (Has(nameof(ModBuff.ModifyBuffText)))
            ItemHooks.Once("BuffText", () =>
            {
                On_Lang.GetBuffName += GetBuffName;
                On_Main.GetBuffTooltip += GetBuffTooltip;
            });
        if (Has(nameof(ModBuff.RightClick))) ItemHooks.Once("BuffRightClick", () => On_GUIBuffs.RemoveBuff_int += RemoveBuff);
        if (Has(nameof(GlobalBuff.CustomBuffTipSize)) || Has(nameof(GlobalBuff.DrawCustomBuffTip)))
            ItemHooks.Once("BuffTip", () => On_Main.MouseText_DrawBuffTooltip += DrawBuffTooltip);
        if (Has(nameof(ModBuff.PreDraw)) || Has(nameof(ModBuff.PostDraw)))
            ItemHooks.Once("BuffDraw", () =>
            {
                look = (IconLook*)Host.Native()->BuffIconLook();
                if (look == null) Log.Error("buff hooks: the icon drawing hooks are not installed; PreDraw changes are not drawn");
                On_GUIBuffs.ItemDraw += ItemDraw;
            });
    }

    static void DrawBuffTooltip(On_Main.orig_MouseText_DrawBuffTooltip orig, Main self, string? cursorText, string? buffString, ref int X, ref int Y)
    {
        string tip = buffString ?? "";
        SpriteFont font = FontAssets.MouseText.Value;
        var sizes = new System.Collections.Generic.List<Vector2> { font.MeasureString(tip), font.MeasureString(cursorText ?? "") };
        int fixedSizes = sizes.Count;
        foreach (GlobalBuff g in BuffLoader.Globals)
        {
            if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.CustomBuffTipSize))) continue;
            try { g.CustomBuffTipSize(tip, sizes); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CustomBuffTipSize", e); }
        }
        if (sizes.Count > fixedSizes)
        {
            Vector2 largest = Vector2.Zero;
            foreach (Vector2 size in sizes)
            {
                largest.X = Math.Max(largest.X, size.X);
                largest.Y = Math.Max(largest.Y, size.Y);
            }
            if (X + largest.X + 24f > Main.screenWidth) X = (int)(Main.screenWidth - largest.X - 24f);
            if (Y + largest.Y + 4f > Main.screenHeight) Y = (int)(Main.screenHeight - largest.Y - 4f);
        }
        orig(self, cursorText, buffString, ref X, ref Y);
        int originY = Y + (int)font.MeasureString(tip).Y;
        foreach (GlobalBuff g in BuffLoader.Globals)
        {
            if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.DrawCustomBuffTip))) continue;
            try { g.DrawCustomBuffTip(tip, Main.spriteBatch, X, originY); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.DrawCustomBuffTip", e); }
        }
    }

    static bool WantsDraw(int type, out ModBuff? own)
    {
        own = BuffLoader.GetBuff(type) is ModBuff m && (Over(m, typeof(ModBuff), nameof(ModBuff.PreDraw)) || Over(m, typeof(ModBuff), nameof(ModBuff.PostDraw)))
            ? m
            : null;
        if (own != null) return true;
        foreach (GlobalBuff g in BuffLoader.Globals)
            if (Over(g, typeof(GlobalBuff), nameof(GlobalBuff.PreDraw)) || Over(g, typeof(GlobalBuff), nameof(GlobalBuff.PostDraw))) return true;
        return false;
    }

    static uint Packed(Color color) => Unsafe.As<Color, uint>(ref color);

    static void ItemDraw(On_GUIBuffs.orig_ItemDraw orig, GUIBuffs self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        int type = 0, buffIndex = -1;
        ModBuff? own = null;
        try
        {
            if (index < self.numActiveBuffs)
            {
                buffIndex = self.activeBuffs[index];
                var types = Main.LocalPlayer.buffType;
                if ((uint)buffIndex < (uint)types.Length) type = types[buffIndex];
            }
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.PreDraw", e); }
        if (type <= 0 || !WantsDraw(type, out own))
        {
            orig(self, gridLayout, index, position, scale);
            return;
        }
        SpriteBatch spriteBatch = Main.spriteBatch;
        BuffDrawParams drawParams;
        bool draw = true;
        try
        {
            var texture = new Texture2D(Host.Native()->TextureAssetValue(TextureAssets.Buff[type].Pointer));
            int width = texture.Width, height = texture.Height;
            var alphas = Main.buffAlpha;
            float alpha = (uint)buffIndex < (uint)alphas.Length ? alphas[buffIndex] : 1f;
            drawParams = new BuffDrawParams(texture, position, new Vector2(position.X, position.Y + height), new Rectangle(0, 0, width, height),
                new Rectangle((int)position.X, (int)position.Y, width, height), new Color(alpha, alpha, alpha, alpha));
            BuffDrawParams before = drawParams;
            foreach (GlobalBuff g in BuffLoader.Globals)
            {
                if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.PreDraw))) continue;
                try { draw &= g.PreDraw(spriteBatch, type, buffIndex, ref drawParams); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.PreDraw", e); }
            }
            if (draw && own != null)
            {
                try { draw = own.PreDraw(spriteBatch, buffIndex, ref drawParams); }
                catch (Exception e) { ItemHooks.Report($"{own.FullName}.PreDraw", e); }
            }
            Look(before, drawParams, draw);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModBuff.PreDraw", e);
            orig(self, gridLayout, index, position, scale);
            return;
        }
        try { orig(self, gridLayout, index, position, scale); }
        finally
        {
            if (look != null) look->Active = 0;
        }
        if (own != null)
        {
            try { own.PostDraw(spriteBatch, buffIndex, drawParams); }
            catch (Exception e) { ItemHooks.Report($"{own.FullName}.PostDraw", e); }
        }
        foreach (GlobalBuff g in BuffLoader.Globals)
        {
            if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.PostDraw))) continue;
            try { g.PostDraw(spriteBatch, type, buffIndex, drawParams); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PostDraw", e); }
        }
    }

    static void Look(BuffDrawParams before, BuffDrawParams after, bool draw)
    {
        if (look == null) return;
        Rectangle a = before.SourceRectangle, b = after.SourceRectangle;
        bool source = a.X != b.X || a.Y != b.Y || a.Width != b.Width || a.Height != b.Height;
        bool color = Packed(before.DrawColor) != Packed(after.DrawColor) || !draw;
        bool texture = before.Texture.Pointer != after.Texture.Pointer && !after.Texture.IsNull;
        float dx = after.Position.X - before.Position.X, dy = after.Position.Y - before.Position.Y;
        float tx = after.TextPosition.X - before.TextPosition.X, ty = after.TextPosition.Y - before.TextPosition.Y;
        if (!source && !color && !texture && dx == 0 && dy == 0 && tx == 0 && ty == 0) return;
        look->Texture = before.Texture.Pointer;
        look->NewTexture = texture ? after.Texture.Pointer : IntPtr.Zero;
        look->Dx = dx;
        look->Dy = dy;
        look->Tx = tx;
        look->Ty = ty;
        look->HasSource = source ? (byte)1 : (byte)0;
        look->SourceX = b.X;
        look->SourceY = b.Y;
        look->SourceWidth = b.Width;
        look->SourceHeight = b.Height;
        look->HasColor = color ? (byte)1 : (byte)0;
        look->Color = draw ? Packed(after.DrawColor) : 0;
        look->Active = 1;
    }

    static bool Over(object instance, Type baseType, string name) =>
        Overridden.GetOrAdd((instance.GetType(), name), key => ItemHooks.Overrides(key.Item1, baseType, key.Item2));

    static ModBuff? Own(int type, string name) => BuffLoader.GetBuff(type) is ModBuff m && Over(m, typeof(ModBuff), name) ? m : null;

    static void UpdateBuffs(On_Player.orig_UpdateBuffs orig, Player self, int i)
    {
        orig(self, i);
        try
        {
            var types = self.buffType;
            var times = self.buffTime;
            for (int b = 0; b < types.Length; b++)
            {
                int type = types[b];
                if (type <= 0 || times[b] <= 0) continue;
                int index = b;
                if (Own(type, nameof(ModBuff.Update)) is ModBuff m)
                {
                    try { m.Update(self, ref index); }
                    catch (Exception e) { ItemHooks.Report($"{m.FullName}.Update", e); }
                }
                foreach (GlobalBuff g in BuffLoader.Globals)
                {
                    if (index != b) break;
                    if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.Update))) continue;
                    try { g.Update(type, self, ref index); }
                    catch (Exception e) { ItemHooks.Report($"{g.FullName}.Update", e); }
                }
                b = index;
            }
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.Update (player)", e); }
    }

    static void UpdateNPCBuffs(On_NPC.orig_UpdateNPC_BuffSetFlags orig, NPC self, bool lowerBuffTime)
    {
        orig(self, lowerBuffTime);
        try
        {
            var types = self.buffType;
            var times = self.buffTime;
            for (int b = 0; b < types.Length; b++)
            {
                int type = types[b];
                if (type <= 0 || times[b] <= 0) continue;
                int index = b;
                if (Own(type, nameof(ModBuff.Update)) is ModBuff m)
                {
                    try { m.Update(self, ref index); }
                    catch (Exception e) { ItemHooks.Report($"{m.FullName}.Update", e); }
                }
                foreach (GlobalBuff g in BuffLoader.Globals)
                {
                    if (index != b) break;
                    if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.Update))) continue;
                    try { g.Update(type, self, ref index); }
                    catch (Exception e) { ItemHooks.Report($"{g.FullName}.Update", e); }
                }
                b = index;
            }
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.Update (NPC)", e); }
    }

    static bool TryUpdatingExistingBuffTime(On_Player.orig_AddBuff_TryUpdatingExistingBuffTime orig, Player self, int type, int time)
    {
        try
        {
            int index = self.FindBuffIndex(type);
            if (index >= 0)
            {
                foreach (GlobalBuff g in BuffLoader.Globals)
                {
                    if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.ReApply))) continue;
                    try
                    {
                        if (g.ReApply(type, self, time, index)) return true;
                    }
                    catch (Exception e) { ItemHooks.Report($"{g.FullName}.ReApply", e); }
                }
                if (Own(type, nameof(ModBuff.ReApply)) is ModBuff m)
                {
                    try
                    {
                        if (m.ReApply(self, time, index)) return true;
                    }
                    catch (Exception e) { ItemHooks.Report($"{m.FullName}.ReApply", e); }
                }
            }
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.ReApply (player)", e); }
        return orig(self, type, time);
    }

    static void AddNPCBuff(On_NPC.orig_AddBuff orig, NPC self, int type, int time, bool quiet)
    {
        try
        {
            int index = self.FindBuffIndex(type);
            if (index >= 0)
            {
                foreach (GlobalBuff g in BuffLoader.Globals)
                {
                    if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.ReApply))) continue;
                    try
                    {
                        if (g.ReApply(type, self, time, index)) return;
                    }
                    catch (Exception e) { ItemHooks.Report($"{g.FullName}.ReApply", e); }
                }
                if (Own(type, nameof(ModBuff.ReApply)) is ModBuff m)
                {
                    try
                    {
                        if (m.ReApply(self, time, index)) return;
                    }
                    catch (Exception e) { ItemHooks.Report($"{m.FullName}.ReApply", e); }
                }
            }
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.ReApply (NPC)", e); }
        orig(self, type, time, quiet);
    }

    static bool WantsText(int type)
    {
        if (Own(type, nameof(ModBuff.ModifyBuffText)) != null) return true;
        foreach (GlobalBuff g in BuffLoader.Globals)
            if (Over(g, typeof(GlobalBuff), nameof(GlobalBuff.ModifyBuffText))) return true;
        return false;
    }

    static void Text(int type, ref string name, ref string tip)
    {
        int rare = 0;
        bool outer = texting;
        texting = true;
        try
        {
            if (Own(type, nameof(ModBuff.ModifyBuffText)) is ModBuff m)
            {
                try { m.ModifyBuffText(ref name, ref tip, ref rare); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyBuffText", e); }
            }
            foreach (GlobalBuff g in BuffLoader.Globals)
            {
                if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.ModifyBuffText))) continue;
                try { g.ModifyBuffText(type, ref name, ref tip, ref rare); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyBuffText", e); }
            }
        }
        finally { texting = outer; }
    }

    static string? GetBuffName(On_Lang.orig_GetBuffName orig, int id)
    {
        string? name = orig(id);
        if (texting || !WantsText(id)) return name;
        string text = name ?? "";
        string tip;
        bool outer = texting;
        texting = true;
        try { tip = Lang.GetBuffDescription(id) ?? ""; }
        finally { texting = outer; }
        Text(id, ref text, ref tip);
        return text;
    }

    static string? GetBuffTooltip(On_Main.orig_GetBuffTooltip orig, Player player, int buffType)
    {
        string? tip = orig(player, buffType);
        if (texting || !WantsText(buffType)) return tip;
        string text = tip ?? "";
        string name;
        bool outer = texting;
        texting = true;
        try { name = Lang.GetBuffName(buffType) ?? ""; }
        finally { texting = outer; }
        Text(buffType, ref name, ref text);
        return text;
    }

    static void RemoveBuff(On_GUIBuffs.orig_RemoveBuff_int orig, GUIBuffs self, int buff)
    {
        try
        {
            Player player = Main.LocalPlayer;
            var types = player.buffType;
            int type = (uint)buff < (uint)types.Length ? types[buff] : 0;
            if (type > 0 && !RightClick(type, buff)) return;
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.RightClick", e); }
        orig(self, buff);
    }

    static bool RightClick(int type, int buffIndex)
    {
        foreach (GlobalBuff g in BuffLoader.Globals)
        {
            if (!Over(g, typeof(GlobalBuff), nameof(GlobalBuff.RightClick))) continue;
            try
            {
                if (!g.RightClick(type, buffIndex)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.RightClick", e); }
        }
        if (Own(type, nameof(ModBuff.RightClick)) is ModBuff m)
        {
            try { return m.RightClick(buffIndex); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.RightClick", e); }
        }
        return true;
    }
}
