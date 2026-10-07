// Baú e cômoda de mod no multijogador. O jogo avisa o servidor pela mensagem
// 34, que só conhece o baú (21), o baú 2 (467) e a cômoda (88): qualquer outro
// tipo vira cômoda de madeira no servidor, e quebrar um de mod nem é avisado.
// Como o tModLoader (ações 100-103 da 34), aqui vai pelo ModNet com o tipo.
class ChestNetworkHooks {
    static #SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
    static #NEW_ITEM = 'int NewItem(int X, int Y, int Width, int Height, int Type, int Stack, bool noBroadcast, int pfix, NewItemOwnership ownership)';
    static #CHEST_MSG = 34;
    static #PLACE_ACTIONS = [0, 2, 4];
    static #DIG = 0;
    static #picking = null;

    static Install() {
        Hooks.Once('tile.chest.net', () => Safe.Run('ChestNetworkHooks.Install', () => ChestNetworkHooks.#Hook()));
    }

    static #Kind(type) {
        if (type < 0 || !TileLoader.ByType.has(type)) return null;
        const Sets = Terraria.ID.TileID.Sets;
        if (Sets.BasicDresser[type]) return 'dresser';
        if (Sets.BasicChest[type]) return 'chest';
        return null;
    }

    static #Hook() {
        ModNet.Install();
        const Main = Terraria.Main;

        // Colocar: o Chest.AfterPlacement_Hook do cliente manda a 34 com o canto
        // de origem; o tile de mod já está lá.
        Terraria.NetMessage[ChestNetworkHooks.#SEND].hook(
            (original, msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7) => {
                if (msgType === ChestNetworkHooks.#CHEST_MSG && Main.netMode === 1 && ChestNetworkHooks.#PLACE_ACTIONS.includes(number)) {
                    const x = Math.round(n2), y = Math.round(n3);
                    const type = bl.tiles.typeAt(x, y);
                    if (ChestNetworkHooks.#Kind(type)) {
                        ModNet.Send({ k: 'chest', a: 'place', x, y, t: type, s: Math.round(n4) });
                        return undefined;
                    }
                }
                return original(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7);
            }, ModNet.Sent('net.send.chest', ChestNetworkHooks.#CHEST_MSG));

        // Quebrar: o PickTile do cliente só manda a 34 de quebra para os tipos
        // do jogo. O golpe que quebra é o que limpa o cache de mineração.
        Terraria.Player['void PickTile(int x, int y, int pickPower, int dealDamageAsIfBaseNumberIs)'].hook(
            (original, self, x, y, pickPower, base) => {
                if (Main.netMode !== 1) return original(self, x, y, pickPower, base);
                const type = bl.tiles.typeAt(x, y);
                if (!ChestNetworkHooks.#Kind(type)) return original(self, x, y, pickPower, base);
                const pick = ChestNetworkHooks.#picking = { x, y, broke: false };
                try {
                    original(self, x, y, pickPower, base);
                } finally {
                    ChestNetworkHooks.#picking = null;
                }
                if (pick.broke) ModNet.Send({ k: 'chest', a: 'kill', x, y, t: type });
                return undefined;
            });
        Terraria.Player['void ClearMiningCacheAt(int x, int y, int hitTileCacheType)'].hook(
            (original, self, x, y, cacheType) => {
                const pick = ChestNetworkHooks.#picking;
                if (pick && pick.x === x && pick.y === y) pick.broke = true;
                return original(self, x, y, cacheType);
            });
    }

    // `from`: no servidor, o cliente; no cliente, 256.
    static Receive(envelope, from) {
        const Main = Terraria.Main;
        if (Main.netMode === 2 && from < 256) {
            if (envelope.a === 'place') ChestNetworkHooks.#ServerPlace(envelope, from);
            else if (envelope.a === 'kill') ChestNetworkHooks.#ServerKill(envelope);
        } else if (Main.netMode === 1) {
            if (envelope.a === 'placed') ChestNetworkHooks.#Placed(envelope);
            else if (envelope.a === 'killed') ChestNetworkHooks.#Killed(envelope);
        }
    }

    static #ServerPlace({ x, y, t, s }, from) {
        if (!ChestNetworkHooks.#Kind(t)) return;
        const id = Terraria.WorldGen.PlaceChest(x, y, t, false, s);
        if (id === -1) {
            ModNet.Send({ k: 'chest', a: 'placed', x, y, t, s, id }, from, -1);
            const drop = TileLoader.GetItemDropFromTypeAndStyle(t, s);
            if (drop > 0) Terraria.Item[ChestNetworkHooks.#NEW_ITEM](x * 16, y * 16, 32, 32, drop, 1, false, 0, 0);
            return;
        }
        ModNet.Send({ k: 'chest', a: 'placed', x, y, t, s, id }, -1, -1);
    }

    static #ServerKill({ x, y, t }) {
        const kind = ChestNetworkHooks.#Kind(t);
        if (!kind || bl.tiles.typeAt(x, y) !== t) return;
        const tile = TileLoader.Tile(x, y);
        if (kind === 'dresser') x -= Math.floor((tile.frameX % 54) / 18);
        else if (tile.frameX % 36 !== 0) x--;
        if (tile.frameY % 36 !== 0) y--;
        const chest = Terraria.Chest.FindChest(x, y);
        Terraria.WorldGen.KillTile(x, y, false, false, false);
        if (bl.tiles.typeAt(x, y) === t) return;
        ModNet.Send({ k: 'chest', a: 'killed', x, y, id: chest }, -1, -1);
    }

    static #Placed({ x, y, t, s, id }) {
        const W = Terraria.WorldGen;
        if (id === -1) {
            W.KillTile(x, y, false, false, false);
            return;
        }
        const kind = ChestNetworkHooks.#Kind(t);
        if (!kind) return;
        SoundEngine.PlaySound(ChestNetworkHooks.#DIG, Vector2.new(x * 16, y * 16));
        if (kind === 'dresser') W.PlaceDresserDirect(x, y, t, s, id);
        else W.PlaceChestDirect(x, y, t, s, id);
    }

    static #Killed({ x, y, id }) {
        Terraria.Chest.DestroyChestDirect(x, y, id);
        Terraria.WorldGen.KillTile(x, y, false, false, false);
    }
}
