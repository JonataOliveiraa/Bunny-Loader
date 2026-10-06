class ModPlayer {
    CumulativeHealth = 0;
    CumulativeMana = 0;
    WeaponDamage = 0;

    get Player() { return Entities.Of(this); }

    Initialize() {}
    OnEnterWorld(player) {}
    OnRespawn(player) {}

    ResetEffects(player) {}
    ModifyMaxStats(player) {
        this.CumulativeHealth = 0;
        this.CumulativeMana = 0;
    }
    PreUpdate(player) {}
    PostUpdate(player) {}
    PreUpdateBuffs(player) {}
    PostUpdateBuffs(player) {}
    UpdateEquips(player) {}
    PostUpdateEquips(player) {}
    UpdateBadLifeRegen(player) {}
    UpdateLifeRegen(player) {}
    UpdateManaRegen(player) {}
    UpdateDead(player) {}
    UpdateMovement(player) {}
    FrameEffects(player) {}

    PreModifyLuck(player, luck) { return true; }
    ModifyLuck(player, luck) {}

    CanUseItem(player, item) { return true; }
    ModifyWeaponDamage(player, item, damage) {}

    ImmuneTo(player, damageSource, cooldownCounter, dodgeable) { return false; }
    FreeDodge(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) { return false; }
    ConsumableDodge(player, info) { return false; }
    ModifyHurt(player, modifiers) {}
    OnHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    PostHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    PreKill(player, damageSource, damage, hitDirection, pvp) { return true; }
    Kill(player, damageSource, damage, hitDirection, pvp) {}
    CatchFish(attempt, itemDrop, npcSpawn, sonar, sonarPosition) {}

    CanHitNPC(player, target) { return true; }
    CanHitNPCWithItem(player, item, target) { return null; }
    CanHitNPCWithProj(player, projectile, target) { return null; }
    CanHitPvp(player, item, target) { return true; }
    CanMeleeAttackCollideWithNPC(player, item, hitbox, target) { return null; }
    ModifyHitNPC(player, target, modifiers) {}
    ModifyHitNPCWithItem(player, item, target, modifiers) {}
    ModifyHitNPCWithProj(player, projectile, target, modifiers) {}
    OnHitNPC(player, target, hit, damageDone) {}
    OnHitNPCWithItem(player, item, target, hit, damageDone) {}
    OnHitNPCWithProj(player, projectile, target, hit, damageDone) {}
    OnHitAnything(player, x, y, victim) {}
    MeleeEffects(player, item, hitbox) {}
    EmitEnchantmentVisualsAt(player, projectile, position, width, height) {}
    CanBeHitByNPC(player, npc, cooldownSlot) { return true; }
    CanBeHitByProjectile(player, projectile) { return true; }
    ModifyHitByNPC(player, npc, modifiers) {}
    ModifyHitByProjectile(player, projectile, modifiers) {}
    OnHitByNPC(player, npc, info) {}
    OnHitByProjectile(player, projectile, info) {}

    CanShoot(player, item) { return true; }
    Shoot(player, item, source, position, velocity, type, damage, knockBack) { return true; }
    ModifyShootStats(player, item, position, velocity, type, damage, knockBack) {}
    CanConsumeAmmo(player, weapon, ammo) { return true; }
    OnConsumeAmmo(player, weapon, ammo) {}
    CanAutoReuseItem(player, item) { return null; }
    ModifyWeaponCrit(player, item, crit) {}
    ModifyWeaponKnockback(player, item, knockback) {}
    ModifyItemScale(player, item, scale) {}
    UseSpeedMultiplier(player, item) { return 1; }
    UseTimeMultiplier(player, item) { return 1; }
    UseAnimationMultiplier(player, item) { return 1; }
    ModifyManaCost(player, item, reduce, mult) {}
    OnConsumeMana(player, item, manaConsumed) {}
    OnMissingMana(player, item, neededMana) {}
    PreItemCheck(player) { return true; }
    PostItemCheck(player) {}
    GetHealLife(player, item, quickHeal, healValue) {}
    GetHealMana(player, item, quickHeal, healValue) {}
    ApplyPotionDelay(player, item, potionDelay) { return true; }

    PreUpdateMovement(player) {}
    PostUpdateMiscEffects(player) {}
    PostUpdateRunSpeeds(player) {}
    NaturalLifeRegen(player, regen) {}
    UpdateAutopause(player) {}
    ProcessTriggers(player, triggersSet) {}
    ResetInfoAccessories(player) {}
    ArmorSetBonusActivated(player) {}
    ArmorSetBonusHeld(player, holdTime) {}
    OnEquipmentLoadoutSwitched(player, oldLoadoutIndex, loadoutIndex) {}

    CanStartExtraJump(player, jump) { return true; }
    CanShowExtraJumpVisuals(player, jump) { return true; }
    ExtraJumpVisuals(player, jump) {}
    ModifyExtraJumpDurationMultiplier(player, jump, duration) {}
    OnExtraJumpStarted(player, jump, playSound) {}
    OnExtraJumpEnded(player, jump) {}
    OnExtraJumpRefreshed(player, jump) {}
    OnExtraJumpCleared(player, jump) {}

    DrawEffects(player, drawInfo, r, g, b, a, fullBright) {}
    DrawPlayer(player, camera) {}
    HideDrawLayers(player, drawInfo) {}
    ModifyDrawInfo(player, drawInfo) {}
    ModifyDrawLayerOrdering(player, positions) {}
    TransformDrawData(player, drawInfo) {}
    ModifyScreenPosition(player) {}
    ModifyZoom(player, zoom) {}

    GetFishingLevel(player, fishingRod, bait, fishingLevel) {}
    ModifyFishingAttempt(player, attempt) {}
    ModifyCaughtFish(player, fish) {}
    CanConsumeBait(player, bait) { return null; }
    AnglerQuestReward(player, rareMultiplier, rewardItems) {}
    CanBuyItem(player, vendor, shopInventory, item) { return true; }
    PostBuyItem(player, vendor, shopInventory, item) {}
    CanSellItem(player, vendor, shopInventory, item) { return true; }
    PostSellItem(player, vendor, shopInventory, item) {}
    ModifyNursePrice(player, nurse, health, removeDebuffs, price) {}
    ModifyNurseHeal(player, nurse, health, removeDebuffs, chatText) { return true; }
    PostNurseHeal(player, nurse, health, removeDebuffs, price) {}
    GetDyeTraderReward(player, rewardPool) {}
    CanCatchNPC(player, target, item) { return null; }
    OnCatchNPC(player, npc, item, failed) {}
    AddStartingItems(player, mediumCoreDeath) { return []; }
    ModifyStartingInventory(player, itemsByMod, mediumCoreDeath) {}
    AddMaterialsForCrafting(player, itemConsumedCallback) { return null; }
    OnPickup(player, item) { return true; }
    HoverSlot(player, inventory, context, slot) { return false; }
    ShiftClickSlot(player, inventory, context, slot) { return false; }
    CanBeTeleportedTo(player, teleportPosition, context) { return true; }

    PreSavePlayer(player) {}
    PostSavePlayer(player) {}
    PreSaveCustomData(player) {}
    SyncPlayer(player, toWho, fromWho, newPlayer) {}
    SendClientChanges(player, clientPlayer) {}
    CopyClientState(player, targetCopy) {}
    PlayerConnect(player) {}
    PlayerDisconnect(player) {}

    SaveData(data) {}
    LoadData(data) {}

    static AddDrawData(drawInfo, drawData) {
        const cache = drawInfo && drawInfo.DrawDataCache;
        const count = drawInfo && drawInfo.DrawDataCacheCount;
        if (!cache || !drawData || !Number.isInteger(count) || count < 0 || count >= cache.length) return false;
        cache[count] = drawData;
        drawInfo.DrawDataCacheCount = count + 1;
        return true;
    }

    static get(player) {
        return PlayerLoader.Of(player).get(this);
    }

    static getByName(name) {
        return PlayerLoader.Find(Terraria.Main.player[Terraria.Main.myPlayer], name);
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModPlayer)) {
            throw new TypeError('ModPlayer.register(Classe): passe a classe, que estende ModPlayer');
        }

        Hooks.Once('player.fields', () => {
            Entities.Define(Terraria.Player, 'ModPlayers');
            bl.defineMethod(Terraria.Player, 'GetModPlayer', function (which) {
                return PlayerLoader.Find(this, which);
            });
        });

        PlayerLoader.Add(cls);
        PlayerLoader.Hook(cls);

        if (['SaveData', 'LoadData', 'PreSavePlayer', 'PostSavePlayer', 'PreSaveCustomData'].some((name) => Hooks.Overrides(cls, ModPlayer, name))) {
            Hooks.Once('player.save', PlayerLoader.InstallSave);
        }
        return cls;
    }
}
