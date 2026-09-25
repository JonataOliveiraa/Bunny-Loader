// A loja da Pessoa do Example Mod, pela conversa do celular: a Pessoa nasce
// ao lado do jogador, o jogador conversa com ela; o SetupButtonText do
// GUINPCDialogue (o do jogo, com Ref nos ref) da o botao "Loja" com icone e o
// de felicidade; o Option1Clicked do jogo abre a loja do mod, com os itens
// dela (a Espada de Energia na moeda do Exemplo de Item); a felicidade dela
// (GetShoppingSettings do jogo) com os textos do mod. No fim a conversa
// fica aberta, para o print do retrato. Loga "townshop ...".
const Main = Terraria.Main;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('townshop ' + label + ': ok');
        else { fails++; bl.log('townshop ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('townshop ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function byName(vanilla, isMod, getMod) {
    const out = {};
    for (let t = vanilla; isMod(t); t++) {
        const m = getMod(t);
        if (m) out[m.constructor.name] = t;
    }
    return out;
}

function run() {
    const items = byName(bl.items.vanillaCount, bl.items.isModItem, ModItem.getModItem);
    const npcs = byName(bl.npcs.vanillaCount, bl.npcs.isModNpc, ModNPC.getModNPC);
    const type = npcs.ExamplePerson;
    const p = Main.player[Main.myPlayer];
    bl.log('townshop passo: NewNPC');
    const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 40,
                       Math.floor(p.position.Y + p.height), type, 0, 0, 0, 0, 0, Main.myPlayer);
    const npc = Main.npc[idx];
    for (let i = 0; i < Main.npc.length - 1; i++) {
        const g = Main.npc[i];
        if (g.active && g.type === 22) {
            bl.log('townshop passo: GetShoppingSettings do Guia');
            const gs = Main.ShopHelper.GetShoppingSettings(p, g);
            bl.log('townshop passo: Guia "' + gs.HappinessReport + '" preco ' + gs.PriceAdjustment);
            break;
        }
    }
    bl.log('townshop passo: GetShoppingSettings');
    const settings = Main.ShopHelper.GetShoppingSettings(p, npc);
    bl.log('townshop felicidade: "' + settings.HappinessReport + '" preco ' + settings.PriceAdjustment);
    check('felicidade: texto do mod (sem chave crua) e preco', () =>
        (typeof settings.HappinessReport === 'string' && settings.HappinessReport.length > 0 &&
         !settings.HappinessReport.includes('TownNPCMood') && settings.PriceAdjustment > 0) ||
        `"${settings.HappinessReport}" ${settings.PriceAdjustment}`);
    bl.log('townshop passo: SetTalkNPC ' + idx);
    p['void SetTalkNPC(int npcIndex)'](idx);
    bl.log('townshop passo: GetChat');
    Main.npcChatText = npc.GetChat();

    bl.log('townshop passo: SetupButtonText');
    const dialogue = bl.classOf('', 'GUIInstance').Active.GUINPCDialogue;
    const text1 = new Ref(''), tex1 = new Ref(null), text2 = new Ref(''), tex2 = new Ref(null);
    const cost = new Ref(0), happy = new Ref(false);
    dialogue['void SetupButtonText(ref string focusText, ref Texture2D option1Tex, ref string focusText3, ref Texture2D option2Tex, ref int cost, ref bool showHappiness)'](
        text1, tex1, text2, tex2, cost, happy);
    check('botao Loja com icone, sem segundo botao', () =>
        (text1.value === Terraria.Localization.Language['string GetTextValue(string key)']('LegacyInterface.28') && tex1.value && !text2.value) ||
        `"${text1.value}" icone ${!!tex1.value} "${text2.value}"`);
    // O mesmo SetupButtonText com o Guia: o botao de felicidade dele e a referencia.
    let guideHappy = null;
    for (let i = 0; i < Main.npc.length - 1; i++) {
        const g = Main.npc[i];
        if (!g.active || g.type !== 22) continue;
        const saved = p.talkNPC;
        p.talkNPC = i;
        const h = new Ref(false);
        dialogue['void SetupButtonText(ref string focusText, ref Texture2D option1Tex, ref string focusText3, ref Texture2D option2Tex, ref int cost, ref bool showHappiness)'](
            new Ref(''), new Ref(null), new Ref(''), new Ref(null), new Ref(0), h);
        p.talkNPC = saved;
        guideHappy = h.value;
        break;
    }
    check('botao de felicidade igual ao dos moradores do jogo', () => happy.value === guideHappy || `mod ${happy.value}, Guia ${guideHappy}`);
    check('a fala continua a do mod', () => typeof Main.npcChatText === 'string' && Main.npcChatText.length > 0 || 'vazia');

    bl.log('townshop passo: Option1Clicked');
    check('retrato do mod em NPCID.Sets.NPCPortraits', () => {
        const mode = dialogue.Mode;
        bl.log('townshop modo do retrato: ' + (typeof mode === 'number' ? mode : mode && mode.value__));
        return Terraria.ID.NPCID.Sets.NPCPortraits.ContainsKey(type) || 'sem retrato';
    });
    check('perfil de morador (festa e shimmer) no TownNPCProfiles', () =>
        Terraria.GameContent.TownNPCProfiles.Instance._townNPCProfiles.ContainsKey(type) || 'sem perfil');
    check('perfil: a cabeca troca depois do shimmer (variacao 1)', () => {
        const Profiles = Terraria.GameContent.TownNPCProfiles;
        const normal = Profiles.GetHeadIndexSafe(npc);
        npc.townNpcVariationIndex = 1;
        const shimmered = Profiles.GetHeadIndexSafe(npc);
        const isShimmer = npc.IsShimmerVariant;
        npc.townNpcVariationIndex = 0;
        return (normal === bl.npcs.headSlot(type) && shimmered === bl.npcs.headSlot(type, true) && isShimmer) ||
               `normal ${normal}, shimmer ${shimmered} (esperado ${bl.npcs.headSlot(type)}/${bl.npcs.headSlot(type, true)}), IsShimmerVariant ${isShimmer}`;
    });
    dialogue['void Option1Clicked(int healCost)'](0);
    const shopIndex = Main.npcShop;
    const shop = Main.instance.shop[shopIndex];
    const got = [];
    for (let i = 0; i < shop.item.length; i++) if (shop.item[i].type > 0) got.push(shop.item[i]);
    const names = got.map((it) => ModItem.getModItem(it.type) ? ModItem.getModItem(it.type).constructor.name : it.type);
    bl.log('townshop loja ' + shopIndex + ': ' + names.join(', '));
    check('o botao abre a loja do mod (indice livre, >= 90)', () => (shopIndex >= 90 && Main.playerInventory) || 'loja ' + shopIndex);
    check('itens da loja (o Ioio so de noite)', () => {
        const want = ['ExampleMeleeWeapon', 'ExampleGun', 'ExampleMagicWeapon'];
        if (!Main.dayTime) want.push('ExampleYoyo');
        want.push('ExampleSwingingEnergySword');
        return JSON.stringify(names) === JSON.stringify(want) || names.join(',');
    });
    // Para o print: a conversa aberta de novo, com o retrato do mod.
    Main.playerInventory = false;
    npc.position = Vector2.new(p.position.X + 40, p.position.Y);
    p['void SetTalkNPC(int npcIndex)'](idx);
    Main.npcChatText = npc.GetChat();
    check('Espada de Energia na moeda do Exemplo de Item', () => {
        const sword = got.find((it) => it.type === items.ExampleSwingingEnergySword);
        return (sword && sword.shopSpecialCurrency >= 0 && sword.isAShopItem) ||
               (sword ? `moeda ${sword.shopSpecialCurrency}` : 'sem a espada');
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames === 90) {
        done = true;
        check('preparo', run);
        bl.log('townshop FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('townshop: carregado');
