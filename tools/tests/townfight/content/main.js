// A Pessoa do Example Mod em combate: com um slime (que fere) por perto, a IA de morador
// do jogo a faz atacar, e o tiro sai com o projetil do mod
// (TownNPCAttackProj/Strength/ProjSpeed) e fere o slime. Depois ela morre
// (StrikeNPC do jogo) e o HitEffect dela solta os gores de Textures/Gores do
// mod (cabeca, 2 bracos, 2 pernas). Loga "townfight ...".
const Main = Terraria.Main;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('townfight ' + label + ': ok');
        else { fails++; bl.log('townfight ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('townfight ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
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

let person = null, slime = null, slimeLife = 0, projs = null, shots = 0, deathFrame = -1;
const src = () => Terraria.DataStructures.EntitySource_DebugCommand.new();

// Conta os tiros no SetDefaults: o projetil pode morrer no mesmo quadro.
Terraria.Projectile['void SetDefaults(int Type)'].hook((original, self, type) => {
    original(self, type);
    if (projs && type === projs.ExampleAdvancedAnimatedProjectile) shots++;
});

function start() {
    const npcs = byName(bl.npcs.vanillaCount, bl.npcs.isModNpc, ModNPC.getModNPC);
    projs = byName(bl.projectiles.vanillaCount, bl.projectiles.isModProjectile, ModProjectile.getModProjectile);
    const p = Main.player[Main.myPlayer];
    const x = Math.floor(p.Center.X), y = Math.floor(p.position.Y + p.height);
    person = Main.npc[newNpc(src(), x + 60, y, npcs.ExamplePerson, 0, 0, 0, 0, 0, Main.myPlayer)];
    slime = Main.npc[newNpc(src(), x + 60 + 12 * 16, y, Terraria.ID.NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer)];
    slime.lifeMax = 50000;
    slime.life = 50000;
    slime.damage = 5;
    slime.defDamage = 5;
    slimeLife = slime.life;
}

function fought() {
    check('a Pessoa atirou o projetil do mod (TownNPCAttackProj)', () => shots > 0 || 'nenhum tiro do mod');
    check('o tiro feriu o slime', () => (slime.life < slimeLife) || `vida ${slime.life} de ${slimeLife}`);
    // Morte: o StrikeNPC do jogo, com dano maior que a vida.
    person['double StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool noEffect, bool fromNet, int owner)'](
        99999, 0, 1, false, false, false, -1);
}

function died() {
    const vanillaGores = Terraria.ID.GoreID.Count;
    const got = [];
    for (let i = 0; i < Main.gore.length; i++) {
        const g = Main.gore[i];
        if (g.active && g.type >= vanillaGores) got.push(g.type);
    }
    check('a Pessoa morreu', () => !person.active || 'ainda viva');
    check('5 gores do mod (cabeca, 2 bracos, 2 pernas)', () => got.length === 5 || `${got.length} gores de mod: ${got.join(',')}`);
    check('gores com textura (TextureAssets.Gore)', () => {
        for (const t of got) {
            const tex = Terraria.GameContent.TextureAssets.Gore[t];
            if (!tex || !tex.Value) return 'sem textura no tipo ' + t;
        }
        return true;
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 60) check('preparo', start);
    if (frames > 60 && frames < 720 && person && slime) {
        // O slime parado perto dela: o teste e o ataque, nao a perseguicao.
        slime.position = Vector2.new(person.position.X + 10 * 16, person.position.Y);
        if (frames % 60 === 0) bl.log(`townfight estado: ai0 ${person.ai[0]} ai1 ${person.ai[1]} localAI3 ${person.localAI[3]} tiros ${shots}`);
    }
    if (frames === 720) check('combate', fought);
    if (frames === 730) {
        done = true;
        check('morte', died);
        bl.log('townfight FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('townfight: carregado');
