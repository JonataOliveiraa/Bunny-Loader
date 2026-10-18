// Poeiras, UI e recursos do Example Mod no multijogador, com um host (netMode 3)
// e um cliente (netMode 1), os dois com este teste e o Example Mod ligados:
//  - os tipos das ModDust são os mesmos nos dois aparelhos;
//  - o projétil de um jogador solta a Sparkle no aparelho do outro;
//  - as frutas/cristais (ExampleStatIncreasePlayer) chegam ao outro lado, na
//    entrada (SyncPlayer) e depois (SendClientChanges), e mudam a vida máxima
//    que ele calcula;
//  - o número da cura do recurso (ExampleResourceEffect) aparece no outro;
//  - no cliente, a tela /coins conta moedas soltas pelo host, segura o toque
//    (não usa o item) e fecha; a arma do recurso gasta o recurso e o foguete
//    dela chega ao host.
// Loga "mpexample host|cliente ...", TAP/SHOT/UISCALE para o roteiro e FIM.
const Main = Terraria.Main;
const { ItemID, ProjectileID } = Terraria.ID;
const NewItem = Terraria.Item['int NewItem(int X, int Y, int Width, int Height, int Type, int Stack, bool noBroadcast, int pfix, NewItemOwnership ownership)'];
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const DUSTS = ['Sparkle', 'ExampleBubble', 'ExampleSolutionDust', 'ExampleCustomDrawDust', 'ExampleAdvancedDust'];

let mod = null, role = '', fails = 0, frame = 0, index = 0, local = 0, done = false, remote = -1;
const inbox = [], syncs = [], joinSyncs = [], modErrors = [], pickups = [];
const state = {};
const log = (s) => bl.log('mpexample ' + role + ' ' + s);
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}
const blError = bl.error;
bl.error = (text) => {
    modErrors.push(String(text).split('\n')[0]);
    return blError(text);
};

function send(kind, ...values) {
    const p = mod.GetPacket();
    p.Write(kind);
    for (const v of values) p.Write(v);
    p.Send(role === 'host' ? remote : -1);
}
function take(kind) {
    const i = inbox.findIndex((m) => m.kind === kind);
    return i < 0 ? null : inbox.splice(i, 1)[0];
}

const me = () => Main.player[Main.myPlayer];
const stats = (p) => p.GetModPlayer('ExampleStatIncreasePlayer');
const resource = (p) => p.GetModPlayer('ExampleResourcePlayer');
const coinsSystem = () => ModContent.GetContent(ModSystem).find((s) => s.constructor.name === 'ExampleCoinsUISystem');
const caller = { CommandType: CommandType.Chat, get Player() { return me(); }, Reply(text) { log('resposta: ' + text); } };
const tap = (x, y) => log('TAP ' + x + ' ' + y);

function countDust(type) {
    let n = 0;
    const all = Main.dust;
    for (let i = 0; i < 6000; i++) {
        const d = all[i];
        if (d.active && d.type === type) n++;
    }
    return n;
}
function findProjectile(type, owner) {
    const all = Main.projectile;
    for (let i = 0; i < 1000; i++) {
        const p = all[i];
        if (p.active && p.type === type && (owner < 0 || p.owner === owner)) return p;
    }
    return null;
}
function hasHealText(text) {
    const all = Main.combatText;
    for (let i = 0; i < 100; i++) {
        const c = all[i];
        if (c.active && c.text === text && c.color.R === 187 && c.color.G === 91) return true;
    }
    return false;
}
function spawnShot(owner) {
    const p = me();
    return NewProjectile(null, p.Center.X, p.Center.Y - 40, 0, -1.5, ModContent.ProjectileType('ExampleSentryShot'), 1, 0, owner, 0, 0, 0, null);
}
// Conta a Sparkle (a cada 10 quadros: varrer 6000 poeiras pela ponte custa).
function watchSparkle(t, key) {
    if (t % 10 === 0) state[key] = Math.max(state[key] || 0, countDust(ModContent.DustType('Sparkle')));
}

const wait = (name, until, then, timeout = 1800) => ({ name, until, then, timeout });

