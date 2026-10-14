// Código de mod para TODO tile (os do jogo e os de mod), como o GlobalTile do
// tModLoader. Os métodos recebem a posição (i, j) e o tipo; os parâmetros
// `ref` chegam como Ref (.value). Cada método só liga o gancho dele quando
// sobrescrito. Os de todo quadro por célula (desenho, luz, NearbyEffects,
// TileFrame) ficaram de fora: no celular eles entrariam no JS para cada tile da tela.
class GlobalTile {
    SetStaticDefaults() {}
    PostSetupContent() {}
    // Depois do SetupContent: o lugar de mexer no Main.tileMerge.
    PostSetupTileMerge() {}

    // false: o tile não quebra (blockDamaged é Ref).
    CanKillTile(i, j, type, blockDamaged) { return true; }
    // fail, effectOnly e noItem são Ref.
    KillTile(i, j, type, fail, effectOnly, noItem) {}
    // Quantas partículas a quebra solta (num é Ref; só diminui).
    NumDust(i, j, type, fail, num) {}
    // dustType é Ref, com a do jogo; false: sem essa partícula.
    CreateDust(i, j, type, dustType) { return true; }
    // false: sem o som da quebra.
    KillSound(i, j, type, fail) { return true; }
    // false: o bloco não solta item. Os objetos do jogo (2x2, 3x2...) soltam pelo código deles.
    CanDrop(i, j, type) { return true; }
    // Depois dos itens do bloco: para soltar outros.
    Drop(i, j, type) {}

    // A colocação pelo jogador. false: não coloca.
    CanPlace(i, j, type) { return true; }
    PlaceInWorld(i, j, type, item) {}
    // A explosão de um projétil. false: o tile fica.
    CanExplode(i, j, type) { return true; }
    // O martelo do jogador. false: o tile fica como está.
    Slope(i, j, type) { return true; }

    // A atualização aleatória do mundo (crescer, espalhar). Entra no JS a cada
    // tile sorteado (umas centenas por quadro).
    RandomUpdate(i, j, type, underground) {}

    // false: o fio não chega ao tile (nem o do jogo nem o HitWire).
    PreHitWire(i, j, type) { return true; }
    HitWire(i, j, type) {}

    // O toque no tile e o tile sob o cursor (perto e longe).
    RightClick(i, j, type) {}
    MouseOver(i, j, type) {}
    MouseOverFar(i, j, type) {}

    // Uma vez por quadro: o lugar de animar (Main.tileFrame, Main.tileFrameCounter).
    AnimateTile() {}
    // Os tipos que o tile do tipo `type` conta como, perto do jogador (estações de criação).
    AdjTiles(type) { return []; }

    // Sacudir uma árvore: PreShakeTree antes das quedas; ShakeTree devolvendo
    // true diz que já soltou o item principal (o jogo não solta o dele).
    PreShakeTree(x, y, treeType) {}
    ShakeTree(x, y, treeType) { return false; }

    static register(cls) {
        const inst = globalTiles.Register(cls);
        GlobalTileLoader.Hook(cls);
        return inst;
    }
}
