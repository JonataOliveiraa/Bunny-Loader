using Microsoft.Xna.Framework;
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

    public virtual bool CanUseItem(Item item) => true;

    public virtual bool? CanAutoReuseItem(Item item) => null;

    public virtual void OnEnterWorld() { }

    public virtual void OnRespawn() { }
}
