// O rastro do projétil (oldPos, oldRot, oldSpriteDirection). No celular são
// structs de tamanho fixo (Vector2_DynamicArray_120...), com `ref T get_Item`.
// Dois projéteis de mod com TrailCacheLength 5 (modos 0 e 2) voam
// uns quadros; depois os casos comparam `[i]`, `get_Item(i)` e a posição.
// Loga "oldpos <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ProjectileID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('oldpos ' + label + ': ok');
        else { fails++; bl.log('oldpos ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('oldpos ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const fmt = (v) => `(${v.X.toFixed(1)}, ${v.Y.toFixed(1)})`;

export class TrailMode0 extends ModProjectile {
    SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[this.Type] = 5;
        ProjectileID.Sets.TrailingMode[this.Type] = 0;
    }
    SetDefaults() {
        this.Projectile.width = this.Projectile.height = 10;
        this.Projectile.aiStyle = -1;
        this.Projectile.tileCollide = false;
        this.Projectile.timeLeft = 600;
    }
    AI(proj) { proj.rotation += 0.1; }
}

export class TrailMode2 extends ModProjectile {
    SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[this.Type] = 5;
        ProjectileID.Sets.TrailingMode[this.Type] = 2;
    }
    SetDefaults() {
        this.Projectile.width = this.Projectile.height = 10;
        this.Projectile.aiStyle = -1;
        this.Projectile.tileCollide = false;
        this.Projectile.timeLeft = 600;
    }
    AI(proj) {
        proj.rotation += 0.1;
        proj.spriteDirection = proj.velocity.X > 0 ? 1 : -1;
    }
}

const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

function dump(name, p) {
    const parts = [];
    for (let i = 0; i < p.oldPos.Length; i++) parts.push(fmt(p.oldPos[i]));
    bl.log(`oldpos: ${name} pos ${fmt(p.position)} Length ${p.oldPos.Length} [i] ${parts.join(' ')}`);
    const viaGet = [];
    for (let i = 0; i < p.oldPos.Length; i++) {
        try { viaGet.push(fmt(p.oldPos.get_Item(i))); } catch (e) { viaGet.push('erro ' + e); }
    }
    bl.log(`oldpos: ${name} get_Item ${viaGet.join(' ')}`);
}

function checks(mode0, mode2) {
    dump('modo 0', mode0);
    dump('modo 2', mode2);

    check('modo 0: Length = TrailCacheLength', () => mode0.oldPos.Length === 5 || mode0.oldPos.Length);
    check('modo 0: oldPos[0] registrado pelo jogo', () => {
        const v = mode0.oldPos[0];
        return !(v.X === 0 && v.Y === 0) || fmt(v);
    });
    check('modo 0: oldPos[1] anda atrás do [0]', () => {
        const a = mode0.oldPos[0], b = mode0.oldPos[1];
        return (a.X !== b.X || a.Y !== b.Y) || `${fmt(a)} ${fmt(b)}`;
    });
    check('modo 0: get_Item(i) = [i]', () => {
        for (let i = 0; i < 5; i++) {
            const a = mode0.oldPos[i], b = mode0.oldPos.get_Item(i);
            if (a.X !== b.X || a.Y !== b.Y) return `i ${i}: [i] ${fmt(a)}, get_Item ${fmt(b)}`;
        }
        return true;
    });
    check('modo 2: oldRot registrado', () => {
        const r = mode2.oldRot[1];
        return r !== 0 || `oldRot[1] ${r}, rotation ${mode2.rotation}`;
    });
    check('modo 2: oldRot.get_Item(i) = [i]', () => {
        for (let i = 0; i < 5; i++) {
            const a = mode2.oldRot[i], b = mode2.oldRot.get_Item(i);
            if (a !== b) return `i ${i}: [i] ${a}, get_Item ${b}`;
        }
        return true;
    });
    check('modo 2: oldSpriteDirection registrado', () => {
        const d = mode2.oldSpriteDirection[1];
        return d === 1 || `oldSpriteDirection[1] ${d}`;
    });
    check('modo 2: oldSpriteDirection.get_Item(i) = [i]', () => {
        for (let i = 0; i < 5; i++) {
            const a = mode2.oldSpriteDirection[i], b = mode2.oldSpriteDirection.get_Item(i);
            if (a !== b) return `i ${i}: [i] ${a}, get_Item ${b}`;
        }
        return true;
    });
    // `ref T` sai como cópia (como `var v = oldPos[i]` no C#); escrever é pelo [i].
    check('get_Item devolve cópia; [i] escreve no jogo', () => {
        const copy = mode0.oldPos.get_Item(4);
        copy.X = 1234;
        if (mode0.oldPos[4].X === 1234) return 'a cópia escreveu no jogo';
        mode0.oldPos[4].X = 1234;
        return mode0.oldPos.get_Item(4).X === 1234 || mode0.oldPos.get_Item(4).X;
    });
}

let frames = 0, ids = null;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    frames++;
    if (frames === 120) {
        const c = self.Center;
        ids = [
            newProj(null, c.X, c.Y - 40, 3, -1, ModContent.ProjectileType(TrailMode0), 0, 0, Main.myPlayer, 0, 0, 0, null),
            newProj(null, c.X, c.Y - 60, 3, -1, ModContent.ProjectileType(TrailMode2), 0, 0, Main.myPlayer, 0, 0, 0, null),
        ];
        bl.log('oldpos: projéteis ' + ids.join(', '));
    }
    if (frames === 150 && ids) {
        const [a, b] = ids.map((k) => Main.projectile[k]);
        checks(a, b);
        for (const p of [a, b]) p.active = false;
        bl.log('oldpos FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});

export default class TestOldPos extends Mod {}
