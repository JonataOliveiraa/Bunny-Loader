// Etapa 13: ModCommand, ModHair, ModCloud, ModEmoteBubble, ModAchievement e o
// peixe de missão. Conteúdo próprio do teste (o que conta chamadas) e, com o
// Example Mod ligado, os exemplos dele.
const Main = Terraria.Main;
const { NPCID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('soltos ' + label + ': ok');
        else { fails++; bl.log('soltos ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('soltos ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const calls = { command: [], cloudSpawn: 0, emoteSpawn: 0, emoteDraw: 0, completed: 0, npcKilled: [] };
let cloudWeight = 0, rareWeight = 0;

// ---- conteúdo do teste ----
export class TestCommand extends ModCommand {
    get Command() { return 'testecmd'; }
    get Aliases() { return ['tc']; }
    Action(caller, input, args) {
        if (args[0] === 'erro') throw new UsageException('erro pedido');
        if (args[0] === 'false') return false;
        calls.command.push(args.join(','));
        caller.Reply('testecmd ok');
    }
}

export class TestHair extends ModHair {}

export class TestCloud extends ModCloud {
    SpawnChance() { return cloudWeight; }
    OnSpawn(cloud) { calls.cloudSpawn++; }
}

export class TestRareCloud extends ModCloud {
    get RareCloud() { return true; }
    SpawnChance() { return rareWeight; }
}

export class TestEmote extends ModEmoteBubble {
    SetStaticDefaults() { this.AddToCategory(Terraria.GameContent.UI.EmoteID.Category.General); }
    OnSpawn() { calls.emoteSpawn++; }
    PreDraw(spriteBatch, texture, position, frame, origin, spriteEffects) { calls.emoteDraw++; return true; }
}

export class TestAchievement extends ModAchievement {
    flag = null;
    SetStaticDefaults() { this.flag = this.AddCondition('TestFlag'); }
    OnCompleted(achievement) { calls.completed++; }
    OnNPCKilled(player, npcId) { calls.npcKilled.push(npcId); }
}

const exampleOn = () => {
    try { return ModContent.ItemType('examplemod/ExampleItem') > 0; } catch (e) { return false; }
};

// ---- comandos ----
function say(text) {
    const message = Terraria.UI.Chat.ChatManager.Commands['ChatMessage CreateOutgoingMessage(string text)'](text);
    Terraria.UI.Chat.ChatManager.Commands['void ProcessIncomingMessage(ChatMessage message, int clientId)'](message, Main.myPlayer);
    return message;
}

function commands() {
    check('comando: /testecmd roda com os argumentos', () => {
        const m = say('/testecmd A b');
        return (calls.command[0] === 'a,b' && m.IsConsumed) || JSON.stringify(calls.command) + ', consumida ' + m.IsConsumed;
    });
    check('comando: pelo apelido (/tc)', () => { say('/tc 1'); return calls.command[1] === '1' || JSON.stringify(calls.command); });
    check('comando: UsageException e false não quebram', () => { say('/testecmd erro'); say('/testecmd false'); return calls.command.length === 2 || calls.command.length; });
    check('comando: o que não é de mod segue para o jogo', () => { say('/naoexiste 1'); return calls.command.length === 2 || JSON.stringify(calls.command); });
    check('comando: o /help lista os de mod', () => {
        const Help = Terraria.Chat.Commands.HelpCommand;
        const text = Help['NetworkText ComposeMessage(Dictionary`2 aliases)'](Help['Dictionary`2 GetCommandAliasesByID()']()).ToString();
        return text.includes('/testecmd') || text.slice(-200);
    });
    if (!exampleOn()) return;
    check('Example: /heal 10 cura', () => {
        const p = Main.LocalPlayer;
        p.statLife = Math.max(1, p.statLifeMax2 - 50);
        const before = p.statLife;
        say('/heal 10');
        return p.statLife === before + 10 || before + ' -> ' + p.statLife;
    });
    check('Example: /addtime 600 (World, sozinho)', () => {
        const before = Main.time, day = Main.dayTime;
        say('/addtime 600');
        const moved = (Main.dayTime === day) ? Main.time - before : 600;
        return Math.abs(moved - 600) < 1 || before + ' -> ' + Main.time;
    });
}

// ---- cabelo ----
function hair() {
    const type = ModContent.GetInstance(TestHair).Type;
    check('cabelo: tipo depois dos 228 e textura', () => {
        const T = Terraria.GameContent.TextureAssets;
        return (type >= 228 && T.PlayerHair.length > type && T.PlayerHair[type].Value.Width === 40 && T.PlayerHairAlt[type].Value.Width === 40) ||
            'tipo ' + type + ', tabela ' + T.PlayerHair.length;
    });
    check('cabelo: na criação de personagem, e o de exemplo não', () => {
        const helper = Terraria.GameContent.HairstyleUnlocksHelper.new();
        helper['void .ctor()']();
        helper._isAtCharacterCreation = true;
        helper['void UpdateUnlocks()']();
        const list = helper.AvailableHairstyles;
        const example = exampleOn() ? ModContent.Find(ModHair, 'examplemod/ExampleHair').Type : -1;
        return (list.Contains(type) && !list.Contains(example)) || 'teste ' + list.Contains(type) + ', exemplo ' + list.Contains(example);
    });
    check('cabelo: o save guarda pelo nome e o arquivo do jogo leva 0', () => {
        const p = Main.LocalPlayer, fd = Main.ActivePlayerFileData;
        const old = p.hair;
        p.hair = type;
        try {
            Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](fd, true, true);
            if (p.hair !== type) return 'o cabelo do jogador mudou no save: ' + p.hair;
            const json = JSON.parse(bl.file.read(fd.Path + '.bl.json') || '{}');
            const key = json['bunny:hair'];
            if (!key || !key.endsWith('/TestHair')) return 'chave ' + key;
            const loaded = Terraria.Player['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'](fd.Path, false);
            return loaded.Player.hair === type || 'carregado com ' + loaded.Player.hair;
        } finally {
            p.hair = old;
            Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](fd, true, true);
        }
    });
}

// ---- nuvens ----
function clouds() {
    const type = ModContent.GetInstance(TestCloud).Type, rare = ModContent.GetInstance(TestRareCloud).Type;
    check('nuvem: tipos depois dos do jogo e textura', () => {
        const T = Terraria.GameContent.TextureAssets;
        return (type >= Terraria.ID.CloudID.Count && T.Cloud.length > rare && T.Cloud[type].Value.Width > 0) ||
            'tipo ' + type + ', tabela ' + T.Cloud.length;
    });
    check('nuvem: comum de mod no addCloud (peso alto), com OnSpawn', () => {
        cloudWeight = 1e6;
        let found = 0, active = 0;
        for (let i = 0; i < 200; i++) Main.cloud[i].active = false;
        for (let i = 0; i < 20; i++) Terraria.Cloud.addCloud();
        for (let i = 0; i < 200; i++) {
            if (!Main.cloud[i].active) continue;
            active++;
            if (Main.cloud[i].type === type) found++;
        }
        cloudWeight = 0;
        // O addCloud não põe nuvem em toda chamada (evita sobrepor): as que
        // entraram são todas de mod.
        // (uma rara do jogo, 1 em 150, não é trocada pela comum: como no tModLoader)
        return (found > 0 && found >= active - 1 && calls.cloudSpawn >= found) || 'de mod ' + found + ' de ' + active + ', OnSpawn ' + calls.cloudSpawn;
    });
    check('nuvem: peso 0 não aparece', () => {
        for (let i = 0; i < 200; i++) Main.cloud[i].active = false;
        for (let i = 0; i < 20; i++) Terraria.Cloud.addCloud();
        let found = 0;
        for (let i = 0; i < 200; i++) if (Main.cloud[i].active && Main.cloud[i].type === type) found++;
        return found === 0 || found;
    });
    check('nuvem: rara de mod no RollRareCloud', () => {
        rareWeight = 1e6;
        const got = Terraria.Cloud['int RollRareCloud()']();
        rareWeight = 0;
        return got === rare || got;
    });
}

// ---- emotes ----
let bubbleId = -1;
function emotes() {
    const type = ModContent.GetInstance(TestEmote).Type;
    check('emote: tipo depois dos 151', () => type >= 151 || type);
    check('emote: NewBubble chama o OnSpawn', () => {
        const anchor = Terraria.GameContent.UI.WorldUIAnchor.new();
        anchor['void .ctor(Entity anchor)'](Main.LocalPlayer);
        bubbleId = Terraria.GameContent.UI.EmoteBubble['int NewBubble(int emoticon, WorldUIAnchor bubbleAnchor, int time)'](type, anchor, 300);
        return calls.emoteSpawn === 1 || 'OnSpawn ' + calls.emoteSpawn;
    });
    check('emote: no menu (categoria geral)', () => {
        const win = GUIInstance.Active.GUIEmotesWindow;
        const list = win._emoteEntries;
        list.Clear();
        win['void GetEmotesGeneral(List`1 emotes)'](list);
        const ok = list.Contains(type);
        const item = exampleOn() && (() => {
            list.Clear();
            win['void GetEmotesItems(List`1 emotes)'](list);
            return list.Contains(ModContent.Find(ModEmoteBubble, 'examplemod/ExampleItemEmote').Type);
        })();
        return (ok && (!exampleOn() || item)) || 'geral ' + ok + ', itens (exemplo) ' + item;
    });
}

function emotesDrawn() {
    check('emote: a bolha de mod é desenhada', () => calls.emoteDraw > 0 || 'PreDraw ' + calls.emoteDraw);
}

// ---- conquistas ----
function achievements() {
    const inst = ModContent.GetInstance(TestAchievement);
    const manager = Main.Achievements;
    manager['bool Clear(string achievementName)'](inst.Name);
    check('conquista: registrada no gerenciador, com nome', () => {
        const a = manager['Achievement GetAchievement(string achievementName)'](inst.Name);
        return (a && a.FriendlyName.Value === 'Test Achievement') || (a ? a.FriendlyName.Value : 'sem conquista');
    });
    check('conquista: a condição completa e o OnCompleted roda', () => {
        inst.flag.Complete();
        return (inst.Achievement.IsCompleted && calls.completed === 1) || 'completa ' + inst.Achievement.IsCompleted + ', OnCompleted ' + calls.completed;
    });
    check('conquista: OnNPCKilled pelo aviso do jogo', () => {
        manager['bool Clear(string achievementName)'](inst.Name);
        Terraria.GameContent.Achievements.AchievementsHelper['void NotifyNPCKilledDirect(Player player, int npcNetID)'](Main.LocalPlayer, NPCID.GiantWormHead);
        return calls.npcKilled.includes(NPCID.GiantWormHead) || JSON.stringify(calls.npcKilled);
    });
    manager['bool Clear(string achievementName)'](inst.Name);
    if (!exampleOn()) return;
    check('Example: ManyWormsKilled conta os vermes e completa no 5º', () => {
        const worms = ModContent.Find(ModAchievement, 'examplemod/ManyWormsKilled');
        manager['bool Clear(string achievementName)'](worms.Name);
        const hiddenBefore = worms.Hidden;
        for (let i = 0; i < 5; i++) {
            Terraria.GameContent.Achievements.AchievementsHelper['void NotifyNPCKilledDirect(Player player, int npcNetID)'](Main.LocalPlayer, NPCID.GiantWormHead);
        }
        const done = worms.Achievement.IsCompleted;
        manager['bool Clear(string achievementName)'](worms.Name);
        return (hiddenBefore && done) || 'escondida antes ' + hiddenBefore + ', completa ' + done;
    });
    check('Example: ExampleBossKilled registrada', () =>
        !!manager['Achievement GetAchievement(string achievementName)']('examplemod/ExampleBossKilled') || 'sem conquista');
}

// ---- peixe de missão ----
function anglerQuest() {
    if (!exampleOn()) return;
    const fish = ModContent.ItemType('examplemod/ExampleQuestFish');
    const ids = Main.anglerQuestItemNetIDs;
    let index = -1;
    for (let i = 0; i < ids.length; i++) if (ids[i] === fish) index = i;
    check('pesca: o peixe na lista do Pescador', () => index >= 0 || 'fora da lista (' + ids.length + ')');
    if (index < 0) return;

    const saved = { quest: Main.anglerQuest, finished: Main.anglerQuestFinished, hard: Main.hardMode };
    try {
        check('pesca: a fala do Pescador vem do AnglerQuestChat', () => {
            Main.anglerQuest = index;
            Main.anglerQuestFinished = false;
            const text = Terraria.Lang['string AnglerQuestChat(bool turnIn)'](false);
            return (/upside-down|ponta-cabe/i.test(text) && text.includes('\n\n(') && Main.npcChatCornerItem === fish) || text;
        });
        check('pesca: fora do modo difícil o sorteio não cai nele', () => {
            Main.hardMode = false;
            for (let i = 0; i < 60; i++) {
                Main['void AnglerQuestSwap()']();
                if (Main.anglerQuestItemNetIDs[Main.anglerQuest] === fish) return 'sorteado na volta ' + i;
            }
            return true;
        });
        check('pesca: CatchFish de ponta-cabeça', () => {
            // O contexto de pesca como o FishingCheck do jogo prepara (sem ele
            // o RollItemDrop do jogo lança, e exceção do jogo no meio de um
            // hook derruba o processo).
            const p = Main.LocalPlayer;
            const bobber = Main.projectile[999];
            bobber.owner = Main.myPlayer;
            bobber.position = p.position;
            const attempt = Terraria.DataStructures.FishingAttempt.new();
            attempt.X = Math.floor(p.Center.X / 16);
            attempt.Y = Math.floor(p.Center.Y / 16);
            attempt.questFish = fish;
            attempt.uncommon = true;
            attempt.fishingLevel = 20;
            const P = Terraria.Projectile;
            bobber['FishingContext PrepareFishingContext(FishingContext context, FishingAttempt fisher)'](P._context, attempt);
            const gravity = p.gravDir;
            p.gravDir = -1;
            const ref = new Ref(attempt);
            try {
                bobber['void FishingCheck_RollItemDrop(ref FishingAttempt fisher)'](ref);
            } finally {
                p.gravDir = gravity;
            }
            return ref.value.rolledItemDrop === fish || 'saiu ' + ref.value.rolledItemDrop;
        });
    } finally {
        Main.anglerQuest = saved.quest;
        Main.anglerQuestFinished = saved.finished;
        Main.hardMode = saved.hard;
    }
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 60) {
        check('comandos', commands);
        check('cabelo', hair);
        check('nuvens', clouds);
        check('emotes', emotes);
        check('conquistas', achievements);
        check('pesca', anglerQuest);
    }
    if (frames === 120) {
        check('emotes (desenho)', emotesDrawn);
        done = true;
        bl.log('soltos FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('soltos: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestSoltos extends Mod {}
