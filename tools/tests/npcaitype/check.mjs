import assert from 'node:assert/strict';
import { createRuntime, FIRST_NPC, AI } from './runtime.mjs';

let checks = 0;

function test(name, body) {
    body();
    checks++;
    console.log('PASS ' + name);
}

function setup(reverse = false) {
    const r = createRuntime(reverse);
    r.run(`
        class OnlyType extends ModNPC {
            SetDefaults(npc) { this.AIType = 2; npc.aiStyle = 2; }
        }
        globalThis.type = ModNPC.register(OnlyType);
    `);
    return r;
}

test('base default and AIType-only registration work without AI overrides', () => {
    const r = setup();
    assert.equal(new r.api.ModNPC().AIType, 0);
    const npc = r.npc(FIRST_NPC);
    r.vanilla(actual => assert.equal(actual.type, 2));
    r.call(npc);
    assert.equal(npc.type, FIRST_NPC);
    assert.equal(npc.netID, FIRST_NPC);
    assert.equal(npc.aiStyle, 2);
    assert.equal(r.entry(AI).entered, 1);
});

test('native filter skips vanilla NPCs when there are no global AI hooks', () => {
    const r = setup();
    const npc = r.npc(2);
    let calls = 0;
    r.vanilla(actual => { calls++; assert.equal(actual.type, 2); });
    r.call(npc);
    assert.equal(calls, 1);
    assert.equal(r.entry(AI).entered, 0);
});

test('unknown mod type passes through once', () => {
    const r = setup();
    const npc = r.npc(FIRST_NPC + 50);
    let calls = 0;
    r.vanilla(actual => { calls++; assert.equal(actual.type, npc.type); });
    r.call(npc);
    assert.equal(calls, 1);
});

test('global with only unrelated hooks keeps the vanilla AI native filter', () => {
    const r = setup();
    r.run('class Unrelated extends GlobalNPC { SetDefaults(npc) {} }; GlobalNPC.register(Unrelated);');
    r.call(r.npc(2));
    assert.equal(r.entry(AI).hooks.length, 1);
    assert.equal(r.entry(AI).entered, 0);
    assert.equal(r.flags.get('npc.AI.local'), true);
});

test('inherited SetDefaults enables AIType and installs the shared hook once', () => {
    const r = setup();
    r.run('class Child extends OnlyType {}; ModNPC.register(Child);');
    const npc = r.npc(FIRST_NPC + 1);
    r.vanilla(actual => assert.equal(actual.type, 2));
    r.call(npc);
    assert.equal(npc.type, FIRST_NPC + 1);
    assert.equal(r.entry(AI).hooks.length, 1);
});

test('AIType declared as a class field is cloned from the registered template', () => {
    const r = createRuntime();
    r.run('class FieldType extends ModNPC { AIType = 4; }; ModNPC.register(FieldType);');
    const npc = r.npc(FIRST_NPC);
    r.vanilla(actual => assert.equal(actual.type, 4));
    r.call(npc);
    assert.equal(npc.type, FIRST_NPC);
    assert.equal(r.api.ModNPC.getModNPC(FIRST_NPC).AIType, 4);
});

for (const value of [0, -1, -3, undefined, null]) {
    test('AIType=' + value + ' leaves the vanilla call type unchanged', () => {
        const r = setup();
        const npc = r.npc(FIRST_NPC);
        npc.ModNPC.AIType = value;
        r.vanilla(actual => assert.equal(actual.type, FIRST_NPC));
        r.call(npc);
        assert.equal(npc.type, FIRST_NPC);
    });
}

test('AIType is read per entity and per tick; template state does not leak', () => {
    const r = setup();
    const a = r.npc(FIRST_NPC);
    const b = r.npc(FIRST_NPC);
    const seen = [];
    a.ModNPC.AIType = 4;
    b.ModNPC.AIType = 6;
    r.vanilla(actual => seen.push(actual.type));
    r.call(a);
    r.call(b);
    a.ModNPC.AIType = 8;
    r.call(a);
    assert.deepEqual(seen, [4, 6, 8]);
    assert.notEqual(a.ModNPC, b.ModNPC);
    assert.equal(a.ModNPC.NPC, a);
    assert.equal(r.api.ModNPC.getModNPC(FIRST_NPC).AIType, 0);
    assert.equal(a.type, FIRST_NPC);
    assert.equal(b.type, FIRST_NPC);
});

