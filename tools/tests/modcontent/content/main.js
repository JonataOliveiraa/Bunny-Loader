// ModContent, como o do tModLoader: o tipo pela classe, pelo nome e por
// 'mod/Nome'; o modelo (GetInstance, Find, this.Mod); a textura carregada uma
// vez (Request, Texture). E o que fica fora do Mod Menu: HideFromModMenu,
// ItemID.Sets.Deprecated e o Hide do Bestiario. Precisa do Example Mod.
// Loga "modcontent ...".
const Main = Terraria.Main;
const { ItemID, NPCID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modcontent ' + label + ': ok');
        else { fails++; bl.log('modcontent ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modcontent ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

class ShownItem extends ModItem {
    Texture = 'TestGun';
    SetDefaults() { this.Item.width = 20; this.Item.height = 20; }
}
class HiddenItem extends ModItem {
    Texture = 'TestGun';
    HideFromModMenu = true;
    SetDefaults() { this.Item.width = 20; this.Item.height = 20; }
}
class OldItem extends ModItem {
    Texture = 'TestGun';
    SetStaticDefaults() { ItemID.Sets.Deprecated[this.Type] = true; }
    SetDefaults() { this.Item.width = 20; this.Item.height = 20; }
}
class HiddenBuff extends ModBuff {
    Texture = 'TestBuff';
    SetStaticDefaults() { this.HideFromModMenu = true; }
}
class HiddenBlob extends ModNPC {
    Texture = 'TestBlob';
    SetStaticDefaults() {
        Main.npcFrameCount[this.Type] = 2;
        // Como no tModLoader: um NPCBestiaryDrawModifiers com Hide = true.
        const drawn = NPCID.Sets.NPCBestiaryDrawOffset;
        const hidden = drawn.get_Item(NPCID.Guide);
        hidden.Hide = true;
        drawn.Add(this.Type, hidden);
    }
    SetDefaults() {
        this.NPC.width = 24;
        this.NPC.height = 18;
        this.NPC.lifeMax = 50;
    }
}

const SHOWN = ModItem.register(ShownItem);
const HIDDEN = ModItem.register(HiddenItem);
const OLD = ModItem.register(OldItem);
const BUFF = ModBuff.register(HiddenBuff);
const BLOB = ModNPC.register(HiddenBlob);

// O tipo do Example Mod pelo nome da classe, sem o ModContent.
function exampleType(isMod, getMod, first, name) {
    for (let t = first; isMod(t); t++) {
        const m = getMod(t);
        if (m && m.constructor.name === name) return t;
    }
    return 0;
}

function run() {
    check('tipo pela classe, pelo nome e por mod/Nome', () => {
        const byClass = ModContent.ItemType(ShownItem);
        const got = [byClass, ModContent.ItemType('ShownItem'), ModContent.ItemType('test-modcontent/ShownItem'),
                     ModItem.getTypeByName('ShownItem')];
        return (byClass === SHOWN && got.every((t) => t === SHOWN)) || got.join(',') + ' (esperado ' + SHOWN + ')';
    });
    check('tipo de outro mod por mod/Nome', () => {
        const item = exampleType(bl.items.isModItem, ModItem.getModItem, bl.items.vanillaCount, 'ExampleItem');
        const boss = exampleType(bl.npcs.isModNpc, ModNPC.getModNPC, bl.npcs.vanillaCount, 'ExampleBoss');
        const got = [ModContent.ItemType('examplemod/ExampleItem'), ModContent.NPCType('examplemod/ExampleBoss'),
                     ModContent.ItemType('ExampleItem')];
        return (item > 0 && boss > 0 && got[0] === item && got[1] === boss && got[2] === item) ||
            got.join(',') + ' (esperado ' + item + ',' + boss + ',' + item + ')';
    });
    check('o que nao existe da 0', () => {
        class Solta extends ModItem {}
        const got = [ModContent.ItemType('NaoExiste'), ModContent.ProjectileType('examplemod/NaoExiste'),
                     ModContent.BuffType('modquenaoha/X'), ModContent.ItemType(Solta)];
        return got.every((t) => t === 0) || got.join(',');
    });
    check('GetInstance e this.Mod', () => {
        const inst = ModContent.GetInstance(ShownItem);
        return (inst && inst.Type === SHOWN && inst.Mod === bl.mod && inst.Mod.id === 'test-modcontent') ||
            'inst ' + !!inst + ', Type ' + (inst && inst.Type) + ', Mod ' + (inst && inst.Mod && inst.Mod.id);
    });
    check('Find e TryFind', () => {
        const found = ModContent.Find(ModItem, 'examplemod/ExampleItem');
        const ref = new Ref(1);
        const missing = ModContent.TryFind(ModItem, 'examplemod/NaoExiste', ref);
        const ok = ModContent.TryFind(ModNPC, 'test-modcontent/HiddenBlob', new Ref());
        return (found.constructor.name === 'ExampleItem' && found.Mod.id === 'examplemod' && !missing &&
                ref.value === undefined && ok) || [found.constructor.name, missing, ref.value, ok].join(', ');
    });
    check('Find lanca quando nao ha', () => {
        try { ModContent.Find(ModItem, 'NaoExiste'); } catch (e) { return true; }
        return 'nao lancou';
    });
    check('textura carregada uma vez', () => {
        const a = ModContent.Request('Textures/TestGun');
        const b = ModContent.Request('TestGun.png');
        const tex = ModContent.Texture('Textures/TestGun.png');
        return (a === b && bl.addressOf(tex) === bl.addressOf(a.Value) && tex.Width > 0 &&
                ModContent.HasAsset('TestGun') && !ModContent.HasAsset('Textures/NaoExiste')) ||
            'mesmo asset ' + (a === b) + ', largura ' + (tex && tex.Width);
    });
    check('Request de arquivo que nao existe lanca', () => {
        try { ModContent.Request('Textures/NaoExiste'); } catch (e) { return /nao achei/.test(String(e)) || String(e); }
        return 'nao lancou';
    });
    check('fora do Mod Menu: HideFromModMenu, Deprecated e Hide do Bestiario', () => {
        const got = {
            shown: bl.menu.isHidden('item', SHOWN), hidden: bl.menu.isHidden('item', HIDDEN),
            old: bl.menu.isHidden('item', OLD), buff: bl.menu.isHidden('buff', BUFF), npc: bl.menu.isHidden('npc', BLOB),
        };
        return (!got.shown && got.hidden && got.old && got.buff && got.npc) || JSON.stringify(got);
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames === 60) {
        done = true;
        run();
        bl.log('modcontent FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modcontent: carregado');
