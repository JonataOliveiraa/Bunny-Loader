// Som de mod pela API (SoundStyle, SoundEngine). Loga "sounds ...". Precisa
// do Example Mod: o som e o tiro da ExampleGun dele (curto), pelo caminho do
// mod (ModLoader.GetMod), para o teste nao carregar audio proprio.
//
// Carga: o SoundStyle desse arquivo e o mesmo objeto com ou
// sem extensao e com as chaves em minusculas; arquivo que nao existe da um
// marcador mudo, sem lancar. No mundo: toca sem posicao, perto (soa) e longe
// (nao soa); MaxInstances 1 troca o velho pelo novo (ReplaceOldest) ou ignora
// o novo (IgnoreNew), e 0 e sem limite; um som do jogo continua passando; o
// FindActiveSound esvazia quando os sons acabam; e o UseSound de um item
// usado de verdade toca o nosso.
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('sounds ' + label + ': ok');
        else { fails++; bl.log('sounds ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('sounds ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const SOUND = ModLoader.GetMod('examplemod').path + '/Sounds/Items/Guns/ExampleGun';
const style = new SoundStyle(SOUND, { Volume: 0.8, PitchVariance: 0.1, MaxInstances: 1 });
const ignoring = new SoundStyle(SOUND, { MaxInstances: 1, SoundLimitBehavior: SoundLimitBehavior.IgnoreNew });
const unlimited = new SoundStyle(SOUND, { MaxInstances: 0, Volume: 0.5 });
let missing = null;

check('SoundStyle e o marcador do jogo', () =>
    (style.SoundId === 1000 && style.Style >= 0) || `SoundId ${style.SoundId} Style ${style.Style}`);
check('o mesmo objeto com extensao e em minusculas', () => {
    if (new SoundStyle(SOUND + '.ogg', { Volume: 0.8, PitchVariance: 0.1, MaxInstances: 1 }) !== style) return 'com .ogg';
    if (new SoundStyle(SOUND, { volume: 0.8, pitchVariance: 0.1, maxInstances: 1 }) !== style) return 'minusculas';
    return (ignoring !== style && unlimited !== style) || 'opcoes diferentes deram o mesmo';
});
check('arquivo que nao existe: marcador mudo', () => {
    missing = new SoundStyle('Sounds/nao-existe');
    return missing.SoundId === 1000 || 'SoundId ' + missing.SoundId;
});

let frames = 0, done = false, forceUse = false, saved = null;
let first = 0, second = 0;
Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, self, i) => {
    if (forceUse && i === Main.myPlayer) self.controlUseItem = true;
    original(self, i);
});
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer) return;
    frames++;
    const p = self;
    if (frames === 60) {
        check('toca sem posicao', () => {
            first = SoundEngine.PlaySound(style);
            return (first > 0 && SoundEngine.FindActiveSound(style) === first) || 'stream ' + first;
        });
        check('MaxInstances 1 (ReplaceOldest): o novo no lugar do velho', () => {
            second = SoundEngine.PlaySound(style);
            return (second > 0 && second !== first && SoundEngine.FindActiveSound(style) === second) ||
                `primeiro ${first}, segundo ${second}, ativo ${SoundEngine.FindActiveSound(style)}`;
        });
        check('MaxInstances 1 (IgnoreNew): o novo nao toca', () => {
            const a = SoundEngine.PlaySound(ignoring);
            const b = SoundEngine.PlaySound(ignoring);
            return (a > 0 && b === 0) || `${a}, ${b}`;
        });
        check('MaxInstances 0: sem limite', () => {
            const s = [SoundEngine.PlaySound(unlimited), SoundEngine.PlaySound(unlimited), SoundEngine.PlaySound(unlimited)];
            return s.every((x) => x > 0) || s.join(',');
        });
        check('com posicao: perto soa, longe nao', () => {
            const near = SoundEngine.PlaySound(unlimited, p.Center);
            const far = SoundEngine.PlaySound(unlimited, Vector2.new(p.Center.X + 30000, p.Center.Y));
            return (near > 0 && far === 0) || `perto ${near}, longe ${far}`;
        });
        check('arquivo que nao existe nao toca', () => SoundEngine.PlaySound(missing) === 0 || 'tocou');
        check('som do jogo continua passando', () => {
            const inst = SoundEngine.PlaySound(Terraria.ID.SoundID.Item1);
            return inst !== null || 'nulo';
        });
    }
    if (frames === 200) {
        check('FindActiveSound esvazia quando o som acaba', () =>
            SoundEngine.FindActiveSound(style) === 0 || 'ainda ' + SoundEngine.FindActiveSound(style));
        saved = { sel: p.selectedItemState.selected, type: p.inventory[0].type, stack: p.inventory[0].stack };
        p.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](Terraria.ID.ItemID.CopperShortsword, null);
        p.inventory[0].UseSound = style;
        p.selectedItemState.selected = 0;
    }
    if (frames === 210) forceUse = true;
    if (frames === 240) {
        forceUse = false;
        check('UseSound de mod num item usado de verdade', () => SoundEngine.FindActiveSound(style) > 0 || 'nada soando');
        p.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](saved.type, null);
        p.inventory[0].stack = saved.stack;
        p.selectedItemState.selected = saved.sel;
        done = true;
        bl.log('sounds FIM ' + (fails ? fails + ' falha(s)' : 'ok'));
    }
});
bl.log('sounds: carregado');
