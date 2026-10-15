// ModBossBar: o BarDummy (um slime de mod, sem ser chefe e sem cabeça de
// chefe: o jogo não lhe daria barra) pede a TestBar.
//   - a barra aparece com a vida do NPC e o ícone do mod;
//   - ModifyInfo devolvendo true: vida 250/1000 e escudo 50/100 no desenho;
//   - ModifyInfo devolvendo false: nada é desenhado;
//   - o desenho é o das barras do jogo no celular (GUIBossBar.Draw).
// Loga "modbossbar <caso>: ok | FALHOU".
const Main = Terraria.Main;

let mode = 'default';
let preDraws = 0, postDraws = 0, last = null, gui = null;

bl.classOf('', 'GUIBossBar')['void Draw(float life, float lifeMax, Texture2D bossIconTexture, Rectangle bossIconFrame, float shield, float shieldMax)'].hook(
    (original, self, life, lifeMax, icon, frame, shield, shieldMax) => {
        if (icon.Width === 30) gui = { life, lifeMax, shield, shieldMax };
        return original();
    });

export class TestBar extends ModBossBar {
    GetIconTexture(frame) { return ModContent.Request('Textures/BarIcon'); }

    ModifyInfo(info, life, lifeMax, shield, shieldMax) {
        if (mode === 'hidden') return false;
        if (mode !== 'custom') return null;
        life.value = 250;
        lifeMax.value = 1000;
        shield.value = 50;
        shieldMax.value = 100;
        return true;
    }

    PreDraw(spriteBatch, npc, params) {
        preDraws++;
        last = { life: params.Life, lifeMax: params.LifeMax, shield: params.Shield, shieldMax: params.ShieldMax,
                 icon: params.IconTexture.Width, npc: npc.whoAmI };
        return true;
    }

    PostDraw(spriteBatch, npc, params) { postDraws++; }
}

export class BarDummy extends ModNPC {
    Texture = 'Textures/BarDummy';
    HideFromModMenu = true;
    HideFromBestiary = true;

    SetDefaults(npc) {
        this.CloneDefaults(Terraria.ID.NPCID.BlueSlime);
        npc.lifeMax = 1000;
        npc.life = 1000;
        npc.damage = 0;
        npc.friendly = true;
        npc.dontTakeDamage = true;
        this.BossBar = ModContent.GetInstance(TestBar);
    }
}

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modbossbar ' + label + ': ok');
        else { fails++; bl.log('modbossbar ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modbossbar ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
let dummy = null, frames = 0, done = false, hiddenFrom = 0;

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 60) {
        const p = Main.player[Main.myPlayer];
        const index = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 8 * 16,
                             Math.floor(p.position.Y), ModContent.NPCType(BarDummy), 0, 0, 0, 0, 0, Main.myPlayer);
        dummy = Main.npc[index];
    }
    if (frames === 150) {
        check('a barra aparece com a vida do NPC e o ícone do mod', () =>
            (preDraws > 0 && postDraws > 0 && last && last.life === 1000 && last.lifeMax === 1000 && last.icon === 30 && last.npc === dummy.whoAmI &&
             ModContent.GetInstance(TestBar).Life === 1000) || JSON.stringify({ preDraws, postDraws, last }));
        check('desenhada pelo GUIBossBar.Draw, sem escudo', () =>
            (gui && gui.life === 1000 && gui.lifeMax === 1000 && gui.shield === -1 && gui.shieldMax === 1) || JSON.stringify(gui));
        mode = 'custom';
    }
    if (frames === 200) {
        check('ModifyInfo true: os valores dados (vida e escudo)', () =>
            (last.life === 250 && last.lifeMax === 1000 && last.shield === 50 && last.shieldMax === 100) || JSON.stringify(last));
        check('GUIBossBar.Draw com os valores dados', () =>
            (gui && gui.life === 250 && gui.lifeMax === 1000 && gui.shield === 50 && gui.shieldMax === 100) || JSON.stringify(gui));
        bl.log('modbossbar pose');
    }
    // A barra do quadro em que o modo muda já foi decidida no Update do jogo:
    // conta a partir de dois quadros depois.
    if (frames === 320) mode = 'hidden';
    if (frames === 322) hiddenFrom = preDraws;
    if (frames === 380) {
        check('ModifyInfo false: a barra some', () => preDraws === hiddenFrom || (preDraws - hiddenFrom) + ' desenho(s) depois');
        if (dummy) dummy.active = false;
        done = true;
        bl.log('modbossbar FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modbossbar: carregado');

export default class TestModBossBar extends Mod {}