const hostPhases = [
    wait('espera o cliente', () => take('hello'), (m) => {
        remote = m.from;
        state.clientPlayer = m.values[0];
        const mine = DUSTS.map((n) => ModContent.DustType(n)).join(',');
        check('o cliente e o jogador que mandou', state.clientPlayer === remote, state.clientPlayer + ' / ' + remote);
        check('tipos das ModDust iguais nos dois', m.values[1] === mine, 'host ' + mine + ', cliente ' + m.values[1]);
        check('SyncPlayer do cliente chegou na entrada', syncs.some((s) => s.who === remote), JSON.stringify(syncs));
        send('hello', Main.myPlayer);
    }, 7200),
    wait('stats do cliente', () => take('stats'), (m) => { state.clientStats = m.values; }),
    {
        name: 'confere stats do cliente', frames: 60,
        end() {
            const p = Main.player[remote], s = stats(p);
            check('frutas/cristais do cliente no host', s.exampleLifeFruits === 2 && s.exampleManaCrystals === 1, s.exampleLifeFruits + '/' + s.exampleManaCrystals);
            check('vida maxima do cliente no host', p.statLifeMax2 === state.clientStats[0], p.statLifeMax2 + ' / cliente ' + state.clientStats[0]);
            check('mana maxima do cliente no host', p.statManaMax2 === state.clientStats[1], p.statManaMax2 + ' / cliente ' + state.clientStats[1]);
        },
    },
    {
        name: 'stats do host', frames: 30,
        start() {
            state.hostLife = me().statLifeMax2;
            stats(me()).exampleLifeFruits = 1;
        },
        end() {
            check('a fruta sobe a vida do host', me().statLifeMax2 === state.hostLife + 10, state.hostLife + ' -> ' + me().statLifeMax2);
            send('hstats', me().statLifeMax2, me().statManaMax2);
        },
    },
    wait('cura do cliente', () => take('healed'), () => {}),
    {
        name: 'numero da cura do cliente', frames: 30,
        tick() { if (hasHealText('7')) state.sawClientHeal = true; },
        end() {
            check('o numero da cura do cliente aparece no host', !!state.sawClientHeal);
            resource(me()).HealExampleResource(me(), 9);
            send('hhealed');
        },
    },
    {
        name: 'tiro do host', frames: 120,
        start() {
            state.hostShot = spawnShot(Main.myPlayer);
            send('hshot');
        },
        tick(t) { watchSparkle(t, 'hostSparkle'); },
        end() { check('o tiro do host solta Sparkle no host', state.hostSparkle > 0, state.hostSparkle); },
    },
    wait('tiro do cliente', () => take('cshot'), () => { state.sparkleFromClient = 0; }),
    {
        name: 'Sparkle do tiro do cliente', frames: 120,
        tick(t) {
            if (findProjectile(ModContent.ProjectileType('ExampleSentryShot'), remote)) state.sawClientShot = true;
            watchSparkle(t, 'sparkleFromClient');
        },
        end() {
            check('o tiro do cliente chega ao host', !!state.sawClientShot);
            check('o tiro do cliente solta Sparkle no host', state.sparkleFromClient > 0, state.sparkleFromClient);
        },
    },
    wait('tela de moedas aberta', () => take('coinsopen'), () => {
        // Os dois nascem no mesmo lugar: longe, o host não pega as moedas antes.
        const h = me();
        h.position.X -= 1200;
        h.fallStart = Math.floor(h.position.Y / 16);
        const c = Main.player[remote].Center;
        state.coinItem = NewItem(Math.floor(c.X), Math.floor(c.Y), 1, 1, ItemID.GoldCoin, 3, false, 0, 0);
        const w = Main.item[state.coinItem];
        log('moedas: item ' + state.coinItem + ' tipo ' + w.type + ' em ' + Math.round(w.position.X) + ',' + Math.round(w.position.Y) + ', cliente em ' + Math.round(c.X) + ',' + Math.round(c.Y));
    }),
    wait('tiro da arma do recurso', () => {
        if (frame % 5 === 0 && findProjectile(ProjectileID.VortexBeaterRocket, remote)) state.sawRocket = true;
        return take('fired');
    }, () => {}, 3600),
    {
        name: 'foguete do cliente', frames: 60,
        tick() { if (findProjectile(ProjectileID.VortexBeaterRocket, remote)) state.sawRocket = true; },
        end() { check('o foguete da arma do recurso do cliente chega ao host', !!state.sawRocket); },
    },
    {
        name: 'captura', frames: 40,
        tick(t) { if (t === 20) log('SHOT host'); },
    },
    wait('fim do cliente', () => take('end'), (m) => {
        check('o cliente terminou sem falhas', m.values[0] === 0, m.values[0]);
        stats(me()).exampleLifeFruits = 0;
        check('nenhum erro de mod', modErrors.length === 0, modErrors.join(' | '));
    }, 3600),
];

