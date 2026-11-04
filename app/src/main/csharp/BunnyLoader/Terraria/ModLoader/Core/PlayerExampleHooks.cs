using System;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class PlayerExampleHooks
{
    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModPlayer.RefreshInfoAccessoriesFromTeamPlayers))) ItemHooks.Once("ModTeamInfo", () => On_Player.RefreshInfoAccsFromTeamPlayers += TeamInfo);
        if (has(nameof(ModPlayer.EmitEnchantmentVisualsAt))) ItemHooks.Once("PlayerEnchantmentVisuals", () => On_Projectile.EmitEnchantmentVisualsAt += Enchant);
    }

    static void TeamInfo(On_Player.orig_RefreshInfoAccsFromTeamPlayers orig, Player player)
    {
        orig(player);
        if ((Main.netMode & NetmodeID.MultiplayerClient) == 0 || player.whoAmI != Main.myPlayer || player.team == 0) return;
        foreach (Player other in Main.ActivePlayers)
        {
            if (other == player || other.dead || other.team != player.team || other.Distance(player.Center) >= 800f) continue;
            PlayerLoader.Each(player, nameof(ModPlayer.RefreshInfoAccessoriesFromTeamPlayers), other, static (m, p) => m.RefreshInfoAccessoriesFromTeamPlayers(p));
        }
    }

    static void Enchant(On_Projectile.orig_EmitEnchantmentVisualsAt orig, Projectile projectile, Vector2 position, int width, int height)
    {
        orig(projectile, position, width, height);
        if (!projectile.TryGetOwner(out Player player) || projectile.noEnchantmentVisuals) return;
        PlayerLoader.Each(player, nameof(ModPlayer.EmitEnchantmentVisualsAt), (projectile, position, width, height), static (m, s) => m.EmitEnchantmentVisualsAt(s.projectile, s.position, s.width, s.height));
    }
}
