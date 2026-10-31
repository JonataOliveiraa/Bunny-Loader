using System;
using System.Linq;

namespace Terraria.ModLoader;

public static partial class TileLoader
{
    internal static class Hooks
    {
        internal static GlobalTile[] AdjTiles = Array.Empty<GlobalTile>();

        internal static GlobalTile[] AnimateTile = Array.Empty<GlobalTile>();

        internal static GlobalTile[] CanDrop = Array.Empty<GlobalTile>();

        internal static GlobalTile[] CanExplode = Array.Empty<GlobalTile>();

        internal static GlobalTile[] CanKillTile = Array.Empty<GlobalTile>();

        internal static GlobalTile[] CanPlace = Array.Empty<GlobalTile>();

        internal static GlobalTile[] CanReplace = Array.Empty<GlobalTile>();

        internal static GlobalTile[] CreateDust = Array.Empty<GlobalTile>();

        internal static GlobalTile[] DrawEffects = Array.Empty<GlobalTile>();

        internal static GlobalTile[] Drop = Array.Empty<GlobalTile>();

        internal static GlobalTile[] HitWire = Array.Empty<GlobalTile>();

        internal static GlobalTile[] IsTileDangerous = Array.Empty<GlobalTile>();

        internal static GlobalTile[] IsTileSpelunkable = Array.Empty<GlobalTile>();

        internal static GlobalTile[] KillSound = Array.Empty<GlobalTile>();

        internal static GlobalTile[] KillTile = Array.Empty<GlobalTile>();

        internal static GlobalTile[] ModifyLight = Array.Empty<GlobalTile>();

        internal static GlobalTile[] MouseOver = Array.Empty<GlobalTile>();

        internal static GlobalTile[] MouseOverFar = Array.Empty<GlobalTile>();

        internal static GlobalTile[] NearbyEffects = Array.Empty<GlobalTile>();

        internal static GlobalTile[] NumDust = Array.Empty<GlobalTile>();

        internal static GlobalTile[] PlaceInWorld = Array.Empty<GlobalTile>();

        internal static GlobalTile[] PostDraw = Array.Empty<GlobalTile>();

        internal static GlobalTile[] PreDraw = Array.Empty<GlobalTile>();

        internal static GlobalTile[] PreHitWire = Array.Empty<GlobalTile>();

        internal static GlobalTile[] RandomUpdate = Array.Empty<GlobalTile>();

        internal static GlobalTile[] RightClick = Array.Empty<GlobalTile>();

        internal static GlobalTile[] SetSpriteEffects = Array.Empty<GlobalTile>();

        internal static GlobalTile[] Slope = Array.Empty<GlobalTile>();

        internal static GlobalTile[] SpecialDraw = Array.Empty<GlobalTile>();

        internal static GlobalTile[] TileFrame = Array.Empty<GlobalTile>();
    }

    static void CacheGlobalHooks()
    {
        Hooks.AdjTiles = globals.Where(g => Overrides(g, nameof(GlobalTile.AdjTiles))).ToArray();
        Hooks.AnimateTile = globals.Where(g => Overrides(g, nameof(GlobalTile.AnimateTile))).ToArray();
        Hooks.CanDrop = globals.Where(g => Overrides(g, nameof(GlobalTile.CanDrop))).ToArray();
        Hooks.CanExplode = globals.Where(g => Overrides(g, nameof(GlobalTile.CanExplode))).ToArray();
        Hooks.CanKillTile = globals.Where(g => Overrides(g, nameof(GlobalTile.CanKillTile))).ToArray();
        Hooks.CanPlace = globals.Where(g => Overrides(g, nameof(GlobalTile.CanPlace))).ToArray();
        Hooks.CanReplace = globals.Where(g => Overrides(g, nameof(GlobalTile.CanReplace))).ToArray();
        Hooks.CreateDust = globals.Where(g => Overrides(g, nameof(GlobalTile.CreateDust))).ToArray();
        Hooks.DrawEffects = globals.Where(g => Overrides(g, nameof(GlobalTile.DrawEffects))).ToArray();
        Hooks.Drop = globals.Where(g => Overrides(g, nameof(GlobalTile.Drop))).ToArray();
        Hooks.HitWire = globals.Where(g => Overrides(g, nameof(GlobalTile.HitWire))).ToArray();
        Hooks.IsTileDangerous = globals.Where(g => Overrides(g, nameof(GlobalTile.IsTileDangerous))).ToArray();
        Hooks.IsTileSpelunkable = globals.Where(g => Overrides(g, nameof(GlobalTile.IsTileSpelunkable))).ToArray();
        Hooks.KillSound = globals.Where(g => Overrides(g, nameof(GlobalTile.KillSound))).ToArray();
        Hooks.KillTile = globals.Where(g => Overrides(g, nameof(GlobalTile.KillTile))).ToArray();
        Hooks.ModifyLight = globals.Where(g => Overrides(g, nameof(GlobalTile.ModifyLight))).ToArray();
        Hooks.MouseOver = globals.Where(g => Overrides(g, nameof(GlobalTile.MouseOver))).ToArray();
        Hooks.MouseOverFar = globals.Where(g => Overrides(g, nameof(GlobalTile.MouseOverFar))).ToArray();
        Hooks.NearbyEffects = globals.Where(g => Overrides(g, nameof(GlobalTile.NearbyEffects))).ToArray();
        Hooks.NumDust = globals.Where(g => Overrides(g, nameof(GlobalTile.NumDust))).ToArray();
        Hooks.PlaceInWorld = globals.Where(g => Overrides(g, nameof(GlobalTile.PlaceInWorld))).ToArray();
        Hooks.PostDraw = globals.Where(g => Overrides(g, nameof(GlobalTile.PostDraw))).ToArray();
        Hooks.PreDraw = globals.Where(g => Overrides(g, nameof(GlobalTile.PreDraw))).ToArray();
        Hooks.PreHitWire = globals.Where(g => Overrides(g, nameof(GlobalTile.PreHitWire))).ToArray();
        Hooks.RandomUpdate = globals.Where(g => Overrides(g, nameof(GlobalTile.RandomUpdate))).ToArray();
        Hooks.RightClick = globals.Where(g => Overrides(g, nameof(GlobalTile.RightClick))).ToArray();
        Hooks.SetSpriteEffects = globals.Where(g => Overrides(g, nameof(GlobalTile.SetSpriteEffects))).ToArray();
        Hooks.Slope = globals.Where(g => Overrides(g, nameof(GlobalTile.Slope))).ToArray();
        Hooks.SpecialDraw = globals.Where(g => Overrides(g, nameof(GlobalTile.SpecialDraw))).ToArray();
        Hooks.TileFrame = globals.Where(g => Overrides(g, nameof(GlobalTile.TileFrame))).ToArray();
    }
}
