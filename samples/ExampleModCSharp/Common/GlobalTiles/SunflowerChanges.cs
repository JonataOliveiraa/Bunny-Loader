using ExampleMod.Content.Tiles;
using System.Linq;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Common.GlobalTiles;

public class SunflowerChanges : GlobalTile
{
    public override void SetStaticDefaults()
    {
        TileObjectData tileObjectData = TileObjectData.GetTileData(TileID.Sunflower, 0);
        tileObjectData.AnchorValidTiles = ((int[])tileObjectData.AnchorValidTiles).Append(ModContent.TileType<ExampleBlock>()).ToArray();
    }

    public override void Unload()
    {
        TileObjectData tileObjectData = TileObjectData.GetTileData(TileID.Sunflower, 0);
        tileObjectData.AnchorValidTiles = ((int[])tileObjectData.AnchorValidTiles).Except([ModContent.TileType<ExampleBlock>()]).ToArray();
    }
}
