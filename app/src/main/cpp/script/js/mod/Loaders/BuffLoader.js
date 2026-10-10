class BuffLoader {
    static ByType = new Map();

    // Os buffs de mod ativos de um jogador ou NPC. Se o fn tirou o buff, o jogo
    // puxou os de trás uma posição, e o que veio para i ainda não rodou.
    static ForEach(entity, fn) {
        const types = entity.buffType;
        const times = entity.buffTime;

        for (let i = 0; i < types.length; i++) {
            const type = types[i];
            if (type < FIRST_BUFF || times[i] <= 0) continue;

            const m = BuffLoader.ByType.get(type);
            if (!m) continue;

            fn(m, i);
            if (types[i] !== type) i--;
        }
    }

    static Hook(cls) {
        const P = Terraria.Player;
        const has = (name) => Hooks.Overrides(cls, ModBuff, name);
        const byType = BuffLoader.ByType;

        // Dentro do UpdateBuffs: antes dos acessórios, como os buffs do jogo.
        if (has('UpdatePlayer')) Hooks.Once('buff.UpdatePlayer', () => {
            P['void UpdateBuffs(int i)'].hook((original, self, i) => {
                original(self, i);
                BuffLoader.ForEach(self, (m, index) =>
                    Safe.Run(m.constructor.name + '.UpdatePlayer', () => m.UpdatePlayer(self, index)));
            });
        });

        if (has('UpdateNPC')) Hooks.Once('buff.UpdateNPC', () => {
            Terraria.NPC['void UpdateNPC_BuffSetFlags(bool lowerBuffTime)'].hook((original, self, lower) => {
                original(self, lower);
                BuffLoader.ForEach(self, (m, index) =>
                    Safe.Run(m.constructor.name + '.UpdateNPC', () => m.UpdateNPC(self, index)));
            });
        });

        if (has('ApplyPlayer')) Hooks.Once('buff.ApplyPlayer', () => {
            P['bool AddBuff_ActuallyTryToAddTheBuff(int type, int time)'].hook((original, self, type, time) => {
                const ok = original(self, type, time);

                const m = ok ? byType.get(type) : undefined;
                if (m) Safe.Run(m.constructor.name + '.ApplyPlayer', () => m.ApplyPlayer(self, time));
                return ok;
            });
        });

        if (has('ReApplyPlayer')) Hooks.Once('buff.ReApplyPlayer', () => {
            P['bool AddBuff_TryUpdatingExistingBuffTime(int type, int time)'].hook((original, self, type, time) => {
                const m = byType.get(type);
                if (!m) return original(self, type, time);

                const index = self['int FindBuffIndex(int type)'](type);
                const renew = index < 0 || Safe.Run(m.constructor.name + '.ReApplyPlayer', () => m.ReApplyPlayer(self, time, index));
                if (renew === false) return true;   // "já estava": o jogo não põe outro

                return original(self, type, time);
            });
        });

        if (has('ApplyNPC') || has('ReApplyNPC')) Hooks.Once('buff.NPC', () => {
            Terraria.NPC['void AddBuff(int type, int time, bool quiet)'].hook((original, self, type, time, quiet) => {
                const m = byType.get(type);
                if (!m) return original(self, type, time, quiet);

                const index = self['int FindBuffIndex(int type)'](type);
                if (index >= 0) {
                    if (Safe.Run(m.constructor.name + '.ReApplyNPC', () => m.ReApplyNPC(self, time, index)) === false) return;
                    return original(self, type, time, quiet);
                }

                original(self, type, time, quiet);
                if (self['int FindBuffIndex(int type)'](type) >= 0) {
                    Safe.Run(m.constructor.name + '.ApplyNPC', () => m.ApplyNPC(self, time));
                }
            });
        });

        // O texto do buff tocado: o GUIBuffs pede o nome ao Lang.GetBuffName (no
        // Draw) e a dica ao Main.GetBuffTooltip (no ChangeBuffString).
        if (has('ModifyBuffText')) Hooks.Once('buff.Text', BuffLoader.#HookText);

        // O toque no ícone da barra de buffs do jogador local.
        if (has('CanRemove') || has('OnRemove')) Hooks.Once('buff.Remove', () => {
            bl.classOf('', 'GUIBuffs')['void RemoveBuff(int buff)'].hook((original, self, index) => {
                const player = Terraria.Main.player[Terraria.Main.myPlayer];
                const type = player.buffType[index];
                const time = player.buffTime[index];
                const m = byType.get(type);
                if (!m) return original(self, index);

                const debuff = !!Terraria.Main.debuff[type];
                const can = Safe.Run(m.constructor.name + '.CanRemove', () => m.CanRemove(player, time, index, debuff));
                if (can === false) return;

                if (can === true && debuff) player['void DelBuff(int b)'](index);
                else original(self, index);

                if (player.buffType[index] !== type) {
                    Safe.Run(m.constructor.name + '.OnRemove', () => m.OnRemove(player, time, index));
                }
            });
        });
    }

    static #texting = false;   // dentro do ModifyBuffText: o nome e a dica saem do jogo

    // { name, tip } depois do ModifyBuffText do buff, ou null.
    static #Text(type, name, tip) {
        const m = BuffLoader.ByType.get(type);
        if (!m || !Hooks.Overrides(m.constructor, ModBuff, 'ModifyBuffText')) return null;

        const buffName = new Ref(name), buffTip = new Ref(tip), rare = new Ref(0);
        BuffLoader.#texting = true;
        try {
            Safe.Run(m.constructor.name + '.ModifyBuffText', () => m.ModifyBuffText(buffName, buffTip, rare));
        } finally {
            BuffLoader.#texting = false;
        }
        return { name: String(buffName.value ?? ''), tip: String(buffTip.value ?? '') };
    }

    static #HookText() {
        const Lang = Terraria.Lang;

        Lang['string GetBuffName(int id)'].hook((original, id) => {
            const name = original(id);
            if (BuffLoader.#texting) return name;
            const text = BuffLoader.#Text(id, name, Lang['string GetBuffDescription(int id)'](id));
            return text ? text.name : name;
        }, { minType: FIRST_BUFF, arg: 0 });

        Terraria.Main['string GetBuffTooltip(Player player, int buffType)'].hook((original, player, type) => {
            const tip = original(player, type);
            if (BuffLoader.#texting) return tip;
            BuffLoader.#texting = true;
            let name;
            try {
                name = Lang['string GetBuffName(int id)'](type);
            } finally {
                BuffLoader.#texting = false;
            }
            const text = BuffLoader.#Text(type, name, tip);
            return text ? text.tip : tip;
        }, { minType: FIRST_BUFF, arg: 1 });
    }
}
