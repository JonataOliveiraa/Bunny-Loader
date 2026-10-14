const { ItemID, NPCID, ProjectileID } = Terraria.ID;
const Main = Terraria.Main;
const WorldGen = Terraria.WorldGen;
const NewItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'];
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const NewNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

// O que cai ao sacudir uma árvore (o GlobalShakeTrees do tModLoader). Para
// a árvore de mod, o ModTree tem o Shake dele (ver ExampleTree).
export class ShakeTrees extends GlobalTile {
    // true diz que o item principal já caiu: o jogo não solta o dele.
    ShakeTree(x, y, treeType) {
        const random = WorldGen.genRand;

        // Árvore da floresta: 5% de chance de um Exemplo de Item.
        if (treeType === TreeTypes.Forest && random['int Next(int maxValue)'](20) === 0) {
            ShakeTrees.#Item(x, y, ModContent.ItemType('ExampleItem'), 1);
            return true;
        }

        // Árvore de cogumelo brilhante: 10% de chance de 3 a 10 Tochas de Cogumelo.
        if (treeType === TreeTypes.Mushroom && random['int Next(int maxValue)'](10) === 0) {
            ShakeTrees.#Item(x, y, ItemID.MushroomTorch, random['int Next(int minValue, int maxValue)'](3, 11));
            return true;
        }

        // Árvore da neve: 5% de chance de um avião de papel voando para um lado.
        if (treeType === TreeTypes.Snow && random['int Next(int maxValue)'](20) === 0) {
            NewProjectile(WorldGen.GetNPCSource_ShakeTree(x, y), x * 16, y * 16, Rand.Next(-16, 16), 0,
                ProjectileID.PaperAirplaneA, 4, 1, Main.myPlayer, 0, 0, 0, null);
            return true;
        }

        // Árvore da selva, na superfície e no Hardmode: 1 em 7 de uma Raposa Voadora Gigante.
        if (treeType === TreeTypes.Jungle && random['int Next(int maxValue)'](7) === 0 && y < Main.worldSurface && Main.hardMode) {
            NewNPC(WorldGen.GetNPCSource_ShakeTree(x, y), x * 16, y * 16, NPCID.GiantFlyingFox, 0, 0, 0, 0, 0, 255);
            return true;
        }

        if (treeType !== TreeTypes.Custom) return false;

        // Árvore de mod, à noite: metade das vezes cai um Slime de Exemplo.
        if (random['int Next(int maxValue)'](2) === 0 && !Main.dayTime) {
            NewNPC(WorldGen.GetNPCSource_ShakeTree(x, y), x * 16, y * 16, ModContent.NPCType('ExampleSlimeNPC'), 0, 0, 0, 0, 0, 255);
            return true;
        }

        // Só a Árvore de Exemplo (plantada no Bloco de Exemplo): metade das vezes, um coco.
        const bottomX = new Ref(0), bottomY = new Ref(0);
        WorldGen['void GetTreeBottom(int i, int j, out int x, out int y)'](x, y, bottomX, bottomY);
        if (bl.tiles.typeAt(bottomX.value, bottomY.value) === ModContent.TileType('ExampleTile') && random['int Next(int maxValue)'](2) === 0) {
            ShakeTrees.#Item(x, y, ItemID.Coconut, 1);
            return true;
        }

        return false;
    }

    // Antes das quedas, e sem tirar a chance delas: no mundo No Traps (e no
    // Get Fixed Boi), 20% de chance de uma flecha cair da árvore.
    PreShakeTree(x, y, treeType) {
        if (WorldGen.genRand['int Next(int maxValue)'](5) === 0 && Main.noTrapsWorld) {
            NewProjectile(WorldGen.GetNPCSource_ShakeTree(x, y), x * 16, y * 16, Rand.Next(-100, 101) * 0.002, 8,
                ProjectileID.WoodenArrowHostile, 10, 0, Main.myPlayer, 0, 0, 0, null);
        }
    }

    static #Item(x, y, type, stack) {
        NewItem(WorldGen.GetItemSource_FromTreeShake(x, y), x * 16, y * 16, 16, 16, type, stack, false, 0, 0, null, null);
    }
}
