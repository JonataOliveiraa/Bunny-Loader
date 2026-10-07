class GlobalItem extends GlobalType {
    SetDefaults(item) {}
    CanUseItem(item, player) { return true; }
    // true: o item conta como usado (tempo de uso; o consumível é gasto).
    UseItem(item, player) {}
    UseStyle(item, player, mountOffset, heldItemFrame) {}
    HoldStyle(item, player, mountOffset, heldItemFrame) {}
    HoldItem(item, player) {}
    ModifyWeaponDamage(item, player, damage) { return damage; }
    // crit e scale são Ref (.value); knockback, StatModifier.
    ModifyWeaponCrit(item, player, crit) {}
    ModifyWeaponKnockback(item, player, knockback) {}
    ModifyItemScale(item, player, scale) {}
    // hitbox é o Rectangle do golpe; noHitbox, Ref (true: sem golpe).
    UseItemHitbox(item, player, hitbox, noHitbox) {}
    MeleeEffects(item, player, hitbox) {}
    UseAnimation(item, player) {}
    UseItemFrame(item, player) {}
    HoldItemFrame(item, player) {}

    // Munição (como no ModItem): null deixa o jogo decidir.
    NeedsAmmo(item, player) { return true; }
    CanChooseAmmo(weapon, ammo, player) { return null; }
    CanBeChosenAsAmmo(ammo, weapon, player) { return null; }
    CanConsumeAmmo(weapon, ammo, player) { return true; }
    CanBeConsumedAsAmmo(ammo, weapon, player) { return true; }
    OnConsumeAmmo(weapon, ammo, player) {}
    OnConsumedAsAmmo(ammo, weapon, player) {}
    // type, speed, damage e knockback são Ref.
    PickAmmo(weapon, ammo, player, type, speed, damage, knockback) {}

    // Cura: healValue e baseDelay são Ref.
    GetHealLife(item, player, quickHeal, healValue) {}
    GetHealMana(item, player, quickHeal, healValue) {}
    ModifyPotionDelay(item, player, baseDelay) {}
    ApplyPotionDelay(item, player, potionDelay) { return true; }
    CanShoot(item, player) { return true; }
    ModifyShootStats(item, player, stats) {}
    Shoot(item, player, position, velocity, type, damage, knockBack, source) { return true; }
    // Golpes (como no ModItem): null deixa o jogo decidir; modifiers é um
    // HitModifiers (NPC) ou HurtModifiers (PvP); hit, um HitInfo.
    CanHitNPC(item, player, target) { return null; }
    CanMeleeAttackCollideWithNPC(item, player, hitbox, target) { return null; }
    ModifyHitNPC(item, player, target, modifiers) {}
    OnHitNPC(item, player, target, hit, damageDone) {}
    CanHitPvp(item, player, target) { return true; }
    ModifyHitPvp(item, player, target, modifiers) {}
    OnHitPvp(item, player, target, hurtInfo) {}
    UpdateInventory(item, player) {}
    UpdateEquip(item, player) {}
    UpdateAccessory(item, player, vanity, hideVisual) {}
    // O item no chão, todo quadro (ver ModItem.PreUpdateInWorld).
    PreUpdateInWorld(item, worldItem) { return true; }
    PostUpdateInWorld(item, worldItem) {}
    OnCraft(item, player, recipe) {}
    ModifyTooltips(item, tooltips) {}
    // O desenho do tooltip (DrawableTooltipLine: X, Y, Font, Color, BaseScale...).
    // x, y e yOffset são Ref; false no Pre não desenha.
    PreDrawTooltip(item, lines, x, y) { return true; }
    PostDrawTooltip(item, lines) {}
    PreDrawTooltipLine(item, line, yOffset) { return true; }
    PostDrawTooltipLine(item, line) {}
    // Prefixos de qualquer item (também os do jogo).
    ChoosePrefix(item, rand) { return -1; }
    PrefixChance(item, pre, rand) { return null; }
    AllowPrefix(item, pre) { return true; }
    ApplyPrefix(item, pre) {}

    // Conjuntos de qualquer item: o nome do conjunto ('' = nenhum), e o efeito por ele.
    CanAccessoryBeEquippedWith(equippedItem, incomingItem, player) { return true; }
    IsArmorSet(head, body, legs) { return ''; }
    UpdateArmorSet(player, set) {}
    // Vaidade: head, body e legs são os SLOTS desenhados; sem sobrescrever, o
    // IsArmorSet dos itens desses slots.
    IsVanitySet(head, body, legs) {
        const sample = (table, slot) => ItemLoader.Sample(slot > 0 && slot < table.length ? table[slot] : 0);
        return this.IsArmorSet(sample(Terraria.Item.headType, head), sample(Terraria.Item.bodyType, body),
                               sample(Terraria.Item.legType, legs));
    }
    PreUpdateVanitySet(player, set) {}
    UpdateVanitySet(player, set) {}
    ArmorSetShadows(player, set) {}
    // armorSlot: 0 cabeça, 1 corpo, 2 pernas; type: o slot dessa parte. equipSlot e robes são Ref.
    SetMatch(armorSlot, type, male, equipSlot, robes) {}
    // Asas (também as do jogo): item é o vestido, ou null.
    VerticalWingSpeeds(item, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend) {}
    HorizontalWingSpeeds(item, player, speed, acceleration) {}
    WingUpdate(wings, player, inUse) { return false; }

    static register(cls) {
        const inst = globalItems.Register(cls, 'GlobalItem');
        GlobalItemLoader.Hook(cls);
        return inst;
    }
}
