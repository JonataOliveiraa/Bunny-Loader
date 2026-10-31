using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcAi.Content;

public sealed class AGlobalNpc : GlobalNPC
{
    public override bool IsLoadingEnabled(Mod mod) => !mod.FileExists("native-only.flag");
    public override bool InstancePerEntity => true;
    public bool VetoPre, VetoActive, VetoDead;
    public int PreCalls, AICalls, PostCalls, FrameCalls, ResetCalls, ActiveCalls, DeadCalls, SpawnCalls, RegenLoss, RegenDamage = -1;
    public NPC.HitInfo Hit;
    public IEntitySource Source;
    public int VariantNetId;
    public static int DefaultsCalls;

    public static void StartObserving() => On_NPC.AI += ObserveAI;

    public static void StopObserving() => On_NPC.AI -= ObserveAI;

    static void ObserveAI(On_NPC.orig_AI orig, NPC npc)
    {
        if (Checks.ObserveOriginal && npc.ModNPC is ProbeNpc own)
        {
            Checks.OriginalType = npc.type;
            Checks.OriginalModType = own.Type;
            Checks.OriginalMod = own;
        }
        orig(npc);
    }

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
        !lateInstantiation && (entity.type == NPCID.Zombie || NPCLoader.GetNPC(entity.type) is ProbeNpc or ScaleNpc);

    public override void SetDefaults(NPC npc)
    {
        DefaultsCalls++;
        if (!Checks.EditVanilla || npc.type != NPCID.Zombie) return;
        npc.width = 30;
        npc.height = 40;
        npc.lifeMax = 100;
        npc.damage = 50;
        npc.buffImmune[BuffID.Poisoned] = false;
    }

    public override void SetDefaultsFromNetId(NPC npc) => VariantNetId = npc.netID;
    public override bool PreAI(NPC npc) { PreCalls++; Checks.Add(1); return !VetoPre; }
    public override void AI(NPC npc) { AICalls++; Checks.Add(5); }
    public override void PostAI(NPC npc) { PostCalls++; Checks.Add(8); }
    public override void FindFrame(NPC npc, int frameHeight) { FrameCalls++; npc.frame.Y += frameHeight; }
    public override void ResetEffects(NPC npc) => ResetCalls++;
    public override bool CheckActive(NPC npc) { ActiveCalls++; return !VetoActive; }
    public override bool CheckDead(NPC npc) { DeadCalls++; return !VetoDead; }
    public override void OnSpawn(NPC npc, IEntitySource source) { SpawnCalls++; Source = source; }
    public override void HitEffect(NPC npc, NPC.HitInfo hit) => Hit = hit;
    public override void UpdateLifeRegen(NPC npc, ref int damage)
    {
        npc.lifeRegen -= RegenLoss;
        if (RegenDamage >= 0) damage = RegenDamage;
    }
    public override void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment)
    {
        if (Checks.EditVanilla && npc.type == NPCID.Zombie) npc.buffImmune[BuffID.OnFire] = true;
        if (npc.ModNPC is not ScaleNpc own) return;
        own.GlobalSeenLife = npc.lifeMax;
        if (!Checks.DisableScaling) npc.lifeMax += 10;
    }
}

public sealed class BGlobalNpc : GlobalNPC
{
    public override bool IsLoadingEnabled(Mod mod) => !mod.FileExists("native-only.flag");
    public override bool InstancePerEntity => true;
    public int PreCalls, AICalls, PostCalls, DeadCalls, ActiveCalls;
    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => !lateInstantiation && NPCLoader.GetNPC(entity.type) is ProbeNpc;
    public override bool PreAI(NPC npc) { PreCalls++; Checks.Add(2); return true; }
    public override void AI(NPC npc) { AICalls++; Checks.Add(6); }
    public override void PostAI(NPC npc) { PostCalls++; Checks.Add(9); }
    public override bool CheckDead(NPC npc) { DeadCalls++; return true; }
    public override bool CheckActive(NPC npc) { ActiveCalls++; return true; }
}
