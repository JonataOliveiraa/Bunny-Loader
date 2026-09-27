// Loja de morador de mod: cada loja ganha um índice livre de Main.shop (de 99
// para baixo; as do jogo vão até ~25).
class NPCShop {
    static #byKey = new Map();
    static #byIndex = new Map();
    static #nextIndex = 99;

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
        Hooks.Once('npc.Shops', NPCShop.#HookSetup);
        return this;
    }

    Open() {
        Terraria.Main.instance['void OpenShop(int shopIndex)'](this.Index);

        const pages = bl.classOf('', 'GUIInstance').Active.GUIPageIcons;
        pages['void OpenUI(GUIPageIcons.Category left, GUIPageIcons.Category right)'](2, 4);
    }

    static get(npcType, name) {
        return NPCShop.#byKey.get(npcType + '/' + name);
    }

    static #HookSetup() {
        Terraria.InventoryStorage['void SetupShop(int type)'].hook((original, self, type) => {
            const shop = NPCShop.#byIndex.get(type);
            original(self, type);
            if (!shop) return;

            const setDefaults = 'void SetDefaults(int Type, ItemVariant variant)';
            const items = self.item;
            for (let i = 0; i < items.length; i++) items[i][setDefaults](0, null);

            let slot = 0;
            for (const entry of shop.Entries) {
                if (slot >= items.length - 1) break;
                if (entry.condition && Safe.Run('NPCShop ' + shop.Name, () => entry.condition()) !== true) continue;

                const item = items[slot++];
                item[setDefaults](entry.type, null);
                item.isAShopItem = true;
                if (entry.currency !== undefined) item.shopSpecialCurrency = entry.currency;
                if (entry.price !== undefined) item.shopCustomPrice = entry.price;
            }
        });
    }
}
