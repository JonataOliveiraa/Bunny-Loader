import { ExampleSurfaceBiome } from '../../Content/Biomes/ExampleSurfaceBiome.js';
import { ExampleUndergroundBiome } from '../../Content/Biomes/ExampleUndergroundBiome.js';

const { NPCID } = Terraria.ID;
const Main = Terraria.Main;

// O Pilar de Exemplo à venda nos moradores, como os pilares do jogo (o
// GetNPCShopEntry do ModPylon, no tModLoader): com outro morador por perto,
// fora da corrupção e do carmim e, como pilar de bioma, no bioma de exemplo.
// O Mercador Viajante, o Mercador Esqueleto e o Taberneiro não vendem pilares.
export class ExamplePylonShop extends GlobalNPC {
    static NotSelling = [NPCID.TravellingMerchant, NPCID.SkeletonMerchant, NPCID.DD2Bartender];

    ModifyShop(shop) {
        if (ExamplePylonShop.NotSelling.includes(shop.NpcType)) return;
        shop.Add(ModContent.ItemType('ExamplePylonItem'), { condition: ExamplePylonShop.ForSale });
    }

    static ForSale() {
        const player = Main.player[Main.myPlayer];
        const center = player.Center;
        const tile = Point16.new(Math.floor(center.X / 16), Math.floor(center.Y / 16));
        const enoughNPCs = Terraria.GameContent.TeleportPylonsSystem['bool DoesPositionHaveEnoughNPCs(int necessaryNPCCount, Point16 centerPoint)'](2, tile);
        const inExampleBiome = player.InModBiome(ExampleSurfaceBiome) || player.InModBiome(ExampleUndergroundBiome);
        return enoughNPCs && !player.ZoneCorrupt && !player.ZoneCrimson && inExampleBiome;
    }
}
