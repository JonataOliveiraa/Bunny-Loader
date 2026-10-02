// As conquistas de mod, como o ModAchievement do tModLoader, no gerenciador do
// jogo (Main.Achievements). Com o jogo pronto: o Achievement, o nome e a
// descrição, as condições (SetStaticDefaults), o registro, o ícone e a
// categoria; depois o arquivo de conquistas é lido de novo (o jogo o leu no
// boot, sem as de mod, e o progresso delas ficaria para trás).
//
// No menu (GUIAchievementsMenu) e no aviso de conquista (AchievementUnlockedPopup)
// a folha de ícones do jogo é trocada pela do mod.
class AchievementLoader {
    static ByName = new Map();
    static #pending = [];
    static #hidden = null;

    static Add(inst) {
        inst.Name = (inst.Mod ? inst.Mod.id || inst.Mod.uuid : 'bl') + '/' + inst.constructor.name;
        AchievementLoader.ByName.set(inst.Name, inst);

        const file = ModFiles.Texture(inst.Texture);
        inst.__file = bl.file.exists(file) ? bl.mod.path + '/' + file : null;
        // Os textos agora, com o mod na pilha: Achievements.<Classe>.X.
        const text = (what) => ModLocalization.TryTranslate('Achievements.' + inst.constructor.name + '.' + what);
        inst.__name = text('FriendlyName') || text('Name') || inst.constructor.name.replace(/([A-Z])/g, ' $1').trim();
        inst.__description = text('Description');

        AchievementLoader.#pending.push(inst);
        if (AchievementLoader.#pending.length === 1) Ready.Add(() => AchievementLoader.#Install(), 'setup');
        AchievementLoader.#Hook(inst.constructor);
    }

    // O Achievement do jogo pelo nome ('mod/Classe'), ou undefined.
    static Of(achievement) {
        return achievement ? AchievementLoader.ByName.get(achievement.Name) : undefined;
    }

    static #Text(key, text) {
        LocalizationLoader.Register(key, text);
        return Terraria.Localization.Language['LocalizedText GetText(string key)'](key);
    }

    static #Install() {
        const pending = AchievementLoader.#pending.splice(0);
        if (!pending.length) return;

        const manager = Terraria.Main.Achievements;
        const A = Terraria.Achievements.Achievement;
        for (const inst of pending) {
            const name = inst.constructor.name;
            Safe.Run(name, () => {
                const achievement = A.new();
                achievement['void .ctor(string name)'](inst.Name);
                achievement.FriendlyName = AchievementLoader.#Text('Achievements.' + inst.Name + '_Name', inst.__name);
                achievement.Description = AchievementLoader.#Text('Achievements.' + inst.Name + '_Description', inst.__description || '');
                inst.Achievement = achievement;
                if (inst.__file) inst.__asset = bl.loadTextureAsset(inst.__file);
                else bl.log(name + ': sem a textura ' + inst.Texture);

                Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
                AchievementLoader.#AutoTracker(inst);

                manager['void Register(Achievement achievement)'](achievement);
                manager['void RegisterIconIndex(string achievementName, int iconIndex)'](inst.Name, inst.Index);
                manager['void RegisterAchievementCategory(string achievementName, AchievementCategory category)'](inst.Name, inst.Category);
            });
        }

        // O progresso salvo das de mod (o jogo leu o arquivo sem elas).
        Safe.Run('conquistas: ler de novo', () => AchievementLoader.#Reload(manager));
        bl.log('conquistas de mod: ' + pending.length);
    }

