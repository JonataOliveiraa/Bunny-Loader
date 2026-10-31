using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcAi.Content;

public sealed class ProbeNpc : ModNPC
{
    public override string Texture => "CsNpcAi/Content/ProbeNpc";
    public bool VetoPre, VetoActive, VetoDead, Mutable;
    public int PreCalls, AICalls, PostCalls, ResetCalls, FrameCalls, FrameHeight, BeforeFrame, SpawnCalls, RegenLoss, RegenDamage = -1;
    public NPC.HitInfo Hit;
    public IEntitySource Source;
    public float SpawnAI;
    public NPC Nested;
    public int SeenRegenDamage;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 6;
        NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
        BuffID.Sets.GrantImmunityWith[ModContent.BuffType<ProbeBuff>()].Add(BuffID.OnFire);
    }

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 36;
        NPC.lifeMax = 100;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        NPC.townNPC = true;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
    }

    public override bool PreAI() { PreCalls++; Checks.Add(3); return !VetoPre; }
    public override void AI() { AICalls++; Checks.Add(4); }
    public override void PostAI() { PostCalls++; Checks.Add(7); }
    public override void ResetEffects() { ResetCalls++; Mutable = false; }
    public override void FindFrame(int frameHeight)
    {
        FrameCalls++;
        FrameHeight = frameHeight;
        BeforeFrame = NPC.frame.Y;
        NPC.frame.Y = frameHeight * 2;
    }
    public override bool CheckActive() => !VetoActive;
    public override bool CheckDead() => !VetoDead;
    public override void OnSpawn(IEntitySource source) { SpawnCalls++; Source = source; SpawnAI = NPC.ai[0]; }
    public override void HitEffect(NPC.HitInfo hit) => Hit = hit;
    public override void UpdateLifeRegen(ref int damage)
    {
        SeenRegenDamage = damage;
        NPC.lifeRegen -= RegenLoss;
        if (RegenDamage >= 0) damage = RegenDamage;
        if (!Nested.IsNull) Nested.UpdateNPC_BuffApplyDOTs();
    }
}

public sealed class ScaleNpc : ModNPC
{
    public override string Texture => "CsNpcAi/Content/ProbeNpc";
    public int Calls, Players, SeenLife, GlobalSeenLife;
    public float Balance, BossAdjustment;
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 6;
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 36;
        NPC.lifeMax = 100;
        NPC.damage = 10;
        NPC.aiStyle = -1;
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        Calls++;
        Players = numPlayers;
        Balance = balance;
        BossAdjustment = bossAdjustment;
        SeenLife = NPC.lifeMax;
        if (!Checks.DisableScaling) NPC.lifeMax *= 2;
    }
}

public sealed class PlainNpc : ModNPC
{
    public static int Constructed;
    public PlainNpc() => Constructed++;
    public override string Texture => "CsNpcAi/Content/ProbeNpc";
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 6;
    public override void SetDefaults() { NPC.lifeMax = 100; NPC.width = NPC.height = 36; NPC.aiStyle = -1; NPC.damage = 0; }
}

public sealed class ProbeBuff : ModBuff
{
    public override string Texture => "CsNpcAi/Content/ProbeNpc";
    public override void SetStaticDefaults() => Main.debuff[Type] = true;
}
