// Cada jogador tem a própria instância de cada ModPlayer, criada na primeira
// vez que alguém pergunta por ela, no campo `ModPlayers` ao lado do Player.
class PlayerLoader {
    static Classes = [];

    static #keys = new Map();         // Classe -> '<uid>/<Classe>', a chave dos dados salvos
    static #owners = new Map();
    static #overriders = new Map();

    static Add(cls) {
        const mod = bl.mod || null;
        const key = (mod ? mod.uuid : 'sem-mod') + '/' + cls.name;
        for (const other of PlayerLoader.Classes) {
            if (PlayerLoader.#keys.get(other) === key) throw new TypeError('ModPlayer.register: este mod ja tem um ModPlayer chamado ' + cls.name);
        }
        PlayerLoader.Classes.push(cls);
        PlayerLoader.#keys.set(cls, key);
        PlayerLoader.#owners.set(cls, mod);
        PlayerLoader.#overriders.clear();
    }

    static Of(player) {
        let all = player.ModPlayers;
        const classes = PlayerLoader.Classes;
        if (all !== undefined && all.size === classes.length) return all;

        const fresh = all === undefined;
        if (fresh) all = new Map();

        const address = bl.addressOf(player);
        const created = [];
        for (const cls of classes) {
            if (all.has(cls)) continue;

            const inst = new cls();
            inst.__entity = address;
            all.set(cls, inst);
            created.push(inst);
        }

        if (fresh) player.ModPlayers = all;
        for (const inst of created) Safe.Run(inst.constructor.name + '.Initialize', () => inst.Initialize());
        return all;
    }

    static Find(player, which) {
        const all = PlayerLoader.Of(player);
        if (typeof which === 'function') return all.get(which);

        const name = String(which);
        const slash = name.lastIndexOf('/');
        if (slash > 0) {
            const mod = ModRegistry.Find(name.slice(0, slash), 'GetModPlayer');
            const wanted = name.slice(slash + 1);
            const cls = mod && PlayerLoader.Classes.find((c) => c.name === wanted && PlayerLoader.#owners.get(c) === mod);
            return cls ? all.get(cls) : undefined;
        }

        const named = PlayerLoader.Classes.filter((c) => c.name === name);
        if (named.length === 1) return all.get(named[0]);
        if (named.length === 0) return undefined;

        const caller = bl.mod;
        const own = caller && named.find((c) => PlayerLoader.#owners.get(c) === caller);
        if (own) return all.get(own);
        Safe.Once('GetModPlayer:' + name, "GetModPlayer: '" + name + "' existe em " + named.length + " mods; peca pela classe ou por 'mod/" + name + "'");
        return undefined;
    }

    static Each(player, method, fn) {
        const list = PlayerLoader.#OverridersOf(method);
        if (list.length === 0) return;

        const all = PlayerLoader.Of(player);
        for (let i = 0; i < list.length; i++) {
            const cls = list[i];
            try { fn(all.get(cls)); }
            catch (error) { Safe.Report(cls.name + '.' + method, error); }
        }
    }

    static Any(player, method, value, fn) {
        let hit = false;
        PlayerLoader.Each(player, method, (inst) => {
            if (fn(inst) === value) hit = true;
        });
        return hit;
    }

    static Call(player, method, ...args) {
        const list = PlayerLoader.#OverridersOf(method);
        if (!list.length) return;
        const all = PlayerLoader.Of(player);
        for (let i = 0; i < list.length; i++) PlayerLoader.#Invoke(all.get(list[i]), method, player, args);
    }

    static Veto(player, method, ...args) {
        const list = PlayerLoader.#OverridersOf(method);
        if (!list.length) return false;
        const all = PlayerLoader.Of(player);
        let veto = false;
        for (let i = 0; i < list.length; i++) {
            if (PlayerLoader.#Invoke(all.get(list[i]), method, player, args) === false) veto = true;
        }
        return veto;
    }

    static Nullable(player, method, ...args) {
        let result = null;
        const list = PlayerLoader.#OverridersOf(method);
        if (!list.length) return result;
        const all = PlayerLoader.Of(player);
        for (let i = 0; i < list.length; i++) {
            const value = PlayerLoader.#Invoke(all.get(list[i]), method, player, args);
            if (value === false) result = false;
            else if (value === true && result !== false) result = true;
        }
        return result;
    }

    static First(player, method, ...args) {
        const list = PlayerLoader.#OverridersOf(method);
        if (!list.length) return false;
        const all = PlayerLoader.Of(player);
        for (let i = 0; i < list.length; i++) {
            if (PlayerLoader.#Invoke(all.get(list[i]), method, player, args) === true) return true;
        }
        return false;
    }

    static Factor(player, method, item) {
        let factor = 1;
        const list = PlayerLoader.#OverridersOf(method);
        if (!list.length) return factor;
        const all = PlayerLoader.Of(player);
        for (let i = 0; i < list.length; i++) {
            const inst = all.get(list[i]);
            let value;
            try { value = inst[method](player, item); }
            catch (error) { Safe.Report(list[i].name + '.' + method, error); }
            if (Number.isFinite(value) && value > 0) factor *= value;
        }
        return Number.isFinite(factor) && factor > 0 ? factor : 1;
    }

    static Wants(cls, methods, key, install) {
        if (methods.some((name) => Hooks.Overrides(cls, ModPlayer, name))) Hooks.Once(key, install);
    }

    static Has(method) { return PlayerLoader.#OverridersOf(method).length > 0; }

    static #Invoke(inst, method, player, args) {
        try { return args.length ? inst[method](player, ...args) : inst[method](player); }
        catch (error) { Safe.Report(inst.constructor.name + '.' + method, error); }
    }

    static KeyOf(cls) { return PlayerLoader.#keys.get(cls); }
    static OwnerOf(cls) { return PlayerLoader.#owners.get(cls); }

    // Outros dados do personagem no mesmo arquivo (o cabelo de mod): chave ->
    // { save(player) -> valor ou undefined, load(player, valor) }.
    static #extras = new Map();
    static AddSaveExtra(key, save, load) {
        PlayerLoader.#extras.set(key, { save, load });
        Hooks.Once('player.save', PlayerLoader.InstallSave);
    }

    // <personagem>.plr.bl.json: { '<uid>/<Classe>': data }. Dados de mod que
    // não está carregado agora continuam no arquivo.
    static InstallSave() {
        const P = Terraria.Player;

        P['void InternalSavePlayerFile(PlayerFileData playerFile)'].hook((original, fileData) => {
            const saving = fileData && fileData.Player;
            if (saving) PlayerLoader.Call(saving, 'PreSavePlayer');
            try {
                original(fileData);
                const file = PlayerLoader.#DataFile(fileData);
                const player = file ? fileData.Player : null;
                if (!player) return;

                const all = PlayerLoader.#ReadData(file);
                const mine = PlayerLoader.Classes.length ? PlayerLoader.Of(player) : null;
                PlayerLoader.Call(player, 'PreSaveCustomData');
                for (const cls of PlayerLoader.Classes) {
                    if (!Hooks.Overrides(cls, ModPlayer, 'SaveData')) continue;

                    const key = PlayerLoader.#keys.get(cls);
                    const data = new TagCompound();
                    Safe.Run(cls.name + '.SaveData', () => mine.get(cls).SaveData(data));

                    if (Object.keys(data).length) all[key] = data;
                    else delete all[key];
                }
                for (const [key, extra] of PlayerLoader.#extras) {
                    const value = Safe.Run('salvar ' + key, () => extra.save(player));
                    if (value !== undefined) all[key] = value;
                    else delete all[key];
                }

                Safe.Run('ModPlayer: gravar ' + file, () => {
                    if (Object.keys(all).length) bl.file.write(file, JSON.stringify(all));
                    else bl.file.delete(file);
                });
            } finally {
                if (saving) PlayerLoader.Call(saving, 'PostSavePlayer');
            }
        });

        P['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'].hook((original, path, cloud) => {
            const fileData = original(path, cloud);
            const file = PlayerLoader.#DataFile(fileData);
            const player = file ? fileData.Player : null;
            if (!player) return fileData;

            const all = PlayerLoader.#ReadData(file);
            const mine = PlayerLoader.Classes.length ? PlayerLoader.Of(player) : null;
            for (const cls of PlayerLoader.Classes) {
                const data = all[PlayerLoader.#keys.get(cls)];
                if (data !== undefined) Safe.Run(cls.name + '.LoadData', () => mine.get(cls).LoadData(TagCompound.from(data)));
            }
            for (const [key, extra] of PlayerLoader.#extras) {
                if (all[key] !== undefined) Safe.Run('carregar ' + key, () => extra.load(player, all[key]));
            }
            return fileData;
        });
    }

    static Hook(cls) {
        const P = Terraria.Player;
        const has = (name) => Hooks.Overrides(cls, ModPlayer, name);
        const each = PlayerLoader.Each;
        const any = PlayerLoader.Any;

        if (has('FrameEffects')) ArmorSetLoader.WantFrame();

        if (has('ResetEffects') || has('ModifyMaxStats')) Hooks.Once('player.ResetEffects', () => {
            P['void ResetEffects()'].hook((original, self) => {
                original(self);
                PlayerLoader.Call(self, 'ResetEffects');

                let life = 0, mana = 0;
                each(self, 'ModifyMaxStats', (m) => {
                    m.ModifyMaxStats(self);
                    life += m.CumulativeHealth || 0;
                    mana += m.CumulativeMana || 0;
                });
                if (life) self.statLifeMax2 = Math.max(1, self.statLifeMax2 + life);
                if (mana) self.statManaMax2 = Math.max(0, self.statManaMax2 + mana);
            });
        });

        if (has('PreUpdate') || has('PostUpdate')) Hooks.Once('player.Update', () => {
            P['void Update(int i)'].hook((original, self, i) => {
                PlayerLoader.Call(self, 'PreUpdate');
                original(self, i);
                PlayerLoader.Call(self, 'PostUpdate');
            });
        });

        if (has('PreUpdateBuffs') || has('PostUpdateBuffs')) Hooks.Once('player.UpdateBuffs', () => {
            P['void UpdateBuffs(int i)'].hook((original, self, i) => {
                PlayerLoader.Call(self, 'PreUpdateBuffs');
                original(self, i);
                PlayerLoader.Call(self, 'PostUpdateBuffs');
            });
        });

        if (has('UpdateEquips') || has('PostUpdateEquips')) Hooks.Once('player.UpdateEquips', () => {
            P['void UpdateEquips(int i)'].hook((original, self, i) => {
                original(self, i);
                PlayerLoader.Call(self, 'UpdateEquips');
                PlayerLoader.Call(self, 'PostUpdateEquips');
            });
        });

        if (has('UpdateBadLifeRegen') || has('UpdateLifeRegen')) Hooks.Once('player.LifeRegen', () => {
            P['void UpdateLifeRegen()'].hook((original, self) => {
                PlayerLoader.Call(self, 'UpdateBadLifeRegen');
                original(self);
                PlayerLoader.Call(self, 'UpdateLifeRegen');
            });
        });

        if (has('UpdateManaRegen')) Hooks.Once('player.ManaRegen', () => {
            P['void UpdateManaRegen()'].hook((original, self) => {
                original(self);
                PlayerLoader.Call(self, 'UpdateManaRegen');
            });
        });

        // A sorte, como no tModLoader: o RecalculateLuck soma a do jogo (joaninha,
        // tochas, poção, pipa, moedas, espelho quebrado); o PreModifyLuck false
        // pula essa soma, e o ModifyLuck mexe no resultado.
        if (has('PreModifyLuck') || has('ModifyLuck')) Hooks.Once('player.Luck', () => {
            P['void RecalculateLuck()'].hook((original, self) => {
                const luck = new Ref(self.luck);
                let vanilla = true;
                each(self, 'PreModifyLuck', (m) => { if (m.PreModifyLuck(self, luck) === false) vanilla = false; });
                if (vanilla) {
                    original(self);
                    luck.value = self.luck;
                }
                each(self, 'ModifyLuck', (m) => m.ModifyLuck(self, luck));
                self.luck = luck.value;
            });
        });

        if (has('UpdateDead')) Hooks.Once('player.UpdateDead', () => {
            P['void UpdateDead()'].hook((original, self) => {
                original(self);
                PlayerLoader.Call(self, 'UpdateDead');
            });
        });

        if (has('UpdateMovement')) Hooks.Once('player.Movement', () => {
            P['void BordersMovement()'].hook((original, self) => {
                PlayerLoader.Call(self, 'UpdateMovement');
                original(self);
            });
        });

        if (has('OnEnterWorld')) Hooks.Once('player.EnterWorld', () => {
            P.Hooks['void EnterWorld(int playerIndex)'].hook((original, index) => {
                original(index);

                const player = Terraria.Main.player[index];
                each(player, 'OnEnterWorld', (m) => m.OnEnterWorld(player));
            });
        });

        if (has('OnRespawn')) Hooks.Once('player.Spawn', () => {
            const REVIVE = Terraria.PlayerSpawnContext.ReviveFromDeath;

            P['void Spawn(PlayerSpawnContext context)'].hook((original, self, context) => {
                original(self, context);
                if (context === REVIVE) each(self, 'OnRespawn', (m) => m.OnRespawn(self));
            });
        });

        if (has('CanUseItem')) Hooks.Once('player.CanUseItem', () => {
            P['bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)'].hook((original, self, item, ignoreCursed) => {
                if (any(self, 'CanUseItem', false, (m) => m.CanUseItem(self, item))) return false;

                return original(self, item, ignoreCursed);
            });
        });

        if (has('ModifyWeaponDamage')) {
            ItemCombatHooks.All('player.WeaponDamage');
            PlayerItemHooks.InstallDamage();
        }

        if (has('PreKill') || has('Kill')) Hooks.Once('player.KillMe', () => {
            P['void KillMe(PlayerDeathReason damageSource, double dmg, int hitDirection, bool pvp)'].hook(
                (original, self, src, dmg, dir, pvp) => {
                    const dies = !self.dead && !self.creativeGodMode;
                    if (dies && any(self, 'PreKill', false, (m) => m.PreKill(self, src, dmg, dir, pvp))) return;

                    original(self, src, dmg, dir, pvp);
                    if (dies && self.dead) each(self, 'Kill', (m) => m.Kill(self, src, dmg, dir, pvp));
                });
        });

        // A pesca: depois do sorteio do jogo (item e inimigo), como o
        // PlayerLoader.CatchFish do tModLoader no Projectile.FishingCheck.
        if (has('CatchFish')) Hooks.Once('player.CatchFish', () => {
            Terraria.Projectile['void FishingCheck_RollItemDrop(ref FishingAttempt fisher)'].hook((original, self, fisher) => {
                original(self, fisher);
                const player = Terraria.Main.player[self.owner];
                const attempt = fisher.value;
                const item = new Ref(attempt.rolledItemDrop), npc = new Ref(attempt.rolledEnemySpawn);
                const sonar = new Ref(null), sonarPosition = new Ref(Vector2.new(self.position.X, self.position.Y));
                each(player, 'CatchFish', (m) => m.CatchFish(attempt, item, npc, sonar, sonarPosition));
                if (item.value !== attempt.rolledItemDrop || npc.value !== attempt.rolledEnemySpawn) {
                    attempt.rolledItemDrop = item.value | 0;
                    attempt.rolledEnemySpawn = npc.value | 0;
                    fisher.value = attempt;
                }
            });
        });

        CombatLoader.WantPlayer(cls);
        PlayerItemHooks.Install(cls);
        PlayerUpdateHooks.Install(cls);
        PlayerJumpHooks.Install(cls);
        PlayerDrawHooks.Install(cls);
        PlayerWorldHooks.Install(cls);
        PlayerNetworkHooks.Install(cls);
    }

    static #DataFile(fileData) {
        if (!fileData || fileData.IsCloudSave) return null;

        const path = fileData.Path;
        return path ? path + '.bl.json' : null;
    }

    static #ReadData(file) {
        const text = bl.file.read(file);
        if (!text) return {};

        try {
            return JSON.parse(text) || {};
        } catch (e) {
            bl.error('ModPlayer: ' + file + ' is broken (' + e + '); the saved data was ignored');
            return {};
        }
    }

    static #OverridersOf(method) {
        let list = PlayerLoader.#overriders.get(method);
        if (!list) {
            list = PlayerLoader.Classes.filter((c) => Hooks.Overrides(c, ModPlayer, method));
            PlayerLoader.#overriders.set(method, list);
        }
        return list;
    }
}