const clientPhases = [
    {
        name: 'entrada', frames: 150,
        end() {
            check('SyncPlayer do cliente na entrada (newPlayer)', joinSyncs.some((s) => s.newPlayer && s.who === Main.myPlayer), JSON.stringify(joinSyncs));
            send('hello', Main.myPlayer, DUSTS.map((n) => ModContent.DustType(n)).join(','));
        },
    },
    wait('resposta do host', () => take('hello'), (m) => { remote = m.values[0]; }),
    {
        name: 'frutas do cliente', frames: 60,
        start() {
            state.life = me().statLifeMax2;
            state.mana = me().statManaMax2;
            stats(me()).exampleLifeFruits = 2;
            stats(me()).exampleManaCrystals = 1;
        },
        end() {
            check('2 frutas: +20 de vida', me().statLifeMax2 === state.life + 20, state.life + ' -> ' + me().statLifeMax2);
            check('1 cristal: +10 de mana', me().statManaMax2 === state.mana + 10, state.mana + ' -> ' + me().statManaMax2);
            send('stats', me().statLifeMax2, me().statManaMax2);
        },
    },
    wait('stats do host', () => take('hstats'), (m) => { state.hostStats = m.values; }),
    {
        name: 'confere stats do host', frames: 90,
        end() {
            const p = Main.player[remote];
            check('fruta do host no cliente', stats(p).exampleLifeFruits === 1, stats(p).exampleLifeFruits);
            check('vida maxima do host no cliente', p.statLifeMax2 === state.hostStats[0], p.statLifeMax2 + ' / host ' + state.hostStats[0]);
            resource(me()).HealExampleResource(me(), 7);
            check('o numero da cura aparece no proprio cliente', hasHealText('7'));
            send('healed');
        },
    },
    wait('cura do host', () => take('hhealed'), () => { state.sawHostHeal = hasHealText('9'); }),
    {
        name: 'numero da cura do host', frames: 30,
        tick() { if (hasHealText('9')) state.sawHostHeal = true; },
        end() { check('o numero da cura do host aparece no cliente', !!state.sawHostHeal); },
    },
    wait('tiro do host', () => take('hshot'), () => { state.sparkleFromHost = 0; }),
    {
        name: 'Sparkle do tiro do host', frames: 120,
        tick(t) {
            if (findProjectile(ModContent.ProjectileType('ExampleSentryShot'), remote)) state.sawHostShot = true;
            watchSparkle(t, 'sparkleFromHost');
            if (t === 30) log('SHOT poeira');
        },
        end() {
            check('o tiro do host chega ao cliente', !!state.sawHostShot);
            check('o tiro do host solta Sparkle no cliente', state.sparkleFromHost > 0, state.sparkleFromHost);
            spawnShot(Main.myPlayer);
            send('cshot');
        },
    },
    {
        name: 'tiro do cliente', frames: 150,
        tick(t) { watchSparkle(t, 'clientSparkle'); },
        end() { check('o tiro do cliente solta Sparkle no cliente', state.clientSparkle > 0, state.clientSparkle); },
    },
    {
        name: 'abrir moedas', frames: 30,
        start() {
            check('o comando /coins abre a tela', CommandLoader.Handle('/coins', caller) === true);
            state.coins = coinsSystem().exampleCoinsUI.MoneyDisplay.collectedCoins;
        },
        end() {
            log('UISCALE ' + UserInterface.ActiveInstance.GetDimensions().Width);
            send('coinsopen');
        },
    },
    wait('moedas do host', (t) => {
        if (t % 120 === 0) {
            const p = me(), coins = [];
            for (let i = 50; i < 54; i++) coins.push(p.inventory[i].type + 'x' + p.inventory[i].stack);
            let near = 0;
            for (let i = 0; i < 400; i++) {
                const w = Main.item[i];
                if (w.active && w.type === ItemID.GoldCoin) { near++; log('moedas: item ' + i + ' em ' + Math.round(w.position.X) + ',' + Math.round(w.position.Y) + ' reservado ' + w.playerIndexTheItemIsReservedFor); }
            }
            log('moedas: no chao ' + near + ', bolsa ' + coins.join(' ') + ', eu em ' + Math.round(p.Center.X) + ',' + Math.round(p.Center.Y) + ', pegas ' + pickups.length);
        }
        return coinsSystem().exampleCoinsUI.MoneyDisplay.collectedCoins > state.coins;
    }, () => {
        check('as moedas soltas pelo host contam na tela', true, state.coins + ' -> ' + coinsSystem().exampleCoinsUI.MoneyDisplay.collectedCoins);
    }, 900),
    {
        name: 'painel', frames: 120,
        start() {
            const p = me();
            state.slot = p.selectedItem;
            state.saved = p.inventory[state.slot].type;
            state.savedStack = p.inventory[state.slot].stack;
            p.inventory[state.slot]['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType('ExampleCustomResourceWeapon'), null);
            resource(p).exampleResourceCurrent = 50;
            state.maxAnim = 0;
            tap(430, 160);
        },
        tick(t) {
            state.maxAnim = Math.max(state.maxAnim, me().itemAnimation);
            if (t === 60) log('SHOT moedas');
        },
        end() {
            check('toque no painel nao usa a arma', state.maxAnim === 0, state.maxAnim);
            state.before = resource(me()).exampleResourceCurrent;
        },
    },
    {
        name: 'fechar', frames: 90,
        start() { tap(551, 121); },
        end() { check('o X fecha a tela', !coinsSystem().exampleCoinUserInterface.CurrentState); },
    },
    {
        name: 'arma', frames: 120,
        start() {
            state.maxAnim = 0;
            state.before = resource(me()).exampleResourceCurrent;
            tap(430, 160);
        },
        tick(t) {
            state.maxAnim = Math.max(state.maxAnim, me().itemAnimation);
            if (t === 10) log('SHOT recurso');
            state.lowest = Math.min(state.lowest ?? Infinity, resource(me()).exampleResourceCurrent);
        },
        end() {
            const after = resource(me()).exampleResourceCurrent;
            check('a arma do recurso dispara', state.maxAnim > 0, state.maxAnim);
            // O recurso volta 1 a cada 60 atualizações: no emulador lento são
            // ~3 por quadro desenhado, então ele sobe de novo antes do fim e
            // entre dois quadros a queda de 5 aparece como 4. Basta ter caído.
            check('o disparo gasta o recurso', state.lowest < state.before, state.before + ' -> ' + state.lowest + ' -> ' + after);
            send('fired');
        },
    },
    {
        name: 'overlay', frames: 40,
        tick(t) { if (t === 20) log('SHOT overlay'); },
        end() {
            const p = me();
            p.inventory[state.slot]['void SetDefaults(int Type, ItemVariant variant)'](state.saved, null);
            p.inventory[state.slot].stack = state.savedStack;
            stats(p).exampleLifeFruits = 0;
            stats(p).exampleManaCrystals = 0;
            check('nenhum erro de mod', modErrors.length === 0, modErrors.join(' | '));
            send('end', fails);
        },
    },
];

function step() {
    const phases = role === 'host' ? hostPhases : clientPhases;
    if (index >= phases.length) {
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    const ph = phases[index];
    if (local === 0) {
        log('fase ' + ph.name);
        if (ph.start) ph.start();
    }
    if (ph.until) {
        const result = ph.until(local);
        local++;
        if (result) {
            ph.then(result);
        } else if (local > ph.timeout) {
            check(ph.name, false, 'tempo esgotado');
        } else return;
        index++;
        local = 0;
        return;
    }
    if (ph.tick) ph.tick(local);
    local++;
    if (local >= ph.frames) {
        if (ph.end) ph.end();
        index++;
        local = 0;
    }
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        if (!role) {
            role = Main.netMode === 3 ? 'host' : Main.netMode === 1 ? 'cliente' : '';
            if (!role) {
                done = true;
                bl.log('mpexample sem multijogador (netMode ' + Main.netMode + ')');
            } else log('INICIO jogador=' + Main.myPlayer);
        }
        if (role) {
            frame++;
            const p = me();
            p.statLife = p.statLifeMax2;
            try {
                step();
            } catch (e) {
                fails++;
                log('erro no quadro ' + frame + ': ' + e + (e.stack ? ' ' + e.stack : ''));
                index++;
                local = 0;
            }
        }
    }
    original(self, gameTime);
});

