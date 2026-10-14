// NPC escondido do Bestiário, como o corpo e a cauda de uma serpente:
//   - HideHead: entrada normal;
//   - HideBody: HideFromBestiary e Hide no NPCBestiaryDrawModifiers;
//   - HideTail: só o Hide (como no tModLoader);
//   - HidePlain: só o HideFromBestiary.
// Nenhum dos três últimos pode ficar com entrada: antes, a genérica que o jogo
// cria para cada NPC ficava (a silhueta preta, com o número repetido).
// Loga "bestiaryhide ...".
const { NPCID } = Terraria.ID;
const log = (s) => bl.log('bestiaryhide ' + s);

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

function hide(type) {
    const value = NPCID.Sets.NPCBestiaryDrawModifiers.new();
    value['void .ctor(int seriouslyWhyCantStructsHaveParameterlessConstructors)'](0);
    value.Hide = true;
    NPCID.Sets.NPCBestiaryDrawOffset.Add(type, value);
}

class Segment extends ModNPC {
    SetDefaults() {
        this.NPC.width = this.NPC.height = 32;
        this.NPC.lifeMax = 100;
        this.NPC.aiStyle = -1;
    }
}

export class HideHead extends Segment {}

export class HideBody extends Segment {
    HideFromBestiary = true;

    SetStaticDefaults() {
        hide(this.Type);
    }
}

export class HideTail extends Segment {
    SetStaticDefaults() {
        hide(this.Type);
    }
}

export class HidePlain extends Segment {
    HideFromBestiary = true;
}

function listed(type) {
    const db = Terraria.Main.BestiaryDB;
    const entry = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'](type);
    return !!entry && db.Entries.Contains(entry);
}

let frame = 0;
Terraria.Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (frame <= 30 && !Terraria.Main.gameMenu) {
        frame++;
        if (frame === 30) {
            try {
                const type = (name) => ModContent.NPCType(name);
                check('cabeça no Bestiário', listed(type('HideHead')), String(type('HideHead')));
                for (const name of ['HideBody', 'HideTail', 'HidePlain']) {
                    check(name + ' fora do Bestiário', !listed(type(name)), String(type(name)));
                }
                check('corpo do Devorador (jogo) fora', !listed(NPCID.DevourerBody), String(NPCID.DevourerBody));
                log('entradas ' + Terraria.Main.BestiaryDB.Entries.Count);
            } catch (e) {
                fails++;
                log('erro: ' + e + (e.stack ? ' ' + e.stack : ''));
            }
            log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestBestiaryHide extends Mod {}
