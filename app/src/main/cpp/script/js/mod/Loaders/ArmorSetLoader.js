// Os conjuntos e o visual vestido, como o ItemLoader/EquipLoader do
// tModLoader: o conjunto de armadura (o efeito, pelos itens), o de vaidade (o
// visual, pelos slots desenhados), as sombras do conjunto, o SetMatch (o manto
// que troca as pernas) e o FrameEffects (ModPlayer e texturas trocando o que
// se veste). Cada gancho só entra quando algum mod o usa.
class ArmorSetLoader {
    // O tipo de equipamento -> o campo do Player com o slot desenhado.
    static PLAYER_FIELDS = {
        Head: 'head', Body: 'body', Legs: 'legs', HandsOn: 'handon', HandsOff: 'handoff', Back: 'back',
        Front: 'front', Shoes: 'shoe', Waist: 'waist', Wings: 'wings', Shield: 'shield', Neck: 'neck',
        Face: 'face', Beard: 'beard', Balloon: 'balloon',
    };

    static WantArmorSets() { ArmorSetLoader.#Want('armor.Sets', ArmorSetLoader.#HookArmorSets); }
    static WantFrame() { ArmorSetLoader.#Want('armor.Frame', ArmorSetLoader.#HookFrame); }
    static WantShadows() { ArmorSetLoader.#Want('armor.Shadows', ArmorSetLoader.#HookShadows); }
    static WantSetMatch() { ArmorSetLoader.#Want('armor.SetMatch', ArmorSetLoader.#HookSetMatch); }

    // Os HidesTopSkin/HidesBottomSkin/HidesHands/HidesArms do tModLoader
    // (ModHelpers): com o conteúdo pronto, só se algum mod marcou algum.
    static CheckDrawFlags() {
        const extras = globalThis.__blExtraStatics;
        const sets = [extras['Terraria.ID.ArmorIDs.Body.Sets'], extras['Terraria.ID.ArmorIDs.Legs.Sets']];
        if (sets.some((s) => Object.values(s).some((table) => Object.keys(table).length > 0))) {
            ArmorSetLoader.#Want('armor.DrawFlags', ArmorSetLoader.#HookDrawFlags);
        }
    }

    // O formato do 1.4.5, como no ExMod do TL Pro: o conjunto entra no
    // ArmorSetBonuses do jogo, que monta o tooltip ("Bônus definido",
    // "(2/3)") e chama o efeito quando ele está completo. O efeito é o delegate
    // do conjunto de abóbora; o hook no Benefits.Pumpkin separa os dois.
    static #sets = [];
    static #effect = null;

    static CreateArmorSet(head, body, legs, text, primaryPart = 0) {
        const { ArmorSetBonuses, ArmorSetBonus } = Terraria.DataStructures;
        ArmorSetLoader.#Want('armor.Bonus', ArmorSetLoader.#HookBonus);

        ArmorSetBonuses['void Add(ArmorSetEffect Effect, string TextKey, PartType PrimaryPart, int Head, int Body, int Legs)'](
            ArmorSetLoader.#Effect(), text, primaryPart, head, body, legs);
        const all = ArmorSetBonuses.All.ToArray();
        const set = all[all.length - 1];
        ArmorSetLoader.#sets.push({ head, body, legs });

        // O BuildLookup do jogo já rodou: o conjunto entra à mão na lista de cada peça.
        for (const type of new Set([head, body, legs])) {
            if (type <= 0) continue;

            const old = ArmorSetBonuses.SetsContaining[type];
            const list = [];
            for (let i = 0; i < old.length; i++) list.push(old[i]);
            list.push(set);
            ArmorSetBonuses.SetsContaining[type] = ArmorSetBonus.newArray(list);
        }
    }

    static #Effect() {
        if (ArmorSetLoader.#effect) return ArmorSetLoader.#effect;

        const all = Terraria.DataStructures.ArmorSetBonuses.All.ToArray();
        for (let i = 0; i < all.length; i++) {
            if (all[i].Head === Terraria.ID.ItemID.PumpkinHelmet) return (ArmorSetLoader.#effect = all[i].Effect);
        }
        throw new Error('o conjunto de abóbora do jogo não foi achado');
    }

    static #HookBonus() {
        Terraria.DataStructures.ArmorSetBonuses.Benefits['void Pumpkin(Player player)'].hook((original, player) => {
            const armor = player.armor;
            const head = armor[0], body = armor[1], legs = armor[2];
            const matches = (want, item) => want <= 0 || want === item.type;
            const mine = ArmorSetLoader.#sets.some((s) => matches(s.head, head) && matches(s.body, body) && matches(s.legs, legs));
            if (!mine) return original(player);

            for (const item of [head, body, legs]) {
                const m = ItemLoader.Of(item);
                if (m) Safe.Run(m.constructor.name + '.UpdateArmorSet', () => m.UpdateArmorSet(item, player));
            }
            return undefined;
        });
    }

    static #Want(key, install) {
        Hooks.Once(key, () => Safe.Run('ganchos de armadura (' + key + ')', install));
    }

    static #Overrides(g, method) { return Hooks.Overrides(g.constructor, GlobalItem, method); }

    // O conjunto do jogo (ArmorSetBonuses) roda no original; os de mod depois,
    // peça por peça, e os Globais pelo nome do conjunto, como o
    // ItemLoader.UpdateArmorSet do tModLoader. O texto do conjunto no tooltip
    // vem do ArmorSetBonuses (CreateArmorSet): o Player.setBonus não existe
    // mais desde a 1.4.5.8.
    static #HookArmorSets() {
        Terraria.Player['void UpdateArmorSets(int i)'].hook((original, self, i) => {
            original(self, i);

            const armor = self.armor;
            const head = armor[0], body = armor[1], legs = armor[2];
            for (const item of [head, body, legs]) {
                const m = ItemLoader.Of(item);
                if (!m) continue;

                const n = m.constructor.name;
                if (Safe.Run(n + '.IsArmorSet', () => m.IsArmorSet(head, body, legs))) {
                    Safe.Run(n + '.UpdateArmorSet', () => m.UpdateArmorSet(item, self));
                }
            }

            for (const g of globalItems.list) {
                if (!ArmorSetLoader.#Overrides(g, 'UpdateArmorSet')) continue;

                const n = g.constructor.name;
                const set = Safe.Run(n + '.IsArmorSet', () => g.IsArmorSet(head, body, legs));
                if (set) Safe.Run(n + '.UpdateArmorSet', () => g.UpdateArmorSet(self, set));
            }
        });
    }

    // Depois do PlayerFrame, que monta os slots desenhados (armadura, vaidade,
    // SetMatch): o PreUpdateVanitySet, o FrameEffects (que pode trocá-los) e o
    // UpdateVanitySet com o que ficou. O desenho vem depois, com o resultado.
    static #HookFrame() {
        Terraria.Player['void PlayerFrame()'].hook((original, self) => {
            original(self);

            ArmorSetLoader.#Vanity(self, 'PreUpdateVanitySet');
            PlayerLoader.Each(self, 'FrameEffects', (m) => m.FrameEffects(self));
            ArmorSetLoader.#EquipFrameEffects(self);
            ArmorSetLoader.#Vanity(self, 'UpdateVanitySet');
        });
    }

    // As sombras e contornos do conjunto (armorEffectDraw*); pedra e
    // invisibilidade apagam depois, como no jogo.
    static #HookShadows() {
        Terraria.Player['void SetArmorEffectVisuals(Player drawPlayer)'].hook((original, self, drawPlayer) => {
            original(self, drawPlayer);
            ArmorSetLoader.#Vanity(drawPlayer, 'ArmorSetShadows');

            if (!drawPlayer.stoned && drawPlayer.stealth === 1) return;
            self.armorEffectDrawOutlines = false;
            self.armorEffectDrawShadow = false;
            self.armorEffectDrawShadowSubtle = false;
        });
    }

    // O jogo pergunta o slot de cada parte (0 cabeça, 1 corpo, 2 pernas): o
    // corpo devolve as pernas que o manto desenha (robes).
    static #HookSetMatch() {
        Terraria.Player['int SetMatch(Player.SetMatchRequest request, ref bool somethingSpecial)'].hook((original, request, special) => {
            const result = original(request, special);

            const part = request.ArmorSlotRequested;
            const kind = part === 1 ? EquipType.Body : part === 2 ? EquipType.Legs : EquipType.Head;
            const slot = part === 1 ? request.Body : part === 2 ? request.Legs : request.Head;
            const male = request.Male;
            const equip = new Ref(result), robes = new Ref(special.value);

            const tex = EquipLoader.GetEquipTexture(kind, slot);
            if (tex) Safe.Run(tex.Name + '.SetMatch', () => tex.SetMatch(male, equip, robes));
            for (const g of globalItems.list) {
                if (ArmorSetLoader.#Overrides(g, 'SetMatch')) {
                    Safe.Run(g.constructor.name + '.SetMatch', () => g.SetMatch(part, slot, male, equip, robes));
                }
            }

            special.value = robes.value;
            return equip.value;
        });
    }

    // Depois de o jogo decidir o que a armadura cobre (por listas de ids): o
    // que os mods marcaram. missingHand/missingArm = "a armadura não cobre,
    // desenhe a pele".
    static #HookDrawFlags() {
        const extras = globalThis.__blExtraStatics;
        const B = extras['Terraria.ID.ArmorIDs.Body.Sets'], L = extras['Terraria.ID.ArmorIDs.Legs.Sets'];

        Terraria.DataStructures.PlayerDrawSet['void BoringSetup(Player player, ref Vector2 drawPosition, float shadowOpacity, float rotation, ref Vector2 rotationOrigin, Projectile overrideHeldProjectile)'].hook(
            (original, self, player, drawPosition, shadowOpacity, rotation, rotationOrigin, heldProjectile) => {
                original(self, player, drawPosition, shadowOpacity, rotation, rotationOrigin, heldProjectile);

                const body = player.body, legs = player.legs;
                if ((body > 0 && B.HidesTopSkin[body]) || (legs > 0 && L.HidesTopSkin[legs])) self.hidesTopSkin = true;
                if ((body > 0 && B.HidesBottomSkin[body]) || (legs > 0 && L.HidesBottomSkin[legs])) self.hidesBottomSkin = true;
                if (body > 0 && B.HidesHands[body] === false) self.missingHand = true;
                if (body > 0 && B.HidesArms[body]) self.missingArm = false;
            });
    }

