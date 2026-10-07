import { order } from './config.js';

const Main = Terraria.Main;
const N = Terraria.NPC;
const { NPCID } = Terraria.ID;
const owned = [];
let tracked = null;
let events = [];
let checks = 0;
let failures = 0;
let frames = 0;
let done = false;
let live = null;

function log(text) {
    bl.log('npcaitype ' + text);
}

function check(name, test) {
    checks++;

    try {
        if (!test()) throw Error('resultado falso');
        log('PASS ' + name);
    }
    catch (error) {
        failures++;
        log('FAIL ' + name + ': ' + error + ' ' + error.stack);
    }
}

function record(npc, name) {
    if (tracked && bl.addressOf(npc) === bl.addressOf(tracked)) events.push([name, npc.type]);
}

function defaults(npc) {
    npc.width = 30;
    npc.height = 32;
    npc.aiStyle = 2;
    npc.damage = 0;
    npc.defense = 0;
    npc.lifeMax = 100;
    npc.noGravity = true;
    npc.noTileCollide = true;
    npc.friendly = true;
    npc.dontTakeDamage = true;
    npc.npcSlots = 0;
    npc.value = 0;
}

class OnlyType extends ModNPC {
    Texture = 'Box';
    HideFromBestiary = true;
    HideFromModMenu = true;

    SetDefaults(npc) {
        defaults(npc);
        this.AIType = NPCID.WanderingEye;
    }
}

class InheritedType extends OnlyType {}

class HookedType extends OnlyType {
    PreAI(npc) {
        record(npc, 'mod.pre');
        if (this.nextType !== undefined) this.AIType = this.nextType;
        return !this.veto;
    }

    AI(npc) {
        record(npc, 'mod.ai');
    }

    PostAI(npc) {
        record(npc, 'mod.post');
    }
}

class ProbeGlobal extends GlobalNPC {
    get InstancePerEntity() { return true; }

    AppliesToEntity(npc) { return npc.type >= bl.npcs.vanillaCount; }

    PreAI(npc) {
        record(npc, 'global.pre');
        return !this.veto;
    }

    AI(npc) {
        record(npc, 'global.ai');
    }

    PostAI(npc) {
        record(npc, 'global.post');
    }
}

if (order === 'global-first') GlobalNPC.register(ProbeGlobal);
const only = ModNPC.register(OnlyType);
const inherited = ModNPC.register(InheritedType);
const hooked = ModNPC.register(HookedType);
if (order === 'mod-first') GlobalNPC.register(ProbeGlobal);

N['void AI_002_FloatingEye()'].hook((original, npc) => {
    record(npc, 'native.eye');
    original(npc);
});

N['void AI_003_Fighters()'].hook((original, npc) => {
    record(npc, 'native.fighter');
    original(npc);
});

function spawn(type, player) {
    const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    const index = N['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'](
        source, Math.floor(player.position.X + 400), Math.floor(player.position.Y - 180), type, 0, 0, 0, 0, 0, Main.myPlayer);

    if (index < 0 || index >= 200) throw Error('NewNPC: ' + index);
    const npc = Main.npc[index];
    owned.push(npc);
    return npc;
}

function runAI(npc) {
    tracked = npc;
    events = [];

    try {
        npc['void AI()']();
        return events;
    }
    finally {
        tracked = null;
    }
}

function expected(type, names, borrowed = NPCID.WanderingEye) {
    return JSON.stringify(names.filter(name => order !== 'local-only' || !name.startsWith('global.'))
        .map(name => [name, name === 'native.eye' ? borrowed : type]));
}

function sameEvents(actual, type, names, borrowed = NPCID.WanderingEye) {
    return JSON.stringify(actual) === expected(type, names, borrowed);
}

function prepareEye(npc, player) {
    defaults(npc);
    npc.position = Vector2.new(player.position.X + 400, player.position.Y - 180);
    npc.velocity = Vector2.new(0, 0);
    npc.life = 40;
    npc.target = Main.myPlayer;
    npc.direction = -1;
    npc.directionY = 1;
    npc.collideX = false;
    npc.collideY = false;

    for (let i = 0; i < 4; i++) {
        npc.ai[i] = 0;
        npc.localAI[i] = 0;
    }
}

