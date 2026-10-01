class GlobalItemLoader {
    static #shot = null;   // o tiro de agora, para o NewProjectile dele

    static Hook(cls) {
        const P = Terraria.Player;
        const registry = globalItems;
        const has = (name) => Hooks.Overrides(cls, GlobalItem, name);

        if (has('SetDefaults') || registry.cached) Hooks.Once('gitem.SetDefaults', () => {
            Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'].hook((original, item, type, variant) => {
                original(item, type, variant);
                if (!(item.type > 0)) return;

                if (registry.cached) registry.Attach(item, true);
                registry.Each(item, 'SetDefaults', (g) => g.SetDefaults(item));
            });
        });

        // A cópia de um item leva a cópia dos Globais por entidade, com o estado.
        if (registry.cached) Hooks.Once('gitem.Clone', () => {
            Terraria.Item['Item Clone()'].hook((original, self) => {
                const copy = original(self);
                const current = copy ? self[registry.field] : undefined;
                if (current && current.type === self.type) {
                    const cloneOf = (g) => (g.__perEntity ? Safe.Run(g.constructor.name + '.Clone', () => g.Clone(self, copy)) || g : g);
                    copy[registry.field] = { type: current.type, list: current.list.map(cloneOf) };
                }
                return copy;
            });
        });

        if (has('CanUseItem')) Hooks.Once('gitem.CanUse', () => {
            P['bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)'].hook((original, self, item, ignoreCursed) => {
                if (!registry.All(item, 'CanUseItem', (g) => g.CanUseItem(item, self))) return false;

                return original(self, item, ignoreCursed);
            });
        });

        if (has('UseItem')) Hooks.Once('gitem.Use', () => {
            P['void ItemCheck_StartActualUse(Item sItem)'].hook((original, self, item) => {
                original(self, item);
                let used = false;
                registry.Each(item, 'UseItem', (g) => {
                    if (g.UseItem(item, self) === true) used = true;
                });
                ItemLoader.MarkUsed(self, item, used);
            });
        });

        const styles = [['ItemCheck_ApplyUseStyle', 'UseStyle'], ['ItemCheck_ApplyHoldStyle', 'HoldStyle']];
        for (const [method, hookName] of styles) {
            if (has(hookName) || has('HoldItem')) Hooks.Once('gitem.' + hookName, () => {
                P['void ' + method + '(float mountOffset, Item sItem, Rectangle heldItemFrame)'].hook(
                    (original, self, mountOffset, item, frame) => {
                        original(self, mountOffset, item, frame);
                        registry.Each(item, hookName, (g) => g[hookName](item, self, mountOffset, frame));
                        registry.Each(item, 'HoldItem', (g) => g.HoldItem(item, self));
                    });
            });
        }

        if (has('ModifyWeaponDamage')) Hooks.Once('gitem.WeaponDamage', () => {
            P['int GetWeaponDamage(Item sItem)'].hook((original, self, item) => {
                let damage = original(self, item);
                registry.Each(item, 'ModifyWeaponDamage', (g) => {
                    const r = g.ModifyWeaponDamage(item, self, damage);
                    if (typeof r === 'number') damage = r;
                });
                return Math.floor(damage);
            });
        });

        if (['CanShoot', 'ModifyShootStats', 'Shoot'].some(has)) Hooks.Once('gitem.Shoot', GlobalItemLoader.#HookShoot);

        if (has('OnHitNPC')) HitLoader.ItemHitsNPC();

        if (has('UpdateEquip') || has('UpdateAccessory')) {
            Hooks.Once('gitem.Accessory', () => {
                P['void ApplyEquipFunctional(int itemSlot, Item currentItem)'].hook((original, self, slot, item) => {
                    original(self, slot, item);

                    registry.Each(item, 'UpdateEquip', (g) => g.UpdateEquip(item, self));
                    if (!item.accessory) return;

                    const hide = !!self.hideVisibleAccessory[slot];
                    registry.Each(item, 'UpdateAccessory', (g) => g.UpdateAccessory(item, self, false, hide));
                });
            });

            if (has('UpdateAccessory')) Hooks.Once('gitem.AccessoryVanity', () => {
                P['void ApplyEquipVanity(int itemSlot, Item currentItem)'].hook((original, self, slot, item) => {
                    original(self, slot, item);
                    if (item.accessory) registry.Each(item, 'UpdateAccessory', (g) => g.UpdateAccessory(item, self, true, false));
                });
            });

            if (has('UpdateEquip')) Hooks.Once('gitem.Armor', () => {
                P['void GrantArmorBenefits(Item armorPiece)'].hook((original, self, item) => {
                    original(self, item);
                    registry.Each(item, 'UpdateEquip', (g) => g.UpdateEquip(item, self));
                });
            });
        }

        // As 58 casas do inventário, a cada quadro.
        if (has('UpdateInventory')) Hooks.Once('gitem.Inventory', () => {
            P['void UpdateEquips(int i)'].hook((original, self, i) => {
                original(self, i);

                const inventory = self.inventory;
                for (let k = 0; k < 58; k++) {
                    const item = inventory[k];
                    if (item.type > 0) registry.Each(item, 'UpdateInventory', (g) => g.UpdateInventory(item, self));
                }
            });
        });

        if (has('OnCraft')) Hooks.Once('gitem.OnCraft', () => {
            Terraria.Main['void CraftItem_GrantItem(Recipe recipe, Item result, bool quickCraft)'].hook(
                (original, recipe, result, quickCraft) => {
                    const player = Terraria.Main.player[Terraria.Main.myPlayer];
                    registry.Each(result, 'OnCraft', (g) => g.OnCraft(result, player, recipe));
                    original(recipe, result, quickCraft);
                });
        });

        if (['ModifyTooltips', 'PreDrawTooltip', 'PostDrawTooltip', 'PreDrawTooltipLine', 'PostDrawTooltipLine'].some(has)) {
            Hooks.Once('item.Tooltips', TooltipLoader.Install);
        }

        const prefixHooks = ['ChoosePrefix', 'PrefixChance', 'AllowPrefix', 'ApplyPrefix'].filter(has);
        if (prefixHooks.length) PrefixLoader.WantItemHooks(prefixHooks);
        if (has('CanAccessoryBeEquippedWith')) ItemLoader.WantAccessoryPairs();
        if (has('UpdateArmorSet')) ArmorSetLoader.WantArmorSets();
        if (has('PreUpdateVanitySet') || has('UpdateVanitySet')) ArmorSetLoader.WantFrame();
        if (has('ArmorSetShadows')) ArmorSetLoader.WantShadows();
        if (has('SetMatch')) ArmorSetLoader.WantSetMatch();
        if (has('VerticalWingSpeeds')) WingLoader.Want('Vertical', true);
        if (has('HorizontalWingSpeeds')) WingLoader.Want('Horizontal', true);
        if (has('WingUpdate')) WingLoader.Want('Update', true);
    }

    static #HookShoot() {
        const registry = globalItems;
        const shootHooks = ['CanShoot', 'ModifyShootStats', 'Shoot'];

        Terraria.Player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'].hook(
            (original, self, i, item, damage, feedback) => {
                if (!registry.AnyWith(item, shootHooks)) return original(self, i, item, damage, feedback);

                if (!registry.All(item, 'CanShoot', (g) => g.CanShoot(item, self))) {
                    self['void ApplyItemTime(Item sItem)'](item);
                    return undefined;
                }

                const outer = GlobalItemLoader.#shot;
                GlobalItemLoader.#shot = { item, player: self };
                try {
                    return original(self, i, item, damage, feedback);
                } finally {
                    GlobalItemLoader.#shot = outer;
                }
            });

        Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'].hook(
            (original, source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier) => {
                const shot = GlobalItemLoader.#shot;
                if (!shot) return original(source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);

                GlobalItemLoader.#shot = null;
                try {
                    const { item, player } = shot;
                    const stats = { position: Vector2.new(x, y), velocity: Vector2.new(sx, sy), type, damage, knockBack };

                    registry.Each(item, 'ModifyShootStats', (g) => g.ModifyShootStats(item, player, stats));
                    const go = registry.All(item, 'Shoot', (g) => g.Shoot(item, player, stats.position,
                        stats.velocity, stats.type, stats.damage, stats.knockBack, source));
                    if (!go) return 1000;   // o "sem vaga" do jogo

                    return original(source, stats.position.X, stats.position.Y, stats.velocity.X, stats.velocity.Y,
                                    stats.type, stats.damage, stats.knockBack, owner, ai0, ai1, ai2, modifier);
                } finally {
                    GlobalItemLoader.#shot = shot;
                }
            });
    }
}
