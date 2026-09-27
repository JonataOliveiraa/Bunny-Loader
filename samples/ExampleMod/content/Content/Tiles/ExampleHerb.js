const { DustID, ItemID, SoundID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const FrameWidth = 18;
const FLIP = 1;   // SpriteEffects.FlipHorizontally

// Plantada, crescendo, pronta: a coluna da textura (frameX / 18).
export const PlantStage = Object.freeze({ Planted: 0, Growing: 1, Grown: 2 });

// Erva de exemplo: a semente planta na grama, cresce no RandomUpdate e, pronta,
// dá o Exemplo de Item e sementes.
export class ExampleHerb extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileObsidianKill[this.Type] = true;
        Main.tileCut[this.Type] = true;
        Main.tileNoFail[this.Type] = true;
        TileID.Sets.ReplaceTileBreakUp[this.Type] = true;
        TileID.Sets.IgnoredInHouseScore[this.Type] = true;
        TileID.Sets.IgnoredByGrowingSaplings[this.Type] = true;

        this.AddMapEntry(Color.new(128, 128, 128), this.CreateMapEntryName());

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.StyleAlch);
        tile.AnchorValidTiles = [TileID.Grass, TileID.HallowedGrass, ModContent.TileType('ExampleTile')];
        tile.AnchorAlternateTiles = [TileID.ClayPot, TileID.PlanterBox];
        TileObjectData.addTile(this.Type);

        this.HitSound = SoundID.Grass;
        this.DustType = DustID.Ambient_DarkBrown;
    }

    static StageAt(i, j) {
        return Math.floor(tileAt(i, j).frameX / FrameWidth);
    }

    SetSpriteEffects(i, j, spriteEffects) {
        if (i % 2 === 0) spriteEffects.value = FLIP;
    }

    // As ervas do jogo descem 2 px.
    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {
        offsetY.value = -2;
    }

    CanDrop(i, j) {
        return ExampleHerb.StageAt(i, j) !== PlantStage.Planted;
    }

    GetItemDrops(i, j) {
        const stage = ExampleHerb.StageAt(i, j);
        const player = Main.player[Terraria.Player.FindClosest(Vector2.new(i * 16, j * 16), 16, 16)];
        let herbStack = 1;
        let seedStack = 1;

        if (player.active && (player.HeldItem.type === ItemID.StaffofRegrowth || player.HeldItem.type === ItemID.AcornAxe)) {
            herbStack = Rand.Next(1, 3);
            seedStack = Rand.Next(1, 6);
        } else if (stage === PlantStage.Grown) {
            seedStack = Rand.Next(1, 4);
        }
        return [
            { type: ModContent.ItemType('ExampleItem'), stack: herbStack },
            { type: ModContent.ItemType('ExampleHerbSeeds'), stack: seedStack },
        ];
    }

    // Cresce uma fase por atualização aleatória, até ficar pronta.
    RandomUpdate(i, j) {
        if (ExampleHerb.StageAt(i, j) === PlantStage.Grown) return;

        const tile = tileAt(i, j);
        tile.frameX = tile.frameX + FrameWidth;
        if (Main.netMode !== NetmodeID.SinglePlayer) {
            Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int centeredSquareSize, TileChangeType changeType)'](-1, i, j, 1, 0);
        }
    }
}
