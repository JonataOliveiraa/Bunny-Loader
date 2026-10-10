// Uma árvore nova, como a ModTree do tModLoader. Cresce como o tile 5 do jogo
// (árvore do jogo e de mod trocam entre si quando o chão é convertido), e é o
// bloco de baixo do tronco que diz de quem ela é: GrowsOnTileId, no
// SetStaticDefaults. As texturas, por padrão, ao lado da classe:
// <Classe>.png (o tronco: uma faixa da folha do tile 5, 176 de largura),
// <Classe>_Tops.png (as copas, 3 de 80x80 com 2 de espaço) e
// <Classe>_Branches.png (os galhos, 2 colunas de 3, 40x40). O desenho do
// tronco e da copa fica no nativo (ModTrees.cpp); o resto, no TreeLoader.
class ModTree {
    GrowsOnTileId = [];
    Texture = this.constructor.name;

    // Na carga do conteúdo, na thread do jogo: pode pedir textura (ModContent.Request).
    SetStaticDefaults() {}
    GetTexture() { return TreeLoader.Texture(this, ''); }
    GetTopTextures() { return TreeLoader.Texture(this, '_Tops'); }
    GetBranchTextures() { return TreeLoader.Texture(this, '_Branches'); }

    // O TreeTypes que o jogo enxerga (sacudir, as quedas da árvore).
    get CountsAsTreeType() { return TreeTypes.Custom; }
    // A poeira ao bater no tronco.
    CreateDust() { return 7; }
    // A gore das folhas (ao crescer, ao sacudir e no vento); -1 = nenhuma.
    TreeLeaf() { return -1; }
    // Ao sacudir: (x, y) é a copa. false: sem as quedas do jogo (createLeaves: Ref).
    Shake(x, y, createLeaves) { return true; }
    CanDropAcorn() { return true; }
    // A muda que a bolota vira no chão desta árvore (style: Ref).
    SaplingGrowthType(style) {
        style.value = 0;
        return Terraria.ID.TileID.Saplings;
    }
    // O item que cai em quantidade ao cortar.
    DropWood() { return Terraria.ID.ItemID.Wood; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModTree)) {
            throw new TypeError('ModTree.register(Class): pass the class, which extends ModTree');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        TreeLoader.Add(inst);
        return inst;
    }
}
