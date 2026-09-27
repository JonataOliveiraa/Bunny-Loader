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
}
