// Prefixos de mod e as regras de prefixo dos itens de mod, como o PrefixLoader
// do tModLoader. O Item.Prefix do celular é o do PC e chama por método os
// pedaços que importam (GetRollablePrefixes, RollAPrefix,
// TryGetPrefixStatMultipliersForItem); os hooks ficam neles e o jogo faz o
// resto: aplica os status, mexe na raridade e no preço, grava o byte. O baú
// (ChestItem.Prefix) rola pela amostra do item e passa pelos mesmos hooks.
// Não há limite de PrefixID.Count compilado no código (conferido na
// libil2cpp): crescem só Lang.prefix e PrefixID.Sets.ReducedNaturalChance.
class PrefixLoader {
    static ByType = new Map();                 // tipo -> ModPrefix

    static #byCategory = new Map();            // PrefixCategory -> [ModPrefix]
    static #categoriesByType = new Map();      // tipo de item -> [PrefixCategory]
    static #names = new Map();                 // tipo -> { key, texts }
    static #vanilla = -1;
    // Quem sobrescreve o quê entre ModItem/GlobalItem (o hook só existe se alguém usa).
    static #itemHooks = new Set();

    static get VanillaCount() {
        if (PrefixLoader.#vanilla < 0) PrefixLoader.#vanilla = bl.items.vanillaPrefixCount();
        return PrefixLoader.#vanilla;
    }

    static get PrefixCount() { return PrefixLoader.VanillaCount + PrefixLoader.ByType.size; }

    static GetPrefix(type) { return PrefixLoader.ByType.get(type); }

    static GetPrefixesInCategory(category) { return PrefixLoader.#byCategory.get(category) || []; }

    static IsWeaponSubCategory(category) {
        return category === PrefixCategory.Melee || category === PrefixCategory.Ranged || category === PrefixCategory.Magic;
    }

    static Register(inst) {
        const name = inst.constructor.name;
        const category = inst.Category;
        if (!Object.values(PrefixCategory).includes(category)) {
            throw new TypeError(name + '.Category: use um PrefixCategory (recebeu ' + category + ')');
        }

        inst.Type = bl.items.registerPrefix(name);
        PrefixLoader.ByType.set(inst.Type, inst);
        if (!PrefixLoader.#byCategory.has(category)) PrefixLoader.#byCategory.set(category, []);
        PrefixLoader.#byCategory.get(category).push(inst);

        const texts = inst.DisplayName || Lang.Localized('PrefixName', name) ||
                      Lang.Localized('Prefixes', name + '.DisplayName') || name;
        const key = 'Mods.' + (inst.Mod ? inst.Mod.id || inst.Mod.uuid : 'bl') + '.Prefixes.' + name + '.DisplayName';
        PrefixLoader.#names.set(inst.Type, { key, texts });
        LocalizationLoader.Register(key, texts);

        const has = (method) => Hooks.Overrides(inst.constructor, ModPrefix, method);
        PrefixLoader.WantRolling();
        Hooks.Once('prefix.setup', () => Ready.Add(PrefixLoader.#Setup, 'groups'));
        if (has('Apply')) PrefixLoader.#WantApply();
        if (has('ApplyAccessoryEffects')) Hooks.Once('prefix.accessory', PrefixLoader.#HookAccessories);
        if (has('GetTooltipLines')) {
            Hooks.Once('item.Tooltips', TooltipLoader.Install);
            Hooks.Once('prefix.Reforge', TooltipLoader.HookReforge);
        }
    }

    // Os métodos de prefixo que um ModItem/GlobalItem sobrescreve (ChoosePrefix, AllowPrefix...).
    static WantItemHooks(methods) {
        for (const m of methods) PrefixLoader.#itemHooks.add(m);
        PrefixLoader.WantRolling();
        if (methods.includes('ApplyPrefix') || methods.includes('PrefixChance')) PrefixLoader.#WantApply();
    }

    // Item de mod ganha os prefixos das categorias dele (MeleePrefix...): sem
    // isto, as tabelas PrefixLegacy.ItemSets do jogo não o conhecem e ele não
    // ganha nenhum.
    static WantRollable() {
        Hooks.Once('prefix.rollable', PrefixLoader.#HookRollable);
    }

    static WantRolling() {
        PrefixLoader.WantRollable();
        Hooks.Once('prefix.rolling', PrefixLoader.#HookRolling);
    }

    // As categorias do item, guardadas por tipo como no tModLoader.
    static Categories(item) {
        const type = item.type;
        const cached = PrefixLoader.#categoriesByType.get(type);
        if (cached) return cached;

        const S = Terraria.GameContent.Prefixes.PrefixLegacy.ItemSets;
        const m = ItemLoader.Of(item);
        const weapon = m && PrefixLoader.#CanBeModWeapon(item);
        const ask = (method) => weapon && Safe.Run(m.constructor.name + '.' + method, () => m[method](item)) === true;

        const out = [];
        if (S.SwordsHammersAxesPicks[type] || ask('MeleePrefix')) out.push(PrefixCategory.Melee);
        if (S.GunsBows[type] || ask('RangedPrefix')) out.push(PrefixCategory.Ranged);
        if (S.Magic[type] || ask('MagicPrefix')) out.push(PrefixCategory.Magic);
        if (S.Summon[type] || ask('SummonPrefix')) out.push(PrefixCategory.Summon);
        if (S.SpearsMacesChainsawsDrillsPunchCannon[type] || S.BoomerangsChakrams[type] ||
            S.ItemsThatCanHaveLegendary2[type] || ask('WeaponPrefix') || out.length) {
            out.push(PrefixCategory.AnyWeapon);
        }
        if (item['bool IsAPrefixableAccessory()']()) out.push(PrefixCategory.Accessory);

        if (type > 0) PrefixLoader.#categoriesByType.set(type, out);
        return out;
    }

    // Os prefixos do jogo de uma categoria (o GetVanillaPrefixes do tModLoader).
    static VanillaPrefixes(category) {
        const P = Terraria.GameContent.Prefixes.PrefixLegacy.Prefixes;
        switch (category) {
            case PrefixCategory.Melee: return P.PrefixesForSwords;
            case PrefixCategory.Ranged: return P.PrefixesForGunsBows;
            case PrefixCategory.Magic: return P.PrefixesForMagic;
            case PrefixCategory.Summon: return P.PrefixesForSummons;
            case PrefixCategory.AnyWeapon: return P.PrefixesForSpears;
            case PrefixCategory.Accessory: return P.PrefixesForAccessories;
            default: return null;
        }
    }

    static AllowPrefix(item, pre) {
        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'AllowPrefix') &&
            Safe.Run(m.constructor.name + '.AllowPrefix', () => m.AllowPrefix(item, pre)) === false) {
            return false;
        }
        return globalItems.All(item, 'AllowPrefix', (g) => g.AllowPrefix(item, pre));
    }

    // As linhas do GetTooltipLines do prefixo do item, ou null.
    static TooltipLines(item) {
        const p = PrefixLoader.GetPrefix(item.prefix);
        if (!p || !Hooks.Overrides(p.constructor, ModPrefix, 'GetTooltipLines')) return null;

        const lines = Safe.Run(p.Name + '.GetTooltipLines', () => p.GetTooltipLines(item));
        const list = lines ? Array.from(lines).filter((l) => l instanceof TooltipLine) : [];
        return list.length ? list : null;
    }

    // Item de mod que pode ganhar prefixo de arma. O celular põe maxStack 9999
    // em todo item, então a munição e o consumível ficam de fora por eles mesmos.
    static #CanBeModWeapon(item) {
        return item.damage > 0 && item.ammo === 0 && !item.consumable &&
               !!Terraria.ID.ItemID.Sets.CanGetPrefixes[item.type];
    }

    // No conteúdo pronto: as tabelas do jogo crescem e os nomes entram.
    static #Setup() {
        Safe.Run('PrefixLoader: tabelas', PrefixLoader.#Tables);
        for (const p of PrefixLoader.ByType.values()) {
            Safe.Run(p.Name + '.SetStaticDefaults', () => p.SetStaticDefaults());
        }

        // A troca de idioma refaz o Lang.prefix (o mesmo array, cheio de Empty).
        Terraria.Lang['void InitializeLegacyLocalization()'].hook((original) => {
            original();
            Safe.Run('PrefixLoader: nomes', PrefixLoader.#Tables);
        });
    }

    static #Tables() {
        const total = PrefixLoader.PrefixCount;
        const L = Terraria.Lang;
        const Sets = Terraria.ID.PrefixID.Sets;
        if (L.prefix.length < total) L.prefix = L.prefix.cloneResized(total);
        if (Sets.ReducedNaturalChance.length < total) Sets.ReducedNaturalChance = Sets.ReducedNaturalChance.cloneResized(total);

        // O LocalizedText da chave: a troca de idioma muda o texto nele.
        const getText = Terraria.Localization.Language['LocalizedText GetText(string key)'];
        const names = L.prefix;
        for (const [type, { key, texts }] of PrefixLoader.#names) {
            LocalizationLoader.Put(key, Lang.Pick(texts));
            names[type] = getText(key);
        }
    }

    // A lista do jogo (ou a das categorias, no item de mod) mais os prefixos de mod.
    static #HookRollable() {
        const Item = Terraria.Item;

        Item['int[] GetRollablePrefixes()'].hook((original, self) => {
            const base = original(self);
            return PrefixLoader.#Rollable(self, base);
        });

        // Custom não entra na rolagem, mas vale onde o prefixo deixa (e o
        // FixAgainstExploit, ao carregar, não tira).
        Item['bool CanRollPrefix(int prefix)'].hook((original, self, prefix) => {
            const p = PrefixLoader.GetPrefix(prefix);
            if (!p || p.Category !== PrefixCategory.Custom) return original(self, prefix);

            return PrefixLoader.AllowPrefix(self, prefix) && Safe.Run(p.Name + '.CanRoll', () => p.CanRoll(self)) === true;
        }, { minType: PrefixLoader.VanillaCount, arg: 0 });
    }

    static #Rollable(item, base) {
        const cats = PrefixLoader.Categories(item);
        const modItem = !!ItemLoader.Of(item);
        const filter = PrefixLoader.#itemHooks.has('AllowPrefix');

        const extra = [];
        for (const c of cats) {
            for (const p of PrefixLoader.GetPrefixesInCategory(c)) {
                if (Safe.Run(p.Name + '.CanRoll', () => p.CanRoll(item)) === true) extra.push(p.Type);
            }
        }
        if (!modItem && !extra.length && !filter) return base;

        const out = [];
        const seen = new Set();
        const add = (id) => {
            if (seen.has(id)) return;
            seen.add(id);
            if (!filter || PrefixLoader.AllowPrefix(item, id)) out.push(id);
        };
        const addAll = (arr) => {
            if (arr) for (let i = 0; i < arr.length; i++) add(arr[i]);
        };

        addAll(base);
        if (modItem) for (const c of cats) addAll(PrefixLoader.VanillaPrefixes(c));
        for (const id of extra) add(id);
        return out.length ? out : null;
    }

    // A rolagem com peso (RollChance) e os status do SetStats.
    static #HookRolling() {
        const Item = Terraria.Item;

        Item['bool RollAPrefix(UnifiedRandom random, ref int rolledPrefix)'].hook((original, self, random, rolled) => {
            const forced = PrefixLoader.#ChoosePrefix(self, random);
            if (forced > 0 && self['bool CanRollPrefix(int prefix)'](forced)) {
                rolled.value = forced;
                return true;
            }

            const list = self['int[] GetRollablePrefixes()']();
            if (!list || !list.length) return false;

            const ids = [];
            const weights = [];
            let total = 0, anyMod = false;
            for (let i = 0; i < list.length; i++) {
                const id = list[i];
                const p = PrefixLoader.GetPrefix(id);
                const w = p ? Number(Safe.Run(p.Name + '.RollChance', () => p.RollChance(self))) || 0 : 1;
                if (p) anyMod = true;
                if (w <= 0) continue;
                ids.push(id);
                weights.push(w);
                total += w;
            }
            // Só prefixos do jogo, todos com peso 1: a rolagem do próprio jogo.
            if (!anyMod) return original(self, random, rolled);
            if (!ids.length) return false;

            let r = random['double NextDouble()']() * total;
            let k = 0;
            while (k < ids.length - 1 && r >= weights[k]) r -= weights[k++];
            rolled.value = ids[k];
            return true;
        });

        Item['bool TryGetPrefixStatMultipliersForItem(int rolledPrefix, out float dmg, out float kb, out float spd, out float size, out float shtspd, out float mcst, out int crt, out int tagdmg, out int arpen, out float value)'].hook(
            (original, self, rolledPrefix, dmg, kb, spd, size, shtspd, mcst, crt, tagdmg, arpen, value) => {
                const p = PrefixLoader.GetPrefix(rolledPrefix);
                if (!p) return original();

                const s = PrefixLoader.#StatsOf(p, self);
                dmg.value = s.dmg;
                kb.value = s.kb;
                spd.value = s.spd;
                size.value = s.size;
                shtspd.value = s.shtspd;
                mcst.value = s.mcst;
                crt.value = s.crt;
                tagdmg.value = s.tagdmg;
                arpen.value = s.arpen;
                value.value = s.value;
                return s.ok;
            }, { minType: PrefixLoader.VanillaCount, arg: 0 });
    }

    // O que o TryGetPrefixStatMultipliersForItem do jogo faz, com o SetStats do prefixo.
    static #StatsOf(p, item) {
        const s = { dmg: 1, kb: 1, spd: 1, size: 1, shtspd: 1, mcst: 1, crt: 0, tagdmg: 0, arpen: 0, value: 1, ok: false };
        if (Safe.Run(p.Name + '.AllStatChangesHaveEffectOn', () => p.AllStatChangesHaveEffectOn(item)) === false) return s;

        if (p.SetStats.length === 1) {
            const stats = { damage: 1, knockBack: 1, speed: 1, size: 1, shootSpeed: 1, mana: 1, crit: 0, tagDamage: 0, armorPenetration: 0 };
            Safe.Run(p.Name + '.SetStats', () => p.SetStats(stats));
            Object.assign(s, { dmg: stats.damage, kb: stats.knockBack, spd: stats.speed, size: stats.size,
                               shtspd: stats.shootSpeed, mcst: stats.mana, crt: stats.crit,
                               tagdmg: stats.tagDamage, arpen: stats.armorPenetration });
        } else {
            const refs = [s.dmg, s.kb, s.spd, s.size, s.shtspd, s.mcst, s.crt, s.tagdmg, s.arpen].map((v) => new Ref(v));
            Safe.Run(p.Name + '.SetStats', () => p.SetStats(...refs));
            [s.dmg, s.kb, s.spd, s.size, s.shtspd, s.mcst, s.crt, s.tagdmg, s.arpen] = refs.map((r) => Number(r.value));
        }

        // Os float do jogo: a conta e a comparação saem iguais às dele.
        const f = Math.fround;
        for (const k of ['dmg', 'kb', 'spd', 'size', 'shtspd', 'mcst']) s[k] = f(Number.isFinite(s[k]) ? s[k] : 1);
        for (const k of ['crt', 'tagdmg', 'arpen']) s[k] = Number.isFinite(s[k]) ? Math.trunc(s[k]) : 0;

        const valueMult = new Ref(f(s.dmg * (2 - s.spd) * (2 - s.mcst) * s.size * s.kb * s.shtspd *
                                    (1 + s.crt * f(0.02)) * (1 + s.arpen * f(0.015)) * (1 + s.tagdmg * f(0.03))));
        Safe.Run(p.Name + '.ModifyValue', () => p.ModifyValue(valueMult));
        s.value = f(Number(valueMult.value) || 0);

        // A regra do jogo: todo status que o prefixo mexe tem de mudar de verdade no item.
        const round = PrefixLoader.#RoundEven;
        s.ok = (s.dmg === 1 || round(item.damage * s.dmg) !== item.damage) &&
               (s.spd === 1 || round(item.useAnimation * s.spd) !== item.useAnimation) &&
               (s.mcst === 1 || round(item.mana * s.mcst) !== item.mana) &&
               (s.kb === 1 || item.knockBack !== 0);
        return s;
    }

    // O Math.Round do C#: meio vai para o par.
    static #RoundEven(x) {
        const r = Math.round(x);
        return Math.abs(x % 1) === 0.5 && r % 2 !== 0 ? r - 1 : r;
    }