function tests(player) {
    const a = spawn(only, player);
    const b = spawn(only, player);
    const child = spawn(inherited, player);
    const probe = spawn(hooked, player);
    const vanilla = spawn(NPCID.WanderingEye, player);

    check('SetDefaults e instancia ligada ao NPC', () => a.ModNPC.AIType === NPCID.WanderingEye && bl.addressOf(a.ModNPC.NPC) === bl.addressOf(a));
    check('AIType sem sobrescrever AI', () => sameEvents(runAI(a), only, ['global.pre', 'native.eye', 'global.ai', 'global.post']));
    check('tipo e netID restaurados', () => a.type === only && a.netID === only);
    check('heranca de SetDefaults', () => sameEvents(runAI(child), inherited, ['global.pre', 'native.eye', 'global.ai', 'global.post']));
    check('ordem completa com tipo original nos callbacks', () => sameEvents(runAI(probe), hooked,
        ['global.pre', 'mod.pre', 'native.eye', 'mod.ai', 'global.ai', 'mod.post', 'global.post']));

    const global = order === 'local-only' ? null : probe.GetGlobalNPC(ProbeGlobal);
    if (global) {
        global.marker = 73;
        runAI(probe);
        check('instancia global conservada durante a troca', () => probe.GetGlobalNPC(ProbeGlobal) === global && global.marker === 73);
    }

    probe.ModNPC.veto = true;
    check('veto local conserva ambos PostAI', () => sameEvents(runAI(probe), hooked, ['global.pre', 'mod.pre', 'mod.post', 'global.post']));
    probe.ModNPC.veto = false;
    if (global) {
        global.veto = true;
        check('veto global pula PreAI local e conserva PostAI', () => sameEvents(runAI(probe), hooked, ['global.pre', 'mod.post', 'global.post']));
        global.veto = false;
    }

    probe.ModNPC.nextType = NPCID.DemonEye;
    check('PreAI pode selecionar AIType no mesmo tick', () => sameEvents(runAI(probe), hooked,
        ['global.pre', 'mod.pre', 'native.eye', 'mod.ai', 'global.ai', 'mod.post', 'global.post'], NPCID.DemonEye));
    probe.ModNPC.nextType = undefined;

    a.ModNPC.AIType = NPCID.DemonEye;
    check('duas instancias usam AIType independente', () => runAI(a).some(e => e[0] === 'native.eye' && e[1] === NPCID.DemonEye) &&
        runAI(b).some(e => e[0] === 'native.eye' && e[1] === NPCID.WanderingEye));

    for (const value of [0, -1]) {
        a.ModNPC.AIType = value;
        check('AIType ' + value + ' mantem o tipo no helper nativo', () => runAI(a).some(e => e[0] === 'native.eye' && e[1] === only));
    }

    a.ModNPC.AIType = NPCID.WanderingEye;
    a.aiStyle = -1;
    check('AIType nao substitui aiStyle', () => !runAI(a).some(e => e[0] === 'native.eye') && a.type === only);
    a.aiStyle = 2;
    const originalAnimation = a.ModNPC.AnimationType;
    a.ModNPC.AnimationType = NPCID.DemonEye;
    runAI(a);
    check('AnimationType independente', () => a.ModNPC.AnimationType === NPCID.DemonEye && a.ModNPC.AIType === NPCID.WanderingEye && a.type === only);
    a.ModNPC.AnimationType = originalAnimation;

    a.aiStyle = 3;
    a.ModNPC.AIType = NPCID.Zombie;
    check('outra familia executa IA de Zombie com tipo restaurado', () => runAI(a).some(e => e[0] === 'native.fighter' &&
        e[1] === NPCID.Zombie) && a.type === only);
    a.aiStyle = 2;
    a.ModNPC.AIType = NPCID.WanderingEye;

    const day = Main.dayTime;
    try {
        Main.dayTime = false;
        prepareEye(a, player);
        prepareEye(vanilla, player);
        runAI(a);
        vanilla['void AI()']();
        const dx = Math.abs(a.velocity.X - vanilla.velocity.X);
        const dy = Math.abs(a.velocity.Y - vanilla.velocity.Y);
        log('comparacao velocidade dx=' + dx + ' dy=' + dy);
        check('movimento corresponde ao WanderingEye vanilla', () => dx < 0.00001 && dy < 0.00001 &&
            a.direction === vanilla.direction && a.directionY === vanilla.directionY);
    }
    finally {
        Main.dayTime = day;
    }

    const inst = a.ModNPC;
    let stable = true;
    for (let i = 0; i < 300; i++) {
        runAI(a);
        if (a.type !== only || a.ModNPC !== inst) stable = false;
    }
    check('300 chamadas reais restauram tipo e preservam instancia', () => stable);
}

Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (done || Main.gameMenu || index !== Main.myPlayer) return;
    frames++;

    try {
        if (frames === 60) {
            live = spawn(only, player);
            tracked = live;
            events = [];
        }

        if (frames === 90) {
            done = true;
            const nativeCalls = events.filter(e => e[0] === 'native.eye');
            check('loop real do mundo executa IA emprestada por varios ticks', () => nativeCalls.length >= 10 &&
                nativeCalls.every(e => e[1] === NPCID.WanderingEye));
            check('loop real conserva tipo e callbacks globais', () => live.type === only && live.netID === only &&
                events.filter(e => e[0] !== 'native.eye').every(e => e[1] === only));
            tracked = null;
            tests(player);
        }
    }
    catch (error) {
        done = true;
        failures++;
        log('FAIL execucao: ' + error + ' ' + error.stack);
    }
    finally {
        if (done) {
            tracked = null;
            for (const npc of owned) npc.active = false;
            log('FIM order=' + order + ' checks=' + checks + ' falhas=' + failures);
        }
    }
});

export default class NPCTypeTests extends Mod {}
