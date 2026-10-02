// Cada jogador tem a própria instância de cada ModPlayer, criada na primeira
// vez que alguém pergunta por ela, no campo `ModPlayers` ao lado do Player.
class PlayerLoader {
    static Classes = [];

    static #keys = new Map();         // Classe -> '<uid>/<Classe>', a chave dos dados salvos
    static #overriders = new Map();

    static Add(cls) {
        PlayerLoader.Classes.push(cls);
        PlayerLoader.#keys.set(cls.name, (bl.mod ? bl.mod.uuid : 'sem-mod') + '/' + cls.name);
        PlayerLoader.#overriders.clear();
    }

    static Of(player) {
        let all = player.ModPlayers;
        const classes = PlayerLoader.Classes;
        if (all !== undefined && all.__count === classes.length) return all;

        const fresh = all === undefined;
        if (fresh) {
            all = Object.create(null);
            Object.defineProperty(all, '__count', { value: 0, writable: true });
        }

        const address = bl.addressOf(player);
        const created = [];
        for (const cls of classes) {
            if (all[cls.name]) continue;

            const inst = new cls();
            inst.__entity = address;
            all[cls.name] = inst;
            created.push(inst);
        }
        all.__count = classes.length;

        if (fresh) player.ModPlayers = all;
        for (const inst of created) Safe.Run(inst.constructor.name + '.Initialize', () => inst.Initialize());
        return all;
    }

    static Each(player, method, fn) {
        const list = PlayerLoader.#OverridersOf(method);
        if (list.length === 0) return;

        const all = PlayerLoader.Of(player);
        for (const cls of list) {
            const inst = all[cls.name];
            Safe.Run(cls.name + '.' + method, () => fn(inst));
        }
    }

    static Any(player, method, value, fn) {
        let hit = false;
        PlayerLoader.Each(player, method, (inst) => {
            if (fn(inst) === value) hit = true;
        });
        return hit;
    }

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
            original(fileData);

            const file = PlayerLoader.#DataFile(fileData);
            const player = file ? fileData.Player : null;
            if (!player) return;

            const all = PlayerLoader.#ReadData(file);
            const mine = PlayerLoader.Classes.length ? PlayerLoader.Of(player) : null;
            for (const cls of PlayerLoader.Classes) {
                if (!Hooks.Overrides(cls, ModPlayer, 'SaveData')) continue;

                const key = PlayerLoader.#keys.get(cls.name);
                const data = new TagCompound();
                Safe.Run(cls.name + '.SaveData', () => mine[cls.name].SaveData(data));

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
        });

        P['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'].hook((original, path, cloud) => {
            const fileData = original(path, cloud);
            const file = PlayerLoader.#DataFile(fileData);
            const player = file ? fileData.Player : null;
            if (!player) return fileData;

            const all = PlayerLoader.#ReadData(file);
            const mine = PlayerLoader.Classes.length ? PlayerLoader.Of(player) : null;
            for (const cls of PlayerLoader.Classes) {
                const data = all[PlayerLoader.#keys.get(cls.name)];
                if (data !== undefined) Safe.Run(cls.name + '.LoadData', () => mine[cls.name].LoadData(TagCompound.from(data)));
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
                each(self, 'ResetEffects', (m) => m.ResetEffects(self));

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
                each(self, 'PreUpdate', (m) => m.PreUpdate(self));
                original(self, i);
                each(self, 'PostUpdate', (m) => m.PostUpdate(self));
            });
        });

        if (has('PreUpdateBuffs') || has('PostUpdateBuffs')) Hooks.Once('player.UpdateBuffs', () => {
            P['void UpdateBuffs(int i)'].hook((original, self, i) => {
                each(self, 'PreUpdateBuffs', (m) => m.PreUpdateBuffs(self));
                original(self, i);
                each(self, 'PostUpdateBuffs', (m) => m.PostUpdateBuffs(self));
            });
        });

        if (has('UpdateEquips') || has('PostUpdateEquips')) Hooks.Once('player.UpdateEquips', () => {
            P['void UpdateEquips(int i)'].hook((original, self, i) => {
                original(self, i);
                each(self, 'UpdateEquips', (m) => m.UpdateEquips(self));
                each(self, 'PostUpdateEquips', (m) => m.PostUpdateEquips(self));
            });
        });

        if (has('UpdateBadLifeRegen') || has('UpdateLifeRegen')) Hooks.Once('player.LifeRegen', () => {
            P['void UpdateLifeRegen()'].hook((original, self) => {
                each(self, 'UpdateBadLifeRegen', (m) => m.UpdateBadLifeRegen(self));
                original(self);
                each(self, 'UpdateLifeRegen', (m) => m.UpdateLifeRegen(self));
            });
        });

        if (has('UpdateManaRegen')) Hooks.Once('player.ManaRegen', () => {
            P['void UpdateManaRegen()'].hook((original, self) => {
                original(self);
                each(self, 'UpdateManaRegen', (m) => m.UpdateManaRegen(self));
            });
        });

        if (has('UpdateDead')) Hooks.Once('player.UpdateDead', () => {
            P['void UpdateDead()'].hook((original, self) => {
                original(self);
                each(self, 'UpdateDead', (m) => m.UpdateDead(self));
            });
        });

        if (has('UpdateMovement')) Hooks.Once('player.Movement', () => {
            P['void BordersMovement()'].hook((original, self) => {
                each(self, 'UpdateMovement', (m) => m.UpdateMovement(self));
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

        if (has('ModifyWeaponDamage')) Hooks.Once('player.WeaponDamage', () => {
            P['int GetWeaponDamage(Item sItem)'].hook((original, self, item) => {
                let damage = original(self, item);

                each(self, 'ModifyWeaponDamage', (m) => {
                    m.WeaponDamage = damage;
                    const r = m.ModifyWeaponDamage(self, item, damage);
                    if (typeof r === 'number') damage = r;
                    else if (typeof m.WeaponDamage === 'number') damage = m.WeaponDamage;
                });
                return Math.floor(damage);
            });
        });

        const hurt = ['ImmuneTo', 'FreeDodge', 'ModifyHurt', 'OnHurt', 'PostHurt'];
        if (hurt.some(has)) Hooks.Once('player.Hurt', PlayerLoader.#HookHurt);

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
    }

    static #HookHurt() {
        const each = PlayerLoader.Each;
        const any = PlayerLoader.Any;

        Terraria.Player['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'].hook(
            (original, self, src, damage, dir, pvp, quiet, crit, cooldown, dodgeable) => {
                if (any(self, 'ImmuneTo', true, (m) => m.ImmuneTo(self, src, cooldown, dodgeable))) return 0;
                if (any(self, 'FreeDodge', true,
                    (m) => m.FreeDodge(self, src, damage, dir, pvp, quiet, crit, cooldown, dodgeable))) return 0;

                const hit = { damage, hitDirection: dir, quiet, crit, dodgeable };
                each(self, 'ModifyHurt', (m) => m.ModifyHurt(self, hit));

                const done = original(self, src, Math.floor(hit.damage), hit.hitDirection, pvp,
                                      hit.quiet, hit.crit, cooldown, hit.dodgeable);
                if (done <= 0) return done;

                const args = [src, done, hit.hitDirection, pvp, hit.quiet, hit.crit, cooldown, hit.dodgeable];
                each(self, 'OnHurt', (m) => m.OnHurt(self, ...args));
                if (!self.dead && self.statLife > 0) each(self, 'PostHurt', (m) => m.PostHurt(self, ...args));

                return done;
            });
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
