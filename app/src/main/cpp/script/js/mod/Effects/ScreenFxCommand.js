// O /fx: as ferramentas de quem cria efeitos de tela, no chat (sempre
// disponível, só no aparelho de quem digita).
//   /fx list                         os efeitos vivos
//   /fx spawn <Efeito> [raio] [ticks] um pronto ou um efeito de mod, no jogador
//   /fx kill [key | Efeito]          mata (sem nada: todos)
//   /fx reload                       relê os .frag agora
//   /fx watch                        relê os .frag sozinho quando mudam
//   /fx debug                        contornos e painel de custo
//   /fx on | off                     liga ou desliga os efeitos (fica salvo)
//   /fx stats                        o custo numa linha
class ScreenFxCommand extends ModCommand {
    get Command() { return 'fx'; }
    get Type() { return CommandType.Chat; }
    get Usage() { return '/fx list | spawn <Effect> [radius] [ticks] | kill [key|Effect] | reload | watch | debug | on | off | stats'; }
    get Description() { return ScreenFxCommand.#Pt() ? 'ferramentas dos efeitos de tela' : 'screen effect tools'; }

    static Install() {
        CommandLoader.Add(new ScreenFxCommand());
    }

    Action(caller, input, args) {
        const pt = ScreenFxCommand.#Pt();
        const say = (ptText, enText, color) => caller.Reply(pt ? ptText : enText, color || ModCommand.ResponseColor);
        const [what = 'help', ...rest] = args;

        switch (what) {
            case 'list': {
                const active = ScreenEffects.Active;
                if (!active.length) return say('Nenhum efeito vivo.', 'No live effects.');
                for (const fx of active) {
                    const key = fx.Key !== null && fx.Key !== undefined ? ' [' + fx.Key + ']' : '';
                    const size = fx.Fullscreen || !fx.Center ? (pt ? 'tela' : 'screen') : 'r ' + Math.round(fx.Radius);
                    const left = fx.Duration > 0 ? ' / ' + (fx.Duration / 60).toFixed(1) + ' s' : '';
                    caller.Reply(fx.constructor.name + key + '  ' + size + '  ' + (fx.Age / 60).toFixed(1) + ' s' + left, ModCommand.ResponseColor);
                }
                return undefined;
            }
            case 'spawn': {
                const cls = ScreenFxCommand.#Class(rest[0]);
                if (!cls) {
                    const names = ScreenEffects.Classes.map((c) => c.name).join(', ');
                    return say('Efeito desconhecido. Há: ' + names, 'Unknown effect. Known: ' + names, Color.new(255, 25, 25));
                }
                const options = {};
                const radius = Number(rest[1]), ticks = Number(rest[2]);
                if (rest[1] !== undefined && radius > 0) options.radius = radius;
                if (rest[2] !== undefined && ticks >= 0) options.duration = ticks;
                cls.Spawn(caller.Player, options);
                return say(cls.name + ' criado no jogador.', cls.name + ' spawned on the player.');
            }
            case 'kill': {
                const target = rest[0];
                let n = 0;
                for (const fx of ScreenEffects.Active) {
                    const key = fx.Key === null || fx.Key === undefined ? '' : String(fx.Key).toLowerCase();
                    if (!target || target === 'all' || key === target || fx.constructor.name.toLowerCase() === target) {
                        fx.Kill();
                        n++;
                    }
                }
                return say(n + ' efeito(s) morto(s).', n + ' effect(s) killed.');
            }
            case 'reload': {
                const files = ScreenShader.Files.length;
                const changed = ScreenEffects.Reload();
                return say(changed + ' de ' + files + ' shader(s) de arquivo mudaram e vão recompilar.',
                    changed + ' of ' + files + ' file shader(s) changed and will recompile.');
            }
            case 'watch':
                ScreenEffects.HotReload = !ScreenEffects.HotReload;
                return ScreenEffects.HotReload
                    ? say('Recarga ligada: os .frag são relidos a cada meio segundo.', 'Hot reload on: .frag files are re-read every half second.')
                    : say('Recarga desligada.', 'Hot reload off.');
            case 'debug':
                ScreenDebug.Enabled = !ScreenDebug.Enabled;
                return ScreenDebug.Enabled ? say('Debug ligado.', 'Debug on.') : say('Debug desligado.', 'Debug off.');
            case 'on':
            case 'off':
                ScreenEffectsSetting.Set(what === 'on');
                return what === 'on' ? say('Efeitos de tela ligados.', 'Screen effects on.') : say('Efeitos de tela desligados.', 'Screen effects off.');
            case 'stats': {
                const s = bl.screen.__stats();
                const gpu = s.gpuSamples > 0 ? (s.gpuNs / s.gpuSamples / 1e6).toFixed(2) + ' ms' : '-';
                return say('GPU média ' + gpu + ', ' + s.draws + ' passos em ' + s.frames + ' quadros, marcador ' + (s.markerReady ? 'ok' : 'não'),
                    'GPU avg ' + gpu + ', ' + s.draws + ' passes in ' + s.frames + ' frames, marker ' + (s.markerReady ? 'ok' : 'no'));
            }
            default:
                return false;
        }
    }

    // A classe pelo nome, sem caixa (o chat chega em minúsculas).
    static #Class(name) {
        if (!name) return null;
        return ScreenEffects.Classes.find((cls) => cls.name.toLowerCase() === name) ?? null;
    }

    static #Pt() {
        return /^pt/.test(ModLocalization.ActiveCultureName || '');
    }
}
