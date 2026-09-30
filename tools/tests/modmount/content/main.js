// ModMount: uma montaria do teste que sobrescreve todos os hooks e conta as
// chamadas; e, com o Example Mod ligado, o carro (balões, dado por jogador) e
// o carrinho de mina (montado pelo buff, pelo BuffID.Sets.MountType do jogo).
const Main = Terraria.Main;
const { MountID, BuffID } = Terraria.ID;
const SET_MOUNT = 'void SetMount(int m, Player mountedPlayer, bool ignoreEffect)';
const DISMOUNT = 'void Dismount(Player mountedPlayer, bool ignoreEffect)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modmount ' + label + ': ok');
        else { fails++; bl.log('modmount ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modmount ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const calls = { SetMount: 0, Dismount: 0, UpdateEffects: 0, UpdateFrame: 0, JumpHeight: 0, JumpSpeed: 0, Draw: 0, layers: new Set() };

export class TestMount extends ModMount {
    SetStaticDefaults() {
        const d = this.MountData;
        d.jumpHeight = 10;
        d.jumpSpeed = 5;
        d.runSpeed = 6;
        d.dashSpeed = 6;
        d.acceleration = 0.1;
        d.heightBoost = 20;
        d.totalFrames = 4;
        d.playerYOffsets = [20, 20, 20, 20];
        d.xOffset = 13;
        d.yOffset = -12;
        d.bodyFrame = 3;
        d.standingFrameCount = 4;
        d.standingFrameDelay = 12;
        d.runningFrameCount = 4;
        d.runningFrameDelay = 12;
        d.inAirFrameCount = 1;
        d.idleFrameCount = 4;
        d.idleFrameDelay = 12;
        d.swimFrameCount = 1;
        d.textureWidth = d.backTexture.Value.Width + 20;
        d.textureHeight = d.backTexture.Value.Height;
        d.spawnDust = 91;
    }
    SetMount(player, skipDust) { calls.SetMount++; ModMount.SetSpecificData(player, { mounted: true }); skipDust.value = true; }
    Dismount(player, skipDust) { calls.Dismount++; skipDust.value = true; }
    UpdateEffects(player) { calls.UpdateEffects++; }
    UpdateFrame(player, state, velocity) { calls.UpdateFrame++; return true; }
    JumpHeight(player, jumpHeight, xVelocity) { calls.JumpHeight++; jumpHeight.value += 7; }
    JumpSpeed(player, jumpSpeed, xVelocity) { calls.JumpSpeed++; jumpSpeed.value *= 2; }
    Draw(playerDrawData, drawType, drawPlayer, texture, glowTexture, drawPosition, frame, drawColor,
         glowColor, rotation, spriteEffects, drawOrigin, drawScale, shadow) {
        calls.Draw++;
        calls.layers.add(drawType);
        return true;
    }
}

const TYPE = () => ModContent.MountType(TestMount);
const exampleOn = () => {
    try { return ModContent.MountType('examplemod/ExampleMount') > 0; } catch (e) { return false; }
};

function registered() {
    check('tipo depois dos do jogo', () => TYPE() > MountID.MeowmereMinecart || 'tipo ' + TYPE());
    check('MountID.Count cresceu', () => MountID.Count > TYPE() || 'Count ' + MountID.Count + ', tipo ' + TYPE());
    check('Mount.mounts e MountID.Sets no tamanho', () =>
        (Terraria.Mount.mounts.length >= MountID.Count && MountID.Sets.Cart.length >= MountID.Count) ||
        'mounts ' + Terraria.Mount.mounts.length + ', Cart ' + MountID.Sets.Cart.length + ', Count ' + MountID.Count);
    check('MountData no Mount.mounts, com a textura _Back', () => {
        const d = Terraria.Mount.mounts[TYPE()];
        if (!d) return 'sem MountData';
        // Sem o arquivo, a camada fica com o Asset vazio que o MountData nasce tendo.
        const front = d.frontTexture && d.frontTexture.Value;
        return (d.backTexture && d.backTexture.Value.Width === 100 && !front && d.jumpHeight === 10) ||
            'back ' + (d.backTexture && d.backTexture.Value.Width) + ', front ' + !!front + ', pulo ' + d.jumpHeight;
    });
}

// Textura de mod sem repetir, como as do jogo: um recorte que passa da borda
// (o textureWidth = largura + 20 do ExampleMount) trazia o outro lado dela.
// Na GPU do MuMu a textura que não é potência de 2 nunca repete; num celular
// de verdade, repetia.
function wrap() {
    const mod = Terraria.Mount.mounts[TYPE()].backTexture.Value.UnityTexture.wrapMode;
    const game = Terraria.Mount.mounts[MountID.Unicorn].backTexture.Value.UnityTexture.wrapMode;
    bl.log('modmount: wrapMode de mod ' + mod + ', do jogo ' + game);
    return (mod === 1 && mod === game) || 'mod ' + mod + ', jogo ' + game;
}

function mount() {
    const p = Main.LocalPlayer;
    p.mount[SET_MOUNT](TYPE(), p, false);
    check('montou', () => (p.mount.Active && p.mount.Type === TYPE()) || 'ativo ' + p.mount.Active + ', tipo ' + p.mount.Type);
    check('SetMount chamado, com o dado por jogador', () => {
        const data = ModMount.GetSpecificData(p);
        return (calls.SetMount === 1 && data && data.mounted) || 'SetMount ' + calls.SetMount;
    });
    check('JumpHeight (+7) e JumpSpeed (x2)', () => {
        const h = p.mount['int JumpHeight(float xVelocity)'](0);
        const s = p.mount['float JumpSpeed(float xVelocity)'](0);
        return (h === 17 && Math.abs(s - 10) < 0.001 && calls.JumpHeight === 1 && calls.JumpSpeed === 1) ||
            'altura ' + h + ', velocidade ' + s;
    });
}

function mounted() {
    const p = Main.LocalPlayer;
    check('UpdateEffects e UpdateFrame a cada quadro', () =>
        (calls.UpdateEffects > 10 && calls.UpdateFrame > 10) || 'efeitos ' + calls.UpdateEffects + ', quadro ' + calls.UpdateFrame);
    check('Draw na camada de trás', () => (calls.Draw > 0 && calls.layers.has(0)) || 'Draw ' + calls.Draw + ', camadas ' + [...calls.layers]);
    p.mount[DISMOUNT](p, false);
    check('desmontou, Dismount chamado e o dado apagado', () =>
        (!p.mount.Active && calls.Dismount === 1 && ModMount.GetSpecificData(p) === undefined) ||
        'ativo ' + p.mount.Active + ', Dismount ' + calls.Dismount + ', dado ' + JSON.stringify(ModMount.GetSpecificData(p)));
}

// ---- Example Mod ----
function exampleCar() {
    const p = Main.LocalPlayer;
    const car = ModContent.MountType('examplemod/ExampleMount');
    check('Example: o item do carro aponta a montaria', () => {
        const item = Terraria.ID.ContentSamples.ItemsByType.get_Item(ModContent.ItemType('examplemod/ExampleMountItem'));
        return item.mountType === car || 'mountType ' + item.mountType;
    });
    check('Example: carro com as duas camadas e o buff', () => {
        const d = Terraria.Mount.mounts[car];
        return (d.backTexture && d.frontTexture && d.textureWidth === 120 && d.buff === ModContent.BuffType('examplemod/ExampleMountBuff')) ||
            'back ' + !!d.backTexture + ', front ' + !!d.frontTexture + ', largura ' + d.textureWidth + ', buff ' + d.buff;
    });
    p.mount[SET_MOUNT](car, p, false);
    check('Example: montou no carro, com os balões', () => {
        const b = ModMount.GetSpecificData(p);
        return (p.mount.Type === car && b && b.count === 3) || 'tipo ' + p.mount.Type + ', balões ' + JSON.stringify(b);
    });
    check('Example: o buff do carro', () => p.FindBuffIndex(ModContent.BuffType('examplemod/ExampleMountBuff')) >= 0 || 'sem buff');
}

function exampleCart() {
    const p = Main.LocalPlayer;
    const car = ModContent.MountType('examplemod/ExampleMount');
    check('Example: o carro ainda montado (o buff renova)', () => p.mount.Type === car || 'tipo ' + p.mount.Type);
    p.mount[DISMOUNT](p, false);
    const cart = ModContent.MountType('examplemod/ExampleMinecartMount');
    const buff = ModContent.BuffType('examplemod/ExampleMinecartBuff');
    check('Example: carrinho é Cart e o buff monta', () =>
        (MountID.Sets.Cart[cart] && BuffID.Sets.MountType[buff] === cart && Main.buffNoSave[buff] && Main.buffNoTimeDisplay[buff]) ||
        'Cart ' + MountID.Sets.Cart[cart] + ', MountType ' + BuffID.Sets.MountType[buff] + ', noSave ' + Main.buffNoSave[buff]);
    check('Example: SetAsMinecart e os delegates copiados', () => {
        const d = Terraria.Mount.mounts[cart];
        return !!(d.Minecart && d.totalFrames === 3 && d.frontTexture && d.delegations.MinecartDust && d.delegations.MinecartLandingSound) ||
            'Minecart ' + d.Minecart + ', quadros ' + d.totalFrames + ', front ' + !!d.frontTexture;
    });
    p.AddBuff(buff, 3600, true, false);
}

function exampleCartMounted() {
    const p = Main.LocalPlayer;
    const cart = ModContent.MountType('examplemod/ExampleMinecartMount');
    check('Example: o jogo montou o carrinho pelo buff', () =>
        (p.mount.Active && p.mount.Type === cart) || 'ativo ' + p.mount.Active + ', tipo ' + p.mount.Type);
    p.ClearBuff(ModContent.BuffType('examplemod/ExampleMinecartBuff'));
    p.mount[DISMOUNT](p, false);
}

// O botão de montaria do toque: Player.QuickMount usa o item do espaço de
// montaria do equipamento (miscEquips[3]).
let miscBefore = null;
function quickMount() {
    const p = Main.LocalPlayer;
    miscBefore = p.miscEquips[3].type;
    p.miscEquips[3]['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType('examplemod/ExampleMountItem'), null);
    p.QuickMount();
}

function quickMounted() {
    const p = Main.LocalPlayer;
    const car = ModContent.MountType('examplemod/ExampleMount');
    check('Example: o botão de montaria (QuickMount) monta o carro', () =>
        (p.mount.Active && p.mount.Type === car) || 'ativo ' + p.mount.Active + ', tipo ' + p.mount.Type);
    p.miscEquips[3]['void SetDefaults(int Type, ItemVariant variant)'](miscBefore, null);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 60) { check('registro', registered); check('textura sem repetir (Clamp)', wrap); check('montar', mount); }
    if (frames === 120) check('montado', mounted);
    if (frames === 130 && exampleOn()) check('Example: carro', exampleCar);
    if (frames === 200 && exampleOn()) check('Example: carrinho', exampleCart);
    if (frames === 230 && exampleOn()) check('Example: carrinho montado', exampleCartMounted);
    if (frames === 232 && exampleOn()) check('Example: QuickMount', quickMount);
    if (frames === 236 && exampleOn()) check('Example: QuickMount montado', quickMounted);
    if (frames === 240) {
        done = true;
        // Montado no carro no fim, para olhar na tela (balões e camadas).
        if (exampleOn()) {
            const p = Main.LocalPlayer;
            p.mount[SET_MOUNT](ModContent.MountType('examplemod/ExampleMount'), p, false);
            bl.log('modmount tela: carro montado');
        }
        bl.log('modmount FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modmount: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestModmount extends Mod {}
