// Instância por entidade (o NewInstance do tModLoader) e a ordem do desenho:
//   - ProbeNpc: cada NPC tem o próprio `serial` (inicializador do campo roda
//     no spawn), o próprio array, o #privado funciona e o que o
//     SetStaticDefaults pôs no molde (this.Shared) chega à instância;
//     AnimationType = -1 no SetDefaults não lança; BestiaryRarityStars = 4
//     vira as estrelas do Bestiário;
//   - ProbeProjectile: o #privado existe no projétil (antes, a cópia do molde
//     não o tinha);
//   - GlowNpc: dois sobrepostos, o brilho vermelho no PostDraw. Na ordem das
//     chamadas o corpo cinza do segundo cobre o brilho do primeiro; com o
//     lote do celular juntando por textura, o brilho ficava por cima.
// Loga "npcinstance ...".
const Main = Terraria.Main;
const NewNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
const log = (s) => bl.log('npcinstance ' + s);

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

let serials = 0;
let spawnFlag = 'antes';

export class ProbeNpc extends ModNPC {
    serial = ++serials;
    flagAtSpawn = spawnFlag;
    list = [];
    #secret = 7;

    SetStaticDefaults() {
        this.Shared = 'do molde';
        this.BestiaryRarityStars = 4;
    }

    SetDefaults() {
        this.NPC.width = this.NPC.height = 32;
        this.NPC.lifeMax = 100;
        this.NPC.aiStyle = -1;
        this.NPC.noGravity = true;
        this.NPC.noTileCollide = true;
        this.NPC.dontTakeDamage = true;
        this.NPC.damage = 0;
        this.AnimationType = -1;
    }

    AI(npc) {
        npc.velocity = Vector2.new(0, 0);
    }

    Secret() {
        return this.#secret;
    }
}

export class ProbeProjectile extends ModProjectile {
    #hits = 3;

    SetDefaults() {
        this.Projectile.width = this.Projectile.height = 8;
        this.Projectile.timeLeft = 30;
        this.Projectile.tileCollide = false;
    }

    AI(proj) {
        this.#hits++;
        proj.velocity = Vector2.new(0, 0);
    }

    Hits() {
        return this.#hits;
    }
}

let glow = null;
const drawOrder = [];
export class GlowNpc extends ModNPC {
    SetDefaults() {
        this.NPC.width = this.NPC.height = 64;
        this.NPC.lifeMax = 100;
        this.NPC.aiStyle = -1;
        this.NPC.noGravity = true;
        this.NPC.noTileCollide = true;
        this.NPC.dontTakeDamage = true;
        this.NPC.damage = 0;
    }

    AI(npc) {
        npc.velocity = Vector2.new(0, 0);
    }

    PostDraw(npc, spriteBatch, screenPos) {
        if (drawOrder.length < 2 && !drawOrder.includes(npc.whoAmI)) drawOrder.push(npc.whoAmI);
        if (!glow) glow = ModContent.Request('Glow').Value;
        const at = Vector2.new(npc.position.X - screenPos.X + 40, npc.position.Y - screenPos.Y);
        spriteBatch[DRAW](glow, at, null, Color.White, 0, Vector2.new(0, 0), 1, 0, 0);
    }
}

const player = () => Main.player[Main.myPlayer];
const typeOf = (cls) => ModContent.NPCType(cls.name);
const spawned = [];

function spawn(type, dx, dy) {
    const p = player();
    const i = NewNPC(p.GetProjectileSource_Item(p.HeldItem), Math.floor(p.Center.X + dx), Math.floor(p.Center.Y + dy), type, 0, 0, 0, 0, 0, 255);
    spawned.push(i);
    return Main.npc[i];
}

let a = null, b = null, proj = null;
const steps = {
    60: () => {
        a = spawn(typeOf(ProbeNpc), -200, -120);
        spawnFlag = 'depois';
        b = spawn(typeOf(ProbeNpc), -120, -120);
        const p = player();
        const k = NewProjectile(p.GetProjectileSource_Item(p.HeldItem), p.Center, Vector2.new(0, 0), ModContent.ProjectileType('ProbeProjectile'), 0, 0, p.whoAmI, 0, 0, 0, null);
        proj = Main.projectile[k];
    },
    70: () => {
        const ma = a.ModNPC, mb = b.ModNPC;
        check('cada NPC com a própria instância', ma && mb && ma !== mb, ma && mb ? 'ok' : 'sem ModNPC');
        check('inicializador roda por NPC', ma.serial !== mb.serial && ma.flagAtSpawn === 'antes' && mb.flagAtSpawn === 'depois',
            'serial ' + ma.serial + '/' + mb.serial + ', flag ' + ma.flagAtSpawn + '/' + mb.flagAtSpawn);
        check('arrays separados', ma.list !== mb.list, ma.list === mb.list ? 'o mesmo array' : 'cada um o seu');
        check('#privado no NPC', ma.Secret() === 7, String(ma.Secret()));
        check('o que o SetStaticDefaults pôs no molde', ma.Shared === 'do molde' && ma.Type === typeOf(ProbeNpc), ma.Shared + ' tipo ' + ma.Type);
        const stars = Terraria.ID.ContentSamples.NpcBestiaryRarityStars.get_Item(typeOf(ProbeNpc));
        const glowStars = Terraria.ID.ContentSamples.NpcBestiaryRarityStars.get_Item(typeOf(GlowNpc));
        check('BestiaryRarityStars no Bestiário', stars === 4 && glowStars !== 4, 'ProbeNpc ' + stars + ', GlowNpc (conta do jogo) ' + glowStars);
        check('#privado no projétil', proj.ModProjectile && proj.ModProjectile.Hits() > 3, proj.ModProjectile ? String(proj.ModProjectile.Hits()) : 'sem ModProjectile');
    },
    80: () => {
        spawn(typeOf(GlowNpc), 0, -150);
        spawn(typeOf(GlowNpc), 60, -150);
    },
    // O primeiro a ser desenhado vai para a esquerda: o brilho dele (à
    // direita) cai sob o corpo do segundo, que na ordem certa o cobre.
    100: () => {
        const first = Main.npc[drawOrder[0]], second = Main.npc[drawOrder[1]];
        const c = player().Center;
        first.position = Vector2.new(c.X - 60, c.Y - 180);
        second.position = Vector2.new(c.X, c.Y - 180);
    },
    110: () => {
        const second = Main.npc[drawOrder[1]];
        const scale = 1600 / Main.screenWidth;
        const x = (second.position.X + 16 - Main.screenPosition.X) * scale;
        const y = (second.position.Y + 32 - Main.screenPosition.Y) * scale;
        log('ordem ' + drawOrder.join(',') + '; pixel ' + Math.round(x) + ' ' + Math.round(y));
    },
    130: () => {
        log('SHOT order');
    },
    200: () => {
        for (const i of spawned) Main.npc[i].active = false;
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    },
};

let frame = 0;
Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (frame <= 200 && !Main.gameMenu) {
        frame++;
        player().statLife = player().statLifeMax2;
        const step = steps[frame];
        if (step) {
            try {
                step();
            } catch (e) {
                fails++;
                log('erro no quadro ' + frame + ': ' + e + (e.stack ? ' ' + e.stack : ''));
            }
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestNpcInstance extends Mod {}
