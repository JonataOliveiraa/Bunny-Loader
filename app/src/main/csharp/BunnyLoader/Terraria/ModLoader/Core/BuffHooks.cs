using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

[Flags]
internal enum BuffHookFlags
{
    None = 0,
    UpdatePlayer = 1 << 0,
    UpdateNPC = 1 << 1,
    ReApplyPlayer = 1 << 2,
    ReApplyNPC = 1 << 3,
    ModifyBuffText = 1 << 4,
    CustomBuffTipSize = 1 << 5,
    DrawCustomBuffTip = 1 << 6,
    PreDraw = 1 << 7,
    PostDraw = 1 << 8,
    RightClick = 1 << 9,
}

internal sealed class GlobalBuffHooks
{
    internal static readonly GlobalBuffHooks Empty = new(Array.Empty<GlobalBuff>());

    internal readonly GlobalBuff[] UpdatePlayer, UpdateNPC, ReApplyPlayer, ReApplyNPC, ModifyBuffText, CustomBuffTipSize, DrawCustomBuffTip, PreDraw, PostDraw, RightClick;

    internal GlobalBuffHooks(GlobalBuff[] all)
    {
        UpdatePlayer = With(all, BuffHookFlags.UpdatePlayer);
        UpdateNPC = With(all, BuffHookFlags.UpdateNPC);
        ReApplyPlayer = With(all, BuffHookFlags.ReApplyPlayer);
        ReApplyNPC = With(all, BuffHookFlags.ReApplyNPC);
        ModifyBuffText = With(all, BuffHookFlags.ModifyBuffText);
        CustomBuffTipSize = With(all, BuffHookFlags.CustomBuffTipSize);
        DrawCustomBuffTip = With(all, BuffHookFlags.DrawCustomBuffTip);
        PreDraw = With(all, BuffHookFlags.PreDraw);
        PostDraw = With(all, BuffHookFlags.PostDraw);
        RightClick = With(all, BuffHookFlags.RightClick);
    }