test('aiStyle, AnimationType, netID and gameplay changes are independent of AIType', () => {
    const r = setup();
    const npc = r.npc(FIRST_NPC);
    npc.ModNPC.AnimationType = 7;
    npc.netID = FIRST_NPC + 100;
    npc.aiStyle = -1;
    r.vanilla(actual => {
        assert.equal(actual.type, 2);
        assert.equal(actual.aiStyle, -1);
        actual.ai[0] = 31;
        actual.active = false;
    });
    r.call(npc);
    assert.equal(npc.ai[0], 31);
    assert.equal(npc.active, false);
    assert.equal(npc.netID, FIRST_NPC + 100);
    assert.equal(npc.ModNPC.AnimationType, 7);
    assert.equal(npc.type, FIRST_NPC);
});

function withHooks(r) {
    r.run(`
        globalThis.events = [];
        class WithHooks extends ModNPC {
            SetDefaults(npc) { this.AIType = 2; }
            PreAI(npc) { events.push(['pre', npc.type]); return this.pre?.(npc); }
            AI(npc) { events.push(['ai', npc.type]); this.ai?.(npc); }
            PostAI(npc) { events.push(['post', npc.type]); this.post?.(npc); }
        }
        globalThis.hooked = ModNPC.register(WithHooks);
    `);
    const npc = r.npc(r.run('hooked'));
    r.vanilla(actual => r.run('events').push(['vanilla', actual.type]));
    return npc;
}

const events = r => Array.from(r.run('events'), event => Array.from(event));

test('PreAI and mod AI/PostAI see the real type; only vanilla sees AIType', () => {
    const r = createRuntime();
    const npc = withHooks(r);
    r.call(npc);
    assert.deepEqual(events(r), [['pre', npc.type], ['vanilla', 2], ['ai', npc.type], ['post', npc.type]]);
});

test('PreAI may select a different AIType for this same tick', () => {
    const r = createRuntime();
    const npc = withHooks(r);
    npc.ModNPC.pre = () => { npc.ModNPC.AIType = 6; };
    r.call(npc);
    assert.equal(events(r)[1][1], 6);
});

test('PreAI false skips vanilla and mod AI while retaining PostAI', () => {
    const r = createRuntime();
    const npc = withHooks(r);
    npc.ModNPC.pre = () => false;
    r.call(npc);
    assert.deepEqual(events(r), [['pre', npc.type], ['post', npc.type]]);
});

test('an exception in vanilla restores type and the following call recovers', () => {
    const r = setup();
    const npc = r.npc(FIRST_NPC);
    const error = Error('native call probe');
    r.vanilla(() => { throw error; });
    assert.throws(() => r.call(npc), actual => actual === error);
    assert.equal(npc.type, FIRST_NPC);
    let calls = 0;
    r.vanilla(actual => { calls++; assert.equal(actual.type, 2); });
    r.call(npc);
    assert.equal(npc.type, FIRST_NPC);
    assert.equal(calls, 1);
});

for (const hook of ['pre', 'ai', 'post']) {
    test('error in ' + hook + ' is reported with the real type and does not corrupt later ticks', () => {
        const r = createRuntime();
        const npc = withHooks(r);
        npc.ModNPC[hook] = actual => {
            assert.equal(actual.type, FIRST_NPC);
            throw Error('expected ' + hook);
        };
        r.call(npc);
        assert.equal(npc.type, FIRST_NPC);
        assert.equal(r.errors.length, 1);
        delete npc.ModNPC[hook];
        r.call(npc);
        assert.equal(npc.type, FIRST_NPC);
        assert.equal(events(r).length, 8);
    });
}

test('type changes in vanilla are restored only when AIType is active', () => {
    const r = setup();
    const npc = r.npc(FIRST_NPC);
    r.vanilla(actual => { actual.type = 21; });
    r.call(npc);
    assert.equal(npc.type, FIRST_NPC);
    npc.ModNPC.AIType = 0;
    r.call(npc);
    assert.equal(npc.type, 21);
});

