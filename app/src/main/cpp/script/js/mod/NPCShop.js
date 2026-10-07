// Loja de morador de mod: cada loja ganha um índice livre de Main.shop (de 99
// para baixo; as do jogo vão até ~25).
//
// As lojas do jogo também aparecem aqui (NPCShop.get(NPCID.Merchant)), para
// os Globais: o que o ModifyShop acrescenta entra depois dos itens do jogo.
class NPCShop {
    static #byKey = new Map();
    static #byIndex = new Map();
    static #nextIndex = 99;
    static #modified = new Set();

    // O número da loja do jogo (Main.npcShop) e o NPC que a abre (NPCInteractions).
    static #VANILLA = [[1, 17], [2, 19], [3, 20], [4, 38], [5, 54], [6, 107], [7, 108], [8, 124], [9, 142],
        [10, 160], [11, 178], [12, 207], [13, 208], [14, 209], [15, 227], [16, 228], [17, 229], [18, 353],
        [19, 368], [20, 453], [21, 550], [22, 588], [23, 633], [24, 663], [25, 227, 'Decor']];

    constructor(npcType, name = 'Shop') {
        this.NpcType = npcType;
        this.Name = name;
        this.Entries = [];
        this.Index = -1;
    }

    // options: { condition: () => bool, price, currency }.
    Add(type, options = {}) {
        this.Entries.push({ type, ...options });
        return this;
    }

    Register() {
        const key = this.NpcType + '/' + this.Name;
        if (NPCShop.#byKey.has(key)) throw new Error('NPCShop: ' + key + ' ja registrada');

        this.Index = NPCShop.#nextIndex--;
        NPCShop.#byKey.set(key, this);
        NPCShop.#byIndex.set(this.Index, this);
        NPCShop.Install();
        return this;
    }

    Open() {
        Terraria.Main.instance['void OpenShop(int shopIndex)'](this.Index);

        const pages = bl.classOf('', 'GUIInstance').Active.GUIPageIcons;
        pages['void OpenUI(GUIPageIcons.Category left, GUIPageIcons.Category right)'](2, 4);
    }

    static get(npcType, name = 'Shop') {
        NPCShop.#Vanilla();
        return NPCShop.#byKey.get(npcType + '/' + name);
    }

    // As lojas, as do jogo e as de mod.
    static get All() {
        NPCShop.#Vanilla();
        return [...NPCShop.#byIndex.values()];
    }

    static Install() {
        Hooks.Once('npc.Shops', NPCShop.#HookSetup);
    }

    static InstallTravel() {
        Hooks.Once('npc.TravelShop', () => {
            Terraria.InventoryStorage['void SetupTravelShop()'].hook((original) => {
                original();
                const shop = Terraria.Main.travelShop;
                let next = 0;
                while (next < shop.length && shop[next] !== 0) next++;
                const slot = new Ref(next);
                for (const g of globalNPCs.Templates('SetupTravelShop')) {
                    try { g.SetupTravelShop(shop, slot); }
                    catch (e) { Safe.Report(g.constructor.name + '.SetupTravelShop', e); }
                }
            });
        });
    }

    static #Vanilla() {
        if (NPCShop.#byIndex.has(1)) return;
        for (const [index, npcType, name] of NPCShop.#VANILLA) {
            const shop = new NPCShop(npcType, name || 'Shop');
            shop.Index = index;
            shop.Vanilla = true;
            NPCShop.#byKey.set(npcType + '/' + shop.Name, shop);
            NPCShop.#byIndex.set(index, shop);
        }
    }

    // O ModifyShop dos Globais, uma vez por loja, na primeira vez que ela abre.
    static #Modify(shop) {
        if (NPCShop.#modified.has(shop)) return;
        NPCShop.#modified.add(shop);
        for (const g of globalNPCs.Templates('ModifyShop')) {
            try { g.ModifyShop(shop); }
            catch (e) { Safe.Report(g.constructor.name + '.ModifyShop', e); }
        }
    }

    // O NPC (deste tipo) com quem o jogador local conversa.
    static #Talking(npcType) {
        const Main = Terraria.Main;
        const player = Main.player[Main.myPlayer];
        const i = player ? player.talkNPC : -1;
        const npc = i >= 0 && i < 200 ? Main.npc[i] : null;
        return npc && npc.active && npc.type === npcType ? npc : null;
    }

    static #HookSetup() {
        Terraria.InventoryStorage['void SetupShop(int type)'].hook((original, self, type) => {
            original(self, type);
            NPCShop.#Vanilla();
            const shop = NPCShop.#byIndex.get(type);
            if (!shop) return;
            NPCShop.#Modify(shop);

            const setDefaults = 'void SetDefaults(int Type, ItemVariant variant)';
            const items = self.item;
            let slot = 0;
            if (shop.Vanilla) {
                while (slot < items.length && items[slot].type > 0) slot++;
            } else {
                for (let i = 0; i < items.length; i++) items[i][setDefaults](0, null);
            }
            for (const entry of shop.Entries) {
                if (slot >= items.length - 1) break;
                if (entry.condition && Safe.Run('NPCShop ' + shop.Name, () => entry.condition()) !== true) continue;

                const item = items[slot++];
                item[setDefaults](entry.type, null);
                item.isAShopItem = true;
                if (entry.currency !== undefined) item.shopSpecialCurrency = entry.currency;
                if (entry.price !== undefined) item.shopCustomPrice = entry.price;
            }

            const npc = NPCShop.#Talking(shop.NpcType);
            const legacy = globalNPCs.Templates('SetupShop');
            if (legacy.length) {
                const next = new Ref(slot);
                for (const g of legacy) {
                    try { g.SetupShop(shop.NpcType, self, next); }
                    catch (e) { Safe.Report(g.constructor.name + '.SetupShop', e); }
                }
            }
            if (!npc) return;
            NPCLoader.Call(npc, 'ModifyActiveShop', shop.Name, items);
            for (const g of globalNPCs.For(npc, 'ModifyActiveShop')) {
                try { g.ModifyActiveShop(npc, shop.Name, items); }
                catch (e) { Safe.Report(g.constructor.name + '.ModifyActiveShop', e); }
            }
        });
    }
}
