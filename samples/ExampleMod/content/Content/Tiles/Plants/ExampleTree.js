// Uma árvore de mod (ModTree) que cresce no Bloco de Exemplo. Uma bolota
// plantada nele vira a ExampleSapling, que cresce nesta árvore. As texturas
// ficam ao lado: ExampleTree.png (o tronco), ExampleTree_Tops.png (as copas)
// e ExampleTree_Branches.png (os galhos).
export class ExampleTree extends ModTree {
    SetStaticDefaults() {
        // Cresce no Bloco de Exemplo.
        this.GrowsOnTileId = [ModContent.TileType('ExampleTile')];
    }

    // A muda que a bolota vira no chão desta árvore.
    SaplingGrowthType(style) {
        style.value = 0;
        return ModContent.TileType('ExampleSapling');
    }

    // Cortar dá Exemplo de Item em vez de madeira.
    DropWood() {
        return ModItem.getTypeByName('ExampleItem');
    }

    // Sacudir dá um Bloco de Exemplo; false: sem as quedas do jogo (bolota,
    // fruta, dinheiro...).
    Shake(x, y, createLeaves) {
        const NewItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'];
        NewItem(Terraria.WorldGen.GetItemSource_FromTreeShake(x, y), x * 16, y * 16, 0, 0, ModItem.getTypeByName('ExampleTileItem'), 1, false, 0, 0, null, null);
        return false;
    }

    // A folha que cai (ao crescer, ao sacudir e com o vento): o PNG em
    // Content/Gores/. SpecialAI 3 é o voo de folha das árvores do jogo; o
    // ChildSafety a deixa aparecer com o sangue desligado.
    TreeLeaf() {
        const leaf = ModGore.getTypeByName('ExampleTree_Leaf');
        Terraria.ID.GoreID.Sets.SpecialAI[leaf] = 3;
        Terraria.GameContent.ChildSafety.SafeGore[leaf] = true;
        return leaf;
    }
}
