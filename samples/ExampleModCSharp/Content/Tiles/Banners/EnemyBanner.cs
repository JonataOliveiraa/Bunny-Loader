using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles.Banners;

// This tile is for enemy banners (https://terraria.wiki.gg/wiki/Banners_(enemy)). Several ModNPC in ExampleMod (ExampleZombieThief and PartyZombie) share a banner with an existing enemy, but the enemies represented in this tile have their own.
// This class inherits from ModBannerTile. By inheriting from ModBannerTile, most of the logic needed to implement an enemy banner tile is automatically handled.
// When placed, this tile will provide bonus damage to specific enemies.
// To support a new NPC, do the following:
//   1. Add an entry to the bottom of the EnemyBanner.StyleID enum.
//   2. Add an item texture and corresponding ModItem class to the Content/Items/Placeable/Banners folder
//   3. Update Content/Tiles/Banners/EnemyBanner.png with the tile sprite
//   4. Assign the banner in each ModNPC that shares the banner. Do this by setting ModNPC.Banner and ModNPC.BannerItem in ModNPC.SetDefaults. In addition, call the RegisterStyle method as well in ModNPC.SetDefaults for the representative ModNPC. See ExampleCustomAISlimeNPC.SetDefaults and ExampleWormHead/Body/Tail.SetDefaults for examples.
public class EnemyBanner : ModBannerTile
{
    // This enum keeps our code clean and readable.
    // Each enum entry has a numerical value (0, 1, ...) which corresponds to the tile style of the placed banner.
    public enum StyleID
    {
        ExampleWormHead,
        ExampleCustomAISlimeNPC
    }
}
