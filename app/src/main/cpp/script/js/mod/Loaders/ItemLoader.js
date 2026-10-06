class ItemLoader {
    // Os campos que o CloneDefaults copia (Item.CloneDefaults não existe nesta versão).
    static CLONED_FIELDS = [
        'wornArmor', 'tooltipContext', 'BestiaryNotes', 'sentry', 'DD2Summon',
        'shopSpecialCurrency', 'expert', 'expertOnly', 'questItem', 'fishingPole', 'bait',
        'hairDye', 'makeNPC', 'dye', 'paint', 'paintCoating', 'tileWand', 'notAmmo', 'crit',
        'mech', 'reuseDelay', 'melee', 'magic', 'ranged', 'summon', 'placeStyle', 'buffTime',
        'buffType', 'mountType', 'cartTrack', 'material', 'noWet', 'vanity', 'mana', 'channel',
        'manaIncrease', 'noMelee', 'noUseGraphic', 'lifeRegen', 'shoot', 'shootSpeed',
        'shootsEveryUse', 'alpha', 'ammo', 'useAmmo', 'autoReuse', 'accessory', 'axe',
        'healMana', 'potion', 'color', 'consumable', 'createTile', 'createWall',
        'useSoundPitch', 'damage', 'defense', 'armorPenetration', 'hammer', 'healLife',
        'holdStyle', 'knockBack', 'maxStack', 'pick', 'rare', 'scale', 'tileBoost', 'useStyle',
        'useTime', 'useAnimation', 'value', 'useTurn', 'buy', 'uniqueStack', 'width', 'height',
        'UseSound',
    ];

    static ByType = new Map();

    static #animations = new Map();
    static #shooting = null;
    static #drawingWorldItem = -1;     // o whoami do Main.DrawItem em curso
    static #opening = null;            // o item sendo aberto: o NPC de mentira e para onde vão os itens
    static #openable = new Set();      // os tipos de mod que abrem (CanRightClick)

    // UseItem que devolveu true: o item conta como usado, como no tModLoader
    // (ApplyItemTime). O jogo só gasta o consumível com o tempo de uso aplicado
    // (itemTime == itemTimeMax); um item que só age no UseItem (a invocação de
    // chefe) nunca o teria. O tempo entra logo depois do ItemCheck_OwnerOnlyCode,
    // onde o tModLoader chama o UseItem: antes da conta do consumo no mesmo quadro.
    static #usedByPlayer = new Map();   // whoAmI -> Item

    static MarkUsed(player, item, result) {
        if (result !== true) return;
        ItemLoader.#usedByPlayer.set(player.whoAmI, item);
        Hooks.Once('item.UseTime', () => {
            Terraria.Player['void ItemCheck_OwnerOnlyCode(ref Player.ItemCheckContext context, Item sItem, int weaponDamage, Rectangle heldItemFrame)'].hook(
                (original, self, context, sItem, weaponDamage, frame) => {
                    original();
                    const used = ItemLoader.#usedByPlayer;
                    if (!used.size) return;
                    const item = used.get(self.whoAmI);
                    if (!item) return;
                    used.delete(self.whoAmI);
                    if (self.ItemTimeIsZero && self.itemAnimation > 0) self['void ApplyItemTime(Item sItem)'](item);
                });
        });
    }

    static WantAccessoryPairs() {
        Hooks.Once('item.AccessoryPairs', () => Safe.Run('pares de acessórios', ItemLoader.#HookAccessoryPairs));
    }

    // O ItemLoader.CanAccessoryBeEquippedWith do tModLoader. O jogo pergunta
    // no CanEquipBothAccessories (o que está no slot, o que chega), e a troca
    // pelo toque vai para o slot do que foi recusado.
    static #HookAccessoryPairs() {
        Terraria.UI.ItemSlot['bool CanEquipBothAccessories(Item acc1, Item acc2, bool vanity)'].hook((original, equipped, incoming, vanity) => {
            if (!original(equipped, incoming, vanity)) return false;
            if (!equipped || !incoming || equipped.type <= 0 || incoming.type <= 0) return true;

            const player = Terraria.Main.player[Terraria.Main.myPlayer];
            return ItemLoader.#Pair(equipped, incoming, player) && ItemLoader.#Pair(incoming, equipped, player);
        });
    }

    static #Pair(equipped, incoming, player) {
        for (const item of [equipped, incoming]) {
            const m = ItemLoader.Of(item);
            if (!m) continue;

            const ok = Safe.Run(m.constructor.name + '.CanAccessoryBeEquippedWith', () => m.CanAccessoryBeEquippedWith(equipped, incoming, player));
            if (ok === false) return false;
        }

        for (const g of globalItems.list) {
            if (!Hooks.Overrides(g.constructor, GlobalItem, 'CanAccessoryBeEquippedWith')) continue;

            const ok = Safe.Run(g.constructor.name + '.CanAccessoryBeEquippedWith', () => g.CanAccessoryBeEquippedWith(equipped, incoming, player));
            if (ok === false) return false;
        }
        return true;
    }

    static Of(item) {
        return Entities.InstanceOf(item, 'ModItem', ItemLoader.ByType);
    }

    // Abrir item de mod (o CanRightClick/RightClick/ModifyItemLoot do
    // tModLoader). O jogo só abre o que está em ItemID.Sets.OpenableBag, e o
    // TryOpenContainer_GrantItems dele é a lista das bolsas do jogo: uma de
    // mod seria gasta sem soltar nada.
    static #HookOpen() {
        Ready.Add(() => {
            const openable = Terraria.ID.ItemID.Sets.OpenableBag;
            for (const [type, m] of ItemLoader.ByType) {
                if (!Hooks.Overrides(m.constructor, ModItem, 'CanRightClick')) continue;
                if (Safe.Run(m.constructor.name + '.CanRightClick', () => m.CanRightClick(ItemLoader.Sample(type)))) {
                    openable[type] = true;
                    ItemLoader.#openable.add(type);
                }
            }
        });

        Terraria.UI.ItemSlot['bool TryOpenContainer_GrantItems(Item item, Player player)'].hook((original, item, player) => {
            const m = ItemLoader.Of(item);
            return m ? ItemLoader.#Open(m, item, player) : original(item, player);
        }, { minType: FIRST_ITEM, on: 0 });

        // Os controles de toque: o botão "Abrir" do inventário sai da lista
        // de bolsas do jogo (GUIPageOptions.CanBeOpened), e a categoria do
        // item decide os botões de usar; a das bolsas (NonFireItems) os trava.
        GUIPageOptions['bool CanBeOpened(Item SelectedItem)'].hook((original, item) =>
            ItemLoader.#openable.has(item.type) || original(item), { minType: FIRST_ITEM, on: 0 });

        VirtualControllerInputState['VirtualControllerInputState.Category GetItemCategory(int item)'].hook((original, type) =>
            ItemLoader.#openable.has(type) ? VirtualControllerInputState.Category.NonFireItems : original(type),
            { minType: FIRST_ITEM, arg: 0 });

        // As regras do jogo soltam "no NPC". Durante a abertura, o NPC é de
        // mentira e o item vai para o jogador pelo QuickSpawnItem, que é como o
        // jogo solta o conteúdo das bolsas dele (e manda ao servidor no
        // multijogador; o DropItemFromNPC só manda quando roda no servidor).
        Terraria.GameContent.ItemDropRules.CommonCode['void DropItemFromNPC(NPC npc, int itemId, int stack, bool scattered)'].hook(
            (original, npc, itemId, stack, scattered) => {
                const opening = ItemLoader.#opening;
                if (opening && npc === opening.npc) {
                    opening.give(itemId, stack);
                    return;
                }
                original(npc, itemId, stack, scattered);
            });
    }

    // true: aberto (o jogo gasta um); false: não abriu.
    static #Open(m, item, player) {
        const name = m.constructor.name;
        if (!Safe.Run(name + '.CanRightClick', () => m.CanRightClick(item))) return false;

        const type = item.type;
        Safe.Run(name + '.RightClick', () => m.RightClick(item, player));
        ItemLoader.DropFromItem(m, type, player);
        return Safe.Run(name + '.ConsumeItem', () => m.ConsumeItem(item, player)) !== false;
    }

    // As regras do ItemLoot do tipo, pelo resolvedor do jogo (que segue as
    // encadeadas: LeadingConditionRule, Chains.OnSuccess...). O DropFromItem do
    // tModLoader.
    static DropFromItem(m, type, player) {
        const rules = ItemLoot.For(m);
        if (!rules.length) return;

        const source = player['IEntitySource GetItemSource_OpenItem(int itemType)'](type);
        const give = (itemId, stack) => player['void QuickSpawnItem(IEntitySource source, int item, int stack)'](source, itemId, stack);

        const npc = Terraria.NPC.new();
        npc['void .ctor()']();
        npc.position = player.position;
        npc.width = player.width;
        npc.height = player.height;

        const info = Terraria.GameContent.ItemDropRules.DropAttemptInfo.new();
        info.npc = npc;
        info.player = player;
        info.rng = Terraria.Main.rand;
        info.IsExpertMode = Terraria.Main.expertMode;
        info.IsMasterMode = Terraria.Main.masterMode;

        const resolve = Terraria.Main.ItemDropSolver['ItemDropAttemptResult ResolveRule(IItemDropRule rule, DropAttemptInfo info)'];
        ItemLoader.#opening = { npc, give };
        try {
            for (const rule of rules) {
                if (__blCoinRules.has(rule)) ItemLoader.#DropCoins(rule, give);
                else Safe.Run(m.constructor.name + ' (ItemLoot)', () => resolve(rule, info));
            }
        } finally {
            ItemLoader.#opening = null;
        }
    }

    // O CoinsRule do tModLoader: o valor em moedas (do NPC, ou o dado), com o
    // bônus aleatório das moedas de chefe.
    static #DropCoins(rule, give) {
        let value = rule.value;
        if (rule.npcId !== undefined) {
            const samples = Terraria.ID.ContentSamples.NpcsByNetId;
            value = samples.ContainsKey(rule.npcId) ? samples.get_Item(rule.npcId).value : 0;
        }
        let scale = 1;
        if (rule.withRandomBonus) {
            const next = (min, max) => min + Math.floor(Math.random() * (max - min));
            scale += next(-20, 21) * 0.01;
            if (next(0, 5) === 0) scale += next(5, 11) * 0.01;
            if (next(0, 10) === 0) scale += next(10, 21) * 0.01;
            if (next(0, 15) === 0) scale += next(15, 31) * 0.01;
            if (next(0, 20) === 0) scale += next(20, 41) * 0.01;
        }
        let money = Math.floor(value * scale);
        const { ItemID } = Terraria.ID;
        for (const coin of [ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin]) {
            const count = money % 100;
            money = Math.floor(money / 100);
            if (count > 0) give(coin, count);
        }
        if (money > 0) give(ItemID.PlatinumCoin, money);
    }

    // O jogo zera as animações no InitializeItemAnimations, que roda depois do
    // SetStaticDefaults dos mods: reaplicadas a cada vez.
    static RegisterAnimation(type, animation) {
        const register = Terraria.Main['void RegisterItemAnimation(int index, DrawAnimation animation)'];
        ItemLoader.#animations.set(type, animation);
        register(type, animation);

        Hooks.Once('item.Animations', () => {
            Terraria.Main['void InitializeItemAnimations()'].hook((original) => {
                original();
                for (const [t, a] of ItemLoader.#animations) Safe.Run('RegisterItemAnimation', () => register(t, a));
            });
        });
    }

    static SetupTooltip(inst, name, type) {
        let base = inst.Tooltip || Lang.Localized('ItemTooltip', name) || '';
        if (typeof base === 'string') base = base ? { '': base } : {};

        const cultures = Object.keys(base);
        if (!cultures.length && Hooks.Overrides(inst.constructor, ModItem, 'ModifyTooltipLines')) cultures.push('');

        const out = {};
        let any = false;
        for (const culture of cultures) {
            inst.TooltipLines = base[culture] ? String(base[culture]).split('\n') : [];
            Safe.Run(name + '.ModifyTooltipLines', () => inst.ModifyTooltipLines());

            if (inst.TooltipLines.length) {
                out[culture] = inst.TooltipLines.join('\n');
                any = true;
            }
        }

        inst.TooltipLines = [];
        if (any) {
            bl.items.setTooltip(type, out);
            const mod = inst.Mod || bl.mod;
            if (mod) Lang.Follow('BunnyLoader.ItemTooltip.' + mod.uuid + '.' + name, out);
        }
    }

    // A arma na mão: o deslocamento gira com o item.
    static ApplyHoldout(offset, player) {
        if (!offset || (!offset.X && !offset.Y)) return;

        const x = offset.X * player.direction;
        const y = offset.Y * player.gravDir;
        const cos = Math.cos(player.itemRotation);
        const sin = Math.sin(player.itemRotation);

        const location = player.itemLocation;
        location.X += x * cos - y * sin;
        location.Y += x * sin + y * cos;
    }

    static Hook(cls, type) {
        ItemCombatHooks.Install(cls, type);
        const P = Terraria.Player;
        const has = (name) => Hooks.Overrides(cls, ModItem, name);
        const onItem = (param) => ({ minType: FIRST_ITEM, on: param });
        const of = ItemLoader.Of;

        if (has('CanRightClick')) Hooks.Once('item.Open', () => ItemLoader.#HookOpen());

        if (has('CanUseItem')) Hooks.Once('item.CanUse', () => {
            P['bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)'].hook((original, self, item, ignoreCursed) => {
                const m = of(item);
                if (m && Safe.Run(m.constructor.name + '.CanUseItem', () => m.CanUseItem(item, self)) === false) return false;

                return original(self, item, ignoreCursed);
            }, onItem(0));
        });

        if (has('OnCraft')) Hooks.Once('item.OnCraft', () => {
            Terraria.Main['void CraftItem_GrantItem(Recipe recipe, Item result, bool quickCraft)'].hook(
                (original, recipe, result, quickCraft) => {
                    const m = of(result);
                    const player = Terraria.Main.player[Terraria.Main.myPlayer];
                    if (m) Safe.Run(m.constructor.name + '.OnCraft', () => m.OnCraft(result, player, recipe));

                    original(recipe, result, quickCraft);
                }, onItem(1));
        });

        if (has('UseItem')) Hooks.Once('item.Use', () => {
            P['void ItemCheck_StartActualUse(Item sItem)'].hook((original, self, item) => {
                original(self, item);

                const m = of(item);
                if (m) ItemLoader.MarkUsed(self, item, Safe.Run(m.constructor.name + '.UseItem', () => m.UseItem(item, self)));
            }, onItem(0));
        });

        if (has('UseStyle') || has('HoldStyle') || has('HoldoutOffset') || has('HoldItem')) {
            const styles = [['ItemCheck_ApplyUseStyle', 'UseStyle'], ['ItemCheck_ApplyHoldStyle', 'HoldStyle']];

            for (const [method, hookName] of styles) Hooks.Once('item.' + hookName, () => {
                P['void ' + method + '(float mountOffset, Item sItem, Rectangle heldItemFrame)'].hook(
                    (original, self, mountOffset, item, frame) => {
                        original(self, mountOffset, item, frame);

                        const m = of(item);
                        if (!m) return;

                        const n = m.constructor.name;
                        Safe.Run(n + '.HoldoutOffset', () => ItemLoader.ApplyHoldout(m.HoldoutOffset(item, self), self));
                        Safe.Run(n + '.' + hookName, () => m[hookName](item, self, mountOffset, frame));
                        Safe.Run(n + '.HoldItem', () => m.HoldItem(item, self));
                    }, onItem(1));
            });
        }

        if (has('CanShoot') || has('ModifyShootStats') || has('Shoot')) Hooks.Once('item.Shoot', ItemLoader.#HookShoot);

        if (has('OnHitNPC')) Hooks.Once('item.OnHitNPC', () => {
            P['void ApplyNPCOnHitEffects(Item sItem, Rectangle itemRectangle, int damage, float knockBack, NPC npc, int dmgRandomized, int dmgDone)'].hook(
                (original, self, item, rect, damage, knockBack, npc, dmgRandomized, dmgDone) => {
                    original(self, item, rect, damage, knockBack, npc, dmgRandomized, dmgDone);

                    const m = of(item);
                    if (!m) return;

                    const crit = dmgDone >= dmgRandomized * 2;
                    Safe.Run(m.constructor.name + '.OnHitNPC', () => m.OnHitNPC(item, self, npc, dmgDone, knockBack, crit));
                }, onItem(0));
        });

        if (has('UpdateEquip') || has('UpdateAccessory') || has('UpdateVanity')) {
            Hooks.Once('item.Accessory', () => {
                P['void ApplyEquipFunctional(int itemSlot, Item currentItem)'].hook((original, self, slot, item) => {
                    original(self, slot, item);

                    const m = of(item);
                    if (!m) return;

                    Safe.Run(m.constructor.name + '.UpdateEquip', () => m.UpdateEquip(item, self));
                    if (item.accessory) {
                        const hide = !!self.hideVisibleAccessory[slot];
                        Safe.Run(m.constructor.name + '.UpdateAccessory', () => m.UpdateAccessory(item, self, false, hide));
                    }
                }, onItem(1));
            });

            if (has('UpdateAccessory') || has('UpdateVanity')) Hooks.Once('item.AccessoryVanity', () => {
                P['void ApplyEquipVanity(int itemSlot, Item currentItem)'].hook((original, self, slot, item) => {
                    original(self, slot, item);

                    const m = of(item);
                    if (m && item.accessory) {
                        Safe.Run(m.constructor.name + '.UpdateAccessory', () => m.UpdateAccessory(item, self, true, false));
                        Safe.Run(m.constructor.name + '.UpdateVanity', () => m.UpdateVanity(item, self));
                    }
                }, onItem(1));
            });

            Hooks.Once('item.Armor', () => {
                P['void GrantArmorBenefits(Item armorPiece)'].hook((original, self, item) => {
                    original(self, item);

                    const m = of(item);
                    if (m) Safe.Run(m.constructor.name + '.UpdateEquip', () => m.UpdateEquip(item, self));
                }, onItem(0));
            });
        }

        // O item no chão: o Main.DrawItem pede a cor ao Item de dentro da WorldItem.
        // Só os tipos que escrevem o GetAlpha entram no JS (o jogo o chama
        // para todo item desenhado).
        if (has('GetAlpha')) bl.hookMarks.set('item.GetAlpha', type);
        if (has('GetAlpha')) Hooks.Once('item.GetAlpha', () => {
            ItemLoader.#HookDrawItem();
            Terraria.Item['Color GetAlpha(Color newColor)'].hook((original, self, color) => {
                const m = of(self);
                if (!m || !Hooks.Overrides(m.constructor, ModItem, 'GetAlpha')) return original(self, color);

                // O mod recebe o item no chão (WorldItem: Center, position), como antes da 1.4.5.8.
                const world = ItemLoader.#WorldItemOf(self) || self;
                const c = Safe.Run(m.constructor.name + '.GetAlpha', () => m.GetAlpha(world, color));
                return original(self, c || color);
            }, { minType: FIRST_ITEM, on: -1, marks: 'item.GetAlpha' });
        });

        if (has('PostUpdate')) bl.hookMarks.set('item.PostUpdate', type);
        if (has('PostUpdate')) Hooks.Once('item.PostUpdate', () => {
            Terraria.WorldItem['void UpdateItem(int i)'].hook((original, self, i) => {
                original(self, i);
                const m = self.active ? of(self.inner) : undefined;
                if (!m || !Hooks.Overrides(m.constructor, ModItem, 'PostUpdate')) return;
                Safe.Run(m.constructor.name + '.PostUpdate', () => m.PostUpdate(self));
            }, { minType: FIRST_ITEM, on: -1, field: 'inner.type', marks: 'item.PostUpdate' });
        });

        // O item no chão: o filtro nativo pelo tipo do Item de dentro (inner.type).
        if (has('PreUpdateInWorld') || has('PostUpdateInWorld')) bl.hookMarks.set('item.UpdateInWorld', type);
        if (has('PreUpdateInWorld') || has('PostUpdateInWorld')) Hooks.Once('item.UpdateInWorld', () => {
            Terraria.WorldItem['void UpdateItem(int i)'].hook((original, world, i) => {
                const item = world.inner;
                const m = world.active ? of(item) : undefined;
                if (!m) return original(world, i);

                const n = m.constructor.name;
                if (Safe.Run(n + '.PreUpdateInWorld', () => m.PreUpdateInWorld(item, world)) !== false) original(world, i);
                if (world.active) Safe.Run(n + '.PostUpdateInWorld', () => m.PostUpdateInWorld(item, world));
            }, { minType: FIRST_ITEM, on: -1, field: 'inner.type', marks: 'item.UpdateInWorld' });
        });

        if (['ModifyTooltips', 'PreDrawTooltip', 'PostDrawTooltip', 'PreDrawTooltipLine', 'PostDrawTooltipLine'].some(has)) {
            Hooks.Once('item.Tooltips', TooltipLoader.Install);
        }

        if (has('ModifyFishingLine')) Hooks.Once('item.FishingLine', ItemLoader.#HookFishingLine);

        if (has('CanAccessoryBeEquippedWith')) ItemLoader.WantAccessoryPairs();

        // Conjuntos, vaidade e asas: nos seus carregadores (ArmorSetLoader, WingLoader).
        if (has('IsArmorSet') || has('UpdateArmorSet')) ArmorSetLoader.WantArmorSets();
        if (has('IsVanitySet') || has('PreUpdateVanitySet') || has('UpdateVanitySet') || has('EquipFrameEffects')) ArmorSetLoader.WantFrame();
        if (has('IsVanitySet') || has('ArmorSetShadows')) ArmorSetLoader.WantShadows();
        if (has('SetMatch')) ArmorSetLoader.WantSetMatch();
        if (has('VerticalWingSpeeds')) WingLoader.Want('Vertical');
        if (has('HorizontalWingSpeeds')) WingLoader.Want('Horizontal');
        if (has('WingUpdate')) WingLoader.Want('Update');

        const prefixHooks = ['ChoosePrefix', 'PrefixChance', 'AllowPrefix', 'ApplyPrefix'].filter(has);
        if (prefixHooks.length) PrefixLoader.WantItemHooks(prefixHooks);

        if (has('UpdateInventory')) Hooks.Once('item.Inventory', () => {
            P['void UpdateEquips(int i)'].hook((original, self, i) => {
                original(self, i);

                for (const item of bl.items.modItemsIn(self)) {
                    const m = of(item);
                    if (m) Safe.Run(m.constructor.name + '.UpdateInventory', () => m.UpdateInventory(item, self));
                }
            });
        });
    }

    // A amostra do jogo (ContentSamples) do tipo; a do "nada" (0) se não há.
    static Sample(type) {
        const samples = Terraria.ID.ContentSamples.ItemsByType;
        return samples.ContainsKey(type) ? samples.get_Item(type) : samples.get_Item(0);
    }

    // O endereço da amostra do tipo; 0 se ainda não há.
    static SampleAddress(type) {
        try {
            const samples = Terraria.ID.ContentSamples.ItemsByType;
            return samples && samples.ContainsKey(type) ? bl.addressOf(samples.get_Item(type)) : 0;
        } catch (e) {
            return 0;
        }
    }

    // Item.Clone copia os campos nativos; a cópia ganha um Clone do ModItem.
    static HookClone() {
        Terraria.Item['Item Clone()'].hook((original, self) => {
            const copy = original(self);
            const m = copy ? self.ModItem : undefined;
            if (m && m.Type === self.type) {
                Safe.Run(m.constructor.name + '.Clone', () => Entities.Bind(m.Clone(copy), copy, 'ModItem'));
            }
            return copy;
        }, { minType: FIRST_ITEM, on: -1 });
    }

    // O NewProjectile que o ItemCheck_Shoot chamar é deste tiro.
    static #HookShoot() {
        Terraria.Player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'].hook(
            (original, self, i, item, damage, feedback) => {
                const m = ItemLoader.Of(item);
                if (!m) return original(self, i, item, damage, feedback);

                if (Safe.Run(m.constructor.name + '.CanShoot', () => m.CanShoot(item, self)) === false) {
                    self['void ApplyItemTime(Item sItem)'](item);
                    return undefined;
                }

                const outer = ItemLoader.#shooting;
                ItemLoader.#shooting = { m, item, player: self };
                try {
                    return original(self, i, item, damage, feedback);
                } finally {
                    ItemLoader.#shooting = outer;
                }
            }, { minType: FIRST_ITEM, on: 1 });

        Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'].hook(
            (original, source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier) => {
                const shot = ItemLoader.#shooting;
                if (!shot) return original(source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);

                // O projétil que o Shoot criar não é mais do tiro.
                ItemLoader.#shooting = null;
                try {
                    const { m, item, player } = shot;
                    const n = m.constructor.name;
                    const stats = { position: Vector2.new(x, y), velocity: Vector2.new(sx, sy), type, damage, knockBack };

                    Safe.Run(n + '.ModifyShootStats', () => m.ModifyShootStats(item, player, stats));
                    const go = Safe.Run(n + '.Shoot', () => m.Shoot(item, player, stats.position, stats.velocity,
                                                                     stats.type, stats.damage, stats.knockBack));
                    if (go === false) return 1000;   // o "sem vaga" do jogo

                    return original(source, stats.position.X, stats.position.Y, stats.velocity.X, stats.velocity.Y,
                                    stats.type, stats.damage, stats.knockBack, owner, ai0, ai1, ai2, modifier);
                } finally {
                    ItemLoader.#shooting = shot;
                }
            });
    }

    // A WorldItem que embrulha este Item (o Main.item guarda WorldItem; o
    // Item.GetAlpha só vê o de dentro): a que o Main.DrawItem está desenhando.
    // Fora dele (inventário, loja), nenhuma.
    static #WorldItemOf(item) {
        const index = ItemLoader.#drawingWorldItem;
        if (index < 0) return null;
        const w = Terraria.Main.item[index];
        return w && w.inner === item ? w : null;
    }

    // O jogo chama o DrawItem para os 400 espaços a cada quadro: o filtro
    // nativo pelo tipo do Item de dentro (inner.type) deixa só o de mod entrar no JS.
    static #HookDrawItem() {
        Terraria.Main['void DrawItem(WorldItem item, int whoami)'].hook((original, self, item, whoami) => {
            const outer = ItemLoader.#drawingWorldItem;
            ItemLoader.#drawingWorldItem = whoami;
            try {
                return original(self, item, whoami);
            } finally {
                ItemLoader.#drawingWorldItem = outer;
            }
        }, { minType: FIRST_ITEM, on: 0, field: 'inner.type', marks: 'item.GetAlpha' });
    }

    // A linha da vara sai do mountedCenter; o deslocamento entra por ele e a
    // cor pelo TryApplyingPlayerStringColor, como no ItemLoader do tModLoader.
    static #HookFishingLine() {
        const Main = Terraria.Main;
        const draw = Main['void DrawProj_FishingLine(Projectile proj, Player theOwner, ref float polePosX, ref float polePosY, Vector2 mountedCenter)'];
        let lineColor = null;

        draw.hook((original, proj, owner, polePosX, polePosY, center) => {
            const item = owner.inventory[owner.selectedItem];
            const m = item ? ItemLoader.Of(item) : undefined;
            if (!m) return original();

            const line = ItemLoader.#FishingLineOf(m, item, proj);
            const offset = line.lineOriginOffset || { X: 0, Y: 0 };
            const dir = owner.direction;
            const x = center.X + offset.X * dir - (dir < 0 ? 13 : 0);
            const y = center.Y + offset.Y * owner.gravDir;

            const outer = lineColor;
            lineColor = line.lineColor || null;
            try {
                return original(proj, owner, polePosX, polePosY, Vector2.new(x, y));
            } finally {
                lineColor = outer;
            }
        });

        Main['Color TryApplyingPlayerStringColor(int playerStringColor, Color stringColor)'].hook(
            (original, playerColor, color) => (lineColor ? original(playerColor, lineColor) : original()),
            { whileIn: draw });
    }

    // ModifyFishingLine(item, bobber, line) ou (item, bobber, ref offset, ref cor).
    static #FishingLineOf(m, item, proj) {
        const name = m.constructor.name + '.ModifyFishingLine';
        const line = { lineOriginOffset: Vector2.new(0, 0), lineColor: Color.new(200, 200, 200, 100) };

        if (m.ModifyFishingLine.length === 3) {
            Safe.Run(name, () => m.ModifyFishingLine(item, proj, line));
            return line;
        }

        const offsetRef = new Ref(line.lineOriginOffset);
        const colorRef = new Ref(line.lineColor);
        Safe.Run(name, () => m.ModifyFishingLine(item, proj, offsetRef, colorRef));
        line.lineOriginOffset = offsetRef.value;
        line.lineColor = colorRef.value;
        return line;
    }
}
