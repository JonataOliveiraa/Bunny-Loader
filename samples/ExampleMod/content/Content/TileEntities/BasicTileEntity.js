// Uma tile entity básica (ModTileEntity), num arquivo só com as três partes:
//   BasicTileEntity      a entidade: os dados e o código presos ao bloco;
//   BasicTileEntityTile  o bloco em que ela fica;
//   BasicTileEntityItem  o item que coloca o bloco.
// Um bloco comum não guarda dados nem roda código a cada atualização; a tile
// entity sim. Esta é um barril que junta água enquanto chove. Cheio, tocar
// nele (ou bater com a picareta) dá um Balde de Água.
const { ItemID, SoundID, TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const NewItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'];
const SendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];
const say = (message) => Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](message, 255, 255, 255, false);
const statusText = (entity) => ModLocalization.Translate('CustomText.BasicTileEntityStatus').replace('{0}', entity.WaterFillPercentage);

export class BasicTileEntity extends ModTileEntity {
    // Enche em 1 minuto de chuva (3600 atualizações).
    static MaxFill = 3600;
    static SyncInterval = BasicTileEntity.MaxFill / 10;

    #waterFillLevel = 0;

    // O nível passa sempre por aqui: fica entre 0 e o máximo e, no servidor,
    // manda os dados aos clientes a cada 10% (mandar a cada gota encheria a rede).
    get WaterFillLevel() { return this.#waterFillLevel; }
    set WaterFillLevel(value) {
        const level = Math.max(0, Math.min(BasicTileEntity.MaxFill, value | 0));
        const sync = Math.floor(this.#waterFillLevel / BasicTileEntity.SyncInterval) !== Math.floor(level / BasicTileEntity.SyncInterval);
        this.#waterFillLevel = level;
        // A mensagem 86 (TileEntitySharing) chama o NetSend desta entidade.
        if (sync && Main.netMode === 2) SendData(86, -1, -1, null, this.ID, 0, 0, 0, 0, 0, 0);
    }

    get WaterFillPercentage() { return Math.floor(this.WaterFillLevel * 100 / BasicTileEntity.MaxFill); }
    // Qual dos 5 desenhos do barril: 0%, 25%, 50%, 75% e 100%.
    get WaterFillStage() { return Math.floor(this.WaterFillLevel * 4 / BasicTileEntity.MaxFill); }

    // Os dados vão no save do mundo...
    SaveData(tag) { tag.WaterFillLevel = this.WaterFillLevel; }
    LoadData(tag) { this.WaterFillLevel = tag.GetInt('WaterFillLevel'); }

    // ...e pela rede, para os clientes.
    NetSend(writer) { writer.Write(this.WaterFillLevel); }
    NetReceive(reader) { this.WaterFillLevel = reader.ReadInt32(); }

    // A cada atualização do mundo, no servidor ou sozinho (no cliente não roda:
    // o que muda chega pelo NetReceive).
    Update() {
        if (Main.raining) this.WaterFillLevel += 1;
    }

    // Obrigatório: o bloco em (x, y) ainda é o desta entidade?
    IsTileValidForEntity(x, y) {
        return bl.tiles.typeAt(x, y) === ModContent.TileType('BasicTileEntityTile');
    }
}

export class BasicTileEntityTile extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        TileID.Sets.DisableSmartCursor[this.Type] = true;
        // Para não quebrar sem querer o que está em cima dele.
        TileID.Sets.PreventsTileRemovalIfOnTopOfIt[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        // A entidade nasce junto com o bloco colocado...
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(BasicTileEntity).Generic_HookPostPlaceMyPlayer;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(200, 200, 200), this.CreateMapEntryName());
    }

    // ...e sai com ele.
    KillMultiTile(i, j, frameX, frameY) {
        ModContent.GetInstance(BasicTileEntity).Kill(i, j);
    }

    // O desenho segue os dados: os 5 estados estão um embaixo do outro na
    // textura, 38 px cada.
    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {
        const entity = ModTileEntity.TryGet(i, j, BasicTileEntity);
        if (entity) tileFrameY.value += entity.WaterFillStage * 38;
    }

    // O nível de água ao lado do ícone, com o dedo em cima. O ícone ligado é o
    // que faz o toque chegar ao RightClick no celular.
    MouseOver(i, j) {
        const entity = ModTileEntity.TryGet(i, j, BasicTileEntity);
        if (!entity) return;
        const player = Main.player[Main.myPlayer];
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType('BasicTileEntityItem');
        player.cursorItemIconText = statusText(entity);
    }

    RightClick(i, j) {
        const entity = ModTileEntity.TryGet(i, j, BasicTileEntity);
        if (!entity) return true;

        if (entity.WaterFillPercentage === 100) {
            // Dar o balde aqui (no cliente) arrisca duplicar no multijogador:
            // como as tile entities do jogo, o toque "quebra" o bloco sem tirar
            // (fail), e o KillTile, que roda no servidor, dá o balde.
            SoundEngine.PlaySound(SoundID.Drown, null);
            W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](i, j, true, false, false);
            // Mensagem 17 (TileManipulation): o mesmo KillTile, com fail, no servidor.
            if (Main.netMode === 1) SendData(17, -1, -1, null, 0, i, j, 1, 0, 0, 0);
        } else {
            say(statusText(entity));
        }
        return true;
    }

    // Cheio, a picareta solta o balde em vez de quebrar o barril (como o
    // suporte de armas solta a arma): quebrar fica para a batida seguinte.
    KillTile(i, j, fail, effectOnly, noItem) {
        const entity = ModTileEntity.TryGet(i, j, BasicTileEntity);
        if (!entity || entity.WaterFillPercentage !== 100) return;

        fail.value = true;
        // O balde só nasce no servidor (ou sozinho); no multijogador o KillTile
        // chega lá e o nível zerado volta aos clientes pela rede.
        if (Main.netMode !== 1) {
            const at = entity.Position;
            NewItem(W.GetItemSource_FromTileBreak(i, j), at.X * 16 + 16, at.Y * 16 + 16, 0, 0, ItemID.WaterBucket, 1, false, 0, 0, null, null);
            entity.WaterFillLevel = 0;
        }
        // Para não quebrar o barril de vez no mesmo toque.
        if (Main.netMode !== 2) Main.player[Main.myPlayer].InterruptItemUsageIfOverTile(this.Type);
        SoundEngine.PlaySound(SoundID.Drown, null);
    }
}

// O item não tem nada de especial: só coloca o bloco.
export class BasicTileEntityItem extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('BasicTileEntityTile'));
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddRecipeGroup('Wood', 9)
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
