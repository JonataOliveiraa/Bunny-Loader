// Superpoderes do Mod Menu: este mod só mede, os poderes são tocados no menu.
//   mana: a cada 5 s a mana vai a 5 no ResetEffects (antes do enchimento do
//     modo deus); o começo do quadro seguinte diz quanto ela tem. Imortal sem
//     Mana infinita: perto de 5. Com Mana infinita (ou sem a correção): cheia.
//   mundo: as flags dos eventos e o WorldGen.AllowedToSpreadInfections.
//   npcs: os NPCs que não são moradores, em blocos, relativos ao jogador.
// Loga "superpowers ..." quando muda.
const Main = Terraria.Main;
const Events = Terraria.GameContent.Events;

let frames = 0, drained = -1, lastWorld = '', lastNpcs = '';

Terraria.Player['void ResetEffects()'].hook((original, self) => {
    original(self);
    if (self.whoAmI !== Main.myPlayer || Main.gameMenu) return;
    if (frames % 300 === 0 && frames > 0) {
        self.statMana = 5;
        drained = frames;
    }
});

function world() {
    const flags = [
        Main.dayTime ? 'dia' : 'noite',
        Main.bloodMoon && 'luaSangue', Main.eclipse && 'eclipse', Main.slimeRain && 'slime',
        Main.invasionType > 0 && 'invasao' + Main.invasionType + '/' + Main.invasionSize,
        Main.pumpkinMoon && 'abobora', Main.snowMoon && 'gelida',
        (Events.BirthdayParty.ManualParty || Events.BirthdayParty.GenuineParty) && 'festa',
        Events.Sandstorm.Happening && 'areia', Main.raining && 'chuva',
    ].filter(Boolean);
    return flags.join(' ') + ' | espalha=' + Terraria.WorldGen.AllowedToSpreadInfections;
}

function npcs(player) {
    const out = [];
    for (let i = 0; i < 200; i++) {
        const n = Main.npc[i];
        if (!n.active || n.townNPC) continue;
        const dx = Math.round((n.position.X - player.position.X) / 16);
        const dy = Math.round((n.position.Y + n.height - player.position.Y - player.height) / 16);
        out.push(`${n.type}@${dx},${dy}`);
    }
    return `${out.length}: ${out.slice(0, 24).join(' ')}`;
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    // No começo do quadro seguinte: depois do enchimento do modo deus, que
    // vem depois do Update de todos os jogadores.
    const local = i === Main.myPlayer && !Main.gameMenu;
    if (local && drained >= 0 && frames === drained + 1) {
        bl.log(`superpowers mana: 5 no quadro anterior, agora ${self.statMana}/${self.statManaMax2} ` +
               `(deus=${self.creativeGodMode}, vida ${self.statLife}/${self.statLifeMax2})`);
    }
    original(self, i);
    if (!local) return;
    ++frames;
    if (frames % 60 !== 0) return;
    const w = world();
    if (w !== lastWorld) bl.log('superpowers mundo: ' + w);
    lastWorld = w;
    const n = npcs(self);
    if (n !== lastNpcs) bl.log(`superpowers npcs (olhando ${self.direction}): ${n}`);
    lastNpcs = n;
});
bl.log('superpowers: carregado');

export default class TestSuperpowers extends Mod {}
