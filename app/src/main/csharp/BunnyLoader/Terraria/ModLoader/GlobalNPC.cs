using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class GlobalNPC : GlobalType<NPC, GlobalNPC>
{
    internal NPCMethod Hooks;
    protected sealed override void Register() => NPCLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults(NPC entity) { }

    public virtual void SetDefaultsFromNetId(NPC npc) { }

    public virtual void OnSpawn(NPC npc, IEntitySource source) { }

    public virtual void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment) { }

    public virtual void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) { }

    public virtual void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) { }

    public virtual void BossHeadSlot(NPC npc, ref int index) { }

    public virtual void BossHeadRotation(NPC npc, ref float rotation) { }

    public virtual void BossHeadSpriteEffects(NPC npc, ref SpriteEffects spriteEffects) { }

    public virtual Color? GetAlpha(NPC npc, Color drawColor) => null;

    public virtual void DrawEffects(NPC npc, ref Color drawColor) { }

    public virtual bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => true;

    public virtual void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { }

    public virtual void DrawBehind(NPC npc, int index) { }

    public virtual void ResetEffects(NPC npc) { }

    public virtual bool PreAI(NPC npc) => true;

    public virtual void AI(NPC npc) { }

    public virtual void PostAI(NPC npc) { }

    public virtual void FindFrame(NPC npc, int frameHeight) { }

    public virtual void HitEffect(NPC npc, NPC.HitInfo hit) { }

    public virtual void UpdateLifeRegen(NPC npc, ref int damage) { }

    public virtual bool CheckActive(NPC npc) => true;

    public virtual bool CheckDead(NPC npc) => true;

    public virtual bool? CanBeHitByItem(NPC npc, Player player, Item item) => null;

    public virtual bool? CanCollideWithPlayerMeleeAttack(NPC npc, Player player, Item item, Microsoft.Xna.Framework.Rectangle meleeAttackHitbox) => null;

    public virtual void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) { }

    public virtual void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) { }

    public virtual bool? CanBeHitByProjectile(NPC npc, Projectile projectile) => null;

    public virtual void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) { }

    public virtual bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => true;

    public virtual void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) { }

    public virtual void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) { }

    public virtual bool CanHitNPC(NPC npc, NPC target) => true;

    public virtual bool CanBeHitByNPC(NPC npc, NPC attacker) => true;

    public virtual void ModifyHitNPC(NPC npc, NPC target, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitNPC(NPC npc, NPC target, NPC.HitInfo hit) { }

    public virtual void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) { }

    public virtual void ModifyGlobalLoot(GlobalLoot globalLoot) { }

    public virtual bool PreKill(NPC npc) => true;

    public virtual void OnKill(NPC npc) { }
}