for (const reverse of [false, true]) {
    for (const globalFirst of [false, true]) {
        const order = `chain=${reverse ? 'last-first' : 'first-last'}, globalFirst=${globalFirst}`;

        function combined() {
            const r = createRuntime(reverse);
            const global = `
                class ProbeGlobal extends GlobalNPC {
                    get InstancePerEntity() { return true; }
                    AppliesToEntity(npc) { return npc.type >= FIRST_NPC; }
                    PreAI(npc) { events.push(['global.pre', npc.type]); return this.allow; }
                    AI(npc) { events.push(['global.ai', npc.type]); }
                    PostAI(npc) { events.push(['global.post', npc.type]); }
                }
                GlobalNPC.register(ProbeGlobal);
            `;
            r.run('globalThis.events = [];');
            if (globalFirst) r.run(global);
            const npc = withHooks(r);
            if (!globalFirst) r.run(global);
            return { r, npc };
        }

        test('global/mod ordering and cached global instance survive the swap: ' + order, () => {
            const { r, npc } = combined();
            const inst = r.api.globalNPCs.Find(npc, 'ProbeGlobal');
            inst.allow = true;
            r.call(npc);
            assert.deepEqual(events(r), [
                ['global.pre', FIRST_NPC], ['pre', FIRST_NPC], ['vanilla', 2],
                ['ai', FIRST_NPC], ['global.ai', FIRST_NPC], ['post', FIRST_NPC], ['global.post', FIRST_NPC],
            ]);
            assert.equal(r.api.globalNPCs.Find(npc, 'ProbeGlobal'), inst);
            assert.equal(r.entry(AI).entered, 1, 'one JS AI dispatch');
        });

        test('mod veto also suppresses global AI, preserving both PostAI hooks: ' + order, () => {
            const { r, npc } = combined();
            npc.ModNPC.pre = () => false;
            r.call(npc);
            assert.deepEqual(events(r), [
                ['global.pre', FIRST_NPC], ['pre', FIRST_NPC], ['post', FIRST_NPC], ['global.post', FIRST_NPC],
            ]);
        });

        test('global veto skips mod PreAI and both AI stages: ' + order, () => {
            const { r, npc } = combined();
            r.api.globalNPCs.Find(npc, 'ProbeGlobal').allow = false;
            r.call(npc);
            assert.deepEqual(events(r), [
                ['global.pre', FIRST_NPC], ['post', FIRST_NPC], ['global.post', FIRST_NPC],
            ]);
        });
    }
}

test('every global PreAI runs even if an earlier global vetoes', () => {
    const r = createRuntime();
    const npc = withHooks(r);
    r.run(`
        class Veto extends GlobalNPC { PreAI(npc) { events.push(['veto', npc.type]); return false; } }
        class Observer extends GlobalNPC { PreAI(npc) { events.push(['observer', npc.type]); return true; } }
        GlobalNPC.register(Veto);
        GlobalNPC.register(Observer);
    `);
    r.call(npc);
    assert.deepEqual(events(r), [['veto', FIRST_NPC], ['observer', FIRST_NPC], ['post', FIRST_NPC]]);
});

test('global-only AI works on vanilla NPCs without registering any ModNPC', () => {
    const r = createRuntime();
    r.run(`
        globalThis.events = [];
        class VanillaGlobal extends GlobalNPC {
            PreAI(npc) { events.push(['pre', npc.type]); return true; }
            AI(npc) { events.push(['ai', npc.type]); }
            PostAI(npc) { events.push(['post', npc.type]); }
        }
        GlobalNPC.register(VanillaGlobal);
    `);
    r.vanilla(npc => r.run('events').push(['vanilla', npc.type]));
    r.call(r.npc(2));
    assert.deepEqual(events(r), [['pre', 2], ['vanilla', 2], ['ai', 2], ['post', 2]]);
});

test('global hook errors are contained and the restored type reaches remaining hooks', () => {
    const r = createRuntime();
    const npc = withHooks(r);
    r.run(`
        class Broken extends GlobalNPC {
            PreAI() { throw Error('pre'); }
            AI() { throw Error('ai'); }
            PostAI() { throw Error('post'); }
        }
        class Observer extends GlobalNPC { PostAI(npc) { events.push(['observer', npc.type]); } }
        GlobalNPC.register(Broken);
        GlobalNPC.register(Observer);
    `);
    r.call(npc);
    assert.equal(r.errors.length, 3);
    assert.deepEqual(events(r).at(-1), ['observer', FIRST_NPC]);
    assert.equal(npc.type, FIRST_NPC);
});

for (const mode of [0, 1, 2]) {
    test('1000 ticks preserve instance identity and type for netMode ' + mode, () => {
        const r = setup();
        r.mode = mode;
        const npc = r.npc(FIRST_NPC);
        const inst = npc.ModNPC;
        let calls = 0;
        r.vanilla(actual => { calls++; assert.equal(actual.type, 2); actual.ai[0]++; });

        for (let i = 0; i < 1000; i++) {
            r.call(npc);
            assert.equal(npc.type, FIRST_NPC);
            assert.equal(npc.ModNPC, inst);
        }

        assert.equal(calls, 1000);
        assert.equal(npc.ai[0], 1000);
    });
}

console.log(`npcaitype FIM: ${checks} testes, tudo ok`);
