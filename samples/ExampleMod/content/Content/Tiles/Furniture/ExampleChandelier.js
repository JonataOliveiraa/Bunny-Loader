const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { AnchorType } = Terraria.Enums;
const Main = Terraria.Main;
const Wiring = Terraria.Wiring;

const tileAt = (i, j) => Main.tile['Tile get_Item(int x, int y)'](i, j);
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

// Os seis estilos da textura (cópias dos lustres do jogo).
const Style = Object.freeze({ Copper: 0, Silver: 1, Frozen: 2, PalmWood: 3, BorealWood: 4, Flesh: 5 });

// Lustre 3x3 pendurado no teto: o fio liga e desliga (a linha de baixo da
// textura, 54 px abaixo, é o apagado); aceso, ilumina e solta faísca.
// O balanço ao vento do tModLoader (MultiTileVine) não existe aqui: ele fica parado.
export class ExampleChandelier extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        Main.tileLighted[this.Type] = true;
        TileID.Sets.MultiTileSway[this.Type] = true;
        TileID.Sets.Wiring.IsAMechanism[this.Type] = true;
        TileID.Sets.Wiring.IgnoreWhenValidatingTraps[this.Type] = true;
        TileID.Sets.RoomNeeds.CountsAsTorch[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style3x3);
        tile.Origin = Point16.new(1, 0);
        tile.AnchorTop = AnchorData.new(AnchorType.SolidTile | AnchorType.SolidSide, 1, 1);
        tile.AnchorBottom = AnchorData.Empty;
        tile.LavaDeath = true;
        tile.RandomStyleRange = 6;
        tile.StyleHorizontal = true;
        tile.StyleLineSkip = 2;
        tile.DrawYOffset = -2;
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(235, 166, 135), 'MapObject.Chandelier');
        this.RegisterItemDrop(ModContent.ItemType('ExampleChandelier'));
    }

    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {
        offsetY.value += 2;
    }

    HitWire(i, j) {
        const tile = tileAt(i, j);
        const topX = i - Math.floor((tile.frameX % 54) / 18);
        const topY = j - Math.floor((tile.frameY % 54) / 18);
        const shift = tile.frameY >= 54 ? -54 : 54;

        for (let x = topX; x < topX + 3; x++) {
            for (let y = topY; y < topY + 3; y++) {
                const cell = tileAt(x, y);
                cell.frameY = cell.frameY + shift;
                Wiring['void SkipWire(int x, int y)'](x, y);
            }
        }
        if (Main.netMode !== NetmodeID.SinglePlayer) {
            Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](-1, topX, topY, 3, 3, 0);
        }
    }

    ModifyLight(i, j, r, g, b) {
        const tile = tileAt(i, j);
        if (Math.floor(tile.frameY / 54) !== 0) return;

        const [red, green, blue] = {
            [Style.Flesh]: [1, 0.6, 0.6],
            [Style.Frozen]: [0.75, 0.85, 1],
            [Style.PalmWood]: [1, 0.95, 0.65],
            [Style.BorealWood]: [0, 0.9, 1],
        }[TileObjectData.GetTileStyle(tile)] || [1, 0.95, 0.8];
        r.value = red;
        g.value = green;
        b.value = blue;
    }

    EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible) {
        if (tileFrameY >= 54 || Math.random() >= 1 / 40) return;

        const style = Math.floor(tileFrameX / 54);
        const column = Math.floor(tileFrameX / 18) % 3;
        if (Math.floor(tileFrameY / 18) % 3 !== 1 || column === 1) return;

        const dustType = style === Style.Copper || style === Style.Silver ? DustID.Torch
            : style === Style.BorealWood ? DustID.BlueTorch : -1;
        if (dustType < 0) return;

        const dust = Main.dust[NewDust(Vector2.new(i * 16, j * 16 + 2), 14, 6, dustType, 0, 0, 100, Color.White, 1)];
        if (Rand.NextBool(3)) dust.noGravity = true;
        dust.velocity = Vector2.new(dust.velocity.X * 0.3, dust.velocity.Y * 0.3 - 1.5);
    }
}
