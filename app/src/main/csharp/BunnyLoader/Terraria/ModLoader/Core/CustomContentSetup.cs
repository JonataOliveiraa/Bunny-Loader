using System;
using System.Collections.Generic;

namespace Terraria.ModLoader.Core;

internal static class CustomContentSetup
{
    static readonly List<ModType> Content = new();
    static readonly HashSet<ModType> Ready = new();
    internal static void Register(ModType type) { if (!Content.Contains(type)) Content.Add(type); }
    internal static void ContentReady()
    {
        foreach (ModType type in Content.ToArray())
        {
            if (Ready.Contains(type)) continue;
            try { type.SetupContent(); Ready.Add(type); }
            catch (Exception e) { ItemHooks.Report($"{type.FullName}.SetupContent", e); }
        }
    }
}
