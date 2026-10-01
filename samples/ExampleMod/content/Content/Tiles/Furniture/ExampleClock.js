const { DustID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;

const text = (key) => Terraria.Localization.Language['string GetTextValue(string key)'](key);

// Relógio de pêndulo 2x5: tocar diz a hora no chat.
export class ExampleClock extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.Clock[this.Type] = true;

        this.DustType = DustID.Platinum;
        this.AdjTiles = [TileID.GrandfatherClocks];

        // Como o relógio do jogo: colocado pelo canto de baixo, 2 px para baixo.
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
        TileObjectData.newTile.Height = 5;
        TileObjectData.newTile.Origin = Point16.new(0, 4);
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(200, 200, 200), 'ItemName.GrandfatherClock');
    }

    RightClick(x, y) {
        // O dia do jogo começa às 4:30; a noite dura 54000 tiques.
        let time = Main.time;
        if (!Main.dayTime) time += 54000;
        time = time / 86400 * 24 - 7.5 - 12;
        if (time < 0) time += 24;

        const period = text(time >= 12 ? 'GameUI.TimePastMorning' : 'GameUI.TimeAtMorning');
        let hours = Math.floor(time);
        const minutes = String(Math.floor((time - hours) * 60)).padStart(2, '0');
        if (hours > 12) hours -= 12;
        if (hours === 0) hours = 12;

        Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](`${hours}:${minutes} ${period}`, 255, 240, 20, false);
        return true;
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }
}
