using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemTooltipHooks
{
    static Hook? linesInfo;

    static bool globalWanted;

    internal static readonly string[] HookNames = { nameof(ModItem.ModifyTooltips), nameof(ModItem.PreDrawTooltip), nameof(ModItem.PostDrawTooltip), nameof(ModItem.PreDrawTooltipLine), nameof(ModItem.PostDrawTooltipLine) };

    static readonly ConcurrentDictionary<Type, bool> ModHooks = new();

    static readonly HashSet<string> Used = new(StringComparer.Ordinal);

    static readonly Names Namer = new();

    internal static IntPtr PreparedItem;

    internal static List<TooltipLine>? Prepared;

    internal static GameArray<GameString> PreparedTexts;

    internal static List<TooltipLine>? Last;

    static bool prefixLines;

    static bool damageLines;

    static bool setBonusLines;

    static readonly uint SetBonusColor = LimeGreen();

    internal static void WantPrefixLines()
    {
        prefixLines = true;
        InstallHooks();
    }

    internal static void WantDamageLines()
    {
        damageLines = true;
        InstallHooks();
    }

    internal static void WantSetBonusLines()
    {
        setBonusLines = true;
        InstallHooks();
    }

    public static void Install(Func<string, bool> has, bool global)
    {
        if (!Array.Exists(HookNames, n => has(n))) return;
        if (global) globalWanted = true;
        InstallHooks();
    }

    static void InstallHooks()
    {
        ItemHooks.Once("TooltipDraw", ItemTooltipDraw.Install);
        ItemHooks.Once("Tooltips", () => linesInfo = ItemHooks.Raw("Terraria", "Main",
            "void MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, ref int setBonusLine, ref Color setBonusColour, ref int sharedLine)",
            (IntPtr)(delegate* unmanaged<IntPtr, int*, int*, int*, float, int*, IntPtr, IntPtr, IntPtr, int*, uint*, int*, IntPtr, void>)&GetLinesInfo));
    }

    internal static bool Wanted(Item item)
    {
        if (item.IsNull) return false;
        int type = item.type;
        if (type <= 0) return false;
        if (ItemLoader.Get(type) is ModItem m && (globalWanted || TooltipHooks(m))) return true;
        if (globalWanted && GlobalTooltipHooks(item)) return true;
        return prefixLines && PrefixLoader.GetPrefix(item.prefix) != null || damageLines && item.damage > 0 || setBonusLines && item.wornArmor;
    }

    static bool TooltipHooks(ModItem m) => ModHooks.GetOrAdd(m.GetType(), static type =>
    {
        foreach (string name in HookNames)
            if (ItemHooks.Overrides(type, typeof(ModItem), name)) return true;
        return false;
    });

    static bool GlobalTooltipHooks(Item item)
    {
        GlobalItem?[]? slots = null;
        foreach (string name in HookNames)
        {
            short[] overriding = ItemLoader.Globals.Overriding(name);
            if (overriding.Length == 0) continue;
            slots ??= ItemLoader.Globals.Slots(item);
            if (slots == null) return false;
            foreach (short index in overriding)
                if (ItemHooks.Applied(slots, index) != null) return true;
        }
        return false;
    }

    internal static bool Has(ModItem m, string name) => ItemHooks.Overrides(m.GetType(), typeof(ModItem), name);

    static bool Overrides(ModItem m) => Has(m, nameof(ModItem.ModifyTooltips));

    [UnmanagedCallersOnly]
    static void GetLinesInfo(IntPtr itemPointer, int* yoyoLogo, int* researchLine, int* materialsLine, float oldKB, int* numLines,
                             IntPtr toolTipLine, IntPtr preFixLine, IntPtr badPreFixLine, int* setBonusLine, uint* setBonusColour,
                             int* sharedLine, IntPtr method)
    {
        Hook? hook = linesInfo;
        if (hook == null) return;
        if (Prepared is List<TooltipLine> prepared && PreparedItem == itemPointer && toolTipLine != IntPtr.Zero && preFixLine != IntPtr.Zero && badPreFixLine != IntPtr.Zero)
        {
            try
            {
                Last = prepared;
                Write(prepared, new GameArray<GameString>(toolTipLine), new GameArray<bool>(preFixLine), new GameArray<bool>(badPreFixLine),
                    yoyoLogo, researchLine, materialsLine, numLines, setBonusLine, setBonusColour, sharedLine, PreparedTexts);
                return;
            }
            catch (Exception e) { ItemHooks.Report("ModifyTooltips", e); }
        }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(itemPointer);
        call.Arg((IntPtr)yoyoLogo);
        call.Arg((IntPtr)researchLine);
        call.Arg((IntPtr)materialsLine);
        call.Arg(oldKB);
        call.Arg((IntPtr)numLines);
        call.Arg(toolTipLine);
        call.Arg(preFixLine);
        call.Arg(badPreFixLine);
        call.Arg((IntPtr)setBonusLine);
        call.Arg((IntPtr)setBonusColour);
        call.Arg((IntPtr)sharedLine);
        call.Arg(method);
        int first = *numLines;
        ItemHooks.Original(ref call, hook, "Main.MouseText_DrawItemTooltip_GetLinesInfo");
        try
        {
            var item = new Item(itemPointer);
            if (!Wanted(item)) return;
            var texts = new GameArray<GameString>(toolTipLine);
            var prefix = new GameArray<bool>(preFixLine);
            var bad = new GameArray<bool>(badPreFixLine);
            if (texts.IsNull || prefix.IsNull || bad.IsNull) return;
            int count = Math.Min(*numLines, Math.Min(texts.Length, Math.Min(prefix.Length, bad.Length)));
            List<TooltipLine> lines;
            if (Prepared != null && PreparedItem == itemPointer) lines = Prepared;
            else
            {
                lines = Lines(item, texts, prefix, bad, first > 0, count, *yoyoLogo, *researchLine, *materialsLine, *setBonusLine, *sharedLine, *setBonusColour);
                if (prefixLines && PrefixLoader.TooltipLines(item) is List<TooltipLine> extra)
                {
                    int at = lines.FindLastIndex(l => l.IsModifier || l.IsModifierBad);
                    lines.InsertRange(at < 0 ? lines.Count : at + 1, extra);
                }
                if (damageLines)
                {
                    try { DamageClassHooks.Tooltip(item, lines); }
                    catch (Exception e) { ItemHooks.Report("DamageClass (tooltip)", e); }
                }
                if (setBonusLines) AddSetBonus(item, lines);
                Modify(item, lines);
                lines.RemoveAll(l => !l.Visible);
            }
            Last = lines;
            Write(lines, texts, prefix, bad, yoyoLogo, researchLine, materialsLine, numLines, setBonusLine, setBonusColour, sharedLine, default);
        }
        catch (Exception e) { ItemHooks.Report("ModifyTooltips", e); }
    }

    static void Write(List<TooltipLine> lines, GameArray<GameString> texts, GameArray<bool> prefix, GameArray<bool> bad, int* yoyoLogo, int* researchLine,
                      int* materialsLine, int* numLines, int* setBonusLine, uint* setBonusColour, int* sharedLine, GameArray<GameString> reuse)
    {
        int capacity = Math.Min(texts.Length, Math.Min(prefix.Length, bad.Length));
        int written = 0;
        *yoyoLogo = *researchLine = *materialsLine = *setBonusLine = *sharedLine = -1;
        foreach (TooltipLine line in lines)
        {
            if (line.Role == "name") continue;
            if (written >= capacity) break;
            int i = written++;
            Put(texts, i, line.Text ?? "", reuse);
            prefix[i] = line.IsModifier || line.IsModifierBad;
            bad[i] = line.IsModifierBad;
            if (line.OneDropLogo) *yoyoLogo = i;
            switch (line.Role)
            {
                case "research": *researchLine = i; break;
                case "materials": *materialsLine = i; break;
                case "setBonus":
                    *setBonusLine = i;
                    if (line.RoleColor is uint color) *setBonusColour = color;
                    break;
                case "shared": *sharedLine = i; break;
            }
        }
        *numLines = written;
    }

    static void Put(GameArray<GameString> texts, int i, string text, GameArray<GameString> reuse)
    {
        IntPtr current = texts[i].Pointer;
        if (current != IntPtr.Zero && Same(current, text)) return;
        if (!reuse.IsNull && reuse.Pointer != texts.Pointer && i < reuse.Length)
        {
            IntPtr kept = reuse[i].Pointer;
            if (kept != IntPtr.Zero && Same(kept, text))
            {
                texts[i] = new GameString(kept);
                return;
            }
        }
        texts[i] = GameString.From(text);
    }

    static bool Same(IntPtr gameString, string text) =>
        *(int*)(gameString + 0x10) == text.Length && new ReadOnlySpan<char>((char*)(gameString + 0x14), text.Length).SequenceEqual(text.AsSpan());

    static uint LimeGreen()
    {
        Color color = new(50, 205, 50, 255);
        return Unsafe.As<Color, uint>(ref color);
    }

    static void AddSetBonus(Item item, List<TooltipLine> lines)
    {
        if (!item.wornArmor || lines.Exists(l => l.Role == "setBonus")) return;
        string bonus = ItemEquipHooks.GetSetBonus(Main.LocalPlayer);
        if (bonus.Length == 0) return;
        string template = Language.GetTextValue("UI.ItemSetBonusGeneral") ?? "";
        string text = template.Contains("{Description}", StringComparison.Ordinal) ? template.Replace("{Description}", bonus) : bonus;
        int at = lines.FindIndex(l => l.Role is "shared" or "research" || l.Name is "LoadoutShareHint" or "JourneyResearch" or "JourneyResearchByTeammate");
        lines.Insert(at < 0 ? lines.Count : at, new TooltipLine("Terraria", "SetBonus", text) { Role = "setBonus", RoleColor = SetBonusColor });
    }

    static void Modify(Item item, List<TooltipLine> lines)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m && Overrides(m))
        {
            try { m.ModifyTooltips(lines); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyTooltips", e); }
        }
        short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.ModifyTooltips));
        if (overriding.Length == 0) return;
        GlobalItem?[]? slots = ItemLoader.Globals.Slots(item);
        foreach (short index in overriding)
        {
            if (ItemHooks.Applied(slots, index) is not GlobalItem g) continue;
            try { g.ModifyTooltips(item, lines); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyTooltips", e); }
        }
    }

    static string? Role(int i, int yoyo, int research, int materials, int setBonus, int shared) =>
        i == shared ? "shared" : i == setBonus ? "setBonus" : i == materials ? "materials" : i == research ? "research" : i == yoyo ? "logo" : null;

    static List<TooltipLine> Lines(Item item, GameArray<GameString> texts, GameArray<bool> prefix, GameArray<bool> bad, bool named, int count,
                                   int yoyo, int research, int materials, int setBonus, int shared, uint setColor)
    {
        Names names = Namer;
        names.Reset(item);
        string name0 = named && count > 0 ? texts[0].ToString() ?? "" : item.HoverName ?? "";
        var lines = new List<TooltipLine>(count + 5) { new("Terraria", "ItemName", name0) { Role = named ? "" : "name" } };
        HashSet<string> used = Used;
        used.Clear();
        used.Add("ItemName");
        for (int i = named ? 1 : 0; i < count; i++)
        {
            string text = texts[i].ToString() ?? "";
            string? role = Role(i, yoyo, research, materials, setBonus, shared);
            bool modifier = prefix[i];
            string name = names.Of(i, text, role, modifier);
            if (!used.Add(name))
            {
                int n = 1;
                while (!used.Add(name + n)) n++;
                name += n;
            }
            var line = new TooltipLine("Terraria", name, text)
            {
                IsModifier = modifier,
                IsModifierBad = modifier && bad[i],
                OneDropLogo = role == "logo",
                Role = role ?? "",
            };
            if (role == "setBonus") line.RoleColor = setColor;
            lines.Add(line);
        }
        return lines;
    }

    sealed class Names
    {
        static readonly int[] DamageTips = { 2, 3, 4, 53, 55 };
        static readonly string?[] Tips = new string?[64], Inter = new string?[66], Misc = new string?[105];
        static readonly Dictionary<string, string> Keys = new(StringComparer.Ordinal);
        static IntPtr culture;

        Item item;
        readonly List<string> tooltip = new();
        int nextTooltip;

        public void Reset(Item item)
        {
            this.item = item;
            tooltip.Clear();
            nextTooltip = 0;
            Refresh();
            try
            {
                var lines = item.ToolTip;
                if (!lines.IsNull)
                    for (int i = 0; i < lines.Lines; i++) tooltip.Add(lines.GetLine(i) ?? "");
            }
            catch (Exception e) { ItemHooks.Report("ModifyTooltips (the item's tooltip)", e); }
        }

        static void Refresh()
        {
            IntPtr now = IntPtr.Zero;
            try { now = Language.ActiveCulture.Pointer; }
            catch (Exception e) { ItemHooks.Report("ModifyTooltips (the active culture)", e); }
            if (now == culture && now != IntPtr.Zero) return;
            culture = now;
            Array.Clear(Tips);
            Array.Clear(Inter);
            Array.Clear(Misc);
            Keys.Clear();
        }

        static string Tip(int i) => Cached(Tips, Lang.tip, i);

        static string Cached(string?[] cache, GameArray<LocalizedText> texts, int i)
        {
            if ((uint)i >= (uint)cache.Length) return At(texts, i);
            string? value = cache[i];
            if (value != null) return value;
            value = At(texts, i);
            if (value.Length > 0 && culture != IntPtr.Zero) cache[i] = value;
            return value;
        }

        static string At(GameArray<LocalizedText> texts, int i) => texts.IsNull || i < 0 || i >= texts.Length || texts[i].IsNull ? "" : texts[i].Value ?? "";

        static string Key(string key)
        {
            if (Keys.TryGetValue(key, out string? value)) return value;
            value = Language.GetTextValue(key) ?? "";
            if (value.Length > 0 && culture != IntPtr.Zero) Keys[key] = value;
            return value;
        }

        static bool Is(string text, string expected) => expected.Length > 0 && text == expected;

        static bool Ends(string text, string suffix) => suffix.Length > 0 && text.EndsWith(suffix, StringComparison.Ordinal);

        static bool Fits(string text, string template)
        {
            int at = template.IndexOf("{0}", StringComparison.Ordinal);
            if (at < 0) return template.Length > 0 && text == template;
            ReadOnlySpan<char> head = template.AsSpan(0, at), tail = template.AsSpan(at + 3);
            if (tail.IndexOf('{') >= 0) tail = ReadOnlySpan<char>.Empty;
            return text.Length >= head.Length + tail.Length && text.AsSpan().StartsWith(head, StringComparison.Ordinal) && text.AsSpan().EndsWith(tail, StringComparison.Ordinal);
        }

        public string Of(int index, string text, string? role, bool modifier)
        {
            switch (role)
            {
                case "logo": return "OneDropLogo";
                case "research": return Fits(text, Key("CommonItemTooltip.ItemUnlockedByTeammate")) ? "JourneyResearchByTeammate" : "JourneyResearch";
                case "materials": return "CraftingMaterials";
                case "setBonus": return "SetBonus";
                case "shared": return "LoadoutShareHint";
            }
            if (modifier) return Prefix(text);
            if (nextTooltip < tooltip.Count && text == tooltip[nextTooltip]) return "Tooltip" + nextTooltip++;
            for (int k = nextTooltip; k < tooltip.Count; k++)
            {
                if (text != tooltip[k]) continue;
                nextTooltip = k + 1;
                return "Tooltip" + k;
            }
            if (Is(text, Tip(56))) return "Favorite";
            if (Is(text, Tip(57))) return "FavoriteDesc";
            if (Is(text, Tip(61))) return "NoSocial";
            for (int t = 6; t <= 13; t++) if (Is(text, Tip(t))) return "Speed";
            for (int t = 14; t <= 22; t++) if (Is(text, Tip(t))) return "Knockback";
            if (Is(text, Tip(23)) || Is(text, Tip(60))) return "Equipable";
            if (Is(text, Tip(24))) return "Vanity";
            if (Is(text, Tip(33))) return "Placeable";
            if (Is(text, Tip(34))) return "Ammo";
            if (Is(text, Tip(35))) return "Consumable";
            if (Is(text, Tip(36))) return "Material";
            if (Is(text, Tip(59))) return "Tooltip0";
            if (Is(text, Cached(Inter, Lang.inter, 65))) return "Quest";
            if (Is(text, Cached(Misc, Lang.misc, 104))) return "EtherianManaWarning";
            if (Is(text, Cached(Misc, Lang.misc, 40))) return "WellFedExpert";
            if (Is(text, Key("GameUI.Expert"))) return "Expert";
            if (Is(text, Key("GameUI.Master"))) return "Master";
            if (Is(text, Key("GameUI.BaitRequired"))) return "NeedsBait";
            if (Is(text, Key("CommonItemTooltip.Wireable"))) return "Wireable";
            if (Is(text, Key("CommonItemTooltip.Container"))) return "Container";
            if (Is(text, Key("UI.ItemLoadoutShared"))) return "Shared";
            if (Is(text, Key("UI.ItemCannotBePlacedInsideItself"))) return "NoTransfer";
            if (Is(text, Key("CommonItemTooltip.WizardHatDuringAnniversary"))) return "WizardHatDuringAnniversary";
            if (Is(text, Key("CommonItemTooltip.BurningBlock"))) return "BurningBlock";
            if (Is(text, Key("CommonItemTooltip.MechSummonDuringEverything"))) return "MechSummonDuringEverything";
            if (Is(text, Key("CommonItemTooltip.MechdusaSummonNotDuringEverything"))) return "MechdusaSummonNotDuringEverything";
            if (Ends(text, Tip(5)) && item.damage > 0) return "CritChance";
            foreach (int t in DamageTips) if (Ends(text, Tip(t)) && item.damage > 0) return "Damage";
            if (Ends(text, Tip(25))) return "Defense";
            if (Ends(text, Tip(26))) return "PickPower";
            if (Ends(text, Tip(27))) return "AxePower";
            if (Ends(text, Tip(28))) return "HammerPower";
            if (Ends(text, Tip(54))) return "TileBoost";
            if (Tip(52).Length > 0 && text.StartsWith(Tip(52), StringComparison.Ordinal)) return "WandConsumes";
            if (Fits(text, Key("GameUI.PrecentFishingPower"))) return "FishingPower";
            if (Fits(text, Key("GameUI.BaitPower"))) return "BaitPower";
            if (Fits(text, Key("CommonItemTooltip.RestoresLife")) || Fits(text, Key("CommonItemTooltip.RestoresLifeRange"))) return "HealLife";
            if (Fits(text, Key("CommonItemTooltip.RestoresMana"))) return "HealMana";
            if (Fits(text, Key("CommonItemTooltip.UsesMana"))) return "UseMana";
            if (Fits(text, Key("CommonItemTooltip.SecondDuration")) || Fits(text, Key("CommonItemTooltip.MinuteDuration"))) return "BuffTime";
            if (Fits(text, Key("UI.ItemLoadoutSharedFrom"))) return "SharedFrom";
            if (Fits(text, Key("CommonItemTooltip.WireTrigger"))) return "WireTrigger";
            return "BestiaryNotes";
        }

        string Prefix(string text)
        {
            bool accessory = item.accessory;
            if (Ends(text, Tip(39))) return accessory && !text.Contains('%') ? "PrefixAccDamage" : "PrefixDamage";
            if (Ends(text, Tip(40))) return "PrefixSpeed";
            if (Ends(text, Tip(41))) return "PrefixCritChance";
            if (Ends(text, Tip(42))) return "PrefixUseMana";
            if (Ends(text, Tip(43))) return "PrefixSize";
            if (Ends(text, Tip(44))) return "PrefixShootSpeed";
            if (Ends(text, Tip(45))) return "PrefixKnockback";
            if (Ends(text, Tip(25))) return "PrefixAccDefense";
            if (Ends(text, Tip(31))) return "PrefixAccMaxMana";
            if (Ends(text, Tip(5))) return "PrefixAccCritChance";
            if (Ends(text, Tip(46))) return "PrefixAccMoveSpeed";
            if (Ends(text, Tip(47))) return "PrefixAccMeleeSpeed";
            if (Fits(text, Key("CommonItemTooltip.PrefixArmorPenetration"))) return "PrefixArmorPenetration";
            if (Fits(text, Key("CommonItemTooltip.PrefixTagDamage"))) return "PrefixTagDamage";
            return "Prefix";
        }
    }
}
