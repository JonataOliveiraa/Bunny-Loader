// As tile entities de mod. O jogo não as conhece: não dá para criar uma classe
// C# nova, e um TileEntity do jogo com tipo desconhecido quebra a carga do
// mundo. Moram aqui, por ID e por posição (o canto de cima à esquerda do
// objeto), gravadas no <mundo>.wld.bl.json e mandadas pelo ModNet. Os IDs
// começam em FIRST_ID, longe dos do jogo: o NetMessage.SendData(86, número: ID)
// do tModLoader (TileEntitySharing) chega aqui em vez de ir ao jogo.
// Servidor é o bit 2 do netMode: no celular, quem hospeda e joga fica com 3.
//
// A colocação: o HookPostPlaceMyPlayer de mod é um PlacementHook com o
// delegate nulo (o jogo pula) e uma marca no badReturn; o hook no
// CallPostPlacementPlayerHook lê a marca e chama o molde.
class TileEntityLoader {
    static FIRST_ID = 0x40000000;
    static ByID = new Map();
    static ByPosition = new Map();   // Key(x, y) -> entidade

    static #MARKER = 0x5E000000;    // badReturn = marca + tipo * 2 + 1 se é o Hook_AfterPlacement da classe
    static #SAVE_KEY = '#modTileEntities';
    static #SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
    static #SHARING = 86;

    static #templates = [];         // por Type
    static #byKey = new Map();      // 'uuid/Classe' -> molde
    static #nextID = TileEntityLoader.FIRST_ID;
    static #unloaded = [];          // gravadas por mods ausentes: voltam ao arquivo como vieram

    static Register(inst) {
        inst.Type = TileEntityLoader.#templates.length;
        TileEntityLoader.#templates.push(inst);
        TileEntityLoader.#byKey.set(TileEntityLoader.KeyOf(inst), inst);

        Hooks.Once('mte.world', () => Safe.Run('ModTileEntity: ganchos', () => TileEntityLoader.#Install()));
        const over = (name) => Hooks.Overrides(inst.constructor, ModTileEntity, name);
        if (['Update', 'PreGlobalUpdate', 'PostGlobalUpdate'].some(over)) {
            Hooks.Once('mte.update', () => Safe.Run('ModTileEntity: atualização', () => TileEntityLoader.#HookUpdates()));
        }
    }

    static KeyOf(inst) {
        return (inst.Mod ? inst.Mod.uuid : 'sem-mod') + '/' + inst.constructor.name;
    }

    static Key(x, y) {
        return (x & 0xFFFF) | ((y & 0xFFFF) << 16);
    }

    static At(x, y) {
        return TileEntityLoader.ByPosition.get(TileEntityLoader.Key(x, y));
    }

    // A posição, como o Point16 do tModLoader.
    static Point(x, y) {
        return Object.freeze({
            X: x, Y: y,
            ToVector2() { return Vector2.new(x, y); },
            ToWorldCoordinates(addX = 8, addY = 8) { return Vector2.new(x * 16 + addX, y * 16 + addY); },
            toString() { return '{X:' + x + ' Y:' + y + '}'; },
        });
    }

    // Uma entidade nova do molde, ainda fora do mundo.
    static #Create(template, x, y, id) {
        const e = new template.constructor();
        e.Mod = template.Mod;
        e.Type = template.Type;
        e.ID = id;
        e.Position = TileEntityLoader.Point(x, y);
        return e;
    }

    static #Add(e) {
        const key = TileEntityLoader.Key(e.Position.X, e.Position.Y);
        const old = TileEntityLoader.ByPosition.get(key);
        if (old && old !== e) TileEntityLoader.ByID.delete(old.ID);
        TileEntityLoader.ByID.set(e.ID, e);
        TileEntityLoader.ByPosition.set(key, e);
    }

    static #Remove(e) {
        TileEntityLoader.ByID.delete(e.ID);
        const key = TileEntityLoader.Key(e.Position.X, e.Position.Y);
        if (TileEntityLoader.ByPosition.get(key) === e) TileEntityLoader.ByPosition.delete(key);
    }

    // No servidor, a entidade nova vai a todos.
    static Place(template, x, y) {
        const e = TileEntityLoader.#PlaceQuiet(template, x, y);
        if (Terraria.Main.netMode & 2) TileEntityLoader.#SendEntity(e, -1, -1);
        return e.ID;
    }

    static #PlaceQuiet(template, x, y) {
        const e = TileEntityLoader.#Create(template, x, y, TileEntityLoader.#nextID++);
        TileEntityLoader.#Add(e);
        return e;
    }

