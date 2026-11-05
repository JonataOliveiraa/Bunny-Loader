using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BunnyLoader.Interop;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly unsafe partial struct Lang
{
    static readonly Dictionary<string, Func<object>> ModSubstitutions = new(StringComparer.Ordinal);
    static readonly Regex ModTokens = new(@"{(\?!?)?([^{}]+/[^{}]+)}", RegexOptions.CultureInvariant);
    public static LanguageSearchFilter CreateDialogFilter(string startsWith, bool checkConditions) =>
        checkConditions ? CreateDialogFilter(startsWith, CreateDialogSubstitutionObject(default)) : CreateDialogFilter(startsWith);

    public static void RegisterModdedGlobalSubstitution(Mod mod, string key, Func<object> getValue)
    {
        ArgumentNullException.ThrowIfNull(getValue);
        ModSubstitutions[mod.Name + "/" + key] = getValue;
        InstallSubstitutions();
    }

    internal static void InstallSubstitutions() => ItemHooks.Once("ModGlobalSubstitutions", () =>
    {
        On_LocalizedText.CanFormatWith += CanFormat;
        On_LocalizedText.FormatWith_object += Format;
    });

    static bool TryGetSubstitution(string key, out object? value)
    {
        if (ModSubstitutions.TryGetValue(key, out var get))
        {
            value = get();
            return true;
        }
        if (key.StartsWith("NPCFirstName_", StringComparison.Ordinal) && ModContent.TryFind<ModNPC>(key["NPCFirstName_".Length..], out ModNPC? npc))
        {
            value = NPC.GetFirstNPCNameOrNull(npc.Type);
            return true;
        }
        if (key.StartsWith("InputTrigger_", StringComparison.Ordinal))
        {
            value = key[(key.IndexOf('/') + 1)..];
            return true;
        }
        value = null;
        return false;
    }

    internal static string FormatChat(NPC npc, string chat)
    {
        if (chat.IndexOf('{') < 0) return chat;
        InstallSubstitutions();
        return LocalizedText.Literal(chat).FormatWith(CreateDialogSubstitutionObject(npc)) ?? chat;
    }
    static LocalizedText Substitute(LocalizedText text, out bool allowed)
    {
        bool accepted = true;
        string value = ModTokens.Replace(text.Value ?? "", match =>
        {
            if (!TryGetSubstitution(match.Groups[2].Value, out object? result)) return match.Value;
            string condition = match.Groups[1].Value;
            if (condition.Length == 0)
            {
                if (result == null) accepted = false;
                return Convert.ToString(result, System.Globalization.CultureInfo.CurrentCulture) ?? "";
            }
            bool truth = result is bool b ? b : result != null && !string.IsNullOrEmpty(result.ToString());
            accepted &= condition == "?!" ? !truth : truth;
            return "";
        });
        allowed = accepted;
        return value == text.Value ? text : LocalizedText.Literal(value);
    }
    static bool CanFormat(On_LocalizedText.orig_CanFormatWith orig, LocalizedText text, GameObject substitutions)
    {
        LocalizedText replaced = Substitute(text, out bool allowed);
        return allowed && orig(replaced, substitutions);
    }
    static string? Format(On_LocalizedText.orig_FormatWith_object orig, LocalizedText text, GameObject substitutions) => orig(Substitute(text, out _), substitutions);
}
