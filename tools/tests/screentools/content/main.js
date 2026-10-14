// Ferramentas dos efeitos de tela (item 4): recarga do .frag (Reload e
// HotReload, inclusive com erro de compilação), o /fx pelo CommandLoader, a
// sobreposição de debug e o desligar salvo em BunnyLoader.screen.json.
// Loga "screentools ...".
import { Hot } from './Content/Effects/Hot.js';

const Main = Terraria.Main;
const log = (s) => bl.log('screentools ' + s);
const FRAG = 'Content/Effects/Hot.frag';
const ORIGINAL = bl.file.read(FRAG);
const tinted = (rgb) => ORIGINAL.replace('vec3(1.0, 0.0, 0.0)', 'vec3(' + rgb + ')');

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

const player = () => Main.player[Main.myPlayer];
const replies = [];
const caller = { CommandType: CommandType.Chat, get Player() { return player(); }, Reply(text) { replies.push(String(text)); log('resposta: ' + text); } };
const fx = (line) => { replies.length = 0; return CommandLoader.Handle(line, caller); };
const settingFile = () => bl.path.join(Main.SavePath, 'BunnyLoader.screen.json');

let hot = null;
const steps = {
    60: () => {
        hot = Hot.Spawn(null, { key: 't:hot' });
        check('Hot vem do .frag', hot.Shader && hot.Shader.Path && hot.Shader.Path.endsWith('Hot.frag'), hot.Shader && hot.Shader.Path);
        check('shader de arquivo listado', ScreenGraphics.Shader.Files.includes(hot.Shader), ScreenGraphics.Shader.Files.length + ' arquivos');
    },
    100: () => {
        check('compilou', hot.Shader.Status === 'ready', hot.Shader.Status);
        log('SHOT red');
    },
    130: () => {
        check('sem mudança, Reload não recompila', ScreenEffects.Reload() === 0, 'nada mudou');
        bl.file.write(FRAG, tinted('0.0, 0.3, 1.0'));
        check('Reload pega a mudança', fx('/fx reload') && /^1 /.test(replies[0] || ''), replies[0]);
    },
    170: () => {
        check('recompilou', hot.Shader.Status === 'ready', hot.Shader.Status);
        log('SHOT blue');
    },
    200: () => {
        bl.file.write(FRAG, ORIGINAL.replace('fragColor', 'fragColour'));
        ScreenEffects.Reload();
    },
    240: () => {
        check('shader quebrado falha sem derrubar nada', hot.Shader.Status === 'failed' && /fragColour/.test(hot.Shader.Error || ''), (hot.Shader.Error || '').split('\n')[0]);
        check('efeito segue vivo', hot.Active, 'Active');
        log('SHOT broken');
    },
    270: () => {
        check('/fx watch liga', fx('/fx watch') && ScreenEffects.HotReload, replies[0]);
        bl.file.write(FRAG, tinted('0.0, 1.0, 0.2'));
    },
    330: () => {
        check('HotReload recompilou sozinho', hot.Shader.Status === 'ready', hot.Shader.Status);
        log('SHOT green');
        fx('/fx watch');
        bl.file.write(FRAG, ORIGINAL);
        ScreenEffects.Reload();
    },
    360: () => {
        check('/fx spawn', fx('/fx spawn twirl 120 0') && ScreenEffects.Active.some((e) => e.constructor === ScreenPresets.Twirl && e.Radius === 120), replies[0]);
        check('/fx spawn desconhecido', fx('/fx spawn nada') && /Hot/.test(replies[0] || ''), (replies[0] || '').slice(0, 80));
        fx('/fx spawn shockwave 200');
        fx('/fx spawn heathaze');
        check('/fx list', fx('/fx list') && replies.length === ScreenEffects.Active.length, replies.length + ' linhas');
        check('/fx debug', fx('/fx debug') && ScreenEffects.Debug, replies[0]);
    },
    420: () => {
        log('SHOT debug');
        check('/fx stats', fx('/fx stats') && /GPU/.test(replies[0] || ''), replies[0]);
        check('/fx kill por nome', fx('/fx kill twirl') && !ScreenEffects.Active.some((e) => e.constructor === ScreenPresets.Twirl), replies[0]);
    },
    450: () => {
        check('/fx off', fx('/fx off') && ScreenEffects.Enabled === false, replies[0]);
        const saved = JSON.parse(bl.file.read(settingFile()) || '{}');
        check('desligado salvo', saved.Enabled === false, JSON.stringify(saved));
    },
    490: () => {
        log('SHOT off');
    },
    520: () => {
        const s0 = bl.screen.__stats().draws;
        steps.s0 = s0;
    },
    560: () => {
        const draws = bl.screen.__stats().draws - steps.s0;
        check('desligados não desenham (só o contorno do debug some também)', draws === 0, draws + ' passos');
        fx('/fx on');
        fx('/fx debug');
        check('/fx on', ScreenEffects.Enabled === true && JSON.parse(bl.file.read(settingFile())).Enabled === true, replies[0]);
        check('/fx kill', fx('/fx kill') && ScreenEffects.Active.length === 0, replies[0]);
        check('/fx sem nada responde o uso', fx('/fx') && /spawn/.test(replies[0] || ''), replies[0]);
    },
    600: () => {
        const s = bl.screen.__stats();
        check('espelho do estado GL confere', s.shadowMismatches === 0, s.shadowChecks + ' conferidos');
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    },
};

let frame = 0;
Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (frame <= 600 && !Main.gameMenu) {
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

export default class TestScreenTools extends Mod {}
