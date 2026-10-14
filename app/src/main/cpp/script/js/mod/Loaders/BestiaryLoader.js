class BestiaryLoader {
    static #added = 0;
    static #finished = false;

    // O jogo já criou uma entrada genérica para cada NPC das amostras: sem
    // tirar, ficavam duas. Com HideFromBestiary, ou Hide no
    // NPCBestiaryDrawModifiers (corpo e cauda de serpente), a genérica sai e
    // não entra outra, como no jogo (GetExclusions).
    static Register(inst, townNpc) {
        const { BestiaryEntry } = Terraria.GameContent.Bestiary;
        const type = inst.Type;
        const db = Terraria.Main.BestiaryDB;
        const generic = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'](type);
        if (generic && db.Entries.Contains(generic)) db.Entries.Remove(generic);
        if (inst.HideFromBestiary || BestiaryLoader.#Hidden(type)) {
            BestiaryLoader.#added++;
            return;
        }

        // O retrato lê as estrelas quando a entrada nasce (BestiaryEntry.Enemy...).
        const stars = inst.BestiaryRarityStars | 0;
        if (stars > 0) Terraria.ID.ContentSamples.NpcBestiaryRarityStars.set_Item(type, stars);

        let entry;
        if (townNpc) entry = BestiaryEntry.TownNPC(type);
        else if (Terraria.ID.NPCID.Sets.CountsAsCritter[type]) entry = BestiaryEntry.Critter(type);
        else entry = BestiaryEntry.Enemy(type);

        // Os biomas de mod do SpawnModBiomes, antes do SetBestiary, como o
        // tModLoader. O SetDefaults o escreve na cópia de cada NPC: vale o da
        // amostra do jogo, ou o do modelo.
        const sample = Safe.Run(inst.constructor.name + ' (SpawnModBiomes)',
            () => NPCLoader.Of(Terraria.ID.ContentSamples.NpcsByNetId.get_Item(type)));
        const biomes = BiomeBestiaryLoader.Biomes((sample && sample.SpawnModBiomes) || inst.SpawnModBiomes);
        for (const biome of biomes) entry.Info.Add(BiomeBestiaryLoader.Of(biome));

        inst.SetBestiary(db, entry);
        Safe.Run(inst.constructor.name + ' (BestiaryIcon)', () => BestiaryLoader.#Icon(inst, entry));
        db['BestiaryEntry Register(BestiaryEntry entry)'](entry);
        db['void ExtractDropsForNPC(ItemDropDatabase dropsDatabase, int npcId)'](Terraria.Main.ItemDropsDB, type);
        BestiaryLoader.#added++;
    }

    static #Hidden(type) {
        const drawn = Terraria.ID.NPCID.Sets.NPCBestiaryDrawOffset;
        return drawn.ContainsKey(type) && !!drawn.get_Item(type).Hide;
    }

    // A imagem própria da entrada, no lugar do NPC desenhado: a lista e o
    // retrato do celular desenham o mesmo entry.Icon, e ele usa o
    // _customTexture quando tem. Vem do CustomTexturePath do
    // NPCBestiaryDrawModifiers (como no tModLoader) ou do BestiaryIcon
    // (padrão: a Texture + '_BestiaryIcon', como no TL Pro). O jogo pede o
    // CustomTexturePath ao Main.Assets, que não acha arquivo de mod: com ele no
    // modificador, o Update do ícone lançaria. Sai de lá e entra pronto.
    static #Icon(inst, entry) {
        const type = inst.Type;
        const drawn = Terraria.ID.NPCID.Sets.NPCBestiaryDrawOffset;
        let file = null;

        if (drawn.ContainsKey(type)) {
            const modifiers = drawn.get_Item(type);
            const custom = modifiers.CustomTexturePath;
            if (custom && !String(custom).startsWith('Images/')) {
                file = BestiaryLoader.#File(inst.Mod, custom);
                if (!file) bl.log(`${inst.constructor.name}: CustomTexturePath '${custom}' não está no mod`);
                modifiers.CustomTexturePath = null;
                drawn.set_Item(type, modifiers);
            }
        }
        if (!file) {
            const path = inst.BestiaryIcon || inst.Texture + '_BestiaryIcon';
            file = BestiaryLoader.#File(inst.Mod, path);
            if (!file && inst.BestiaryIcon) bl.log(`${inst.constructor.name}: BestiaryIcon '${inst.BestiaryIcon}' não está no mod`);
        }
        if (file) entry.Icon._customTexture = ContentLookup.RequestFile(file);
    }

    // O caminho completo do arquivo no mod (com ou sem o id do mod na frente).
    static #File(mod, path) {
        if (!mod || !mod.path) return null;
        const full = bl.path.join(mod.path, ModFiles.Texture(ModFiles.Normalize(path, mod), mod));
        return bl.file.exists(full) ? full : null;
    }

    static Finish() {
        if (BestiaryLoader.#finished || !BestiaryLoader.#added) return;

        BestiaryLoader.#finished = true;
        Safe.Run('Bestiario: filtros de bioma', () => BiomeBestiaryLoader.Finish());
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
