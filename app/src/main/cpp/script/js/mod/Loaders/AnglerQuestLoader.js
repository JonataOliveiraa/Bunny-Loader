// Peixe de missão do Pescador, como no tModLoader: o item com
// ItemID.Sets.IsQuestFish[Type] (ou IsQuestFish() true, do ExMod) entra no
// fim de Main.anglerQuestItemNetIDs. No sorteio do dia (AnglerQuestSwap), um de
// mod com IsAnglerQuestAvailable() false é sorteado de novo. A fala do
// Pescador (Lang.AnglerQuestChat) vem do AnglerQuestChat do item: o jogo
// buscaria "AnglerQuestText.Quest_<nome>", que não existe para item de mod.
class AnglerQuestLoader {
    static #fish = new Set();

    static Watch() {
        Ready.Add(() => AnglerQuestLoader.#Install());
    }

    static #IsQuestFish(type, m) {
        const set = Terraria.ID.ItemID.Sets.IsQuestFish;
        return (type < set.length && set[type]) ||
            Safe.Run(m.constructor.name + '.IsQuestFish', () => m.IsQuestFish()) === true;
    }

    static #Install() {
        const Main = Terraria.Main;
        const old = Main.anglerQuestItemNetIDs;
        const has = new Set();
        for (let i = 0; i < old.length; i++) has.add(old[i]);
        const added = [];
        for (const [type, m] of ItemLoader.ByType) {
            if (AnglerQuestLoader.#fish.has(type) || !AnglerQuestLoader.#IsQuestFish(type, m)) continue;
            AnglerQuestLoader.#fish.add(type);
            if (!has.has(type)) added.push(type);
        }
        if (!AnglerQuestLoader.#fish.size) return;
        if (added.length) {
            const ids = old.cloneResized(old.length + added.length);
            added.forEach((type, i) => { ids[old.length + i] = type; });
            Main.anglerQuestItemNetIDs = ids;
        }
        Hooks.Once('angler.quest', () => AnglerQuestLoader.#Hook());
        bl.log('peixes de missão de mod: ' + AnglerQuestLoader.#fish.size);
    }

    static #Available(type) {
        const m = ItemLoader.ByType.get(type);
        return !m || Safe.Run(m.constructor.name + '.IsAnglerQuestAvailable', () => m.IsAnglerQuestAvailable()) !== false;
    }

    // A fala do tModLoader: "<descrição>\n\n(<onde pescar>)".
    static Chat(type) {
        const m = ItemLoader.ByType.get(type);
        const description = new Ref(''), location = new Ref('');
        if (m) Safe.Run(m.constructor.name + '.AnglerQuestChat', () => m.AnglerQuestChat(description, location));
        const text = description.value || Terraria.Lang['string GetItemNameValue(int id)'](type);
        return location.value ? text + '\n\n(' + location.value + ')' : text;
    }

    static #Hook() {
        const Main = Terraria.Main;

        // Sorteia de novo enquanto cair num de mod indisponível (com teto).
        Main['void AnglerQuestSwap()'].hook((original) => {
            original();
            for (let tries = 0; tries < 50; tries++) {
                const ids = Main.anglerQuestItemNetIDs;
                if (Main.anglerQuestFinished || AnglerQuestLoader.#Available(ids[Main.anglerQuest])) return;
                original();
            }
        });

        Terraria.Lang['string AnglerQuestChat(bool turnIn)'].hook((original, turnIn) => {
            const type = Main.anglerQuestItemNetIDs[Main.anglerQuest];
            if (turnIn || Main.anglerQuestFinished || !AnglerQuestLoader.#fish.has(type)) return original(turnIn);
            Main.npcChatCornerItem = type;
            return AnglerQuestLoader.Chat(type);
        });
    }
}
