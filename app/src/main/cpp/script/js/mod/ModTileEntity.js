// Dados e código presos a um bloco colocado, como o ModTileEntity do
// tModLoader. O molde (ModContent.GetInstance) coloca, acha e tira; cada
// objeto colocado tem a sua instância, com ID e Position (o canto de cima à
// esquerda). O Update roda no servidor (ou sozinho), a cada atualização do
// mundo; no cliente, os dados chegam pelo NetReceive. Ver TileEntityLoader.
//
// No SetStaticDefaults do ModTile:
//   TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(MinhaEntidade).Generic_HookPostPlaceMyPlayer;
// e no KillMultiTile dele: ModContent.GetInstance(MinhaEntidade).Kill(i, j).
class ModTileEntity {
    static get ByID() { return TileEntityLoader.ByID; }
    static get ByPosition() { return TileEntityLoader.ByPosition; }

    Type = -1;
    ID = -1;
    Position = null;
    // Sem Update sobrescrito, a entidade fica fora do laço de atualização.
    RequiresUpdates = Hooks.Overrides(this.constructor, ModTileEntity, 'Update');

    get Name() { return this.constructor.name; }
    get FullName() { return (this.Mod ? this.Mod.id || this.Mod.uuid : 'BunnyLoader') + '/' + this.Name; }

    // Obrigatório: o bloco em (x, y) ainda é desta entidade? Ao carregar o
    // mundo e na colocação pelo servidor; a que não passa é descartada.
    IsTileValidForEntity(x, y) { return false; }

    Load() {}
    Update() {}
    PreGlobalUpdate() {}
    PostGlobalUpdate() {}
    // No servidor, colocada a pedido de um cliente (antes de ir para todos).
    OnNetPlace() {}
    // Só pelo Kill(i, j).
    OnKill() {}
    SaveData(tag) {}
    LoadData(tag) {}
    // Para mandar de novo do servidor: NetMessage.SendData(86, -1, -1, null, this.ID, ...).
    NetSend(writer) {}
    NetReceive(reader) {}
    // O gancho do PlacementHook() da classe (o tModLoader não o chama sozinho).
    Hook_AfterPlacement(i, j, type, style, direction, alternate) { return -1; }

    // O ID da entidade nova em (i, j).
    Place(i, j) { return TileEntityLoader.Place(this, i, j); }
    Kill(i, j) { TileEntityLoader.Kill(this, i, j); }
    // O ID da entidade deste tipo em (i, j), ou -1.
    Find(i, j) {
        const e = TileEntityLoader.At(i, j);
        return e && e.Type === this.Type ? e.ID : -1;
    }

    // Para o TileObjectData.newTile.HookPostPlaceMyPlayer: a entidade nasce no
    // canto de cima à esquerda do objeto que o jogador colocou.
    get Generic_HookPostPlaceMyPlayer() { return TileEntityLoader.PlacementHook(this, false, true); }
    // O mesmo, chamando o Hook_AfterPlacement desta classe.
    PlacementHook(processedCoordinates = false) { return TileEntityLoader.PlacementHook(this, true, processedCoordinates); }

    Generic_Hook_AfterPlacement(i, j, type, style, direction, alternate) {
        return TileEntityLoader.AfterPlacement(this, i, j, type, style, alternate);
    }

    static CountInWorld() { return TileEntityLoader.ByID.size; }

    // A entidade do objeto em (i, j), em qualquer célula dele, se for de `cls`;
    // senão null (o TileEntity.TryGet do tModLoader).
    static TryGet(i, j, cls = ModTileEntity) {
        const top = Terraria.ObjectData.TileObjectData.TopLeft(i, j);
        const e = TileEntityLoader.At(top.X, top.Y);
        return e instanceof cls ? e : null;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModTileEntity)) {
            throw new TypeError('ModTileEntity.register(Class): pass the class, which extends ModTileEntity');
        }
        if (!Hooks.Overrides(cls, ModTileEntity, 'IsTileValidForEntity')) {
            throw new TypeError(cls.name + ': a ModTileEntity must override IsTileValidForEntity(x, y)');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        TileEntityLoader.Register(inst);
        Safe.Run(cls.name + '.Load', () => inst.Load());
        return inst.Type;
    }
}
