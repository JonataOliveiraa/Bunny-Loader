// Multijogador: receitas de mod no cliente e item de mod que atravessa a rede
// nos dois sentidos. Instale no host e no cliente, com o Example Mod.
// Loga "mp <papel> <caso>: ok | FALHOU" e "mp <papel> FIM".
const Main = Terraria.Main;
const Recipe = Terraria.Recipe;
const { ItemID } = Terraria.ID;

let gems = null;
class MpRecipes extends ModSystem {
    AddRecipeGroups() {
        gems = ModRecipe.CreateRecipeGroup('MpGems', [ItemID.Ruby, ItemID.Sapphire, ItemID.Emerald]);
    }
    AddRecipes() {
        new ModRecipe().SetResult(ItemID.Amber).AddRecipeGroup('MpGems', 3).Register();
    }
}
ModSystem.register(MpRecipes);

// Estas pilhas marcam o item: ninguem mais larga 17 almas ou 23 itens.
const CLIENT_STACK = 17;
const HOST_STACK = 23;
const TIMEOUT = 900;

let role = '';
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mp ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mp ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mp ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function itemType(...names) {
    for (let t = bl.items.vanillaCount; t < bl.items.vanillaCount + 400; t++) {
        if (!bl.items.isModItem(t)) break;
        if (names.includes(Terraria.Lang['string GetItemNameValue(int id)'](t))) return t;
    }
    return -1;
}

const newItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int Type, int Stack, bool noBroadcast, int pfix, bool noGrabDelay)'];
const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];

// Longe do jogador (fora do alcance de pegar), no ar acima dele.
function drop(type, stack, fromClient) {
    const p = Main.player[Main.myPlayer];
    const x = Math.floor(p.position.X - 320), y = Math.floor(p.position.Y - 64);
    const idx = newItem(null, x, y, 16, 16, type, stack, true, 0, false);
    sendData(21, -1, -1, null, idx, fromClient ? 1 : 0, 0, 0, 0, 0, 0);
    return idx;
}

function findWorldItem(type, stack) {
    for (let i = 0; i < Main.item.length; i++) {
        const it = Main.item[i];
        if (it.active && it.type === type && it.stack === stack) return i;
    }
    return -1;
}

function craftable(recipeIndex, type, stack) {
    const p = Main.player[Main.myPlayer];
    const slot = p.inventory[49];
    const saved = { type: slot.type, stack: slot.stack };
    slot['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    slot.stack = stack;
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
    const avail = Main['int[] get_availableRecipe()']();
    const n = Main['int get_numAvailableRecipes()']();
    let found = false;
    for (let i = 0; i < n && !found; i++) found = avail[i] === recipeIndex;
    slot['void SetDefaults(int Type, ItemVariant variant)'](saved.type, null);
    slot.stack = saved.stack;
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
    return found;
}

function findRecipe(result, pred) {
    for (let i = 0; i < Recipe.numRecipes; i++) {
        const r = Main.recipe[i];
        if (r.createItem.type === result && pred(r)) return i;
    }
    return -1;
}

let SOUL = -1, EXITEM = -1;
let started = -1, done = false, dropped = false;

function clientStart() {
    check('receitas de mod', () => {
        const rod = findRecipe(ItemID.RodofDiscord, (r) => r.requiredItem[0].type === ItemID.ChaosFish);
        return rod >= 0 || 'sem a receita do Bastao da Discordia (numRecipes ' + Recipe.numRecipes + ')';
    });
    check('grupo: craftavel com esmeralda', () => {
        const idx = findRecipe(ItemID.Amber, (r) => {
            for (let i = 0; i < r.acceptedGroups.length; i++) if (r.acceptedGroups[i] === gems.RegisteredId) return true;
            return false;
        });
        if (idx < 0) return 'receita nao achada';
        return craftable(idx, ItemID.Emerald, 3) || 'nao aparece com 3 esmeraldas';
    });
    check('larga item de mod', () => {
        const idx = drop(SOUL, CLIENT_STACK, true);
        dropped = true;
        return (idx >= 0 && Main.item[idx].type === SOUL) || 'NewItem ' + idx;
    });
}

function finish() {
    done = true;
    bl.log('mp ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

function remotePlayer() {
    for (let i = 0; i < 255; i++) {
        if (i !== Main.myPlayer && Main.player[i].active) return i;
    }
    return -1;
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 1) {
        const mode = Main.netMode;
        role = (mode & 2) ? 'host' : mode === 1 ? 'cliente' : '';
        SOUL = itemType('Alma de Exemplo', 'Example Soul');
        EXITEM = itemType('Exemplo de Item', 'Example Item');
        bl.log(`mp: netMode ${mode}, papel ${role || 'nenhum'}, alma ${SOUL}, item ${EXITEM}`);
        if (!role) { done = true; return; }
    }

    if (role === 'cliente') {
        if (frames === 120) { clientStart(); started = frames; }
        if (started > 0 && findWorldItem(EXITEM, HOST_STACK) >= 0) {
            check('ve o item largado pelo host', () => true);
            finish();
        } else if (started > 0 && frames - started > TIMEOUT) {
            check('ve o item largado pelo host', () => 'nao chegou em ' + TIMEOUT + ' quadros');
            finish();
        }
        return;
    }

    // host: espera o cliente entrar
    if (started < 0) {
        const r = remotePlayer();
        if (r >= 0) { started = frames; bl.log('mp host: cliente entrou (' + Main.player[r].name + ')'); }
        return;
    }
    if (!dropped && frames - started === 240) {
        check('larga item de mod', () => {
            const idx = drop(EXITEM, HOST_STACK, false);
            dropped = true;
            return (idx >= 0 && Main.item[idx].type === EXITEM) || 'NewItem ' + idx;
        });
    }
    if (findWorldItem(SOUL, CLIENT_STACK) >= 0 && dropped) {
        check('ve o item largado pelo cliente', () => true);
        finish();
    } else if (frames - started > TIMEOUT + 240) {
        check('ve o item largado pelo cliente', () => 'nao chegou em ' + TIMEOUT + ' quadros');
        finish();
    }
});
bl.log('mp: carregado');
