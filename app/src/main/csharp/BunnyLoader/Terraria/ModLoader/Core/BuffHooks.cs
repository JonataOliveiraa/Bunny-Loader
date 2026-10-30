using System;
using System.Collections.Concurrent;

namespace Terraria.ModLoader.Core;

internal static class BuffHooks
{
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
