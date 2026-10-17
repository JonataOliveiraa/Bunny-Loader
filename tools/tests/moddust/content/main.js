// ModDust (content/dusts/ModDusts.cpp + Loaders/DustLoader.js), fase por fase:
//   registro e tabelas, nascimento (OnSpawn, moldura, NewDustDirect/Perfect),
//   atualizacao do jogo, MidUpdate, UpdateType contra a poeira do jogo,
//   Update false, morte com `active = false` (o padrao do tModLoader), a
//   pilha de poeiras livres (vazamento e poeira revivida), GetAlpha, PreDraw,
//   erro no Update, tipo trocado a mao, desenho (SHOT para a captura) e custo.
// Loga "moddust ...", "SHOT <nome>" e "moddust FIM".
const Main = Terraria.Main;
const Dust = Terraria.Dust;
const DustID = Terraria.ID.DustID;
// Vector2.new(x, y) e Color.new(...) sao os atalhos globais (o .new() da classe so aloca).
const XColor = Microsoft.Xna.Framework.Graphics.Color;
const log = (s) => bl.log('moddust ' + s);

let fails = 0;
// Erro de mod (Safe.Report -> bl.error, o painel): a checagem final conta.
const modErrors = [];
const blError = bl.error;
bl.error = (text) => {
    modErrors.push(String(text).split('\n')[0]);
    return blError(text);
};
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}
const near = (a, b, eps = 1e-3) => Math.abs(a - b) <= eps * Math.max(1, Math.abs(a), Math.abs(b));

const calls = { mid: 0, spawn: 0, staticUpdate: 0, preDraw: 0, throwing: 0, converted: 0, sparkleUpdate: 0 };

export class TestPlainDust extends ModDust {
    get Texture() { return 'TestMagentaDust'; }
}

export class TestSpawnDust extends ModDust {
    get Texture() { return 'TestMagentaDust'; }
    OnSpawn(dust) {
        calls.spawn++;
        dust.noGravity = true;
        dust.fadeIn = 3.25;
    }
}

export class TestVanillaTexDust extends ModDust {
    get Texture() { return null; }
    OnSpawn(dust) { dust.HackFrame(DustID.Electric); }
}

export class TestStaticDust extends ModDust {
    get Texture() { return 'TestCyanDust'; }
    OnSpawn(dust) {
        dust.noGravity = true;
        dust.fullBright = true;
        dust.velocity.X = 0;
        dust.velocity.Y = 0;
    }
    Update(dust) {
        calls.staticUpdate++;
        return false;
    }
}

export class TestSparkleDust extends ModDust {
    OnSpawn(dust) {
        dust.velocity.X *= 0.4;
        dust.velocity.Y *= 0.4;
        dust.noGravity = true;
        dust.noLight = true;
        dust.scale *= 1.5;
    }
    Update(dust) {
        calls.sparkleUpdate++;
        dust.position.X += dust.velocity.X;
        dust.position.Y += dust.velocity.Y;
        dust.rotation += dust.velocity.X * 0.15;
        dust.scale *= 0.95;
        if (dust.scale < 0.5) dust.active = false;
        return false;
    }
}

export class TestUpdateTypeDust extends ModDust {
    Texture = null;
    SetStaticDefaults() { this.UpdateType = DustID.Torch; }
}

export class TestMidDust extends ModDust {
    get Texture() { return 'TestMagentaDust'; }
    MidUpdate(dust) {
        calls.mid++;
        return true;
    }
}

export class TestMoverDust extends ModDust {
    get Texture() { return 'TestCyanDust'; }
    Update(dust) {
        dust.position.X += dust.velocity.X;
        dust.position.Y += dust.velocity.Y;
        dust.rotation += dust.velocity.X * 0.15;
        dust.scale *= 0.999;
        return false;
    }
}

export class TestAlphaDust extends ModDust {
    get Texture() { return 'TestMagentaDust'; }
    GetAlpha(dust, lightColor) {
        return Color.new(10, 20, 30, 40);
    }
}

export class TestPreDrawDust extends ModDust {
    get Texture() { return 'TestMagentaDust'; }
    PreDraw(dust) {
        calls.preDraw++;
        const tex = this.Texture2D.Value;
        Main.spriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](
            tex, Vector2.new(dust.position.X - Main.screenPosition.X, dust.position.Y - Main.screenPosition.Y - 6), dust.frame,
            XColor.White, 0, Vector2.new(4, 4), dust.scale, 0, 0);
        return dust.fadeIn !== 9;
    }
}