    // Pelas texturas dos slots desenhados (a cabeça decide pela cabeça...), e
    // pelos Globais com o nome do conjunto.
    static #Vanity(player, method) {
        const head = player.head, body = player.body, legs = player.legs;

        for (const [kind, slot] of [[EquipType.Head, head], [EquipType.Body, body], [EquipType.Legs, legs]]) {
            const tex = EquipLoader.GetEquipTexture(kind, slot);
            if (!tex) continue;

            if (Safe.Run(tex.Name + '.IsVanitySet', () => tex.IsVanitySet(head, body, legs))) {
                Safe.Run(tex.Name + '.' + method, () => tex[method](player));
            }
        }

        for (const g of globalItems.list) {
            if (!ArmorSetLoader.#Overrides(g, method)) continue;

            const n = g.constructor.name;
            const set = Safe.Run(n + '.IsVanitySet', () => g.IsVanitySet(head, body, legs));
            if (set) Safe.Run(n + '.' + method, () => g[method](player, set));
        }
    }

    // O FrameEffects de cada textura vestida, só nos tipos que têm textura de mod.
    static #EquipFrameEffects(player) {
        for (const kind of EquipLoader.InstalledKinds()) {
            const tex = EquipLoader.GetEquipTexture(kind, player[ArmorSetLoader.PLAYER_FIELDS[kind]]);
            if (tex) Safe.Run(tex.Name + '.FrameEffects', () => tex.FrameEffects(player, kind));
        }
    }
}
