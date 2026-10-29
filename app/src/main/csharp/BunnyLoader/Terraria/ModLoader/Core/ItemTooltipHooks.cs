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

    internal static IntPtr PreparedItem;

    internal static List<TooltipLine>? Prepared;

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

    internal static bool Wanted(Item item) =>
        !item.IsNull && item.type > 0 && (globalWanted || ItemHooks.ModItemOf(item) is ModItem m && Array.Exists(HookNames, n => Has(m, n)) ||
                                          prefixLines && PrefixLoader.GetPrefix(item.prefix) != null || damageLines && item.damage > 0 ||
                                          setBonusLines && item.wornArmor);

    internal static bool Has(ModItem m, string name) => ItemHooks.Overrides(m.GetType(), typeof(ModItem), name);

    static bool Overrides(ModItem m) => Has(m, nameof(ModItem.ModifyTooltips));

    [UnmanagedCallersOnly]
    static void GetLinesInfo(IntPtr itemPointer, int* yoyoLogo, int* researchLine, int* materialsLine, float oldKB, int* numLines,
                             IntPtr toolTipLine, IntPtr preFixLine, IntPtr badPreFixLine, int* setBonusLine, uint* setBonusColour,
                             int* sharedLine, IntPtr method)
    {
        Hook? hook = linesInfo;
        if (hook == null) return;
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
            var roles = new Dictionary<int, string>
            {
                [*yoyoLogo] = "logo", [*researchLine] = "research", [*materialsLine] = "materials",
                [*setBonusLine] = "setBonus", [*sharedLine] = "shared",
            };
            List<TooltipLine> lines;
            if (Prepared != null && PreparedItem == itemPointer) lines = Prepared;
            else
            {
                lines = Lines(item, texts, prefix, bad, first > 0, count, roles);
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
            int capacity = Math.Min(texts.Length, Math.Min(prefix.Length, bad.Length));
            int written = 0;
            *yoyoLogo = *researchLine = *materialsLine = *setBonusLine = *sharedLine = -1;
            foreach (TooltipLine line in lines)
            {
                if (line.Role == "name") continue;
                if (written >= capacity) break;
                int i = written++;
                texts[i] = GameString.From(line.Text ?? "");
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
        catch (Exception e) { ItemHooks.Report("ModifyTooltips", e); }
    }

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
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.ModifyTooltips(item, lines); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyTooltips", e); }
        }
    }

    static List<TooltipLine> Lines(Item item, GameArray<GameString> texts, GameArray<bool> prefix, GameArray<bool> bad, bool named, int count,
                                   Dictionary<int, string> roles)
    {
        var names = new Names(item);
        string name0 = named && count > 0 ? texts[0].ToString() ?? "" : item.HoverName ?? "";
        var lines = new List<TooltipLine>(count + 5) { new("Terraria", "ItemName", name0) { Role = named ? "" : "name" } };
        var used = new HashSet<string> { "ItemName" };
        for (int i = named ? 1 : 0; i < count; i++)
        {
            string text = texts[i].ToString() ?? "";
            roles.TryGetValue(i, out string? role);
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
            lines.Add(line);
        }
        return lines;
    }

    sealed class Names
    {
        readonly Item item;
        readonly List<string> tooltip = new();
        int nextTooltip;

        public Names(Item item)
        {
            this.item = item;
            try
            {
                var lines = item.ToolTip;
                if (!lines.IsNull)
                    for (int i = 0; i < lines.Lines; i++) tooltip.Add(lines.GetLine(i) ?? "");
            }
            catch (Exception e) { ItemHooks.Report("ModifyTooltips (the item's tooltip)", e); }
        }

        static string Tip(int i) => At(Lang.tip, i);

        static string At(GameArray<LocalizedText> texts, int i) => texts.IsNull || i < 0 || i >= texts.Length || texts[i].IsNull ? "" : texts[i].Value ?? "";

        static string Key(string key) => Language.GetTextValue(key) ?? "";

        static bool Is(string text, string expected) => expected.Length > 0 && text == expected;

        static bool Ends(string text, string suffix) => suffix.Length > 0 && text.EndsWith(suffix, StringComparison.Ordinal);

        static bool Fits(string text, string template)
        {
            int at = template.IndexOf("{0}", StringComparison.Ordinal);
            if (at < 0) return template.Length > 0 && text == template;
            string head = template[..at], tail = template[(at + 3)..];
            int next = tail.IndexOf('{');
            if (next >= 0) tail = "";
            return text.Length >= head.Length + tail.Length && text.StartsWith(head, StringComparison.Ordinal) && text.EndsWith(tail, StringComparison.Ordinal);
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
            if (Is(text, At(Lang.inter, 65))) return "Quest";
            if (Is(text, At(Lang.misc, 104))) return "EtherianManaWarning";
            if (Is(text, At(Lang.misc, 40))) return "WellFedExpert";
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
            foreach (int t in new[] { 2, 3, 4, 53, 55 }) if (Ends(text, Tip(t)) && item.damage > 0) return "Damage";
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
