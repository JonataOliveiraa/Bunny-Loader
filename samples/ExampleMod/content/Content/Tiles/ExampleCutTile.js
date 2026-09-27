const { ProjectileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { AnchorType } = Terraria.Enums;
const Main = Terraria.Main;
const WorldGen = Terraria.WorldGen;

const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

// Pedra pendurada 3x3 (Main.tileCut: arma e projétil cortam). Cortada, cai
// uma pedra rolante no lugar.
export class ExampleCutTile extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileCut[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style3x3);
        tile.AnchorTop = AnchorData.new(AnchorType.SolidTile | AnchorType.SolidSide, 1, 1);
        tile.AnchorBottom = AnchorData.Empty;
        tile.Origin = Point16.new(1, 0);
        tile.DrawYOffset = -2;
        TileObjectData.addTile(this.Type);
    }

    CreateDust(i, j, type) {
        return false;
    }

    KillMultiTile(i, j, frameX, frameY) {
        if (WorldGen.isGeneratingOrLoadingWorld || Main.netMode === NetmodeID.MultiplayerClient) return;

        NewProjectile(WorldGen.GetItemSource_FromTileBreak(i, j), (i + 1.5) * 16, (j + 1.5) * 16, 0, 0,
                      ProjectileID.Boulder, 70, 10, Main.myPlayer, 0, 0, 0, null);
    }
}