    static Kill(template, x, y) {
        const e = TileEntityLoader.At(x, y);
        if (!e || e.Type !== template.Type) return;
        Safe.Run(e.Name + '.OnKill', () => e.OnKill());
        TileEntityLoader.#Remove(e);
        if (Terraria.Main.netMode & 2) ModNet.Send({ k: 'mte-kill', id: e.ID }, -1, -1);
    }

    // O Generic_Hook_AfterPlacement do tModLoader: a entidade no canto de cima
    // à esquerda do objeto. No cliente o servidor é quem coloca: vai antes o
    // quadrado de blocos (o jogo só manda a colocação depois deste gancho).
    static AfterPlacement(template, i, j, type, style, alternate) {
        const Main = Terraria.Main;
        const top = Terraria.ObjectData.TileObjectData.TopLeft(i, j);
        if (Main.netMode !== 1) return TileEntityLoader.Place(template, top.X, top.Y);

        const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](type, style, alternate);
        const width = data ? data.Width : 1, height = data ? data.Height : 1;
        Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](
            Main.myPlayer, top.X, top.Y, width, height, 0);
        ModNet.Send({ k: 'mte-place', x: top.X, y: top.Y, t: TileEntityLoader.KeyOf(template) });
        return -1;
    }

    // O valor do HookPostPlaceMyPlayer: `custom` chama o Hook_AfterPlacement
    // da classe, senão o genérico.
    static PlacementHook(template, custom, processedCoordinates) {
        Hooks.Once('mte.place', () => Safe.Run('ModTileEntity: colocação', () => TileEntityLoader.#HookPlacement()));
        const hook = Terraria.DataStructures.PlacementHook.new();
        hook.badReturn = TileEntityLoader.#MARKER + template.Type * 2 + (custom ? 1 : 0);
        hook.badResponse = 0;
        hook.processedCoordinates = !!processedCoordinates;
        return hook;
    }

    static Receive(envelope, from) {
        const Main = Terraria.Main;
        switch (envelope.k) {
            case 'mte': {
                if (Main.netMode !== 1) return;
                const template = TileEntityLoader.#byKey.get(envelope.t);
                if (!template || !Number.isInteger(envelope.id)) return;
                let e = TileEntityLoader.ByID.get(envelope.id);
                if (!e || e.Type !== template.Type) {
                    e = TileEntityLoader.#Create(template, envelope.x, envelope.y, envelope.id);
                    TileEntityLoader.#Add(e);
                }
                Safe.Run(e.Name + '.NetReceive', () => e.NetReceive(new NetReader(envelope.d)));
                return;
            }
            case 'mte-kill': {
                if (Main.netMode !== 1) return;
                const e = TileEntityLoader.ByID.get(envelope.id);
                if (e) TileEntityLoader.#Remove(e);
                return;
            }
            case 'mte-place': {
                // O PlaceEntityNet do jogo: dentro do mundo, sem outra ali e com o bloco certo.
                if (!(Main.netMode & 2) || from >= 256) return;
                const template = TileEntityLoader.#byKey.get(envelope.t);
                const x = envelope.x, y = envelope.y;
                if (!template || !Number.isInteger(x) || !Number.isInteger(y)) return;
                if (!Terraria.WorldGen['bool InWorld(int x, int y, int fluff)'](x, y, 0) || TileEntityLoader.At(x, y)) return;
                if (Safe.Run(template.Name + '.IsTileValidForEntity', () => template.IsTileValidForEntity(x, y)) !== true) return;
                const e = TileEntityLoader.#PlaceQuiet(template, x, y);
                Safe.Run(e.Name + '.OnNetPlace', () => e.OnNetPlace());
                TileEntityLoader.#SendEntity(e, -1, -1);
                return;
            }
        }
    }

    static #SendEntity(e, remote, ignore) {
        const writer = new NetWriter();
        Safe.Run(e.Name + '.NetSend', () => e.NetSend(writer));
        ModNet.Send({ k: 'mte', id: e.ID, x: e.Position.X, y: e.Position.Y, t: TileEntityLoader.KeyOf(e), d: writer.values }, remote, ignore);
    }

    static #Install() {
        ModNet.Install();
        const self = TileEntityLoader;
        const WorldFile = Terraria.IO.WorldFile;

        // O cliente entra num mundo pela rede, sem LoadWorld: o clearWorld zera.
        Terraria.WorldGen['void clearWorld()'].hook((original) => {
            original();
            self.#Clear();
        });
        WorldFile['void LoadWorld(bool canCheckFileState)'].hook((original, check) => {
            original(check);
            Safe.Run('ModTileEntity: carregar o mundo', () => self.#Load());
        });
        WorldFile['void InternalSaveWorld(bool useCloudSaving, bool resetTime)'].hook((original, cloud, reset) => {
            original(cloud, reset);
            if (!cloud) Safe.Run('ModTileEntity: gravar o mundo', () => self.#Save());
        });

        // Quem entra recebe as entidades de cada pedaço do mundo que chega.
        Terraria.NetMessage['void SendSection(int whoAmi, int sectionX, int sectionY)'].hook((original, who, sx, sy) => {
            original(who, sx, sy);
            if (!(Terraria.Main.netMode & 2) || !self.ByID.size) return;
            Safe.Run('ModTileEntity: entidades do pedaço', () => {
                const x0 = sx * 200, y0 = sy * 150;
                for (const e of self.ByID.values()) {
                    const { X: x, Y: y } = e.Position;
                    if (x >= x0 && x < x0 + 200 && y >= y0 && y < y0 + 150) self.#SendEntity(e, who, -1);
                }
            });
        });

        // TileEntitySharing de uma entidade de mod: o NetSend dela, não o jogo.
        Terraria.NetMessage[self.#SEND].hook((original, msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7) => {
            if (msgType !== self.#SHARING || number < self.FIRST_ID) {
                return original(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7);
            }
            const e = self.ByID.get(number);
            if (e && (Terraria.Main.netMode & 2)) Safe.Run('ModTileEntity: mandar ' + e.Name, () => self.#SendEntity(e, remote, ignore));
            return undefined;
        }, ModNet.Sent('net.send.mte', self.#SHARING));
    }

    static #HookPlacement() {
        const self = TileEntityLoader;
        const getData = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'];
        Terraria.ObjectData.TileObjectData['bool CallPostPlacementPlayerHook(int tileX, int tileY, int type, int style, int dir, int alternate, TileObject data)'].hook(
            (original, x, y, type, style, dir, alternate, data) => {
                const result = original(x, y, type, style, dir, alternate, data);
                const alt = data.alternate;
                const tileData = getData(type, style, alt);
                if (!tileData) return result;
                const hook = tileData.HookPostPlaceMyPlayer;
                const code = hook.badReturn - self.#MARKER;
                const template = code >= 0 ? self.#templates[code >> 1] : undefined;
                if (!template) return result;

                if (hook.processedCoordinates) {
                    x -= tileData.Origin.X;
                    y -= tileData.Origin.Y;
                }
                const name = template.Name + ((code & 1) ? '.Hook_AfterPlacement' : '.Generic_Hook_AfterPlacement');
                Safe.Run(name, () => (code & 1)
                    ? template.Hook_AfterPlacement(x, y, type, style, dir, alt)
                    : template.Generic_Hook_AfterPlacement(x, y, type, style, dir, alt));
                return result;
            }, { minType: FIRST_TILE, arg: 2 });
    }

    // O TileEntity.PerformUpdates do jogo roda no servidor (ou sozinho), uma
    // vez por atualização do mundo: PreGlobalUpdate de todas, o jogo, Update
    // de cada uma, PostGlobalUpdate de todas.
    static #HookUpdates() {
        const self = TileEntityLoader;
        Terraria.DataStructures.TileEntity['void PerformUpdates()'].hook((original) => {
            if (!self.ByID.size) return original();
            const list = [...self.ByID.values()];
            for (const e of list) self.#Call(e, 'PreGlobalUpdate');
            original();
            for (const e of list) {
                if (e.RequiresUpdates && self.ByID.get(e.ID) === e) self.#Call(e, 'Update');
            }
            for (const e of list) self.#Call(e, 'PostGlobalUpdate');
        });
    }

    static #Call(e, method) {
        try {
            e[method]();
        } catch (error) {
            Safe.Report(e.Name + '.' + method, error);
        }
    }

    static #Clear() {
        TileEntityLoader.ByID.clear();
        TileEntityLoader.ByPosition.clear();
        TileEntityLoader.#unloaded = [];
        TileEntityLoader.#nextID = TileEntityLoader.FIRST_ID;
    }

    static #WorldFile() {
        const path = Terraria.Main.worldPathName;
        return path ? path + '.bl.json' : null;
    }

    static #Read(file) {
        const text = file ? bl.file.read(file) : '';
        if (!text) return {};
        try {
            return JSON.parse(text) || {};
        } catch (e) {
            return null;
        }
    }

    // Cada uma volta só se o bloco ainda for dela (IsTileValidForEntity).
    static #Load() {
        const self = TileEntityLoader;
        self.#Clear();
        const all = self.#Read(self.#WorldFile());
        const entries = all && all[self.#SAVE_KEY];
        if (!Array.isArray(entries)) return;

        let loaded = 0, dropped = 0;
        for (const entry of entries) {
            const template = self.#byKey.get(entry.t);
            if (!template) {
                self.#unloaded.push(entry);
                continue;
            }
            if (!Number.isInteger(entry.x) || !Number.isInteger(entry.y)) continue;
            if (Safe.Run(template.Name + '.IsTileValidForEntity', () => template.IsTileValidForEntity(entry.x, entry.y)) !== true) {
                dropped++;
                continue;
            }
            const e = self.#Create(template, entry.x, entry.y, self.#nextID++);
            Safe.Run(e.Name + '.LoadData', () => e.LoadData(TagCompound.from(entry.d)));
            self.#Add(e);
            loaded++;
        }
        if (loaded || dropped || self.#unloaded.length) {
            bl.log('tile entities de mod: ' + loaded + ' carregada(s), ' + dropped + ' sem o bloco, ' + self.#unloaded.length + ' de mod ausente');
        }
    }

    static #Save() {
        const self = TileEntityLoader;
        const file = self.#WorldFile();
        const all = self.#Read(file);
        if (!file || !all) return;

        const entries = [...self.#unloaded];
        for (const e of self.ByID.values()) {
            const tag = new TagCompound();
            Safe.Run(e.Name + '.SaveData', () => e.SaveData(tag));
            const entry = { t: self.KeyOf(e), x: e.Position.X, y: e.Position.Y };
            if (Object.keys(tag).length) entry.d = tag;
            entries.push(entry);
        }

        const key = self.#SAVE_KEY;
        if (entries.length) all[key] = entries;
        else if (!(key in all)) return;
        else delete all[key];
        if (Object.keys(all).length) bl.file.write(file, JSON.stringify(all));
        else bl.file.delete(file);
    }
}
