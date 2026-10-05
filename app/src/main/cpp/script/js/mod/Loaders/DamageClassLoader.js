// As classes de dano (o DamageClassLoader do tModLoader) e o que as liga ao
// jogo do celular, que é compilado e não tem DamageClass.
//
// As do jogo continuam sendo as flags do item e do projétil (melee, ranged,
// magic, summon) e os campos do jogador (meleeDamage, meleeCrit, meleeSpeed,
// minionDamage...). O que um mod soma nelas é dobrado nesses campos antes do
// CapAttackSpeeds, e todo código do jogo que os lê já vê o bônus.
//
// As outras, "de gancho" (Generic, Throwing e MagicSummonHybrid usadas num
// item, e as de mod), não cabem nas flags: o dano, o crítico, a repulsão, a
// velocidade, a penetração e o dano de lacaio delas passam pelos hooks abaixo.
// O item delas ganha as flags das classes que ela "conta como"
// (GetEffectInheritance) ou cujos prefixos aceita: os efeitos e os prefixos do
// jogo seguem as flags sozinhos.
class DamageClassLoader {
    static #classes = [];
    static #vanilla = null;
    static #native = new Map();     // classe do jogo -> flags { melee, ranged, magic, summon, noSpeed, whip }
    static #inherit = [];           // [i][j] -> StatInheritanceData, ou null (nada)
    static #counts = [];            // [i][j] -> conta como
    static #itemClass = new Map();  // tipo -> classe, gravada no SetDefaults do tipo
    static #projClass = new Map();
    static #defaulting = 0;         // endereço do item/projétil no SetDefaults agora
    static #firing = null;          // o projétil de gancho no Damage agora