    // O Achievement.Load SOMA as condições completas ao _completedCount, e o
    // IsCompleted é "contador == condições". Ler o arquivo de novo dobrava o
    // contador das do jogo (já lidas no boot): toda conquista do jogo já
    // completa aparecia bloqueada, e a condição, já feita, não a completava
    // mais. As do jogo voltam ao contador de antes; só as de mod ficam com o
    // que a leitura trouxe. O Load(path, cloud) é a leitura sem o
    // LoadPersistantData do usuário, que o Load() faz depois dela.
    static #Reload(manager) {
        const kept = [];
        const list = manager.CreateAchievementsList();
        for (let i = 0; i < list.Count; i++) {
            const a = list.get_Item(i);
            if (!AchievementLoader.ByName.has(a.Name)) kept.push([a, a._completedCount]);
        }
        try {
            manager['void Load(string path, bool cloud)'](manager._savePath, manager._isCloudSave);
        } finally {
            for (const [a, count] of kept) a._completedCount = count;
        }
    }

    // O AutoStaticDefaults do tModLoader: com várias condições, o progresso é
    // quantas completaram; com uma, o da própria condição (as de número).
    static #AutoTracker(inst) {
        const achievement = inst.Achievement;
        if (achievement.HasTracker) return;
        const conditions = inst.__conditions || [];
        if (conditions.length > 1) {
            achievement['void UseConditionsCompletedTracker()']();
            return;
        }
        const condition = conditions[0];
        const type = condition ? condition.GetType().Name : '';
        if (type === 'CustomIntCondition' || type === 'CustomFloatCondition') {
            Safe.Run(inst.constructor.name + ' (tracker)',
                () => achievement['void UseTrackerFromCondition(string conditionName)'](condition.Name));
        }
    }

    static #Hook(cls) {
        const has = (name) => Hooks.Overrides(cls, ModAchievement, name);
        const Helper = Terraria.GameContent.Achievements.AchievementsHelper;
        const each = (method, fn) => {
            for (const inst of AchievementLoader.ByName.values()) {
                if (!inst.Achievement || inst.Achievement.IsCompleted) continue;
                if (!Hooks.Overrides(inst.constructor, ModAchievement, method)) continue;
                Safe.Run(inst.constructor.name + '.' + method, () => fn(inst));
            }
        };

        Hooks.Once('achievement.draw', () => AchievementLoader.#HookDraw());

        if (has('OnCompleted')) Hooks.Once('achievement.OnCompleted', () => {
            Terraria.Achievements.Achievement['void OnConditionComplete(AchievementCondition condition)'].hook((original, self, condition) => {
                const inst = AchievementLoader.Of(self);
                const before = inst ? self.IsCompleted : true;
                original(self, condition);
                if (inst && !before && self.IsCompleted) Safe.Run(inst.constructor.name + '.OnCompleted', () => inst.OnCompleted(self));
            });
        });

        if (has('OnNPCKilled')) Hooks.Once('achievement.OnNPCKilled', () => {
            Helper['void NotifyNPCKilledDirect(Player player, int npcNetID)'].hook((original, player, npcId) => {
                original(player, npcId);
                each('OnNPCKilled', (inst) => inst.OnNPCKilled(player, npcId));
            });
        });

        if (has('OnItemPickup')) Hooks.Once('achievement.OnItemPickup', () => {
            Helper['void NotifyItemPickup(Player player, Item item, int customStack)'].hook((original, player, item, stack) => {
                original(player, item, stack);
                each('OnItemPickup', (inst) => inst.OnItemPickup(player, item.type, stack));
            });
        });

        if (has('OnItemCraft')) Hooks.Once('achievement.OnItemCraft', () => {
            Helper['void NotifyItemCraft(Recipe recipe)'].hook((original, recipe) => {
                original(recipe);
                const item = recipe.createItem;
                each('OnItemCraft', (inst) => inst.OnItemCraft(item.type, item.stack));
            });
        });

        if (has('OnTileDestroyed')) Hooks.Once('achievement.OnTileDestroyed', () => {
            Helper['void NotifyTileDestroyed(Player player, ushort tile)'].hook((original, player, tile) => {
                original(player, tile);
                each('OnTileDestroyed', (inst) => inst.OnTileDestroyed(player, tile));
            });
        });
    }

    static #HookDraw() {
        const Menu = GUIAchievementsMenu;

        // O menu guarda a lista na primeira vez que a monta, e isso pode ter
        // sido antes das de mod: na primeira abertura depois delas, refeita.
        const refreshed = new Set();
        Menu['void Open(int backTo, int selected)'].hook((original, self, backTo, selected) => {
            const address = bl.addressOf(self);
            if (!refreshed.has(address) && AchievementLoader.#pending.length === 0) {
                refreshed.add(address);
                self._achievements = null;
            }
            original(self, backTo, selected);
        });

        // A entrada de mod no menu: a folha de ícones do mod e, se escondida,
        // "???" no lugar do nome e da descrição.
        Menu['void AchievementDraw(ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)'].hook(
            (original, self, grid, index, position, scale) => {
                const list = self.SortedAchievementsData;
                const achievement = list && index >= 0 && index < list.Count ? list.get_Item(index) : null;
                const inst = AchievementLoader.Of(achievement);
                if (!inst || !inst.__asset) return original(self, grid, index, position, scale);

                const icons = Menu.AchievementsIcons;
                const name = achievement.FriendlyName, description = achievement.Description;
                const hide = !achievement.IsCompleted && Safe.Run(inst.constructor.name + '.Hidden', () => inst.Hidden);
                if (hide) {
                    AchievementLoader.#hidden = AchievementLoader.#hidden ||
                        Terraria.Localization.Language['LocalizedText GetText(string key)']('???');
                    achievement.FriendlyName = AchievementLoader.#hidden;
                    achievement.Description = AchievementLoader.#hidden;
                }
                Menu.AchievementsIcons = inst.__asset.Value;
                try {
                    original(self, grid, index, position, scale);
                } finally {
                    Menu.AchievementsIcons = icons;
                    if (hide) {
                        achievement.FriendlyName = name;
                        achievement.Description = description;
                    }
                }
            });

        // O aviso de conquista completada, com o ícone do mod.
        Terraria.UI.InGamePopups.AchievementUnlockedPopup['void .ctor(Achievement achievement)'].hook((original, self, achievement) => {
            original(self, achievement);
            const inst = AchievementLoader.Of(achievement);
            if (inst && inst.__asset) self._achievementTexture = inst.__asset;
        });
    }
}
