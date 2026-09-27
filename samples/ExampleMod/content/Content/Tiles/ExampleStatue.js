const { DustID, ItemID, NPCID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { TileObjectDirection } = Terraria.Enums;
const Main = Terraria.Main;
const Wiring = Terraria.Wiring;

const NewItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int Type, int Stack, bool noBroadcast, int pfix, bool noGrabDelay)'];
const NewNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

// Estátua 2x3 ligada no fio: quase sempre solta uma moeda de prata (raramente
// de ouro ou platina); às vezes, um peixinho dourado.
export class ExampleStatue extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileObsidianKill[this.Type] = true;
        Main.tileSpelunker[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        TileID.Sets.Wiring.IsAMechanism[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.CopyFrom(TileObjectData.Style2xX);
        tile.DrawYOffset = 2;
        tile.StyleMultiplier = 2;
        tile.Direction = TileObjectDirection.PlaceLeft;
        TileObjectData.newAlternate.CopyFrom(tile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.addAlternate(1);
        TileObjectData.addTile(this.Type);

        this.DustType = DustID.Silver;
        this.AddMapEntry(Color.new(144, 148, 144), 'Statue');
    }

    HitWire(i, j) {
        const { X: x, Y: y } = TileObjectData.TopLeft(i, j);
        const width = 2, height = 3;
        for (let yy = y; yy < y + height; yy++) {
            for (let xx = x; xx < x + width; xx++) Wiring['void SkipWire(int x, int y)'](xx, yy);
        }

        const spawnX = (x + width * 0.5) * 16;
        const spawnY = (y + height * 0.65) * 16;
        const source = Wiring.GetProjectileSource(x, y);

        if (Rand.NextFloat() < 0.95) {
            const mech = (type) => Terraria.Item.MechSpawn(spawnX, spawnY, type);
            if (Wiring.CheckMech(x, y, 60) && mech(ItemID.SilverCoin) && mech(ItemID.GoldCoin) && mech(ItemID.PlatinumCoin)) {
                let id = ItemID.SilverCoin;
                if (Rand.NextBool(100)) {
                    id++;
                    if (Rand.NextBool(100)) id++;
                }
                NewItem(source, Math.floor(spawnX), Math.floor(spawnY - 20), 0, 0, id, 1, false, 0, false);
            }
            return;
        }

        if (!Wiring.CheckMech(x, y, 30) || !Terraria.NPC.MechSpawn(spawnX, spawnY, NPCID.Goldfish)) return;

        const index = NewNPC(source, Math.floor(spawnX), Math.floor(spawnY) - 12, NPCID.Goldfish, 0, 0, 0, 0, 0, 255);
        if (index < 0) return;

        const npc = Main.npc[index];
        npc.value = 0;
        npc.npcSlots = 0;
        npc.SpawnedFromStatue = true;
        npc.CanBeReplacedByOtherNPCs = true;
    }
}
