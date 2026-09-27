class GlobalItem extends GlobalType {
    SetDefaults(item) {}
    CanUseItem(item, player) { return true; }
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
    OnCraft(item, player, recipe) {}
    ModifyTooltips(item, tooltips) {}

    static register(cls) {
        const inst = globalItems.Register(cls, 'GlobalItem');
        GlobalItemLoader.Hook(cls);
        return inst;
    }
}
