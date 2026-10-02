// As conquistas do jogo com as de mod registradas (AchievementLoader). No
// mundo: quantas há e quantas estão completas, a TIMBER ("Madeiraaaa!!") e a
// condição dela, e a TIMBER de novo depois de zerada e de um aviso de coleta de
// madeira (AchievementsHelper.NotifyItemPickup, o que o jogo chama ao pegar o
// item). Loga "achievements ...".
const Main = Terraria.Main;
const Ach = Terraria.GameContent.Achievements;
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('achievements ' + label + ': ok');
        else { fails++; bl.log('achievements ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('achievements ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function conditionsOf(a) {
    const out = [];
    const dict = a._conditions;
    const keys = dict.Keys;
    const it = keys.GetEnumerator();
    while (it.MoveNext()) {
        const name = it.Current;
        const c = dict.get_Item(name);
        out.push(`${name}=${c.IsCompleted ? 'feita' : 'aberta'} (${c.GetType().Name})`);
    }
    return out;
}

function survey(tag) {
    const manager = Main.Achievements;
    const list = manager.CreateAchievementsList();
    let total = 0, done = 0, mod = 0, modDone = 0;
    const doneNames = [];
    for (let i = 0; i < list.Count; i++) {
        const a = list.get_Item(i);
        const isMod = String(a.Name).includes('/');
        total++;
        if (isMod) mod++;
        if (a.IsCompleted) { done++; if (isMod) modDone++; doneNames.push(a.Name); }
    }
    bl.log(`achievements ${tag}: ${total} conquistas (${mod} de mod), ${done} completas (${modDone} de mod): ${doneNames.join(', ')}`);
    return { total, done };
}

let frames = 0, ran = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || ran) return;
    if (++frames < 60) return;
    ran = true;

    const manager = Main.Achievements;
    check('gerenciador', () => !!manager || 'Main.Achievements nulo');
    survey('no mundo');
    // O IsCompleted é "_completedCount == condições": o contador tem de bater
    // com as condições completas (ler o arquivo duas vezes o dobrava).
    check('contador de cada conquista = condições completas', () => {
        const list = manager.CreateAchievementsList();
        const wrong = [];
        for (let k = 0; k < list.Count; k++) {
            const a = list.get_Item(k);
            const it = a._conditions.Values.GetEnumerator();
            let done = 0;
            while (it.MoveNext()) if (it.Current.IsCompleted) done++;
            if (a._completedCount !== done) wrong.push(`${a.Name} ${a._completedCount}/${done}`);
        }
        return wrong.length === 0 || wrong.length + ' erradas: ' + wrong.slice(0, 12).join(', ');
    });

    const timber = manager.GetAchievement('TIMBER');
    check('TIMBER existe', () => !!timber || 'sem TIMBER');
    if (!timber) return;
    bl.log('achievements TIMBER: ' + (timber.IsCompleted ? 'completa' : 'aberta') + '; condições: ' + conditionsOf(timber).join(', '));
    bl.log('achievements: _isMining ' + Ach.AchievementsHelper._isMining +
           ', ouvintes de coleta da madeira (9): ' + (Ach.ItemPickupCondition._listeners.ContainsKey(9) ? Ach.ItemPickupCondition._listeners.get_Item(9).Count : 0) +
           ', ouvintes de árvore (5): ' + (Ach.TileDestroyedCondition._listeners && Ach.TileDestroyedCondition._listeners.ContainsKey(5)
               ? Ach.TileDestroyedCondition._listeners.get_Item(5).Count : 0));

    // Zerada, e o aviso de que o jogador pegou madeira.
    check('TIMBER zerada', () => manager['bool Clear(string achievementName)']('TIMBER') !== undefined && !timber.IsCompleted || 'continua completa');
    bl.log('achievements TIMBER zerada: ' + conditionsOf(timber).join(', '));
    const wood = Terraria.Item.new();
    wood['void .ctor()']();
    wood['void SetDefaults(int Type, ItemVariant variant)'](9, null);
    Ach.AchievementsHelper['void NotifyItemPickup(Player player, Item item, int customStack)'](self, wood, 1);
    bl.log('achievements TIMBER depois da madeira: ' + conditionsOf(timber).join(', '));
    check('TIMBER completa ao pegar madeira', () => timber.IsCompleted || 'aberta');
    survey('no fim');
    bl.log('achievements FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});

export default class TestAchievements extends Mod {}
