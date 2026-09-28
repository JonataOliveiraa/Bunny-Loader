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

    static Of(item) {
        return Entities.InstanceOf(item, 'ModItem', ItemLoader.ByType);
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
        if (any) bl.items.setTooltip(type, out);
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

    static Hook(cls) {
        const P = Terraria.Player;
        const has = (name) => Hooks.Overrides(cls, ModItem, name);
        const onItem = (param) => ({ minType: FIRST_ITEM, on: param });
        const of = ItemLoader.Of;

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
                if (m) Safe.Run(m.constructor.name + '.UseItem', () => m.UseItem(item, self));
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
            P['void ApplyNPCOnHitEffects(Item sItem, Rectangle itemRectangle, int damage, float knockBack, int npcIndex, int dmgRandomized, int dmgDone)'].hook(
                (original, self, item, rect, damage, knockBack, npcIndex, dmgRandomized, dmgDone) => {
                    original(self, item, rect, damage, knockBack, npcIndex, dmgRandomized, dmgDone);

                    const m = of(item);
                    if (!m) return;

                    const npc = Terraria.Main.npc[npcIndex];
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

        // WorldItem.type é propriedade, não campo: sem filtro nativo.
        if (has('GetAlpha')) Hooks.Once('item.GetAlpha', () => {
            Terraria.WorldItem['Color GetAlpha(Color newColor)'].hook((original, self, color) => {
                const m = ItemLoader.ByType.has(self.type) ? of(self.inner) : undefined;
                if (!m || !Hooks.Overrides(m.constructor, ModItem, 'GetAlpha')) return original(self, color);

                const c = Safe.Run(m.constructor.name + '.GetAlpha', () => m.GetAlpha(self, color));
                return original(self, c || color);
            });
        });

        if (has('ModifyTooltips')) Hooks.Once('item.Tooltips', TooltipLoader.Install);

        if (has('ModifyFishingLine')) Hooks.Once('item.FishingLine', ItemLoader.#HookFishingLine);

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
