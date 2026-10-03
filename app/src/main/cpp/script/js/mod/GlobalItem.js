class GlobalItem extends GlobalType {
    SetDefaults(item) {}
    CanUseItem(item, player) { return true; }
    // true: o item conta como usado (tempo de uso; o consumível é gasto).
    UseItem(item, player) {}
    UseStyle(item, player, mountOffset, heldItemFrame) {}
    HoldStyle(item, player, mountOffset, heldItemFrame) {}
    HoldItem(item, player) {}
    ModifyWeaponDamage(item, player, damage) { return damage; }
    CanShoot(item, player) { return true; }
    ModifyShootStats(item, player, stats) {}
    Shoot(item, player, position, velocity, type, damage, knockBack, source) { return true; }
    OnHitNPC(item, player, target, damageDone, knockBack, crit) {}
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
