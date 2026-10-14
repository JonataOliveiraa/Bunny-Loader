// Botões de toque de mod:
//   - Blink (TouchButtonLoader.RegisterButton, ícone em Assets/TouchButtons)
//     e Charge (KeybindLoader.RegisterKeybind, o apelido do tModLoader);
//   - as ações entram nas listas do jogo (a do editor, a de desenho, a dos
//     controles);
//   - o Blink vai para um espaço de botão do layout ativo (no lugar da
//     Montaria): 'SHOT bound' mostra o ícone; tocar/segurar o botão loga
//     JustPressed, Current e JustReleased vindos do ProcessTriggers;
//   - no fim o espaço volta à ação do jogo.
// Loga "touchbuttons ...".
const Main = Terraria.Main;
const log = (s) => bl.log('touchbuttons ' + s);

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

let Blink = null, Charge = null;
let presses = 0, releases = 0, heldTicks = 0, chargePresses = 0, onPress = 0;

export class TouchButtonsSystem extends ModSystem {
    Load() {
        Blink = TouchButtonLoader.RegisterButton(this.Mod, 'Blink', { onPress: () => onPress++ });
        Charge = KeybindLoader.RegisterKeybind(this.Mod, 'Charge', 'P');
    }
}

export class TouchButtonsPlayer extends ModPlayer {
    ProcessTriggers(player, triggers) {
        if (Blink.JustPressed) {
            presses++;
            log('JustPressed (Current ' + Blink.Current + ', Old ' + Blink.Old + ') #' + presses);
        }
        if (Blink.Current) heldTicks++;
        if (Blink.JustReleased) {
            releases++;
            log('JustReleased depois de ' + heldTicks + ' atualizações segurando #' + releases);
            heldTicks = 0;
        }
        if (Charge.JustPressed) chargePresses++;
    }
}

let slot = -1, original = -1, frame = 0;
const STEPS_END = 1500;

function profile() {
    return bl.classOf('', 'VirtualControlsStyles_Layout').Instance.ActiveProfile;
}

const steps = {
    40: () => {
        const ids = TouchButtonLoader.Ids;
        log('ids ' + JSON.stringify(ids));
        check('números lidos do jogo', ids.unbound === 28 && ids.quickUnbound === 35 && ids.firstControl === 33, JSON.stringify(ids));
        check('apelido ModKeybind', ModKeybind === ModTouchButton && Charge instanceof ModKeybind, String(Charge));
        check('FullName', Blink.FullName === 'test-touchbuttons/Blink', Blink.FullName);
        check('nome do idioma (Localization)', Blink.DisplayName.Value === 'Piscar' || Blink.DisplayName.Value === 'Blink', Blink.DisplayName.Value);
        check('nome sem texto: o nome separado', Charge.DisplayName.Value === 'Charge', Charge.DisplayName.Value);

        const action = TouchButtonLoader.ActionOf(TouchButtonLoader.ByKey(Blink.FullName));
        const config = bl.classOf('', 'VirtualInputControllerControlConfiguration_Layout').Instance;
        let listed = false;
        for (let i = 0; i < config.ButtonControls.length; i++) if (config.ButtonControls[i] === action) listed = true;
        check('na lista de ações do editor', listed, 'ação ' + action + ', ' + config.ButtonControls.length + ' na lista');
        const quick = bl.classOf('', 'GUIVirtualInputController').QuickActionControlFromLayoutControl;
        check('na tabela de desenho', quick.length > action && quick[action] === 100, 'tamanho ' + quick.length + ', valor ' + quick[action]);

        const p = profile();
        const slots = p.HardwareConfig.Slots;
        const rows = [];
        for (let i = 0; i < p.Controls.length; i++) {
            const b = p.Controls[i];
            const type = slots[i] ? slots[i].Style.ControlType : -1;
            rows.push(i + ':' + b.SlotId + ':' + type + ':' + b.Action);
            if (slot < 0 && type === 1 && b.Action === 7) slot = i;
        }
        log('perfil ' + p.Id + ' ' + rows.join(' '));
        check('um espaço de botão (Montaria)', slot >= 0, 'espaço ' + slot);
        if (slot >= 0) {
            original = p.Controls[slot].Action;
            p.Controls[slot].Action = action;
            log('espaço ' + slot + ' (' + p.Controls[slot].SlotId + '): ação ' + original + ' -> ' + action);
        }
    },
    50: () => {
        check('IsBound', Blink.IsBound && !Charge.IsBound, Blink.IsBound + '/' + Charge.IsBound);
    },
    90: () => log('SHOT bound'),
    [STEPS_END - 10]: () => {
        check('toque vira JustPressed', presses >= 1, presses + ' toque(s), onPress ' + onPress);
        check('soltar vira JustReleased', releases >= 1 && releases === presses, releases + ' soltura(s)');
        check('onPress uma vez por toque', onPress === presses, onPress + '/' + presses);
        check('Charge sem espaço não dispara', chargePresses === 0, String(chargePresses));
        if (slot >= 0) profile().Controls[slot].Action = original;
        log('espaço devolvido: ' + profile().Controls[slot].Action);
    },
    [STEPS_END]: () => log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)')),
};

Main['void DoDraw(GameTime gameTime)'].hook((orig, self, gameTime) => {
    if (frame <= STEPS_END && !Main.gameMenu) {
        frame++;
        const player = Main.player[Main.myPlayer];
        player.statLife = player.statLifeMax2;
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
    orig(self, gameTime);
});
log('carregado');

export default class TestTouchButtons extends Mod {}
