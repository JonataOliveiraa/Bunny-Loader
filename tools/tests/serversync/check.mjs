import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const file = new URL('../../../app/src/main/cpp/script/js/mod/Loaders/ServerSyncLoader.js', import.meta.url);
const sendKey = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
const processKey = 'void ProcessData(byte[] messageData, int length, out int messageType)';
const bootKey = 'void BootPlayer(int plr, NetworkText msg)';

function runtime(mods, mode) {
    const hooks = new Map(), once = new Set();
    const sent = [], kicked = [], offers = [];
    const method = key => ({ hook: fn => hooks.set(key, fn) });
    const main = { netMode: mode, myPlayer: 0, clientUUID: 'original-uuid', ActivePlayerFileData: { Path: '/Players/Test.plr' } };
    const sandbox = {
        Terraria: {
            Main: main,
            Netplay: { ServerIPText: '127.0.0.1', ListenPort: 7777 },
            NetMessage: { [sendKey]: method(sendKey), [bootKey]: (player, reason) => kicked.push({ player, reason }) },
            MessageBuffer: { [processKey]: method(processKey) },
            Localization: { NetworkText: { 'NetworkText FromLiteral(string text)': text => text } },
        },
        HostSettingsMenu: { SyncMods: true, AllowTextures: true },
        ModLoader: { Mods: mods },
        ModLocalization: { ActiveCultureName: 'pt-BR' },
        ModNet: { Install() {}, Sent: () => ({}), Received: () => ({}), Send: (envelope, to) => sent.push({ envelope, to }) },
        Hooks: { Once(key, fn) { if (!once.has(key)) { once.add(key); fn(); } } },
        Safe: { Run: (_, fn) => fn() },
        ServerSyncMenu: { Open() {} },
        bl: { log() {}, __texturePacks: () => [], __offerServerSync: text => { offers.push(JSON.parse(text)); return true; }, classOf: () => ({ 'void HostServer()': method('host') }) },
    };
    const context = vm.createContext(sandbox);
    vm.runInContext(fs.readFileSync(file, 'utf8') + '\nglobalThis.Loader = ServerSyncLoader;', context);
    sandbox.Loader.Install();
    if (mode !== 1) hooks.get('host')(() => {});
    return { ...sandbox, hooks, sent, kicked, offers, main };
}

const mod = version => ({ uuid: '11111111-1111-1111-1111-111111111111', id: 'probe', name: 'Probe', version });
function hello(client) {
    let packet;
    client.hooks.get(sendKey)((type) => {
        if (type !== 68) return;
        const text = Buffer.from(client.main.clientUUID, 'utf8');
        const size = [];
        for (let n = text.length; ; n >>>= 7) {
            size.push((n & 127) | (n >= 128 ? 128 : 0));
            if (n < 128) break;
        }
        packet = Uint8Array.from([68, ...size, ...text]);
    }, 6, -1, -1, null, 0, 0, 0, 0, 0, 0, 0);
    assert.equal(client.main.clientUUID, 'original-uuid');
    return packet;
}
function connect(host, client) {
    const packet = hello(client);
    let original = 0;
    host.hooks.get(processKey)(() => { original++; }, { whoAmI: 1 }, packet, packet.length, { value: 0 });
    assert.equal(original, 0);
}

let checks = 0;
function test(name, fn) { fn(); checks++; console.log('PASS ' + name); }

test('different versions of the same uid are rejected and offer the host version', () => {
    const host = runtime([mod('1.1.0')], 3), client = runtime([mod('1.0.0')], 1);
    connect(host, client);
    assert.equal(host.kicked.length, 1);
    assert.equal(host.sent[0].envelope.mods[0].version, '1.1.0');
    client.Loader.Receive(host.sent[0].envelope, 256);
    assert.equal(client.offers[0].mods[0].version, '1.1.0');
});
test('equal versions join without sync', () => {
    const host = runtime([mod('1.1.0')], 3);
    connect(host, runtime([mod('1.1.0')], 1));
    assert.equal(host.kicked.length, 0);
});
test('missing versions cannot masquerade as compatible', () => {
    const host = runtime([mod('')], 3);
    connect(host, runtime([mod('')], 1));
    assert.equal(host.kicked.length, 1);
});
test('a newer client must sync to the host version too', () => {
    const host = runtime([mod('1.0.0')], 3);
    connect(host, runtime([mod('1.1.0')], 1));
    assert.equal(host.kicked.length, 1);
    assert.equal(host.sent[0].envelope.mods[0].version, '1.0.0');
});
test('load order still matters', () => {
    const a = mod('1'), b = { ...mod('1'), uuid: '22222222-2222-2222-2222-222222222222' };
    const host = runtime([a, b], 3);
    connect(host, runtime([b, a], 1));
    assert.equal(host.kicked.length, 1);
});
test('disabling sync preserves the explicit host option', () => {
    const host = runtime([mod('1.1.0')], 3);
    host.HostSettingsMenu.SyncMods = false;
    connect(host, runtime([mod('1.0.0')], 1));
    assert.equal(host.kicked.length, 0);
});
test('normal client UUID packets pass through untouched', () => {
    const host = runtime([], 3);
    const bytes = Uint8Array.from([68, 3, 97, 98, 99]);
    let called = 0;
    host.hooks.get(processKey)(() => called++, { whoAmI: 1 }, bytes, bytes.length, {});
    assert.equal(called, 1);
});
console.log('PASS server sync handshake: ' + checks + ' cases');
