const { DustID, ProjectileID, TileID } = Terraria.ID;
const Main = Terraria.Main;
const Wiring = Terraria.Wiring;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

// A direção pelo quadro X (girada no martelo): esquerda, direita, cima, baixo.
const FRAME_X_CYCLE = [2, 3, 4, 5, 1, 0];

// Armadilha de bloco: dois estilos (linhas da textura), cada um com um item.
// Colocada, olha para o lado do jogador; o martelo gira; no fio, atira.
export class ExampleTrap extends ModTile {
    SetStaticDefaults() {
        TileID.Sets.DrawsWalls[this.Type] = true;
        TileID.Sets.DontDrawTileSliced[this.Type] = true;
        TileID.Sets.IgnoresNearbyHalfbricksWhenDrawn[this.Type] = true;
        TileID.Sets.Wiring.IsAMechanism[this.Type] = true;
        Main.tileSolid[this.Type] = true;
        Main.tileBlockLight[this.Type] = true;
        Main.tileFrameImportant[this.Type] = true;

        this.AddMapEntry(Color.new(21, 179, 192), 'MapObject.Trap');
        this.AddMapEntry(Color.new(0, 141, 63), 'MapObject.Trap');
    }

    GetMapOption(i, j) {
        return Math.floor(tileAt(i, j).frameY / 18);
    }

    GetItemDrops(i, j) {
        const style = Math.floor(tileAt(i, j).frameY / 18);
        return [ModContent.ItemType(style === 0 ? 'ExampleTrapIchorBullet' : 'ExampleTrapChlorophyteBullet')];
    }

    CreateDust(i, j, type) {
        const style = Math.floor(tileAt(i, j).frameY / 18);
        type.value = style === 0 ? DustID.Glass : DustID.JungleGrass;
        return true;
    }

    PlaceInWorld(i, j, item) {
        const player = Main.LocalPlayer;
        const tile = tileAt(i, j);
        tile.frameY = player.HeldItem.placeStyle * 18;
        if (player.direction === 1) tile.frameX = tile.frameX + 18;

        if (Main.netMode === NetmodeID.MultiplayerClient) {
            Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int centeredSquareSize, TileChangeType changeType)'](-1, i, j, 1, 0);
        }
    }

    Slope(i, j) {
        const tile = tileAt(i, j);
        tile.frameX = FRAME_X_CYCLE[Math.floor(tile.frameX / 18)] * 18;

        if (Main.netMode === NetmodeID.MultiplayerClient) {
            Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int centeredSquareSize, TileChangeType changeType)'](-1, i, j, 1, 0);
        }
        return false;
    }

    HitWire(i, j) {
        const tile = tileAt(i, j);
        const style = Math.floor(tile.frameY / 18);
        const dirX = tile.frameX === 0 ? -1 : tile.frameX === 18 ? 1 : 0;
        const dirY = tile.frameX < 36 ? 0 : tile.frameX < 72 ? -1 : 1;
        const [delay, speed, projectile, damage] = style === 0
            ? [60, 6, ProjectileID.IchorBullet, 20]
            : [200, 8, ProjectileID.ChlorophyteBullet, 40];

        if (!Wiring.CheckMech(i, j, delay)) return;
        NewProjectile(Wiring.GetProjectileSource(i, j), i * 16 + 8, j * 16 + 9, dirX * speed, dirY * speed,
                      projectile, damage, 2, Main.myPlayer, 0, 0, 0, null);
    }
}
