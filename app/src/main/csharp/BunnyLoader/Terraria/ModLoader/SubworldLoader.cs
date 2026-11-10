using System.Collections.Generic;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class SubworldLoader
{
    static readonly List<ModSubworld> subworlds = new();

    public static IReadOnlyList<ModSubworld> Subworlds => subworlds;

    public static ModSubworld? Current { get; internal set; }

    public static bool InSubworld => Current != null;

    public static bool Traveling => SubworldTravel.Busy || SubworldTransition.Active || SubworldNet.HostTraveling;

    public static bool IsActive<T>() where T : ModSubworld => Current is T;

    public static bool Enter<T>() where T : ModSubworld => Enter(ModContent.GetInstance<T>());

    public static bool Enter(ModSubworld subworld) => subworld != null && SubworldTravel.Begin(subworld);

    public static bool Exit() => Current != null && SubworldTravel.Begin(null);

    internal static void Register(ModSubworld subworld)
    {
        lock (subworlds) subworlds.Add(subworld);
        SubworldTravel.Install();
    }
}