    static GlobalBuff[] With(GlobalBuff[] all, BuffHookFlags flag) => Array.FindAll(all, g => (g.HookFlags & flag) != 0);
}

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

    static bool texting;

    internal static BuffHookFlags FlagsOf(Type type, Type baseType)
    {
        bool global = baseType == typeof(GlobalBuff);
        Type index = typeof(int).MakeByRefType();
        Type[] Args(Type entity, params Type[] rest)
        {
            var args = new Type[rest.Length + (global ? 2 : 1)];
            int at = 0;
            if (global) args[at++] = typeof(int);
            args[at++] = entity;
            rest.CopyTo(args, at);
            return args;
        }
        bool Has(string name) => ItemHooks.Overrides(type, baseType, name);
        var flags = BuffHookFlags.None;
        if (Exact(type, baseType, nameof(ModBuff.Update), Args(typeof(Player), index))) flags |= BuffHookFlags.UpdatePlayer;
        if (Exact(type, baseType, nameof(ModBuff.Update), Args(typeof(NPC), index))) flags |= BuffHookFlags.UpdateNPC;
        if (Exact(type, baseType, nameof(ModBuff.ReApply), Args(typeof(Player), typeof(int), typeof(int)))) flags |= BuffHookFlags.ReApplyPlayer;
        if (Exact(type, baseType, nameof(ModBuff.ReApply), Args(typeof(NPC), typeof(int), typeof(int)))) flags |= BuffHookFlags.ReApplyNPC;
        if (Has(nameof(ModBuff.ModifyBuffText))) flags |= BuffHookFlags.ModifyBuffText;
        if (Has(nameof(GlobalBuff.CustomBuffTipSize))) flags |= BuffHookFlags.CustomBuffTipSize;
        if (Has(nameof(GlobalBuff.DrawCustomBuffTip))) flags |= BuffHookFlags.DrawCustomBuffTip;
        if (Has(nameof(ModBuff.PreDraw))) flags |= BuffHookFlags.PreDraw;
        if (Has(nameof(ModBuff.PostDraw))) flags |= BuffHookFlags.PostDraw;
        if (Has(nameof(ModBuff.RightClick))) flags |= BuffHookFlags.RightClick;
        return flags;
    }

    static bool Exact(Type type, Type baseType, string name, Type[] parameters)
    {
        MethodInfo? method;
        try { method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, parameters, null); }
        catch (AmbiguousMatchException) { return true; }
        return method != null && method.DeclaringType != baseType && method.GetBaseDefinition().DeclaringType == baseType;
    }

    internal static void Install(BuffHookFlags flags)
    {
        if ((flags & BuffHookFlags.UpdatePlayer) != 0) PlayerHooks.WantBuffUpdates();
        if ((flags & BuffHookFlags.UpdateNPC) != 0) ItemHooks.Once("BuffUpdateNPC", () => On_NPC.UpdateNPC_BuffSetFlags += UpdateNPCBuffs);
        if ((flags & BuffHookFlags.ReApplyPlayer) != 0)
            ItemHooks.Once("BuffReApply", () => On_Player.AddBuff_TryUpdatingExistingBuffTime += TryUpdatingExistingBuffTime);
        if ((flags & BuffHookFlags.ReApplyNPC) != 0) ItemHooks.Once("BuffReApplyNPC", () => On_NPC.AddBuff += AddNPCBuff);
        if ((flags & BuffHookFlags.ModifyBuffText) != 0)
        {
            ItemHooks.Once("BuffText", () =>
            {
                On_Lang.GetBuffName += GetBuffName;
                On_Main.GetBuffTooltip += GetBuffTooltip;
            });
            ItemHooks.Once("BuffTip", () => On_Main.MouseText_DrawBuffTooltip += DrawBuffTooltip);
        }
        if ((flags & BuffHookFlags.RightClick) != 0) ItemHooks.Once("BuffRightClick", () => On_GUIBuffs.RemoveBuff_int += RemoveBuff);
        if ((flags & (BuffHookFlags.CustomBuffTipSize | BuffHookFlags.DrawCustomBuffTip)) != 0)
            ItemHooks.Once("BuffTip", () => On_Main.MouseText_DrawBuffTooltip += DrawBuffTooltip);
        if ((flags & (BuffHookFlags.PreDraw | BuffHookFlags.PostDraw)) != 0)
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
        bool merged = tip.Contains('\n') && !ShowsBanners();
        string name = merged ? (cursorText ?? "") + "\n" + tip : cursorText ?? "";
        string shown = merged ? "" : tip;
        SpriteFont font = FontAssets.MouseText.Value;
        var sizes = new System.Collections.Generic.List<Vector2> { font.MeasureString(shown), font.MeasureString(name) };
        int fixedSizes = sizes.Count;
        GlobalBuffHooks hooks = BuffLoader.GlobalHooks;
        foreach (GlobalBuff g in hooks.CustomBuffTipSize)
        {
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
        orig(self, name, shown, ref X, ref Y);
        int originY = Y + (int)font.MeasureString(merged ? name : tip).Y;
        foreach (GlobalBuff g in hooks.DrawCustomBuffTip)
        {
            try { g.DrawCustomBuffTip(tip, Main.spriteBatch, X, originY); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.DrawCustomBuffTip", e); }
        }
    }

    static bool WantsDraw(int type, out ModBuff? own)
    {
        own = Own(type, BuffHookFlags.PreDraw | BuffHookFlags.PostDraw);
        if (own != null) return true;
        GlobalBuffHooks hooks = BuffLoader.GlobalHooks;
        return hooks.PreDraw.Length > 0 || hooks.PostDraw.Length > 0;
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
        GlobalBuffHooks hooks = BuffLoader.GlobalHooks;
        try
        {
            var texture = new Texture2D(Host.Native()->TextureAssetValue(TextureAssets.Buff[type]?.Pointer ?? IntPtr.Zero));
            int width = texture.Width, height = texture.Height;
            var alphas = Main.buffAlpha;
            float alpha = (uint)buffIndex < (uint)alphas.Length ? alphas[buffIndex] : 1f;
            drawParams = new BuffDrawParams(texture, position, new Vector2(position.X, position.Y + height), new Rectangle(0, 0, width, height),
                new Rectangle((int)position.X, (int)position.Y, width, height), new Color(alpha, alpha, alpha, alpha));
            BuffDrawParams before = drawParams;
            foreach (GlobalBuff g in hooks.PreDraw)
            {
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
        foreach (GlobalBuff g in hooks.PostDraw)
        {
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

    static ModBuff? Own(int type, BuffHookFlags flag) => BuffLoader.GetBuff(type) is ModBuff m && (m.HookFlags & flag) != 0 ? m : null;

    internal const int LiveSlots = 64;

    internal static int Live(Player player, Span<int> live)
    {
        try
        {
            var types = player.buffType;
            var times = player.buffTime;
            return types.IsNull || times.IsNull ? 0 : Live(types.AsSpan(), times.AsSpan(), live);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModBuff.Update (player)", e);
            return 0;
        }
    }

    static int Live(NPC npc, Span<int> live)
    {
        try
        {
            var types = npc.buffType;
            var times = npc.buffTime;
            return types.IsNull || times.IsNull ? 0 : Live(types.AsSpan(), times.AsSpan(), live);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModBuff.Update (NPC)", e);
            return 0;
        }
    }

    static int Live(Span<int> types, Span<int> times, Span<int> live)
    {
        int slots = Math.Min(types.Length, times.Length), count = 0;
        for (int b = 0; b < slots && count < live.Length; b++)
            if (types[b] > 0 && times[b] > 0) live[count++] = types[b];
        return count;
    }

    internal static void AfterPlayerBuffs(Player self, ReadOnlySpan<int> live)
    {
        try
        {
            GlobalBuff[] globals = BuffLoader.GlobalHooks.UpdatePlayer;
            var types = self.buffType;
            var times = self.buffTime;
            for (int b = 0; b < types.Length; b++)
            {
                int type = types[b];
                if (type <= 0 || (times[b] <= 0 && !live.Contains(type))) continue;
                int index = b;
                if (Own(type, BuffHookFlags.UpdatePlayer) is ModBuff m)
                {
                    try { m.Update(self, ref index); }
                    catch (Exception e) { ItemHooks.Report($"{m.FullName}.Update", e); }
                }
                foreach (GlobalBuff g in globals)
                {
                    if (index != b) break;
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
        Span<int> live = stackalloc int[LiveSlots];
        live = live[..Live(self, live)];
        orig(self, lowerBuffTime);
        try
        {
            GlobalBuff[] globals = BuffLoader.GlobalHooks.UpdateNPC;
            var types = self.buffType;
            var times = self.buffTime;
            for (int b = 0; b < types.Length; b++)
            {
                int type = types[b];
                if (type <= 0 || (times[b] <= 0 && !live.Contains(type))) continue;
                int index = b;
                if (Own(type, BuffHookFlags.UpdateNPC) is ModBuff m)
                {
                    try { m.Update(self, ref index); }
                    catch (Exception e) { ItemHooks.Report($"{m.FullName}.Update", e); }
                }
                foreach (GlobalBuff g in globals)
                {
                    if (index != b) break;
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
                foreach (GlobalBuff g in BuffLoader.GlobalHooks.ReApplyPlayer)
                {
                    try
                    {
                        if (g.ReApply(type, self, time, index)) return true;
                    }
                    catch (Exception e) { ItemHooks.Report($"{g.FullName}.ReApply", e); }
                }
                if (Own(type, BuffHookFlags.ReApplyPlayer) is ModBuff m)
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
        if (!ReApplyNPC(self, type, time))
        {
            orig(self, type, time, quiet);
            return;
        }
        try
        {
            if (!quiet && Main.NetClientOnly) NetMessage.SendData(Terraria.ID.MessageID.AddNPCBuff, -1, -1, default, self.whoAmI, type, time);
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.ReApply (NPC sync)", e); }
    }

    static bool ReApplyNPC(NPC self, int type, int time)
    {
        try
        {
            int index = self.FindBuffIndex(type);
            if (index < 0) return false;
            foreach (GlobalBuff g in BuffLoader.GlobalHooks.ReApplyNPC)
            {
                try
                {
                    if (g.ReApply(type, self, time, index)) return true;
                }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.ReApply", e); }
            }
            if (Own(type, BuffHookFlags.ReApplyNPC) is ModBuff m)
            {
                try
                {
                    if (m.ReApply(self, time, index)) return true;
                }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.ReApply", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("ModBuff.ReApply (NPC)", e); }
        return false;
    }

    static bool ShowsBanners()
    {
        GUIInstance gui = GUIInstance.Active;
        return !gui.IsNull && !gui.GUIBuffs.IsNull && gui.GUIBuffs.lastDrawBuffText == Terraria.ID.BuffID.MonsterBanner;
    }

    static bool WantsText(int type) =>
        Own(type, BuffHookFlags.ModifyBuffText) != null || BuffLoader.GlobalHooks.ModifyBuffText.Length > 0;

    static void Text(int type, ref string name, ref string tip)
    {
        int rare = 0;
        bool outer = texting;
        texting = true;
        try
        {
            if (Own(type, BuffHookFlags.ModifyBuffText) is ModBuff m)
            {
                try { m.ModifyBuffText(ref name, ref tip, ref rare); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyBuffText", e); }
            }
            foreach (GlobalBuff g in BuffLoader.GlobalHooks.ModifyBuffText)
            {
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
        foreach (GlobalBuff g in BuffLoader.GlobalHooks.RightClick)
        {
            try
            {
                if (!g.RightClick(type, buffIndex)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.RightClick", e); }
        }
        if (Own(type, BuffHookFlags.RightClick) is ModBuff m)
        {
            try { return m.RightClick(buffIndex); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.RightClick", e); }
        }
        return true;
    }
}