    static get Vanilla() {
        if (!DamageClassLoader.#vanilla) DamageClassLoader.#BuildVanilla();
        return DamageClassLoader.#vanilla;
    }

    static get DamageClassCount() { return DamageClassLoader.Vanilla && DamageClassLoader.#classes.length; }

    static GetDamageClass(type) { return (DamageClassLoader.Vanilla && DamageClassLoader.#classes[type]) || null; }

    static NameOf(cls) { return cls.Name || cls.constructor.name; }

    static Add(inst) {
        DamageClassLoader.Vanilla;
        inst.Type = DamageClassLoader.#classes.length;
        DamageClassLoader.#classes.push(inst);
        DamageClassLoader.#inherit = [];
        DamageClassLoader.#counts = [];
        DamageClassLoader.#Localize(inst);
        Ready.Add(() => Safe.Run(DamageClassLoader.NameOf(inst) + '.SetStaticDefaults', () => inst.SetStaticDefaults()), 'setup');
        DamageClassLoader.Activate();
        return inst.Type;
    }

    // A classe pedida como instância, classe registrada ou Type.
    static Resolve(which) {
        DamageClassLoader.Vanilla;
        if (which instanceof DamageClass) return which;
        if (typeof which === 'function') {
            const inst = Templates.Get(which);
            if (inst instanceof DamageClass) return inst;
        }
        if (Number.isInteger(which) && DamageClassLoader.#classes[which]) return DamageClassLoader.#classes[which];
        throw new TypeError('DamageClass: espera DamageClass.X, a classe de um DamageClass registrado, a instância ou o Type');
    }

    // De gancho: o jogo não a entende pelas flags.
    static IsHooked(cls) { return !DamageClassLoader.#native.has(cls); }

    static CountsAs(cls, other) {
        const row = DamageClassLoader.#counts[cls.Type] || (DamageClassLoader.#counts[cls.Type] = []);
        let v = row[other.Type];
        if (v === undefined) {
            v = cls === other || !!Safe.Run(DamageClassLoader.NameOf(cls) + '.GetEffectInheritance', () => cls.GetEffectInheritance(other));
            row[other.Type] = v;
        }
        return v;
    }

    // ---- item e projétil ----

    // O SetDefaults de um item ou projétil: o DamageType escrito nele vale para
    // o tipo inteiro (sobrevive ao Clone, ao NewItem e à rede).
    static Defaulting(entity, fn) {
        const outer = DamageClassLoader.#defaulting;
        DamageClassLoader.#defaulting = bl.addressOf(entity);
        try {
            return fn();
        } finally {
            DamageClassLoader.#defaulting = outer;
        }
    }

    static ItemClass(item) {
        const own = item.__damageClass;
        if (own && own.type === item.type) return own.cls;
        return DamageClassLoader.#itemClass.get(item.type) || DamageClassLoader.#ItemFromFlags(item);
    }

    static SetItemClass(item, which) {
        const cls = DamageClassLoader.Resolve(which);
        const flags = DamageClassLoader.#FlagsOf(cls);
        item.melee = !!flags.melee;
        item.ranged = !!flags.ranged;
        item.magic = !!flags.magic;
        item.summon = !!flags.summon;

        if (DamageClassLoader.#defaulting === bl.addressOf(item)) {
            DamageClassLoader.#itemClass.set(item.type, cls);
            const S = Terraria.ID.ItemID.Sets;
            if (flags.noSpeed) S.NoMeleeSpeedBonus[item.type] = true;
            if (flags.whip) S.SummonerWeaponThatScalesWithAttackSpeed[item.type] = true;
        } else {
            item.__damageClass = { type: item.type, cls };
        }
        if (DamageClassLoader.IsHooked(cls)) DamageClassLoader.Activate();
    }

    static ProjectileClass(proj) {
        const own = proj.__damageClass;
        if (own && own.type === proj.type) return own.cls;
        return DamageClassLoader.#projClass.get(proj.type) || DamageClassLoader.#ProjectileFromFlags(proj);
    }

    static SetProjectileClass(proj, which) {
        const cls = DamageClassLoader.Resolve(which);
        const flags = DamageClassLoader.#FlagsOf(cls);
        proj.melee = !!flags.melee;
        proj.ranged = !!flags.ranged;
        proj.magic = !!flags.magic;

        if (DamageClassLoader.#defaulting === bl.addressOf(proj)) DamageClassLoader.#projClass.set(proj.type, cls);
        else proj.__damageClass = { type: proj.type, cls };
        if (DamageClassLoader.IsHooked(cls)) {
            bl.hookMarks.set('dmgclass.proj', proj.type);
            DamageClassLoader.Activate();
        }
    }

    // As flags do jogo de uma classe: as dela, se é do jogo; senão as das
    // classes que ela conta como ou cujos prefixos aceita.
    static #FlagsOf(cls) {
        const own = DamageClassLoader.#native.get(cls);
        if (own) return own;

        const V = DamageClassLoader.Vanilla;
        const takes = (base) => DamageClassLoader.CountsAs(cls, base) || cls.GetsPrefixesFor(base);
        return {
            melee: takes(V.Melee) || takes(V.MeleeNoSpeed),
            ranged: takes(V.Ranged),
            magic: takes(V.Magic),
            summon: takes(V.Summon) || takes(V.SummonMeleeSpeed),
        };
    }

    static #ItemFromFlags(item) {
        const V = DamageClassLoader.Vanilla;
        const S = Terraria.ID.ItemID.Sets;
        if (item.melee) return S.NoMeleeSpeedBonus[item.type] ? V.MeleeNoSpeed : V.Melee;
        if (item.ranged) return V.Ranged;
        if (item.magic) return V.Magic;
        if (item.summon) return S.SummonerWeaponThatScalesWithAttackSpeed[item.type] ? V.SummonMeleeSpeed : V.Summon;
        return V.Default;
    }

    static #ProjectileFromFlags(proj) {
        const V = DamageClassLoader.Vanilla;
        const S = Terraria.ID.ProjectileID.Sets;
        if (proj.melee) return V.Melee;
        if (proj.ranged) return V.Ranged;
        if (proj.magic) return V.Magic;
        if (S.IsAWhip[proj.type]) return V.SummonMeleeSpeed;
        if (proj.minion || proj.sentry || S.MinionShot[proj.type] || S.SentryShot[proj.type]) return V.Summon;
        return V.Default;
    }

    // ---- jogador ----

    // Os bônus por classe deste jogador, zerados todo quadro (ResetEffects).
    static Data(player) {
        let d = player.__damageClassData;
        if (!d) {
            DamageClassLoader.Activate();
            d = player.__damageClassData = { stats: [], applied: [], snap: null };
            DamageClassLoader.#Reset(player, d);
        }
        return d;
    }

    static Stats(player, which) {
        const cls = DamageClassLoader.Resolve(which);
        const d = DamageClassLoader.Data(player);
        if (cls.Type >= d.stats.length) DamageClassLoader.#Reset(player, d);
        return d.stats[cls.Type];
    }

    // O total de uma classe: o dela mais o de cada classe herdada, como o
    // GetTotalDamage do tModLoader. As do jogo são lidas dos campos do jogador.
    static Total(player, which) {
        const cls = DamageClassLoader.Resolve(which);
        const d = DamageClassLoader.Data(player);
        if (cls.Type >= d.stats.length) DamageClassLoader.#Reset(player, d);

        const self = DamageClassLoader.#Effective(player, d, cls);
        let damage = self.damage.Clone(), knockback = self.knockback.Clone();
        let crit = self.crit, speed = self.speed, pen = self.pen;
        for (const other of DamageClassLoader.#classes) {
            if (other === cls) continue;
            const inh = DamageClassLoader.#Inheritance(cls, other);
            if (!inh) continue;

            const s = DamageClassLoader.#Effective(player, d, other);
            damage = damage.CombineWith(s.damage.Scale(inh.damageInheritance));
            crit += s.crit * inh.critChanceInheritance;
            speed += (s.speed - 1) * inh.attackSpeedInheritance;
            pen += s.pen * inh.armorPenInheritance;
            knockback = knockback.CombineWith(s.knockback.Scale(inh.knockbackInheritance));
        }
        return { damage, crit, speed, pen, knockback };
    }

    static #Fresh() {
        return DamageClassLoader.#classes.map(() => ({
            damage: new StatModifier(), crit: { value: 0 }, speed: { value: 1 }, armorPen: { value: 0 },
            knockback: new StatModifier(),
        }));
    }

