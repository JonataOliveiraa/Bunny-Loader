const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { TileObjectDirection } = Terraria.Enums;
const Main = Terraria.Main;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const SIT_DISTANCE = 40;   // o PlayerSittingHelper.ChairSittingMaxDistance do jogo

// Cadeira: senta ao tocar. NextStyleHeight é a soma das CoordinateHeights com
// o CoordinatePaddingFix: a altura de cada estilo na textura.
export class ExampleChair extends ModTile {
    static NextStyleHeight = 40;

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.CanBeSatOnForNPCs[this.Type] = true;
        TileID.Sets.CanBeSatOnForPlayers[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.RoomNeeds.CountsAsChair[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.Chairs];
        this.AddMapEntry(Color.new(200, 200, 200), 'MapObject.Chair');

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinatePaddingFix = Point16.new(0, 2);
        TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
        TileObjectData.newTile.StyleWrapLimit = 2;
        TileObjectData.newTile.StyleMultiplier = 2;
        TileObjectData.newTile.StyleHorizontal = true;

        // Virada para a direita: a segunda coluna da textura.
        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.addAlternate(1);
        TileObjectData.addTile(this.Type);
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }

    // Chamado para jogador e para NPC: use info.RestingEntity, nunca Main.LocalPlayer.
    ModifySittingTargetInfo(i, j, info) {
        const tile = tileAt(i, j);
        info.TargetDirection = tile.frameX !== 0 ? 1 : -1;

        // A âncora é a célula de baixo da cadeira.
        info.AnchorTilePosition.X = i;
        info.AnchorTilePosition.Y = tile.frameY % ExampleChair.NextStyleHeight === 0 ? j + 1 : j;
    }

    RightClick(i, j) {
        const player = Main.LocalPlayer;
        if (player.IsWithinSnappngRangeToTile(i, j, SIT_DISTANCE)) {
            player.GamepadEnableGrappleCooldown();
            player.sitting.SitDown(player, i, j);
        }
        return true;
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        if (!player.IsWithinSnappngRangeToTile(i, j, SIT_DISTANCE)) return;

        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExampleChair');
        if (tileAt(i, j).frameX < 18) player.cursorItemIconReversed = true;
    }
}
