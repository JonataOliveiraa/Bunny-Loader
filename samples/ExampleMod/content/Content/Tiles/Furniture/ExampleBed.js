const { DustID, ItemID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;
const Player = Terraria.Player;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const text = (key) => Terraria.Localization.Language['string GetTextValue(string key)'](key);
const say = (message) => Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](message, 255, 240, 20, false);
const SLEEP_DISTANCE = 96;   // o PlayerSleepingHelper.BedSleepingMaxDistance do jogo

// Cama 4x2: a metade da cabeceira deita, a dos pés marca o ponto de nascimento.
export class ExampleBed extends ModTile {
    static NextStyleHeight = 38;

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.HasOutlines[this.Type] = true;
        TileID.Sets.CanBeSleptIn[this.Type] = true;
        TileID.Sets.InteractableByNPCs[this.Type] = true;
        TileID.Sets.IsValidSpawnPoint[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.RoomNeeds.CountsAsChair[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.Beds];

        TileObjectData.newTile.CopyFrom(TileObjectData.Style4x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinatePaddingFix = Point16.new(0, -2);
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(191, 142, 111), 'ItemName.Bed');
    }

    // A cama é mais baixa que a do jogo: deita um pouco mais embaixo.
    ModifySleepingTargetInfo(i, j, info) {
        const offset = info.VisualOffset;
        offset.Y += 4;
        info.VisualOffset = offset;
    }

    NumDust(i, j, fail, num) {
        num.value = 1;
    }

    RightClick(i, j) {
        const player = Main.LocalPlayer;
        const tile = tileAt(i, j);
        const spawnX = i - Math.floor(tile.frameX / 18) + (tile.frameX >= 72 ? 5 : 2);
        const spawnY = tile.frameY % ExampleBed.NextStyleHeight !== 0 ? j + 1 : j + 2;

        if (!Player.IsHoveringOverABottomSideOfABed(i, j)) {
            if (player.IsWithinSnappngRangeToTile(i, j, SLEEP_DISTANCE)) {
                player.GamepadEnableGrappleCooldown();
                player.sleeping.StartSleeping(player, i, j);
            }
            return true;
        }

        player.FindSpawn();
        if (player.SpawnX === spawnX && player.SpawnY === spawnY) {
            player.RemoveSpawn();
            say(text('Game.SpawnPointRemoved'));
        } else if (Player.CheckSpawn(spawnX, spawnY)) {
            player.ChangeSpawn(spawnX, spawnY);
            say(text('Game.SpawnPointSet'));
        }
        return true;
    }

    MouseOver(i, j) {
        const player = Main.LocalPlayer;
        if (!Player.IsHoveringOverABottomSideOfABed(i, j)) {
            if (!player.IsWithinSnappngRangeToTile(i, j, SLEEP_DISTANCE)) return;

            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ItemID.SleepingIcon;
            return;
        }

        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('ExampleBed');
    }
}
