// Carga na tabela de campos extras (script/bridge/ExtraFields.cpp): milhares de
// itens de mod, cada um com o seu item.ModItem, largados; coleta forcada; e
// mais itens, para a varredura rodar e descartar as entradas dos recolhidos.
// Os vivos tem de manter a instancia. Loga "extrafields <caso>: ok | FALHOU".
const Main = Terraria.Main;
const Collect = System.GC['void Collect(int generation, GCCollectionMode mode, bool blocking)'];

function modItem() {
    for (let t = bl.items.vanillaCount; t < bl.items.vanillaCount + 200; t++) {
        if (bl.items.isModItem(t)) return t;
    }
    return -1;
}

function make(type) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return it;
}

function run() {
    const type = modItem();
    const keep = [];
    for (let i = 0; i < 20; i++) {
        const it = make(type);
        it.ModItem.marca = i;
        keep.push(it);
    }
    for (let round = 0; round < 3; round++) {
        for (let i = 0; i < 1500; i++) make(type);
        Collect(2, 1, true);
    }
    let ok = true;
    for (let i = 0; i < keep.length; i++) {
        const m = keep[i].ModItem;
        if (!m || m.marca !== i || bl.addressOf(m.Item) !== bl.addressOf(keep[i])) { ok = false; break; }
    }
    bl.log('extrafields vivos mantem a instancia: ' + (ok ? 'ok' : 'FALHOU'));
    bl.log('extrafields FIM: ' + (ok ? 'tudo ok' : '1 falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames === 90) run();
});
