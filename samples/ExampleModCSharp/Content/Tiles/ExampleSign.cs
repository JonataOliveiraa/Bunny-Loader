using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public class ExampleSign : ModTile
{
    public static LocalizedText DefaultSignText { get; private set; }

    public override void SetStaticDefaults()
    {
        Main.tileSign[Type] = true;
        Main.tileFrameImportant[Type] = true;
        Main.tileLavaDeath[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileID.Sets.FramesOnKillWall[Type] = true;
        TileID.Sets.AvoidedByNPCs[Type] = true;
        TileID.Sets.TileInteractRead[Type] = true;
        TileID.Sets.InteractableByNPCs[Type] = true;

        VanillaFallbackOnModDeletion = TileID.Signs;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleMultiplier = 5;
        TileObjectData.newTile.AnchorBottom = AnchorData.Empty;

        AnchorData SolidOrSolidSideAnchor2TilesLong = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 2, 0);

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Origin = Point16.Zero;
        TileObjectData.newAlternate.AnchorTop = SolidOrSolidSideAnchor2TilesLong;
        TileObjectData.addAlternate(1);

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Origin = Point16.Zero;
        TileObjectData.newAlternate.AnchorLeft = SolidOrSolidSideAnchor2TilesLong;
        TileObjectData.addAlternate(2);

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Origin = new Point16(1, 0);
        TileObjectData.newAlternate.AnchorRight = SolidOrSolidSideAnchor2TilesLong;
        TileObjectData.addAlternate(3);

        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Origin = Point16.Zero;
        TileObjectData.newAlternate.AnchorWall = true;
        TileObjectData.addAlternate(4);

        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table | AnchorType.SolidSide, 2, 0);
        TileObjectData.addTile(Type);

        LocalizedText name = CreateMapEntryName();
        AddMapEntry(new Color(200, 200, 200), name);

        DefaultSignText = this.GetLocalization("DefaultSignText");
    }

    public override void PlaceInWorld(int i, int j, Item item)
    {
        int signId = Sign.ReadSign(i, j, true);
        if (signId != -1)
        {
            Sign.TextSign(signId, DefaultSignText.Value);
        }
    }

    public override bool RightClick(int i, int j)
    {
        int signId = Sign.ReadSign(i, j);
        if (signId != -1)
        {
            string signText = Main.sign[signId].text;
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(signText), Color.White);
        }
        return true;
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    {
        Sign.KillSign(i, j);
    }

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings)
    {
        return true;
    }
}
