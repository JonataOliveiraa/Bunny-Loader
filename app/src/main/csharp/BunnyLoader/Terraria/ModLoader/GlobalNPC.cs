using Terraria.DataStructures;
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
}
