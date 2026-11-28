using Terraria.ModLoader.Core;

namespace Terraria.GameContent.Drawing;

public readonly unsafe partial struct TileDrawing
{
    public void CrawlToTopOfVineAndAddSpecialPoint(int j, int i)
    {
        if (!TileHooks.AddVineRoot(i, j, false))
            CrawlToTopOfVineAndAddSpecialPoint_(j, i);
    }

    public void CrawlToBottomOfReverseVineAndAddSpecialPoint(int j, int i)
    {
        if (!TileHooks.AddVineRoot(i, j, true))
            CrawlToBottomOfReverseVineAndAddSpecialPoint_(j, i);
    }

    public static bool IsVisible(Tile tile)
    {
        bool invisible = tile.invisibleBlock();
        switch (tile.type)
        {
            case 19:
                if (tile.frameY / 18 == 48)
                    invisible = true;
                break;
            case 541:
            case 631:
                invisible = true;
                break;
        }

        return !invisible || Main.instance.TilesRenderer._shouldShowInvisibleBlocks;
    }
}
