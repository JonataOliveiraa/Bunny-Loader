using ExampleMod.Content.Dusts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles.Furniture;

public class ExampleClock : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;
        TileID.Sets.Clock[Type] = true;
        TileID.Sets.HasOutlines[Type] = true;

        DustType = ModContent.DustType<Sparkle>();
        AdjTiles = [TileID.GrandfatherClocks];

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
        TileObjectData.newTile.Height = 5;
        TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(200, 200, 200), Language.GetText("ItemName.GrandfatherClock"));
    }

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings)
    {
        return true;
    }

    public override bool RightClick(int x, int y)
    {
        string morningOrEvening = Language.GetTextValue("GameUI.TimeAtMorning");

        double time = Main.time;
        if (!Main.dayTime)
        {
            time += 54000.0;
        }

        time = (time / 86400.0) * 24.0;

        time = time - 7.5 - 12.0;

        if (time < 0.0)
        {
            time += 24.0;
        }

        if (time >= 12.0)
        {
            morningOrEvening = Language.GetTextValue("GameUI.TimePastMorning");
        }

        int hours = (int)time;

        double timeRemainder = time - hours;

        timeRemainder = (int)(timeRemainder * 60.0);

        string minutes = string.Concat(timeRemainder);
        if (timeRemainder < 10.0)
        {
            minutes = "0" + minutes;
        }

        if (hours > 12)
        {
            hours -= 12;
        }

        if (hours == 0)
        {
            hours = 12;
        }

        Main.NewText(Language.GetTextValue("CLI.Time", $"{hours}:{minutes} {morningOrEvening}"), 255, 240, 20);
        return true;
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        num = fail ? 1 : 3;
    }
}
