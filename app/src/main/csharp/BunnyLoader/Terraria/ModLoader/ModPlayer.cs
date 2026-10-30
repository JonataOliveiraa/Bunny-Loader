using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.GameInput;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public abstract class ModPlayer : ModType<Player, ModPlayer>
{
    public ushort Index { get; internal set; }

    public Player Player => Entity;

    protected override Player CreateTemplateEntity() => default;

    public override ModPlayer NewInstance(Player entity)
    {
        ModPlayer instance = base.NewInstance(entity);
        instance.Index = Index;
        return instance;
    }

    public bool TypeEquals(ModPlayer other) => Mod == other.Mod && Name == other.Name;

    protected sealed override void Register() => PlayerLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void Initialize() { }

    public virtual void ResetEffects() { }

    public virtual void ResetInfoAccessories() { }

    public virtual void NaturalLifeRegen(ref float regen) { }

    public virtual void UpdateAutopause() { }

    public virtual void ProcessTriggers(TriggersSet triggersSet) { }

    public virtual void ArmorSetBonusActivated() { }

    public virtual void ArmorSetBonusHeld(int holdTime) { }

    public virtual void UpdateVisibleAccessories() { }

    public virtual void UpdateVisibleVanityAccessories() { }

    public virtual void UpdateDyes() { }

    public virtual void PostUpdateMiscEffects() { }

    public virtual void PostUpdateRunSpeeds() { }

    public virtual void PreUpdateMovement() { }

    public virtual void OnEquipmentLoadoutSwitched(int oldLoadoutIndex, int loadoutIndex) { }

    public virtual void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        health = StatModifier.Default;
        mana = StatModifier.Default;
    }

    public virtual void UpdateDead() { }

    public virtual void PreSaveCustomData() { }

    public virtual void SaveData(TagCompound tag) { }

    public virtual void LoadData(TagCompound tag) { }

    public virtual void CopyClientState(ModPlayer targetCopy) { }

    public virtual void SyncPlayer(int toWho, int fromWho, bool newPlayer) { }

    public virtual void SendClientChanges(ModPlayer clientPlayer) { }

    public virtual void PlayerConnect() { }

    public virtual void PlayerDisconnect() { }

    public virtual void PreSavePlayer() { }

    public virtual void PostSavePlayer() { }

    public virtual void UpdateBadLifeRegen() { }

    public virtual void UpdateLifeRegen() { }

    public virtual void PreUpdate() { }

    public virtual void PreUpdateBuffs() { }

    public virtual void PostUpdateBuffs() { }

    public virtual void UpdateEquips() { }

    public virtual void PostUpdateEquips() { }

    public virtual void PostUpdate() { }

    public virtual void FrameEffects() { }

    public virtual bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => false;

    public virtual bool FreeDodge(Player.HurtInfo info) => false;

    public virtual bool ConsumableDodge(Player.HurtInfo info) => false;

    public virtual void ModifyHurt(ref Player.HurtModifiers modifiers) { }

    public virtual void OnHurt(Player.HurtInfo info) { }

    public virtual void PostHurt(Player.HurtInfo info) { }

    public virtual bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => true;

    public virtual bool CanBeHitByProjectile(Projectile proj) => true;

    public virtual void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) { }

    public virtual void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) { }

    public virtual void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) { }

    public virtual void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) { }

    public virtual bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) => true;

    public virtual void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) { }

    public virtual bool PreModifyLuck(ref float luck) => true;

    public virtual void ModifyLuck(ref float luck) { }

    public virtual bool PreItemCheck() => true;

    public virtual void PostItemCheck() { }

    public virtual float UseTimeMultiplier(Item item) => 1f;

    public virtual float UseAnimationMultiplier(Item item) => 1f;

    public virtual float UseSpeedMultiplier(Item item) => 1f;

    public virtual void GetHealLife(Item item, bool quickHeal, ref int healValue) { }

    public virtual void GetHealMana(Item item, bool quickHeal, ref int healValue) { }

    public virtual void ModifyManaCost(Item item, ref float reduce, ref float mult) { }

    public virtual void OnMissingMana(Item item, int neededMana) { }

    public virtual void OnConsumeMana(Item item, int manaConsumed) { }

    public virtual bool ApplyPotionDelay(Item item, int potionDelay) => true;

    public virtual void ModifyWeaponDamage(Item item, ref StatModifier damage) { }

    public virtual void ModifyWeaponKnockback(Item item, ref StatModifier knockback) { }

    public virtual void ModifyWeaponCrit(Item item, ref float crit) { }

    public virtual bool CanConsumeAmmo(Item weapon, Item ammo) => true;

    public virtual void OnConsumeAmmo(Item weapon, Item ammo) { }

    public virtual bool CanShoot(Item item) => true;

    public virtual void ModifyShootStats(Item item, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) { }

    public virtual bool Shoot(Item item, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) => true;

    public virtual void MeleeEffects(Item item, Rectangle hitbox) { }

    public virtual void ModifyItemScale(Item item, ref float scale) { }

    public virtual void OnHitAnything(float x, float y, Entity victim) { }

    public virtual bool CanHitNPC(NPC target) => true;

    public virtual bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle meleeAttackHitbox, NPC target) => null;

    public virtual void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { }

    public virtual bool? CanHitNPCWithItem(Item item, NPC target) => null;

    public virtual void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) { }

    public virtual bool CanHitPvp(Item item, Player target) => true;

    public virtual bool CanHitPvpWithProj(Projectile proj, Player target) => true;

    public virtual bool CanUseItem(Item item) => true;

    public virtual bool? CanAutoReuseItem(Item item) => null;

    public virtual bool? CanCatchNPC(NPC target, Item item) => null;

    public virtual void OnCatchNPC(NPC npc, Item item, bool failed) { }

    public virtual bool ShiftClickSlot(Item[] inventory, int context, int slot) => false;

    public virtual bool HoverSlot(Item[] inventory, int context, int slot) => false;

    public virtual void PostSellItem(NPC vendor, Item[] shopInventory, Item item) { }

    public virtual bool CanSellItem(NPC vendor, Item[] shopInventory, Item item) => true;

    public virtual void PostBuyItem(NPC vendor, Item[] shopInventory, Item item) { }

    public virtual bool CanBuyItem(NPC vendor, Item[] shopInventory, Item item) => true;

    public virtual bool ModifyNurseHeal(NPC nurse, ref int health, ref bool removeDebuffs, ref string chatText) => true;

    public virtual void ModifyNursePrice(NPC nurse, int health, bool removeDebuffs, ref int price) { }

    public virtual void PostNurseHeal(NPC nurse, int health, bool removeDebuffs, int price) { }

    public virtual IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) => System.Array.Empty<Item>();

    public virtual void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath) { }

    public virtual void ModifyFishingAttempt(ref FishingAttempt attempt) { }

    public virtual void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) { }

    public virtual void ModifyCaughtFish(Item fish) { }

    public virtual bool? CanConsumeBait(Item bait) => null;

    public virtual void GetFishingLevel(Item fishingRod, Item bait, ref float fishingLevel) { }

    public virtual void AnglerQuestReward(float rareMultiplier, List<Item> rewardItems) { }

    public virtual void GetDyeTraderReward(List<int> rewardPool) { }

    public virtual bool OnPickup(WorldItem item) => true;

    public virtual bool CanBeTeleportedTo(Vector2 teleportPosition, string context) => true;

    public virtual void OnEnterWorld() { }

    public virtual void OnRespawn() { }
}
