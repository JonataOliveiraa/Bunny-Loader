class TileEntityItemHooks {
    static #SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
    static #SINGLE = [
        ['TEItemFrame', 'void PlaceItemInFrame(Player player, int x, int y)', 89],
        ['TEWeaponsRack', 'void PlaceItemInFrame(Player player, int x, int y)', 123],
        ['TEFoodPlatter', 'void PlaceItemInFrame(Player player, int x, int y)', 133],
        ['TEDeadCellsDisplayJar', 'void PlaceItemInJar(Player player, int x, int y)', 149],
    ];
    static #SYNC_ENTITY = 86;
    static #DOLL = 121;
    static #HAT_RACK = 124;
    static #DOLL_ARRAYS = { 0: 'e', 1: 'd', 3: 'm' };
    static #SAVE_KEY = '#tileEntityItems';
    static #PENDING_MS = 10000;

    static #placing = null;
    static #pending = new Map();
    static #tracked = new Set();

    static Install() {
        Hooks.Once('item.TileEntities', () => Safe.Run('TileEntityItemHooks.Install', () => TileEntityItemHooks.#Hook()));
    }

    static #Hook() {
        ModNet.Install();
        const TE = Terraria.GameContent.Tile_Entities;
        const self = TileEntityItemHooks;
        const placeMessages = new Set();

        for (const [name, place, message] of self.#SINGLE) {
            const cls = TE[name];
            placeMessages.add(message);
            cls[place].hook((original, player, x, y) => {
                const item = player.inventory[player.selectedItem];
                if (!item || item.type < FIRST_ITEM || item.stack <= 0) return original(player, x, y);
                const previous = self.#placing;
                self.#placing = item;
                try {
                    return original(player, x, y);
                } finally {
                    self.#placing = previous;
                }
            });
            cls['void TryPlacing(int x, int y, int type, int prefix, int stack)'].hook((original, x, y, type, prefix, stack) => {
                const source = self.#placing;
                const run = () => original(x, y, type, prefix, stack);
                if (source && source.type === type) ItemNetworkHooks.DropFrom(source, run);
                else run();
                Safe.Run('ModItem: dados no expositor', () => self.#Placed(x, y, type, source));
            });
            cls['void DropItem()'].hook((original, te) => {
                const item = te.item;
                if (!item || item.type < FIRST_ITEM) return original(te);
                return ItemNetworkHooks.DropFrom(item, () => original(te));
            });
        }

        Terraria.NetMessage[self.#SEND].hook((original, msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7) => {
            const mode = Terraria.Main.netMode;
            if (mode === 1 && self.#placing && placeMessages.has(msgType)) {
                Safe.Run('ModItem: mandar o item do expositor', () => self.#SendPlacing(number, n2));
            }
            original(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7);
            if (mode === 0) return;
            if (msgType === self.#SYNC_ENTITY && mode === 2) {
                Safe.Run('ModItem: dados do expositor', () => self.#SendEntity(self.#ById(number), remote, ignore));
            } else if (msgType === self.#DOLL || msgType === self.#HAT_RACK) {
                Safe.Run('ModItem: dados do expositor', () => self.#SendSlot(msgType, Math.round(n2), Math.round(n3), Math.round(n4), remote, ignore));
            }
        }, ModNet.Sent('net.send.te', self.#SYNC_ENTITY, self.#DOLL, self.#HAT_RACK, ...placeMessages));

        Terraria.NetMessage['void SendSection(int whoAmi, int sectionX, int sectionY)'].hook((original, who, sx, sy) => {
            original(who, sx, sy);
            if (Terraria.Main.netMode !== 2 || !self.#tracked.size) return;
            Safe.Run('ModItem: dados dos expositores do pedaço', () => {
                const x0 = sx * 200, y0 = sy * 150;
                for (const id of [...self.#tracked]) {
                    const te = self.#ById(id);
                    if (!te) { self.#tracked.delete(id); continue; }
                    const x = te.Position.X, y = te.Position.Y;
                    if (x >= x0 && x < x0 + 200 && y >= y0 && y < y0 + 150) self.#SendEntity(te, who, -1);
                }
            });
        });

        const WorldFile = Terraria.IO.WorldFile;
        WorldFile['void LoadWorld(bool canCheckFileState)'].hook((original, check) => {
            self.#tracked.clear();
            self.#pending.clear();
            original(check);
            Safe.Run('ModItem: dados dos expositores do mundo', () => self.#Load());
        });
        WorldFile['void InternalSaveWorld(bool useCloudSaving, bool resetTime)'].hook((original, cloud, reset) => {
            original(cloud, reset);
            if (!cloud) Safe.Run('ModItem: gravar os expositores', () => self.#Save());
        });
    }

    static Receive(envelope, from) {
        const Main = Terraria.Main;
        if (envelope.k === 'te-place') {
            if (Main.netMode !== 2 || from >= 256 || !Number.isInteger(envelope.x) || !Number.isInteger(envelope.y)) return;
            TileEntityItemHooks.#pending.set(envelope.x + ',' + envelope.y, { envelope, time: Date.now() });
            return;
        }
        if (!envelope.s || typeof envelope.s !== 'object') return;
        const te = TileEntityItemHooks.#ById(envelope.id);
        if (!te || te.Position.X !== envelope.x || te.Position.Y !== envelope.y) return;
        let applied = false;
        for (const [slot, data] of Object.entries(envelope.s)) {
            const item = TileEntityItemHooks.#Item(te, slot);
            if (item && ItemNetworkHooks.Apply(item, data)) applied = true;
        }
        if (applied) TileEntityItemHooks.#tracked.add(te.ID);
        if (Main.netMode === 2 && from < 256) ModNet.Send(envelope, -1, from);
    }

    static #Placed(x, y, type, source) {
        const Main = Terraria.Main;
        let envelope = null;
        if (!source && Main.netMode === 2) {
            const key = x + ',' + y;
            const entry = TileEntityItemHooks.#pending.get(key);
            TileEntityItemHooks.#pending.delete(key);
            if (entry && Date.now() - entry.time <= TileEntityItemHooks.#PENDING_MS) envelope = entry.envelope;
        }
        if (!source && !envelope) return;
        const te = TileEntityItemHooks.#AtPosition(x, y);
        const item = te && te.item;
        if (!item || item.type !== type) return;
        if (source) ItemDataLoader.Copy(source, item);
        else if (!ItemNetworkHooks.Apply(item, envelope)) return;
        TileEntityItemHooks.#tracked.add(te.ID);
        if (Main.netMode === 2) TileEntityItemHooks.#SendEntity(te, -1, -1);
    }

    static #SendPlacing(x, y) {
        const item = TileEntityItemHooks.#placing;
        const payload = ItemNetworkHooks.Payload(item);
        if (!payload) return;
        ModNet.Send({ k: 'te-place', x: Math.round(x), y: Math.round(y), t: ItemNetworkHooks.Key(item), ...payload });
    }

    static #SendEntity(te, remote, ignore) {
        if (!te) return;
        const slots = {};
        let any = false;
        for (const [slot, item] of TileEntityItemHooks.#Slots(te)) {
            const data = TileEntityItemHooks.#Payload(item);
            if (!data) continue;
            slots[slot] = data;
            any = true;
        }
        if (!any) return;
        TileEntityItemHooks.#tracked.add(te.ID);
        ModNet.Send({ k: 'te-data', id: te.ID, x: te.Position.X, y: te.Position.Y, s: slots }, remote, ignore);
    }

    // 121: n3 é o índice e n4 o comando (0 equipamento, 1 tintura, 2 pose, 3 diversos).
    // 124: n3 é o índice e n4 diz se é tintura.
    static #SendSlot(msgType, id, index, command, remote, ignore) {
        const te = TileEntityItemHooks.#ById(id);
        if (!te) return;
        const prefix = msgType === TileEntityItemHooks.#DOLL ? TileEntityItemHooks.#DOLL_ARRAYS[command] : command ? 'd' : 'i';
        if (!prefix) return;
        const slot = prefix + index;
        const data = TileEntityItemHooks.#Payload(TileEntityItemHooks.#Item(te, slot));
        if (!data) return;
        TileEntityItemHooks.#tracked.add(te.ID);
        ModNet.Send({ k: 'te-data', id: te.ID, x: te.Position.X, y: te.Position.Y, s: { [slot]: data } }, remote, ignore);
    }

    static #Payload(item) {
        if (!item || item.type < FIRST_ITEM || item.stack <= 0) return null;
        const payload = ItemNetworkHooks.Payload(item);
        return payload ? { t: ItemNetworkHooks.Key(item), ...payload } : null;
    }

    static #Slots(te) {
        const list = [];
        const add = (prefix, items) => {
            if (!items) return;
            for (let i = 0; i < items.length; i++) list.push([prefix + i, items[i]]);
        };
        switch (te.GetType().Name) {
            case 'TEItemFrame':
            case 'TEWeaponsRack':
            case 'TEFoodPlatter':
            case 'TEDeadCellsDisplayJar':
                list.push(['item', te.item]);
                break;
            case 'TEDisplayDoll':
                add('e', te._equip);
                add('d', te._dyes);
                add('m', te._misc);
                break;
            case 'TEHatRack':
                add('i', te._items);
                add('d', te._dyes);
                break;
        }
        return list;
    }

    static #Item(te, slot) {
        for (const [name, item] of TileEntityItemHooks.#Slots(te)) {
            if (name === slot) return item;
        }
        return null;
    }

    static #ById(id) {
        const all = Terraria.DataStructures.TileEntity.ByID;
        return Number.isInteger(id) && all.ContainsKey(id) ? all.get_Item(id) : null;
    }

    static #AtPosition(x, y) {
        const all = Terraria.DataStructures.TileEntity.ByPosition;
        const key = (x & 0xFFFF) | (y << 16);
        return all.ContainsKey(key) ? all.get_Item(key) : null;
    }

    static #WorldFile() {
        const path = Terraria.Main.worldPathName;
        return path ? path + '.bl.json' : null;
    }

    static #ReadFile(file) {
        const text = file ? bl.file.read(file) : '';
        if (!text) return {};
        try {
            return JSON.parse(text) || {};
        } catch (e) {
            return null;
        }
    }

    static #Save() {
        const file = TileEntityItemHooks.#WorldFile();
        const all = TileEntityItemHooks.#ReadFile(file);
        if (!all) return;
        const entries = [];
        const tracked = new Set();
        const TileEntity = Terraria.DataStructures.TileEntity;
        const last = TileEntity.TileEntitiesNextID;
        for (let id = 0; id < last; id++) {
            const te = TileEntityItemHooks.#ById(id);
            if (!te) continue;
            for (const [slot, item] of TileEntityItemHooks.#Slots(te)) {
                if (!item || item.type < FIRST_ITEM || item.stack <= 0) continue;
                const data = ItemDataLoader.Save(item);
                if (!data) continue;
                entries.push({ x: te.Position.X, y: te.Position.Y, s: slot, t: ItemNetworkHooks.Key(item), d: data });
                tracked.add(id);
            }
        }
        TileEntityItemHooks.#tracked = tracked;
        const key = TileEntityItemHooks.#SAVE_KEY;
        if (entries.length) all[key] = entries;
        else if (!(key in all)) return;
        else delete all[key];
        if (Object.keys(all).length) bl.file.write(file, JSON.stringify(all));
        else bl.file.delete(file);
    }

    static #Load() {
        const all = TileEntityItemHooks.#ReadFile(TileEntityItemHooks.#WorldFile());
        const entries = all && all[TileEntityItemHooks.#SAVE_KEY];
        if (!Array.isArray(entries)) return;
        let restored = 0;
        for (const entry of entries) {
            const te = Number.isInteger(entry.x) && Number.isInteger(entry.y) ? TileEntityItemHooks.#AtPosition(entry.x, entry.y) : null;
            const item = te && TileEntityItemHooks.#Item(te, entry.s);
            if (!item || item.type < FIRST_ITEM || ItemNetworkHooks.Key(item) !== entry.t) continue;
            Safe.Run('ModItem.LoadData (expositor)', () => {
                ItemDataLoader.Load(item, entry.d);
                TileEntityItemHooks.#tracked.add(te.ID);
                restored++;
            });
        }
        if (restored) bl.log('expositores: dados de ' + restored + ' item(ns) de mod repostos');
    }
}