    static #Reset(player, d) {
        d.stats = DamageClassLoader.#Fresh();
        d.applied = DamageClassLoader.#Fresh();
        for (const cls of DamageClassLoader.#classes) {
            if (cls.IsVanilla) continue;
            Safe.Run(DamageClassLoader.NameOf(cls) + '.SetDefaultStats', () => cls.SetDefaultStats(player));
        }
    }

    // O que o mod somou nas classes do jogo vai para os campos do jogador.
    // Antes do CapAttackSpeeds: ele troca meleeSpeed (velocidade) por
    // 1/velocidade (multiplicador do tempo de uso).
    static #Fold(player) {
        const d = player.__damageClassData;
        if (!d) return;

        const V = DamageClassLoader.Vanilla;
        const damageFields = new Map([[V.Generic, ['meleeDamage', 'rangedDamage', 'magicDamage', 'minionDamage']],
                                      [V.Melee, ['meleeDamage']], [V.Ranged, ['rangedDamage']],
                                      [V.Magic, ['magicDamage']], [V.Summon, ['minionDamage']]]);
        const critFields = new Map([[V.Generic, ['meleeCrit', 'rangedCrit', 'magicCrit']], [V.Melee, ['meleeCrit']],
                                    [V.Ranged, ['rangedCrit']], [V.Magic, ['magicCrit']]]);
        const speedFields = new Map([[V.Generic, ['meleeSpeed']], [V.Melee, ['meleeSpeed']], [V.Summon, ['summonerWeaponSpeedBonus']]]);
        const penFields = new Map([[V.Generic, ['armorPenetration']], [V.Melee, ['meleeArmorPenetration']]]);

        for (const [cls, fields] of damageFields) {
            const cur = d.stats[cls.Type], app = d.applied[cls.Type];
            const add = cur.damage.Additive - app.damage.Additive;
            const mult = cur.damage.Multiplicative / app.damage.Multiplicative;
            if (add !== 0 || mult !== 1) for (const f of fields) player[f] = (player[f] + add) * mult;

            const crit = Math.round(cur.crit.value - app.crit.value);
            if (crit && critFields.has(cls)) for (const f of critFields.get(cls)) player[f] += crit;

            const speed = cur.speed.value - app.speed.value;
            if (speed && speedFields.has(cls)) for (const f of speedFields.get(cls)) player[f] += speed;

            const pen = Math.round(cur.armorPen.value - app.armorPen.value);
            if (pen && penFields.has(cls)) for (const f of penFields.get(cls)) player[f] += pen;

            app.damage = cur.damage.Clone();
            app.crit.value = cur.crit.value;
            app.speed.value = cur.speed.value;
            app.armorPen.value = cur.armorPen.value;
        }
        d.snap = { melee: player.meleeSpeed, summon: player.summonerWeaponSpeedBonus };
    }

    // Os números de uma classe sozinha. As do jogo vêm dos campos do jogador,
    // que já têm o equipamento do jogo e o que foi dobrado; o Generic é a parte
    // comum aos quatro (o jogo soma "todo dano" em cada um).
    static #Effective(player, d, cls) {
        const V = DamageClassLoader.Vanilla;
        const own = d.stats[cls.Type];
        const folded = cls === V.Generic || cls === V.Melee || cls === V.Ranged || cls === V.Magic || cls === V.Summon;
        if (!folded) {
            return { damage: own.damage, crit: own.crit.value, speed: own.speed.value, pen: own.armorPen.value, knockback: own.knockback };
        }

        const common = Math.min(player.meleeDamage, player.rangedDamage, player.magicDamage, player.minionDamage);
        const commonCrit = Math.min(player.meleeCrit, player.rangedCrit, player.magicCrit);
        const genericSpeed = d.stats[V.Generic.Type].speed.value;
        let add, crit, speed, pen;
        if (cls === V.Generic) {
            add = common; crit = commonCrit; speed = genericSpeed; pen = player.armorPenetration;
        } else if (cls === V.Melee) {
            add = 1 + player.meleeDamage - common;
            crit = player.meleeCrit - commonCrit;
            speed = d.snap ? d.snap.melee - genericSpeed + 1 : own.speed.value;
            pen = player.meleeArmorPenetration;
        } else if (cls === V.Ranged) {
            add = 1 + player.rangedDamage - common; crit = player.rangedCrit - commonCrit; speed = own.speed.value; pen = own.armorPen.value;
        } else if (cls === V.Magic) {
            add = 1 + player.magicDamage - common; crit = player.magicCrit - commonCrit; speed = own.speed.value; pen = own.armorPen.value;
        } else {
            add = 1 + player.minionDamage - common;
            crit = own.crit.value;
            speed = d.snap ? 1 + d.snap.summon : own.speed.value;
            pen = own.armorPen.value;
        }
        return { damage: new StatModifier(add, 1, own.damage.Flat, own.damage.Base), crit, speed, pen, knockback: own.knockback };
    }

    static #Inheritance(cls, other) {
        const row = DamageClassLoader.#inherit[cls.Type] || (DamageClassLoader.#inherit[cls.Type] = []);
        let v = row[other.Type];
        if (v === undefined) {
            const r = Safe.Run(DamageClassLoader.NameOf(cls) + '.GetModifierInheritance', () => cls.GetModifierInheritance(other));
            v = r instanceof StatInheritanceData && !r.IsNone ? r : null;
            row[other.Type] = v;
        }
        return v;
    }

    // ---- ganchos ----

    static Activate() {
        Hooks.Once('dmgclass', () => DamageClassLoader.#Install());
        // O tooltip do jogo escreveria a classe pelas flags ("dano corpo a corpo").
        Hooks.Once('item.Tooltips', TooltipLoader.Install);
    }

    static #Install() {
        const P = Terraria.Player;
        const Pr = Terraria.Projectile;
        const D = DamageClassLoader;
        const V = D.Vanilla;
        const meleeLike = (cls) => cls === V.Melee || cls === V.MeleeNoSpeed || cls === V.SummonMeleeSpeed;

        P['void ResetEffects()'].hook((original, self) => {
            original(self);
            const d = self.__damageClassData;
            if (d) D.#Reset(self, d);
        });

        P['void CapAttackSpeeds()'].hook((original, self) => {
            D.#Fold(self);
            original(self);
        });

        // O multiplicador de dano, que o GetWeaponDamage, a munição (PickAmmo)
        // e o tooltip do jogo usam. Base e Flat entram nele, pelo dano do
        // item: assim o GetWeaponDamage do jogo segue sendo o caminho, e os
        // ModifyWeaponDamage dos mods recebem o valor já com a classe.
        P['float GetWeaponDamageMultiplier(Item item)'].hook((original, self, item) => {
            const cls = D.ItemClass(item);
            const t = D.Total(self, cls).damage;
            const mult = D.IsHooked(cls) ? t.Additive * t.Multiplicative : original(self, item);
            const damage = item.damage;
            if ((!t.Base && !t.Flat) || damage <= 0) return mult;
            return ((damage + t.Base) * mult + t.Flat) / damage;
        });

        PlayerItemHooks.Stats = {
            crit(self, item, vanilla) {
                const cls = D.ItemClass(item);
                if (!D.IsHooked(cls)) return vanilla;
                return D.UsesCrit(cls) ? Math.round(D.Total(self, cls).crit) + item.crit : 0;
            },
            knockback(self, item, vanilla) {
                const modifier = D.Total(self, D.ItemClass(item)).knockback;
                return modifier.Equals(StatModifier.Default) ? vanilla : modifier.ApplyTo(vanilla);
            },
            animation(self, item) {
                const cls = D.ItemClass(item);
                if (!D.IsHooked(cls) && meleeLike(cls)) return;
                const speed = D.Total(self, cls).speed, hooked = D.IsHooked(cls);
                if (!hooked && Math.abs(speed - 1) < 1e-6) return;
                const base = hooked ? item.useAnimation : self.itemAnimationMax;
                const frames = Math.max(1, Math.trunc(base / Math.min(Math.max(speed, 0.01), 3)));
                self.itemAnimation = frames;
                self.itemAnimationMax = frames;
            },
            time(self, item) {
                const cls = D.ItemClass(item);
                if (!D.IsHooked(cls) && meleeLike(cls)) return;
                const speed = D.Total(self, cls).speed;
                if (Math.abs(speed - 1) < 1e-6) return;
                const frames = Math.max(1, Math.trunc(item.useTime / Math.min(Math.max(speed, 0.01), 3)));
                self.itemTime = frames;
                self.itemTimeMax = frames;
            }
        };
        PlayerItemHooks.InstallStats();

        // Golpe de item: a penetração da classe que o jogo não soma sozinho.
        P['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'].hook(
            (original, self, item, rect, damage, knockBack, npc) => {
                const extra = D.#ExtraPen(self, D.ItemClass(item), item.melee);
                if (extra <= 0) return original(self, item, rect, damage, knockBack, npc);

                self.armorPenetration += extra;
                try {
                    return original(self, item, rect, damage, knockBack, npc);
                } finally {
                    self.armorPenetration -= extra;
                }
            });

        // Projétil: a penetração da classe vai nele ao nascer.
        Pr['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'].hook(
            (original, ...args) => {
                const i = original(...args);
                if (i < 0 || i >= 1000) return i;

                const p = Terraria.Main.projectile[i];
                const owner = p.owner;
                if (!p.active || owner < 0 || owner >= 255) return i;

                const player = Terraria.Main.player[owner];
                if (!player || !player.active || !player.__damageClassData) return i;

                const extra = D.#ExtraPen(player, D.ProjectileClass(p), p.melee);
                if (extra > 0) p.armorPenetration += extra;
                return i;
            });

        // Projétil de gancho: o crítico sorteado de novo pela classe, logo
        // depois do sorteio do jogo (que foi pelas flags).
        const mark = { minType: 0, marks: 'dmgclass.proj' };
        Pr['void Damage()'].hook((original, p) => {
            const outer = D.#firing;
            D.#firing = p;
            bl.hookFlags.set('dmgclass.crit', true);
            try {
                return original(p);
            } finally {
                D.#firing = outer;
                bl.hookFlags.set('dmgclass.crit', !!outer);
            }
        }, mark);

        P['void TryConsumingTimerCrit(Rectangle hitbox, ref bool crit)'].hook((original, self, hitbox, crit) => {
            const p = D.#firing;
            if (p) {
                const cls = D.ProjectileClass(p);
                if (D.IsHooked(cls)) {
                    let chance = D.UsesCrit(cls) ? D.Total(self, cls).crit + p.bonusCritChance : -1;
                    const held = self.HeldItem;
                    if (chance >= 0 && held && D.ItemClass(held) === cls) chance += held.crit;
                    crit.value = chance >= 0 && Math.random() * 100 < chance;
                }
            }
            return original(self, hitbox, crit);
        }, { flag: 'dmgclass.crit' });

        // Lacaio e sentinela: o jogo refaz o dano com o minionDamage a cada quadro.
        Pr['void Update(int i)'].hook((original, p, i) => {
            original(p, i);
            if (!p.active || !(p.minion || p.sentry) || p.originalDamage <= 0) return;

            const owner = Terraria.Main.player[p.owner];
            if (!owner) return;
            p.damage = Math.trunc(D.Total(owner, D.ProjectileClass(p)).damage.ApplyTo(p.originalDamage) + 5e-6);
        }, mark);
    }

    static UsesCrit(cls) {
        return !!Safe.Run(DamageClassLoader.NameOf(cls) + '.UseStandardCritCalcs', () => cls.UseStandardCritCalcs);
    }

    // A penetração da classe menos o que o jogo já soma no acerto
    // (armorPenetration, e meleeArmorPenetration para o corpo a corpo).
    static #ExtraPen(player, cls, melee) {
        const total = DamageClassLoader.Total(player, cls).pen;
        return Math.round(total - player.armorPenetration - (melee ? player.meleeArmorPenetration : 0));
    }

    // ---- nomes ----

    // Mods.<id>.DamageClasses.<Classe>.DisplayName, do Localization do mod;
    // sem ele, o nome da classe separado. Inclui a palavra "dano", como no
    // tModLoader ("example damage"). Um DisplayName escrito na classe ganha.
    static #Localize(inst) {
        const name = inst.constructor.name;
        const pretty = name.replace(/([A-Z])/g, ' $1').trim();
        const key = 'Mods.' + (inst.Mod ? inst.Mod.id || inst.Mod.uuid : 'bl') + '.DamageClasses.' + name + '.DisplayName';
        const own = inst.DisplayName;
        const display = (typeof own === 'string' && own) || (own && typeof own === 'object' && !own.Key && own) ||
                        Lang.Localized('DamageClasses', name + '.DisplayName') || pretty;
        LocalizationLoader.Register(key, display);
        Object.defineProperty(inst, 'DisplayName', {
            value: DamageClassLoader.#Text(key, () => Terraria.Localization.Language['string GetTextValue(string key)'](key)),
            writable: true, configurable: true,
        });
    }

    static #Text(key, value) {
        return Object.freeze({ Key: key, get Value() { return value(); }, toString() { return this.Value; } });
    }

    // As 10 do tModLoader, nesta ordem de Type.
    static #BuildVanilla() {
        const V = {};
        const classes = DamageClassLoader.#classes;
        const native = DamageClassLoader.#native;
        const full = () => StatInheritanceData.Full;
        const none = () => StatInheritanceData.None;
        const make = (name, tip, flags, overrides = {}) => {
            const inst = new DamageClass();
            for (const [k, v] of Object.entries(overrides)) {
                Object.defineProperty(inst, k, { value: v, writable: true, configurable: true });
            }
            inst.Name = name;
            inst.IsVanilla = true;
            inst.Type = classes.length;
            inst.DisplayName = DamageClassLoader.#Text('LegacyTooltip.' + tip, () => Terraria.Lang.tip[tip].Value);
            classes.push(inst);
            if (flags) native.set(inst, flags);
            return inst;
        };

        V.Default = make('DefaultDamageClass', 55, {}, { GetModifierInheritance: none });
        V.Generic = make('GenericDamageClass', 55, null, { GetModifierInheritance: none });
        V.Melee = make('MeleeDamageClass', 2, { melee: true });
        V.MeleeNoSpeed = make('MeleeNoSpeedDamageClass', 2, { melee: true, noSpeed: true }, {
            GetModifierInheritance: (c) => (c === V.Generic || c === V.Melee
                ? Object.assign(full(), { attackSpeedInheritance: 0 }) : none()),
            GetEffectInheritance: (c) => c === V.Melee,
        });
        V.Ranged = make('RangedDamageClass', 3, { ranged: true });
        V.Magic = make('MagicDamageClass', 4, { magic: true });
        V.Summon = make('SummonDamageClass', 53, { summon: true }, {
            UseStandardCritCalcs: false,
            ShowStatTooltipLine: (player, line) => line !== 'CritChance' && line !== 'Speed',
            GetPrefixInheritance: (c) => c === V.Magic,
        });
        V.SummonMeleeSpeed = make('SummonMeleeSpeedDamageClass', 53, { summon: true, whip: true }, {
            GetModifierInheritance: (c) => (c === V.Melee ? new StatInheritanceData({ attackSpeedInheritance: 1 })
                : c === V.Generic || c === V.Summon ? full() : none()),
            GetEffectInheritance: (c) => c === V.Summon,
            UseStandardCritCalcs: false,
            ShowStatTooltipLine: (player, line) => line !== 'CritChance',
            GetPrefixInheritance: (c) => c === V.Melee,
        });
        V.MagicSummonHybrid = make('MagicSummonHybridDamageClass', 4, null, {
            GetModifierInheritance: (c) => (c === V.Generic || c === V.Magic || c === V.Summon ? full() : none()),
            GetEffectInheritance: (c) => c === V.Magic || c === V.Summon,
        });
        V.Throwing = make('ThrowingDamageClass', 58, null, { GetPrefixInheritance: (c) => c === V.Ranged });

        DamageClassLoader.#vanilla = Object.freeze(V);
    }

    // ---- o que os mods veem no item, no projétil e no jogador ----

    static Install() {
        Hooks.Once('dmgclass.api', () => {
            const D = DamageClassLoader;
            bl.defineField(Terraria.Player, '__damageClassData');
            bl.defineField(Terraria.Item, '__damageClass');
            bl.defineField(Terraria.Projectile, '__damageClass');

            bl.defineProperty(Terraria.Item, 'DamageType',
                function () { return D.ItemClass(this); },
                function (value) { D.SetItemClass(this, value); });
            bl.defineProperty(Terraria.Projectile, 'DamageType',
                function () { return D.ProjectileClass(this); },
                function (value) { D.SetProjectileClass(this, value); });
            bl.defineMethod(Terraria.Item, 'CountsAsClass', function (which) {
                return D.ItemClass(this).CountsAsClass(which);
            });
            bl.defineMethod(Terraria.Projectile, 'CountsAsClass', function (which) {
                return D.ProjectileClass(this).CountsAsClass(which);
            });

            const P = Terraria.Player;
            bl.defineMethod(P, 'GetDamage', function (which) { return D.Stats(this, which).damage; });
            bl.defineMethod(P, 'GetCritChance', function (which) { return D.Stats(this, which).crit; });
            bl.defineMethod(P, 'GetAttackSpeed', function (which) { return D.Stats(this, which).speed; });
            bl.defineMethod(P, 'GetKnockback', function (which) { return D.Stats(this, which).knockback; });
            // O do jogo, GetArmorPenetration(bool melee), segue pela assinatura.
            bl.defineMethod(P, 'GetArmorPenetration', function (which) {
                if (typeof which === 'boolean') return this['int GetArmorPenetration(bool melee)'](which);
                return D.Stats(this, which).armorPen;
            }, { override: true });

            bl.defineMethod(P, 'GetTotalDamage', function (which) { return D.Total(this, which).damage; });
            bl.defineMethod(P, 'GetTotalCritChance', function (which) { return D.Total(this, which).crit; });
            bl.defineMethod(P, 'GetTotalAttackSpeed', function (which) { return D.Total(this, which).speed; });
            bl.defineMethod(P, 'GetTotalArmorPenetration', function (which) { return D.Total(this, which).pen; });
            bl.defineMethod(P, 'GetTotalKnockback', function (which) { return D.Total(this, which).knockback; });
            bl.defineMethod(P, 'GetWeaponAttackSpeed', function (item) {
                const speed = D.Total(this, D.ItemClass(item)).speed;
                return 1 + (speed - 1) * Terraria.ID.ItemID.Sets.BonusMeleeSpeedMultiplier[item.type];
            });
            bl.defineMethod(P, 'GetWeaponArmorPenetration', function (item) {
                return Math.trunc(item.armorPenetration + D.Total(this, D.ItemClass(item)).pen);
            });
        });
    }
}

DamageClassLoader.Install();