export class MpExamplePickupProbe extends GlobalItem {
    OnPickup(item, player) {
        pickups.push(item.type);
        log('OnPickup tipo ' + item.type + ' x' + item.stack + ' jogador ' + player.whoAmI);
        return true;
    }
}

export class MpExampleProbe extends ModPlayer {

    SyncPlayer(player, toWho, fromWho, newPlayer) {
        joinSyncs.push({ who: player.whoAmI, toWho, fromWho, newPlayer, mode: Main.netMode });
    }
}

export default class TestMpExample extends Mod {
    Load() {
        mod = this;
        this.GetPacket();
    }

    // O ReceivePlayerSync do ExampleStatIncreasePlayer, vigiado: quem chegou e quando.
    PostSetupContent() {
        const proto = Object.getPrototypeOf(ModPlayer.getByName('ExampleStatIncreasePlayer'));
        const original = proto.ReceivePlayerSync;
        proto.ReceivePlayerSync = function (reader) {
            original.call(this, reader);
            syncs.push({ who: this.Player.whoAmI, fruits: this.exampleLifeFruits, crystals: this.exampleManaCrystals, frame });
        };
    }

    HandlePacket(reader, from) {
        const kind = reader.ReadString();
        const values = [];
        while (reader.HasMore) values.push(reader.Read());
        inbox.push({ kind, values, from });
    }
}
bl.log('mpexample carregado');