export class TestThrowDust extends ModDust {
    get Texture() { return 'TestMagentaDust'; }
    Update(dust) {
        calls.throwing++;
        throw new Error('teste: Update que lanca');
    }
}

export class TestConvertedDust extends ModDust {
    get Texture() { return 'TestMagentaDust'; }
    Update(dust) {
        calls.converted++;
        return true;
    }
}

const player = () => Main.player[Main.myPlayer];
const at = (dx, dy) => Vector2.new(player().Center.X + dx * 16, player().Center.Y + dy * 16);
const pool = () => bl.dusts.__pool();
const spawn = (type, dx, dy, vx = 0, vy = 0, scale = 1) => {
    const p = at(dx, dy);
    return Dust.NewDust(p, 0, 0, type, vx, vy, 0, XColor.White, scale);
};
const dustAt = (i) => Main.dust[i];
const typeOf = (cls) => ModContent.DustType(cls);

function poolConsistent(label) {
    const s = pool();
    check(label + ': pilha confere', s.active === s.activeCount && s.active + s.freeCount === 6000,
        'ativas ' + s.active + ', contador ' + s.activeCount + ', livres ' + s.freeCount);
}

// ---- as fases: start no 1o quadro, ends no ultimo (DoDraw conta quadros) ----
const state = {};
const phases = [
    {
        name: 'registro', frames: 2,
        start() {
            const classes = [TestPlainDust, TestSpawnDust, TestVanillaTexDust, TestStaticDust, TestSparkleDust,
                TestUpdateTypeDust, TestMidDust, TestAlphaDust, TestPreDrawDust, TestThrowDust, TestConvertedDust, TestMoverDust];
            const types = classes.map(typeOf);
            check('tipos depois do DustID.Count', types.every((t) => t >= DustID.Count), types.join(','));
            const sorted = [...types].sort((a, b) => a - b);
            check('tipos contiguos', sorted.every((t, i) => i === 0 || t === sorted[i - 1] + 1), sorted[0] + '..' + sorted[sorted.length - 1]);
            check('DustLoader.GetDust', DustLoader.GetDust(types[0]) instanceof TestPlainDust);
            check('bl.dusts.isModDust', bl.dusts.isModDust(types[0]) && !bl.dusts.isModDust(6));
            check('ModDust.getTypeByName', ModDust.getTypeByName('TestPlainDust') === types[0]);
            check('ModContent.DustType por nome', ModContent.DustType('TestStaticDust') === types[3]);
            const last = types[types.length - 1];
            const D = bl.classOf('Terraria', 'Dust');
            const Safety = bl.classOf('Terraria.GameContent', 'ChildSafety');
            check('Dust.StaticAlpha cresceu', D.StaticAlpha.length > last, D.StaticAlpha.length);
            check('Dust.StaticAlphaValue cresceu', D.StaticAlphaValue.length > last, D.StaticAlphaValue.length);
            check('StaticAlpha falso no tipo de mod', D.StaticAlpha[last] === false);
            check('ChildSafety.SafeDust cresceu e e seguro', Safety.SafeDust.length > last && Safety.SafeDust[last] === true,
                Safety.SafeDust.length);
            check('Texture2D com textura propria', DustLoader.GetDust(types[0]).Texture2D !== null);
            check('Texture2D da folha do jogo', DustLoader.GetDust(types[2]).Texture2D.Value === Terraria.GameContent.TextureAssets.Dust.Value);
            check('UpdateType vai ao nativo', DustLoader.GetDust(types[5]).UpdateType === DustID.Torch);
            log('maxDustToDraw ' + Main.maxDustToDraw + ', pilha ' + JSON.stringify(pool()));
        },
    },
    {
        name: 'nascimento', frames: 2,
        start() {
            const before = calls.spawn;
            const i = spawn(typeOf(TestSpawnDust), 0, -3, 1, 1);
            const d = dustAt(i);
            check('NewDust devolveu vaga', i >= 0 && i < 6000, i);
            check('tipo de mod na poeira', d.type === typeOf(TestSpawnDust), d.type);
            check('moldura no topo da textura', d.frame.X === 0 && d.frame.Y >= 0 && d.frame.Y < 30, d.frame.X + ',' + d.frame.Y);
            check('OnSpawn rodou', calls.spawn === before + 1 && d.noGravity === true && near(d.fadeIn, 3.25));
            const direct = Dust.NewDustDirect(at(1, -3), 0, 0, typeOf(TestSpawnDust), 0, 0, 0, XColor.White, 1);
            check('NewDustDirect chama OnSpawn', calls.spawn === before + 2 && direct.type === typeOf(TestSpawnDust));
            const perfect = Dust.NewDustPerfect(at(2, -3), typeOf(TestSpawnDust), Vector2.new(0, 0), 0, XColor.White, 1);
            check('NewDustPerfect chama OnSpawn', calls.spawn === before + 3 && near(perfect.fadeIn, 3.25));
            const v = dustAt(spawn(typeOf(TestVanillaTexDust), 3, -3));
            check('HackFrame do OnSpawn sobrevive', v.frame.X === (DustID.Electric * 10) % 1000, v.frame.X + ',' + v.frame.Y);
            const vanilla = dustAt(spawn(DustID.Torch, 4, -3));
            check('poeira do jogo nao passa pelo OnSpawn', calls.spawn === before + 3 && vanilla.type === DustID.Torch);
            [d, direct, perfect, v, vanilla].forEach((x) => Dust.SetNotActive(x));
        },
    },
    {
        name: 'atualizacao', frames: 20,
        start() {
            const plain = dustAt(spawn(typeOf(TestPlainDust), -4, -4, 2, 0, 1.2));
            const mid = dustAt(spawn(typeOf(TestMidDust), -4, -5, 2, 0, 1.2));
            // Com gravidade: so o freio de 0,99 mexe no X (o noGravity tambem multiplica por 0,92).
            [plain, mid].forEach((d) => { d.velocity.X = 2; d.velocity.Y = 0; d.noGravity = false; d.scale = 1.2; });
            const torch = dustAt(spawn(DustID.Torch, -6, -4, 1, -1, 1.1));
            const twin = dustAt(spawn(typeOf(TestUpdateTypeDust), -6, -5, 1, -1, 1.1));
            for (const d of [torch, twin]) {
                d.velocity.X = 1; d.velocity.Y = -1; d.scale = 1.1; d.noGravity = false; d.noLight = true;
                d.fadeIn = 0; d.alpha = 0; d.rotation = 0;
            }
            Object.assign(state, { plain, mid, torch, twin, updates0: pool().updates, plainX0: plain.position.X, mid0: calls.mid });
        },
        end() {
            const { plain, mid, torch, twin } = state;
            const updates = pool().updates - state.updates0;
            check('o jogo atualiza a poeira sem Update', plain.active && plain.velocity.X < 2 && plain.position.X > state.plainX0,
                'vx ' + plain.velocity.X.toFixed(4) + ' em ' + updates + ' atualizacoes');
            check('freio do jogo: vx = 2 * 0,99^n', near(plain.velocity.X, 2 * Math.pow(0.99, updates), 2e-3),
                plain.velocity.X.toFixed(5) + ' x ' + (2 * Math.pow(0.99, updates)).toFixed(5));
            check('MidUpdate chamado a cada atualizacao', calls.mid - state.mid0 === updates, (calls.mid - state.mid0) + ' de ' + updates);
            check('MidUpdate true desfaz o freio', mid.active && near(mid.velocity.X, 2, 1e-3), mid.velocity.X.toFixed(5));
            check('UpdateType: tipo volta ao de mod', twin.active && twin.type === typeOf(TestUpdateTypeDust), twin.type);
            check('UpdateType: igual a tocha do jogo', torch.active === twin.active && near(torch.velocity.Y, twin.velocity.Y, 1e-4) &&
                near(torch.scale, twin.scale, 1e-4) && near(torch.position.Y - twin.position.Y, 16, 1e-3),
                'vy ' + torch.velocity.Y.toFixed(4) + '/' + twin.velocity.Y.toFixed(4) + ' escala ' + torch.scale.toFixed(4) + '/' + twin.scale.toFixed(4));
            [plain, mid, torch, twin].forEach((x) => { if (x.active) Dust.SetNotActive(x); });
        },
    },
    {
        name: 'update false', frames: 30,
        start() {
            const d = dustAt(spawn(typeOf(TestStaticDust), 0, -6));
            d.position.X = Math.round(d.position.X);
            d.position.Y = Math.round(d.position.Y);
            Object.assign(state, { stat: d, x: d.position.X, y: d.position.Y, calls0: calls.staticUpdate, updates0: pool().updates });
        },
        end() {
            const d = state.stat;
            const updates = pool().updates - state.updates0;
            check('Update false: o jogo nao move', d.active && d.position.X === state.x && d.position.Y === state.y,
                d.position.X + ',' + d.position.Y);
            check('Update chamado a cada atualizacao', calls.staticUpdate - state.calls0 === updates,
                (calls.staticUpdate - state.calls0) + ' de ' + updates);
            d.active = false;   // o jeito do tModLoader
        },
    },
    {
        name: 'morte no Update', frames: 400,
        start() {
            bl.dusts.__repair();   // a fase anterior apagou com active = false fora dos hooks
            Object.assign(state, { repairs0: pool().repairs, spawned: 0, failed: 0, rounds: 0 });
        },
        tick(t) {
            if (t % 40 !== 0 || state.rounds >= 8) return;
            state.rounds++;
            for (let k = 0; k < 300; k++) {
                const i = spawn(typeOf(TestSparkleDust), (k % 30) - 15, -8 - Math.floor(k / 30), Math.sin(k), Math.cos(k), 1.5);
                if (i < 6000) state.spawned++; else state.failed++;
            }
        },
        end() {
            check('2400 poeiras nasceram', state.spawned === 2400 && state.failed === 0, state.spawned + ' ok, ' + state.failed + ' falharam');
            const s = pool();
            check('todas morreram pelo active = false', s.modActive === 0, s.modActive + ' de mod ativas');
            check('sem conserto geral da pilha (vaga devolvida na hora)', s.repairs === state.repairs0, (s.repairs - state.repairs0) + ' consertos');
            poolConsistent('morte no Update');
        },
    },
    {
        name: 'vazamento', frames: 3,
        start() {
            state.repairs0 = pool().repairs;
            const list = [];
            for (let k = 0; k < 100; k++) list.push(dustAt(spawn(DustID.Torch, (k % 20) - 10, -4)));
            list.forEach((d) => { d.active = false; });
            const s = pool();
            check('active = false direto descasa os contadores', s.activeCount - s.active >= 100, JSON.stringify(s));
        },
        end() {
            check('a pilha foi refeita', pool().repairs > state.repairs0, pool().repairs - state.repairs0);
            poolConsistent('vazamento');
        },
    },
    {
        name: 'revivida', frames: 3,
        start() {
            state.repairs0 = pool().repairs;
            const D = bl.classOf('Terraria', 'Dust');
            const top = D.freeDust[D.freeDustCount];
            top.active = true;   // revive uma vaga que esta na pilha
            state.top = top;
            const i = spawn(DustID.Torch, 0, -4);
            check('com o topo vivo o NewDust falha (o defeito)', i === 6000, i);
        },
        end() {
            check('a pilha foi refeita', pool().repairs > state.repairs0);
            const i = spawn(DustID.Torch, 0, -4);
            check('NewDust volta a funcionar', i < 6000, i);
            state.top.active = false;
            if (i < 6000) Dust.SetNotActive(dustAt(i));
            bl.dusts.__repair();
            poolConsistent('revivida');
        },
    },
    {
        name: 'GetAlpha', frames: 2,
        start() {
            const d = dustAt(spawn(typeOf(TestAlphaDust), 0, -4));
            const c = d['Color GetAlpha(Color newColor)'](XColor.White);
            check('GetAlpha(Color) do mod', c.R === 10 && c.G === 20 && c.B === 30 && c.A === 40, c.R + ',' + c.G + ',' + c.B + ',' + c.A);
            const ref = new Ref(XColor.White);
            d['void GetAlpha(ref Color newColor)'](ref);
            const r = ref.value;
            check('GetAlpha(ref Color) do mod', r.R === 10 && r.A === 40, r.R + ',' + r.A);
            d.fullBright = true;
            const w = d['Color GetAlpha(Color newColor)'](XColor.Black);
            check('fullBright passa por cima do mod', w.R === 255 && w.A === 255, w.R + ',' + w.A);
            const plain = dustAt(spawn(typeOf(TestPlainDust), 1, -4));
            plain.alpha = 0;
            const p = plain['Color GetAlpha(Color newColor)'](XColor.White);
            check('sem GetAlpha: a cor do jogo', p.R === 255 && p.A === 255, p.R + ',' + p.A);
            Dust.SetNotActive(d);
            Dust.SetNotActive(plain);
        },
    },
    {
        name: 'PreDraw', frames: 12,
        start() {
            state.list = [];
            for (let k = 0; k < 10; k++) {
                const d = dustAt(spawn(typeOf(TestPreDrawDust), k - 5, -7));
                d.noGravity = true; d.velocity.X = 0; d.velocity.Y = 0;
                d.fullBright = true;   // no escuro o jogo apaga a poeira de cor preta
                if (k % 2) d.fadeIn = 9;   // PreDraw devolve false
                state.list.push(d);
            }
            state.calls0 = calls.preDraw;
            state.draws0 = pool().draws;
        },
        end() {
            const draws = pool().draws - state.draws0;
            const n = calls.preDraw - state.calls0;
            check('PreDraw por poeira por quadro', n === draws * 10, n + ' chamadas em ' + draws + ' desenhos');
            state.list.forEach((d) => { if (d.active) Dust.SetNotActive(d); });
        },
    },
    {
        name: 'tipo trocado a mao', frames: 6,
        start() {
            const d = dustAt(spawn(DustID.Torch, 0, -5));
            d.type = typeOf(TestConvertedDust);
            Object.assign(state, { conv: d, calls0: calls.converted, updates0: pool().updates });
        },
        end() {
            const updates = pool().updates - state.updates0;
            check('poeira do jogo virada em mod entra no Update', calls.converted - state.calls0 === updates,
                (calls.converted - state.calls0) + ' de ' + updates);
            if (state.conv.active) Dust.SetNotActive(state.conv);
        },
    },
    {
        name: 'desenho', frames: 300,
        start() {
            state.grid = [];
            for (let gx = -6; gx <= 6; gx++) {
                for (let gy = 0; gy < 4; gy++) {
                    const cyan = dustAt(spawn(typeOf(TestStaticDust), gx * 1.5, -6 - gy * 1.5, 0, 0, 3));
                    cyan.scale = 3;
                    state.grid.push(cyan);
                    const magenta = dustAt(spawn(typeOf(TestMidDust), gx * 1.5 + 0.7, -6 - gy * 1.5, 0, 0, 3));
                    magenta.noGravity = true; magenta.fullBright = true; magenta.velocity.X = 0; magenta.velocity.Y = 0; magenta.scale = 3;
                    state.grid.push(magenta);
                }
            }
        },
        tick(t) {
            for (const d of state.grid) { if (d.active) { d.scale = 3; d.velocity.X = 0; d.velocity.Y = 0; } }
            if (t === 58) log('desenho: ' + state.grid.filter((d) => d.active).length + ' ativas, pilha ' + JSON.stringify(pool()));
            if (t === 60) log('SHOT desenho');
            if (t === 180) state.grid.forEach((d) => { if (d.active) Dust.SetNotActive(d); });
            if (t === 200) log('SHOT vazio');
        },
        end() {
            poolConsistent('desenho');
        },
    },
    {
        name: 'cores', frames: 200,
        start() {
            state.cores = [];
            for (let k = 0; k < 6; k++) {
                const ref = dustAt(spawn(typeOf(TestPreDrawDust), -6 + k * 0.6, -8, 0, 0, 4));
                ref.fadeIn = 9; ref.noGravity = true; ref.fullBright = true;
                const plain = dustAt(spawn(typeOf(TestPlainDust), 3 + k * 0.6, -8, 0, 0, 4));
                plain.noGravity = true; plain.fullBright = true;
                state.cores.push(ref, plain);
            }
        },
        tick(t) {
            for (const d of state.cores) { if (d.active) { d.scale = 4; d.velocity.X = 0; d.velocity.Y = 0; } }
            if (t === 20) {
                const p = state.cores[1];
                log('cores: tela ' + Main.screenPosition.X.toFixed(0) + ',' + Main.screenPosition.Y.toFixed(0) + ' poeira ' + p.position.X.toFixed(0) + ',' + p.position.Y.toFixed(0)
                    + ' cor ' + p.color.R + ',' + p.color.G + ',' + p.color.B + ',' + p.color.A + ' alpha ' + p.alpha + ' shader ' + p.shader);
            }
            if (t === 30) log('SHOT cores');
            if (t === 150) state.cores.forEach((d) => { if (d.active) Dust.SetNotActive(d); });
        },
    },
    {
        name: 'custo', frames: 560,
        start() { state.round = 0; },
        tick(t) {
            const kinds = [['vanilla', DustID.Torch], ['mod sem JS', typeOf(TestPlainDust)], ['mod com Update vazio', typeOf(TestStaticDust)],
                ['mod com Update que move', typeOf(TestMoverDust)]];
            const k = Math.floor(t / 140);
            const local = t % 140;
            if (k >= kinds.length) return;
            if (local === 0) {
                state.batch = [];
                for (let n = 0; n < 1500; n++) {
                    const d = dustAt(spawn(kinds[k][1], (n % 50) - 25, -4 - Math.floor(n / 50) * 0.5, 0, 0, 1));
                    d.noGravity = true;
                    d.fullBright = true;
                    state.batch.push(d);
                }
            }
            for (const d of state.batch) { if (d.active) d.scale = 1; }
            if (local === 20) state.mark = pool();
            if (local === 60) log('SHOT custo' + k);
            if (local === 120) {
                const a = state.mark, b = pool();
                const u = Math.max(1, b.updates - a.updates), dr = Math.max(1, b.draws - a.draws);
                log('custo ' + kinds[k][0] + ' (1500 poeiras, ' + b.modActive + ' de mod): update '
                    + ((b.updateNs - a.updateNs) / u / 1000).toFixed(0) + ' us, desenho ' + ((b.drawNs - a.drawNs) / dr / 1000).toFixed(0)
                    + ' us, JS ' + ((b.jsNs - a.jsNs) / u / 1000).toFixed(0) + ' us por quadro');
            }
            if (local === 130) state.batch.forEach((d) => { if (d.active) Dust.SetNotActive(d); });
        },
        end() {
            poolConsistent('custo');
        },
    },
    {
        name: 'erro no Update', frames: 10,
        start() {
            const bad = dustAt(spawn(typeOf(TestThrowDust), 0, -5));
            const good = dustAt(spawn(typeOf(TestStaticDust), 1, -5));
            Object.assign(state, { bad, good, throws0: calls.throwing, static0: calls.staticUpdate, updates0: pool().updates });
        },
        end() {
            const updates = pool().updates - state.updates0;
            check('o Update que lanca e chamado de novo', calls.throwing - state.throws0 === updates, (calls.throwing - state.throws0) + ' de ' + updates + ', ativa ' + state.bad.active + ' tipo ' + state.bad.type);
            check('as outras poeiras seguem', calls.staticUpdate - state.static0 === updates, (calls.staticUpdate - state.static0) + ' de ' + updates + ', ativa ' + state.good.active);
            check('a poeira com erro segue viva (o jogo a atualiza)', state.bad.active === true || state.bad.scale < 0.2);
            [state.bad, state.good].forEach((d) => { if (d.active) Dust.SetNotActive(d); });
        },
    },
];

let frame = 0, index = 0, local = 0, done = false;

function step() {
    if (frame < 90) return;
    if (index >= phases.length) {
        const unexpected = modErrors.filter((e) => !(e.startsWith('error in TestThrowDust.Update')));
        check('nenhum erro de mod', unexpected.length === 0, unexpected.join(' | '));
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    const p = phases[index];
    if (local === 0) {
        log('fase ' + p.name);
        if (p.start) p.start();
    }
    if (p.tick) p.tick(local);
    local++;
    if (local >= p.frames) {
        if (p.end) p.end();
        index++;
        local = 0;
    }
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frame++;
        const p = player();
        p.statLife = p.statLifeMax2;
        try {
            step();
        } catch (e) {
            fails++;
            log('erro no quadro ' + frame + ' (fase ' + (phases[index] ? phases[index].name : '?') + '): ' + e + (e.stack ? ' ' + e.stack : ''));
            index++;
            local = 0;
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestModDust extends Mod {}