    static #ChoosePrefix(item, rand) {
        if (!PrefixLoader.#itemHooks.has('ChoosePrefix')) return -1;

        let pre = -1;
        globalItems.Each(item, 'ChoosePrefix', (g) => {
            const v = g.ChoosePrefix(item, rand);
            if (pre <= 0 && v > 0) pre = v;
        });
        if (pre > 0) return pre;

        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'ChoosePrefix')) {
            const v = Safe.Run(m.constructor.name + '.ChoosePrefix', () => m.ChoosePrefix(item, rand));
            if (v > 0) return v;
        }
        return -1;
    }

    // false (algum bloqueia) vence true (algum força); null = o do jogo.
    static #PrefixChance(item, pre, rand) {
        let result = null;
        const take = (r) => {
            if (r === true || r === false) result = r && (result ?? true);
        };
        globalItems.Each(item, 'PrefixChance', (g) => take(g.PrefixChance(item, pre, rand)));

        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'PrefixChance')) {
            take(Safe.Run(m.constructor.name + '.PrefixChance', () => m.PrefixChance(item, pre, rand)));
        }
        return result;
    }

    // Depois do Item.Prefix: o Apply do prefixo de mod e o ApplyPrefix do item.
    static #WantApply() {
        Hooks.Once('prefix.apply', () => {
            const W = Terraria.WorldGen;

            Terraria.Item['bool Prefix(int prefixWeWant, out bool rolledPrefixIsTopTier)'].hook((original, self, want, topTier) => {
                let arg = want;
                // -1 (criar, baú): true pula a chance de vir sem prefixo, como o -2 da reforja.
                if (want < 0 && want > -3 && PrefixLoader.#itemHooks.has('PrefixChance')) {
                    const rand = W.isGeneratingOrLoadingWorld ? W.genRand : Terraria.Main.rand;
                    const chance = PrefixLoader.#PrefixChance(self, want, rand);
                    if (chance === false) return false;
                    if (chance === true && want === -1) arg = -2;
                }

                const ok = original(self, arg, topTier);
                // -3 só pergunta se dá (a reforja monta a lista assim): nada foi aplicado.
                if (!ok || want === -3 || self.prefix === 0) return ok;

                const pre = self.prefix;
                const p = PrefixLoader.GetPrefix(pre);
                if (p) Safe.Run(p.Name + '.Apply', () => p.Apply(self));

                const m = ItemLoader.Of(self);
                if (m && Hooks.Overrides(m.constructor, ModItem, 'ApplyPrefix')) {
                    Safe.Run(m.constructor.name + '.ApplyPrefix', () => m.ApplyPrefix(self, pre));
                }
                globalItems.Each(self, 'ApplyPrefix', (g) => g.ApplyPrefix(self, pre));
                return ok;
            });
        });
    }

    // Acessório com prefixo de mod, a cada quadro (o jogo só dá os dele).
    static #HookAccessories() {
        Terraria.Player['void GrantPrefixBenefits(Item item)'].hook((original, self, item) => {
            original(self, item);

            const p = PrefixLoader.GetPrefix(item.prefix);
            if (p) Safe.Run(p.Name + '.ApplyAccessoryEffects', () => p.ApplyAccessoryEffects(self));
        }, { minType: PrefixLoader.VanillaCount, field: 'prefix', on: 0 });
    }
}
