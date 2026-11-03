using System.Collections.Generic;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class LauncherAmmoHooks
{
    internal static void Install() =>
        ItemHooks.Once("LauncherAmmo", () => On_Player.PickAmmo_TryFindingSpecificMatches += TryFindingSpecificMatches);

    static bool TryFindingSpecificMatches(On_Player.orig_PickAmmo_TryFindingSpecificMatches orig, Player self, int launcher, int ammo, out int pickedProjectileId)
    {
        if (Find(orig, self, launcher, ammo, out pickedProjectileId)) return true;
        int[] fallback = AmmoID.Sets.SpecificLauncherAmmoProjectileFallback;
        int other = (uint)launcher < (uint)fallback.Length ? fallback[launcher] : -1;
        return other != -1 && Find(orig, self, other, ammo, out pickedProjectileId);
    }

    static bool Find(On_Player.orig_PickAmmo_TryFindingSpecificMatches orig, Player self, int launcher, int ammo, out int pickedProjectileId)
    {
        if (AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.TryOwn(launcher, out Dictionary<int, int> matches))
            return matches.TryGetValue(ammo, out pickedProjectileId);
        return orig(self, launcher, ammo, out pickedProjectileId);
    }
}
