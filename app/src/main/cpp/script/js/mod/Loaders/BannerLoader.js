// Banner IDs de mod são os tipos dos NPCs representantes, como no tML.
// Os IDs vanilla continuam sendo os do BannerSystem (não NPCIDs).
class BannerLoader {
    static ByItem = new Map();
    static #items = new Map();
    static #kept = {};
    static #networkSave = 0;
    static #size = FIRST_NPC;

    static Track(m) {
        const template = NPCLoader.ByType.get(m.Type);
        if (!template) return;
        template.Banner = m.Banner | 0;
        template.BannerItem = m.BannerItem | 0;
    }

    static Resize() {
        const B = Terraria.GameContent.BannerSystem;
        let size = Math.max(FIRST_NPC, Terraria.Main.npcFrameCount.length);
        for (const type of NPCLoader.ByType.keys()) size = Math.max(size, type + 1);
        BannerLoader.#size = size;
        for (const key of ['killCount', 'claimableBanners', 'newBanner']) {
            const array = B[key];
            if (array.length < size) B[key] = array.cloneResized(size);
        }
        BannerLoader.Metrics(Terraria.Main.SceneMetrics);
        BannerLoader.Metrics(Terraria.Main.PlayerSceneMetrics);
    }

    static Metrics(metrics) {
        if (!metrics) return;
        const size = Math.max(BannerLoader.#size, Terraria.Main.npcFrameCount.length);
        const array = metrics.NPCBannerBuff;
        if (array.length < size) metrics.NPCBannerBuff = array.cloneResized(size);
    }

    static Finish() {
        BannerLoader.Resize();
        BannerLoader.#items.clear();
        BannerLoader.ByItem.clear();
        for (const m of NPCLoader.ByType.values()) {
            if (m.Banner > 0 && m.BannerItem > 0) {
                const previous = BannerLoader.#items.get(m.Banner);
                if (previous && previous !== m.BannerItem) {
                    bl.error(m.constructor.name + ': BannerItem conflita com o da bandeira ' + m.Banner);
                    continue;
                }
                BannerLoader.#items.set(m.Banner, m.BannerItem);
                BannerLoader.ByItem.set(m.BannerItem, m.Banner);
                bl.hookMarks.set('npc.BannerItem', m.Banner);
            }
        }
        for (const m of NPCLoader.ByType.values()) {
            if (m.BannerItem && !m.Banner || m.Banner >= FIRST_NPC && !BannerLoader.#items.has(m.Banner))
                bl.error(m.constructor.name + ': configure Banner e BannerItem no NPC representante');
        }
    }

    static Install() {
        Hooks.Once('npc.Banners', () => {
            const B = Terraria.GameContent.BannerSystem;
            B['int NPCtoBanner(int i)'].hook((original, type) => {
                const m = NPCLoader.ByType.get(type);
                return m ? m.Banner | 0 : original(type);
            }, { minType: FIRST_NPC, arg: 0 });
            B['int BannerToItem(int banner)'].hook((original, banner) =>
                BannerLoader.#items.get(banner) || original(banner), { minType: 0, arg: 0, marks: 'npc.BannerItem' });
            B['int BannerToNPC(int i)'].hook((original, banner) =>
                NPCLoader.ByType.has(banner) ? banner : original(banner), { minType: FIRST_NPC, arg: 0 });
            Terraria.SceneMetrics['void Reset()'].hook((original, metrics) => {
                BannerLoader.Metrics(metrics);
                original(metrics);
            });
            B['void Clear()'].hook(original => {
                original();
                BannerLoader.#kept = {};
                BannerLoader.Resize();
                for (const key of ['killCount', 'claimableBanners', 'newBanner']) {
                    const array = B[key];
                    for (let i = FIRST_NPC; i < array.length; i++) array[i] = key === 'newBanner' ? false : 0;
                }
            });
            // O protocolo nativo sincroniza arrays estendidos por índice. O
            // arquivo vanilla guarda só IDs vanilla; os de mod vão no sidecar
            // do ModSystem por UID/nome, para resistir a mudanças na carga.
            Terraria.GameContent.BannerSystem.NetBannersModule['NetPacket WriteFullState()'].hook(original => {
                BannerLoader.#networkSave++;
                try { return original(); }
                finally { BannerLoader.#networkSave--; }
            });
            B['void Save(BinaryWriter writer)'].hook((original, writer) => {
                if (BannerLoader.#networkSave) return original(writer);
                const kills = B.killCount, claimable = B.claimableBanners;
                B.killCount = kills.cloneResized(B.MaxBannerTypes);
                B.claimableBanners = claimable.cloneResized(B.MaxBannerTypes);
                try { return original(writer); }
                finally { B.killCount = kills; B.claimableBanners = claimable; }
            });
            const system = new BannerWorldSystem();
            SystemLoader.Add(system, 'BunnyLoader/Banners');
            SystemLoader.Hook(BannerWorldSystem);
            Ready.Add(BannerLoader.Finish, 'setup');
        });
    }

    static Snapshot() {
        const B = Terraria.GameContent.BannerSystem, result = { ...BannerLoader.#kept };
        for (const m of NPCLoader.ByType.values()) {
            const key = m.Mod.uuid + '/' + m.constructor.name;
            const count = B.killCount[m.Type], claimable = B.claimableBanners[m.Type];
            if (count || claimable) result[key] = { count, claimable, fresh: !!B.newBanner[m.Type] };
            else delete result[key];
        }
        return result;
    }

    static Restore(data) {
        BannerLoader.Resize();
        BannerLoader.#kept = data && typeof data === 'object' && !Array.isArray(data) ? { ...data } : {};
        const B = Terraria.GameContent.BannerSystem;
        for (const m of NPCLoader.ByType.values()) {
            const entry = BannerLoader.#kept[m.Mod.uuid + '/' + m.constructor.name];
            const count = entry && Number.isInteger(entry.count) && entry.count >= 0 ? Math.min(entry.count, 2147483647) : 0;
            const claimable = entry && Number.isInteger(entry.claimable) && entry.claimable >= 0 ? Math.min(entry.claimable, 65535) : 0;
            B.killCount[m.Type] = count;
            B.claimableBanners[m.Type] = claimable;
            B.newBanner[m.Type] = claimable > 0 && !!entry.fresh;
        }
        B['void RefreshHasBannerAvailable()']();
        B['void RefreshAnyNewClaimableBanners()']();
        B.bannerState++;
    }
}

class BannerWorldSystem extends ModSystem {
    SaveWorldData(tag) { tag.banners = BannerLoader.Snapshot(); }
    LoadWorldData(tag) { BannerLoader.Restore(tag.banners); }
    NetSend(writer) { writer.Write(BannerLoader.Snapshot()); }
    NetReceive(reader) { BannerLoader.Restore(reader.Read()); }
}
