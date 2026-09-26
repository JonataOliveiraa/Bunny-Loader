// Teste do save de itens de mod (content/items/ModItemSave.cpp): personagem e baus
// do mundo. Cada rodada num processo novo (tools/bench/run.sh reabre o jogo).
//
// Com o Example Mod ligado:
//   1a vez: "restaurado: NAO" (nada salvo ainda); poe os itens, salva tudo;
//   depois: "restaurado: ok" — voltaram no lugar, com pilha e favorito.
// Com o Example Mod DESLIGADO (tire a pasta dele do aparelho):
//   o mundo tem de abrir, e os itens dele viram "?" no mesmo lugar, com a
//   mesma pilha ("ausente como ?: ok"). Religado, voltam a ser o item.
//
// Mexe no personagem de teste: inventario 3 e 4 e o porquinho 0. Nos baus
// so conta (e salva) o que ja houver.
const Main = Terraria.Main;
const SetDefaults = 'void SetDefaults(int Type, ItemVariant variant)';
const EXAMPLE_ITEM = 6147;
const SWORD = 6148;
const itemName = Terraria.Lang['string GetItemNameValue(int id)'];

function isUnloaded(type) {
    return type >= EXAMPLE_ITEM && /^(Item n[aã]o carregado|Unloaded item)/.test(itemName(type));
}

/** O que tem de estar em cada lugar depois de reabrir. */
function expected(player) {
    return [
        {where: 'inventario[3]', item: player.inventory[3], type: SWORD, stack: 1, favorited: true},
        {where: 'inventario[4]', item: player.inventory[4], type: EXAMPLE_ITEM, stack: 57, favorited: false},
        {where: 'porquinho[0]', item: player.bank.item[0], type: EXAMPLE_ITEM, stack: 5, favorited: false},
    ];
}

/** Itens de mod (ou "?") nos baus do mundo: "tipo x pilha" de cada um. */
function chestModItems() {
    const out = [];
    const chests = Main.chest;
    for (let c = 0; c < chests.length; c++) {
        const chest = chests[c];
        if (chest === null) continue;
        const items = chest.item;
        for (let i = 0; i < items.length; i++) {
            const it = items[i];
            if (it.type >= EXAMPLE_ITEM) out.push(`${isUnloaded(it.type) ? '?' : it.type}x${it.stack}`);
        }
    }
    return out;
}

function saveAll() {
    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](
        Main.ActivePlayerFileData, false, true);
    Terraria.IO.WorldFile['void SaveWorld(bool useCloudSaving, bool resetTime, WorldSaveContext saveContext)'](
        false, false, 0);
}

function run() {
    const player = Main.player[Main.myPlayer];
    const modLoaded = !isUnloaded(EXAMPLE_ITEM) && !isUnloaded(SWORD);
    bl.log('modsave mod ' + (modLoaded ? 'ligado' : 'DESLIGADO') + '; baus: [' + chestModItems().join(', ') + ']');

    if (!modLoaded) {
        const wrong = [];
        for (const e of expected(player)) {
            const it = e.item;
            if (!isUnloaded(it.type) || it.stack !== e.stack) wrong.push(`${e.where}=${it.type}x${it.stack}`);
        }
        bl.log('modsave ausente como ?: ' + (wrong.length === 0 ? 'ok' : 'NAO (' + wrong.join(', ') + ')'));
        saveAll();
        bl.log('modsave FIM: salvo com o mod desligado');
        return;
    }

    const wrong = [];
    for (const e of expected(player)) {
        const it = e.item;
        if (it.type !== e.type || it.stack !== e.stack || it.favorited !== e.favorited) {
            wrong.push(`${e.where}=${it.type}x${it.stack}${it.favorited ? '*' : ''}`);
        }
    }
    bl.log('modsave restaurado: ' + (wrong.length === 0 ? 'ok' : 'NAO (' + wrong.join(', ') + ')'));

    // Nenhuma COPIA a mais: item de mod so onde o teste pos. (Uma versao
    // repunha o do porquinho, que o jogo ja carrega, tambem no inventario.)
    const extra = [];
    const inv = player.inventory;
    for (let slot = 0; slot < inv.length; slot++) {
        const it = inv[slot];
        if (it.type >= EXAMPLE_ITEM && slot !== 3 && slot !== 4) {
            extra.push(`inventario[${slot}]=${it.type}x${it.stack}`);
            it['void TurnToAir(bool fullReset)'](false);
        }
    }
    bl.log('modsave sem copia: ' + (extra.length === 0 ? 'ok' : 'NAO (' + extra.join(', ') + ')'));

    for (const e of expected(player)) {
        e.item[SetDefaults](e.type, null);
        e.item.stack = e.stack;
        e.item.favorited = e.favorited;
    }
    saveAll();
    bl.log('modsave FIM: gravado');
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || ++frames !== 60) return;
    run();
});
bl.log('modsave: carregado');
