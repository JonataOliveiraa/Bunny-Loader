class BestiaryLoader {
    static #added = 0;
    static #finished = false;

    // O jogo já criou uma entrada genérica para cada NPC das amostras: sem
    // tirar, ficavam duas.
    static Register(inst, townNpc) {
        const { BestiaryEntry } = Terraria.GameContent.Bestiary;
        const type = inst.Type;

        let entry;
        if (townNpc) entry = BestiaryEntry.TownNPC(type);
        else if (Terraria.ID.NPCID.Sets.CountsAsCritter[type]) entry = BestiaryEntry.Critter(type);
        else entry = BestiaryEntry.Enemy(type);

        const db = Terraria.Main.BestiaryDB;
        const generic = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'](type);
        if (generic && db.Entries.Contains(generic)) db.Entries.Remove(generic);

        inst.SetBestiary(db, entry);
        db['BestiaryEntry Register(BestiaryEntry entry)'](entry);
        db['void ExtractDropsForNPC(ItemDropDatabase dropsDatabase, int npcId)'](Terraria.Main.ItemDropsDB, type);
        BestiaryLoader.#added++;
    }

    static Finish() {
        if (BestiaryLoader.#finished || !BestiaryLoader.#added) return;

        BestiaryLoader.#finished = true;
        Safe.Run('Bestiario', () => Terraria.ID.ContentSamples['void CreateBestiarySortingIds(BestiaryDatabase database)'](Terraria.Main.BestiaryDB));
        bl.log('Bestiario: ' + BestiaryLoader.#added + ' NPC(s) de mod');
    }

    // O Bestiário tira os drops uma vez, ao ser montado: para os NPCs cuja
    // tabela mudou, os drops antigos saem e a tabela é lida de novo.
    static RefreshDrops(types) {
        const db = Terraria.Main.BestiaryDB;

        for (const type of new Set(types)) {
            Safe.Run('Bestiario: drops do NPC ' + type, () => {
                const entry = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'](type);
                if (!entry) return;

                const info = entry.Info;
                for (let i = info.Count - 1; i >= 0; i--) {
                    if (info.get_Item(i).GetType().Name === 'ItemDropBestiaryInfoElement') info.RemoveAt(i);
                }
                db['void ExtractDropsForNPC(ItemDropDatabase dropsDatabase, int npcId)'](Terraria.Main.ItemDropsDB, type);
            });
        }
    }
}
