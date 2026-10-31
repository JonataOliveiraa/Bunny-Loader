using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class HairLoader
{
    internal static readonly List<ModHair> hairs = new();

    public static int Count => HairID.Count + hairs.Count;

    internal static int Register(ModHair hair)
    {
        lock (hairs)
        {
            hairs.Add(hair);
            HairHooks.Install();
            return Count - 1;
        }
    }

    public static ModHair GetHair(int type) => type >= HairID.Count && type < Count ? hairs[type - HairID.Count] : null!;

    public static void UpdateUnlocks(HairstyleUnlocksHelper unlocksHelper, bool inCharacterCreation)
    {
        var list = new BunnyLoader.Interop.GameList(unlocksHelper.AvailableHairstyles);
        foreach (ModHair hair in hairs)
        {
            if (inCharacterCreation ? hair.AvailableDuringCharacterCreation : hair.GetUnlockConditions().All(x => x.IsMet()))
                list.Add(hair.Type);
        }
    }

    internal static void ResizeArrays()
    {
        TextureAssets.ResizePlayerHair(Count);
        HairID.Sets.Resize(Count);
    }
}
