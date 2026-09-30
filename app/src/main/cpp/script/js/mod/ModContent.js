class ModContent {
    static ItemType(which) { return ContentLookup.TypeOf(ItemLoader.ByType, which); }
    static ProjectileType(which) { return ContentLookup.TypeOf(ProjectileLoader.ByType, which); }
    static NPCType(which) { return ContentLookup.TypeOf(NPCLoader.ByType, which); }
    static BuffType(which) { return ContentLookup.TypeOf(BuffLoader.ByType, which); }
    static TileType(which) { return ContentLookup.TypeOf(TileLoader.ByType, which); }
    static PrefixType(which) { return ContentLookup.TypeOf(PrefixLoader.ByType, which); }
    static MountType(which) { return ContentLookup.TypeOf(MountLoader.ByType, which); }

    static GetInstance(cls) { return Templates.Get(cls); }

    // ModContent.Find(ModItem, 'examplemod/ExampleItem'); lança se não há.
    static Find(base, name) {
        const inst = ContentLookup.Find(ContentLookup.Registry(base), name);
        if (!inst) throw new Error("ModContent.Find: nao ha '" + name + "'");

        return inst;
    }

    static TryFind(base, name, result) {
        const inst = ContentLookup.Find(ContentLookup.Registry(base), name);
        if (result && typeof result === 'object') result.value = inst;
        return inst !== undefined;
    }

    static GetModItem(type) { return ItemLoader.ByType.get(type); }
    static GetModProjectile(type) { return ProjectileLoader.ByType.get(type); }
    static GetModNPC(type) { return NPCLoader.ByType.get(type); }
    static GetModBuff(type) { return BuffLoader.ByType.get(type); }
    static GetModTile(type) { return TileLoader.ByType.get(type); }
    static GetModPrefix(type) { return PrefixLoader.ByType.get(type); }
    static GetModMount(type) { return MountLoader.ByType.get(type); }

    // Carregada uma vez, só na thread do jogo com ele rodando (num hook, no
    // SetStaticDefaults ou no PostSetupContent).
    static Request(path) { return ContentLookup.Request(path); }
    static Texture(path) { return ContentLookup.Request(path).Value; }
    static HasAsset(path) { return ContentLookup.FindTexture(path) !== null; }
    static SoundStyle(path, options) { return new SoundStyle(path, options); }
}

Object.freeze(ModContent);
