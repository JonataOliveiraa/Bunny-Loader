using System;
using BunnyLoader.Interop;
using CsNpcAi.Content;
using ExampleMod.Content.NPCs;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcAi;

public static class Checks
{
    static int pass, fail, frames, visible = -1, control = -1, states, spriteFrames;
    static bool done, tracing;
    static int trace;
    public static bool EditVanilla, DisableScaling, ObserveOriginal;
    public static int OriginalType, OriginalModType;
    public static ModNPC? OriginalMod;
    static ILog Log => ModContent.GetInstance<CsNpcAiMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = states = spriteFrames = 0;
        visible = control = -1;
        done = false;
        EditVanilla = DisableScaling = ObserveOriginal = tracing = false;
    }

    public static void Add(int value) { if (tracing) trace = trace * 10 + value; }

    public static void Frame(Player player)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60)
            {
                Run(player);
                visible = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 100, (int)player.Center.Y,
                    ModContent.NPCType<ExampleCustomAISlimeNPC>());
                if ((uint)visible < 200u)
                {
                    Main.npc[visible].townNPC = true;
                    Main.npc[visible].damage = 0;
                }
            }
            if ((uint)visible < 200u && Main.npc[visible].active)
            {
                NPC shown = Main.npc[visible];
                states |= 1 << (int)shown.ai[0];
                int height = TextureAssets.Npc[shown.type].Value.Height / Main.npcFrameCount[shown.type];
                spriteFrames |= 1 << (shown.frame.Y / Math.Max(1, height));
            }
            if (frames == 140) Log.Info("csnpcai Flutter Slime visible for screenshot");
            if (frames != 300) return;
            Report("the ported slime runs all five AI states", (states & 31) == 31, $"states=0x{states:X}");
            Report("the ported slime animates multiple sprite frames", (spriteFrames & (spriteFrames - 1)) != 0, $"frames=0x{spriteFrames:X}");
            Report("the controlled example survives csenv", (uint)visible < 200u && Main.npc[visible].active && Main.npc[visible].ModNPC is ExampleCustomAISlimeNPC,
                $"index={visible}");
            done = true;
        }
        catch (Exception e) { Report("run", false, e.ToString()); done = true; }
        if (!done) return;
        if ((uint)visible < 200u) Main.npc[visible].active = false;
        if ((uint)control < 200u) Main.npc[control].active = false;
        Log.Info($"csnpcai END pass={pass} fail={fail}");
    }

    static void Run(Player player)
    {
        var random = Main.rand;
        var replacement = Terraria.Utilities.UnifiedRandom.New(12345);
        using (var randomHandle = new GameHandle(replacement.Pointer))
        {
            bool replaced;
            try
            {
                Main.rand = replacement;
                replaced = Main.rand == replacement;
            }
            finally { Main.rand = random; }
            Report("thread-static random supports reading and writing", !random.IsNull && replaced && Main.rand == random);
        }
        try
        {
            Report("AI style names resolve through the ID dictionary", NPCAIStyleID.Search.GetId("Fighter") == NPCAIStyleID.Fighter &&
                NPCAIStyleID.Search.GetName(NPCAIStyleID.Fighter) == "Fighter");
        }
        catch (Exception e) { Report("AI style names resolve through the ID dictionary", false, e.ToString()); }
        Report("immunity rows cover all registered NPC and buff types", NPCID.Sets.SpecificDebuffImmunity.Length == NPCLoader.NPCCount &&
            Array.TrueForAll(NPCID.Sets.SpecificDebuffImmunity, static row => row != null && row.Length == BuffLoader.BuffCount));
        int probeType = ModContent.NPCType<ProbeNpc>();
        using (var identityPin = New(probeType, out NPC identity))
        {
            identity.type = ModContent.NPCType<PlainNpc>();
            bool replaced = identity.ModNPC is PlainNpc;
            identity.type = probeType;
            Report("lazy binding uses registered identity while Type follows the entity", replaced && identity.ModNPC is ProbeNpc);
        }
        if (!ModContent.TryFind<GlobalNPC>(ModContent.GetInstance<CsNpcAiMod>().Name + "/AGlobalNpc", out _))
        {
            RunNativeOnly();
            return;
        }
        int type = ModContent.NPCType<ProbeNpc>();
        using var pin = New(type, out NPC npc);
        var own = (ProbeNpc)npc.ModNPC!;
        var alpha = npc.GetGlobalNPC<AGlobalNpc>();
        var beta = npc.GetGlobalNPC<BGlobalNpc>();
        npc.aiStyle = -1;
        tracing = true;
        trace = 0;
        npc.AI();
        tracing = false;
        Report("AI follows global pre, mod pre, vanilla, mod AI, global AI and post order", trace == 123456789,
            $"trace={trace}");
        int ownPre = own.PreCalls, ownAI = own.AICalls, alphaAI = alpha.AICalls, betaPre = beta.PreCalls, post = own.PostCalls;
        alpha.VetoPre = true;
        npc.AI();
        Report("a global PreAI veto still visits other globals and all post hooks", own.PreCalls == ownPre && own.AICalls == ownAI && alpha.AICalls == alphaAI &&
            beta.PreCalls == betaPre + 1 && own.PostCalls == post + 1, $"ownPre={own.PreCalls} betaPre={beta.PreCalls} post={own.PostCalls}");
        alpha.VetoPre = false;
        own.VetoPre = true;
        npc.AI();
        Report("a mod PreAI veto skips AI and keeps PostAI", own.AICalls == ownAI && own.PostCalls == post + 2, $"ai={own.AICalls} post={own.PostCalls}");
        own.VetoPre = false;
        ObserveOriginal = true;
        own.AIType = NPCID.BlueSlime;
        npc.AI();
        ObserveOriginal = false;
        Report("AIType is temporary and preserves the bound ModNPC in vanilla code", OriginalType == NPCID.BlueSlime && OriginalModType == NPCID.BlueSlime &&
            ReferenceEquals(OriginalMod, own) && npc.type == type && own.Type == type && ReferenceEquals(npc.ModNPC, own),
            $"inside={OriginalType}/{OriginalModType} restored={npc.type}/{own.Type}");
        AGlobalNpc.StopObserving();
        own.AIType = 0;
        own.AnimationType = NPCID.BlueSlime;
        npc.frameCounter = 7;
        npc.frame.Y = 0;
        npc.aiAction = 0;
        npc.velocity = Vector2.Zero;
        npc.FindFrame();
        int height = TextureAssets.Npc[type].Value.Height / 6;
        Report("AnimationType uses the mod sheet before mod and global FindFrame", own.BeforeFrame == height && own.FrameHeight == height &&
            npc.frame.Y == height * 3 && npc.type == type && own.FrameCalls == 1 && alpha.FrameCalls == 1,
            $"height={height} before={own.BeforeFrame} final={npc.frame.Y} restored={npc.type}");
        own.AnimationType = 0;
        own.Mutable = true;
        npc.buffImmune[BuffID.Poisoned] = false;
        npc.AddBuff(BuffID.Poisoned, 120);
        npc.UpdateNPC_BuffSetFlags(false);
        Report("ResetEffects runs before vanilla buff flags", !own.Mutable && own.ResetCalls == 1 && alpha.ResetCalls == 1 && npc.poisoned,
            $"mod={own.ResetCalls} global={alpha.ResetCalls} poisoned={npc.poisoned}");

        npc.townNPC = false;
        own.VetoActive = true;
        npc.timeLeft = 2;
        npc.CheckActive();
        Report("mod CheckActive veto skips globals and despawn logic", npc.active && npc.timeLeft == 2 && alpha.ActiveCalls == 0,
            $"active={npc.active} time={npc.timeLeft} global={alpha.ActiveCalls}");
        own.VetoActive = false;
        alpha.VetoActive = true;
        npc.CheckActive();
        Report("global CheckActive veto stops later globals and vanilla", npc.active && npc.timeLeft == 2 && alpha.ActiveCalls == 1 && beta.ActiveCalls == 0,
            $"time={npc.timeLeft} globals={alpha.ActiveCalls},{beta.ActiveCalls}");
        alpha.VetoActive = false;
        own.VetoDead = true;
        npc.life = 0;
        npc.checkDead();
        Report("CheckDead evaluates globals after a mod veto and keeps the entity active", npc.active && alpha.DeadCalls == 1 && beta.DeadCalls == 1,
            $"active={npc.active} globals={alpha.DeadCalls},{beta.DeadCalls}");
        own.VetoDead = false;
        alpha.VetoDead = true;
        npc.checkDead();
        Report("a global CheckDead veto still visits the other globals", npc.active && alpha.DeadCalls == 2 && beta.DeadCalls == 2,
            $"globals={alpha.DeadCalls},{beta.DeadCalls}");
        alpha.VetoDead = false;
        npc.life = npc.lifeMax;

        NPC.HitInfo hit = new() { Damage = 7, SourceDamage = 321, Crit = true, HitDirection = -1, Knockback = 2, DamageType = DamageClass.Melee };
        npc.HitEffect(hit);
        Report("HitEffect carries the supplied HitInfo to both sides", own.Hit.Damage == 7 && alpha.Hit.SourceDamage == 321 && own.Hit.Crit &&
            alpha.Hit.HitDirection == -1 && own.Hit.Knockback == 2 && ReferenceEquals(own.Hit.DamageType, DamageClass.Melee),
            $"damage={own.Hit.Damage} source={alpha.Hit.SourceDamage} crit={own.Hit.Crit}");
        npc.HitEffect(1, 9999);
        Report("legacy HitEffect reconstructs instant-kill metadata", own.Hit.InstantKill && own.Hit.Damage == 1 && alpha.Hit.HitDirection == 1,
            $"instant={own.Hit.InstantKill} damage={own.Hit.Damage}");

        IEntitySource source = player.GetSource_FromThis();
        control = NPC.NewNPC(source, (int)player.Center.X + 50, (int)player.Center.Y, type, ai0: 17);
        NPC spawned = Main.npc[control];
        var spawnedOwn = (ProbeNpc)spawned.ModNPC!;
        var spawnedGlobal = spawned.GetGlobalNPC<AGlobalNpc>();
        Report("NewNPC calls OnSpawn once with the original source and initialized AI", spawnedOwn.SpawnCalls == 1 && spawnedGlobal.SpawnCalls == 1 &&
            spawnedOwn.Source.Pointer == source.Pointer && spawnedGlobal.Source.Pointer == source.Pointer && spawnedOwn.SpawnAI == 17,
            $"index={control} own={spawnedOwn.SpawnCalls} global={spawnedGlobal.SpawnCalls} ai={spawnedOwn.SpawnAI}");
        spawnedOwn.RegenLoss = 240;
        spawnedOwn.RegenDamage = 3;
        spawnedGlobal.RegenLoss = 120;
        spawnedGlobal.RegenDamage = 5;
        spawned.lifeRegenCount = 0;
        for (int i = 0; i < 2; i++) { spawned.UpdateNPC_BuffFlagsReset(); spawned.UpdateNPC_BuffApplyDOTs(); }
        Report("UpdateLifeRegen changes actual debuff damage and its batch size", spawned.life == 95 && spawned.lifeRegenCount == -120,
            $"life={spawned.life} count={spawned.lifeRegenCount}");
        spawnedOwn.RegenLoss = -120;
        spawnedOwn.RegenDamage = spawnedGlobal.RegenDamage = -1;
        spawnedGlobal.RegenLoss = 0;
        spawned.life = 99;
        spawned.lifeRegenCount = 0;
        spawned.UpdateNPC_BuffFlagsReset();
        spawned.UpdateNPC_BuffApplyDOTs();
        Report("positive mod regeneration enters vanilla healing", spawned.life == 100 && spawned.lifeRegenCount == 0,
            $"life={spawned.life} count={spawned.lifeRegenCount}");
        spawned.UpdateNPC_BuffFlagsReset();
        spawned.poisoned = true;
        spawned.UpdateNPC_BuffApplyDOTs();
        Report("vanilla DOT blocks positive regeneration after the mod hook", spawned.lifeRegen < 0 && spawned.lifeRegenCount < 0,
            $"regen={spawned.lifeRegen} count={spawned.lifeRegenCount}");
        spawned.life = 100;
        spawned.lifeRegenCount = 0;
        spawnedOwn.RegenLoss = 3600;
        spawnedOwn.RegenDamage = 17;
        spawned.UpdateNPC_BuffFlagsReset();
        spawned.onFire3 = true;
        spawned.UpdateNPC_BuffApplyDOTs();
        Report("ref damage remains effective alongside vanilla DOT", spawned.life == 83 && spawnedOwn.SeenRegenDamage == 5 &&
            spawned.lifeRegenCount < -120, $"life={spawned.life} initialDamage={spawnedOwn.SeenRegenDamage} count={spawned.lifeRegenCount}");
        using var nestedPin = New(type, out NPC nested);
        var nestedOwn = (ProbeNpc)nested.ModNPC!;
        nestedOwn.RegenLoss = 1;
        spawnedOwn.Nested = nested;
        spawnedOwn.RegenLoss = 2;
        spawnedOwn.RegenDamage = -1;
        spawned.lifeRegenCount = 0;
        spawned.UpdateNPC_BuffFlagsReset();
        spawned.UpdateNPC_BuffApplyDOTs();
        Report("nested regeneration keeps separate native contexts", spawned.lifeRegenCount == -2 && nested.lifeRegenCount == -1,
            $"outer={spawned.lifeRegenCount} inner={nested.lifeRegenCount}");
        spawned.active = false;
        control = -1;

        NPCSpawnParams master = new() { difficultyOverride = GameDifficultyLevel.Master, playerCountForMultiplayerDifficultyOverride = 3 };
        DisableScaling = true;
        using var baselinePin = New(ModContent.NPCType<ScaleNpc>(), out NPC baseline, master);
        DisableScaling = false;
        using var scaledPin = New(ModContent.NPCType<ScaleNpc>(), out NPC scaled, master);
        var scaleOwn = (ScaleNpc)scaled.ModNPC!;
        NPC.GetStatScalingFactors(3, out float balance, out _);
        int expectedLife = (int)Math.Round(610d * baseline.lifeMax / 300d);
        Report("difficulty hooks run before the final player-count multiplier", scaleOwn.Calls == 1 && scaleOwn.SeenLife == 300 &&
            scaleOwn.GlobalSeenLife == 600 && scaled.lifeMax == expectedLife && scaled.defLifeMax == expectedLife,
            $"before={scaleOwn.SeenLife}/{scaleOwn.GlobalSeenLife} life={scaled.lifeMax}/{expectedLife} baseline={baseline.lifeMax}");
        Report("difficulty hooks receive the local tML scaling contract", scaleOwn.Players == 3 && Math.Abs(scaleOwn.Balance - balance) < 0.0001f &&
            scaleOwn.BossAdjustment == 1f, $"players={scaleOwn.Players} balance={scaleOwn.Balance}/{balance} boss={scaleOwn.BossAdjustment}");
        using var classicPin = New(ModContent.NPCType<ScaleNpc>(), out NPC classic);
        Report("Classic defaults do not invoke expert scaling hooks", ((ScaleNpc)classic.ModNPC!).Calls == 0 && classic.lifeMax == 100,
            $"calls={((ScaleNpc)classic.ModNPC!).Calls} life={classic.lifeMax}");

        EditVanilla = true;
        using var vanillaPin = New(NPCID.Zombie, out NPC vanilla, new NPCSpawnParams { difficultyOverride = GameDifficultyLevel.Master, sizeScaleOverride = 1.5f });
        EditVanilla = false;
        Report("vanilla global defaults run before dimensions and difficulty scaling", vanilla.width == 45 && vanilla.height == 60 &&
            vanilla.lifeMax == 300 && vanilla.defLifeMax == 300 && vanilla.damage == 150 && !vanilla.buffImmune[BuffID.Poisoned] && vanilla.buffImmune[BuffID.OnFire],
            $"size={vanilla.width}x{vanilla.height} life={vanilla.lifeMax}/{vanilla.defLifeMax} damage={vanilla.damage} poison={vanilla.buffImmune[BuffID.Poisoned]} fire={vanilla.buffImmune[BuffID.OnFire]}");
        int defaults = AGlobalNpc.DefaultsCalls;
        using var variantPin = New(NPCID.SmallZombie, out NPC variant);
        Report("negative variants keep one defaults instance and expose the final net ID", variant.netID == NPCID.SmallZombie &&
            variant.GetGlobalNPC<AGlobalNpc>().VariantNetId == NPCID.SmallZombie && AGlobalNpc.DefaultsCalls == defaults + 1,
            $"netId={variant.netID} seen={variant.GetGlobalNPC<AGlobalNpc>().VariantNetId} defaults={AGlobalNpc.DefaultsCalls - defaults}");
        Immunity(type);

        npc.aiStyle = -1;
        own.VetoActive = true;
        for (int i = 0; i < 30; i++) { npc.AI(); npc.FindFrame(); npc.UpdateNPC_BuffSetFlags(false); npc.CheckActive(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { npc.AI(); npc.FindFrame(); npc.UpdateNPC_BuffSetFlags(false); npc.CheckActive(); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Report("warm AI, frame, reset and active callbacks allocate no managed memory", allocated == 0, $"bytes={allocated} iterations=100");
        using var plainPin = New(ModContent.NPCType<PlainNpc>(), out NPC plain);
        plain.AI();
        Report("an NPC with no lifecycle overrides or applicable globals keeps the vanilla path", plain.active && plain.EntityGlobals.Count == 0 && plain.ModNPC is PlainNpc,
            $"active={plain.active} globals={plain.EntityGlobals.Count}");
    }

    static void RunNativeOnly()
    {
        int plainType = ModContent.NPCType<PlainNpc>();
        using var pin = New(NPCID.Bunny, out NPC lazy);
        lazy.type = plainType;
        lazy.aiStyle = -1;
        int constructors = PlainNpc.Constructed;
        lazy.AI();
        lazy.FindFrame();
        Report("native type filters bypass unmarked AI and frame entries", PlainNpc.Constructed == constructors,
            $"constructors={constructors}/{PlainNpc.Constructed}");
        var plain = (PlainNpc)lazy.ModNPC!;
        Report("filtered calls leave the mod instance lazy until explicitly requested", PlainNpc.Constructed == constructors + 1 && plain.NPC == lazy,
            $"constructors={PlainNpc.Constructed}");
        using var probePin = New(ModContent.NPCType<ProbeNpc>(), out NPC npc);
        var own = (ProbeNpc)npc.ModNPC!;
        npc.aiStyle = -1;
        own.VetoPre = true;
        tracing = true;
        trace = 0;
        npc.AI();
        int vetoTrace = trace;
        own.VetoPre = false;
        trace = 0;
        npc.AI();
        tracing = false;
        Report("mod lifecycle order works with all globals disabled", vetoTrace == 37 && trace == 347,
            $"veto={vetoTrace} allowed={trace}");
        npc.townNPC = false;
        own.VetoActive = true;
        for (int i = 0; i < 30; i++) { npc.AI(); npc.FindFrame(); npc.UpdateNPC_BuffSetFlags(false); npc.CheckActive(); lazy.AI(); lazy.FindFrame(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { npc.AI(); npc.FindFrame(); npc.UpdateNPC_BuffSetFlags(false); npc.CheckActive(); lazy.AI(); lazy.FindFrame(); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Report("marked and filtered native paths allocate no managed memory", allocated == 0, $"bytes={allocated} iterations=100");

        plain.AIType = NPCID.BlueSlime;
        using var aiPin = New(NPCID.Bunny, out NPC aiLazy);
        aiLazy.type = plainType;
        aiLazy.aiStyle = -1;
        constructors = PlainNpc.Constructed;
        aiLazy.AI();
        Report("AIType enables a previously unmarked type", PlainNpc.Constructed == constructors + 1 && aiLazy.type == plainType,
            $"constructors={constructors}/{PlainNpc.Constructed}");
        plain.AnimationType = NPCID.BlueSlime;
        using var framePin = New(NPCID.Bunny, out NPC frameLazy);
        frameLazy.type = plainType;
        constructors = PlainNpc.Constructed;
        frameLazy.FindFrame();
        Report("AnimationType enables a previously unmarked type", PlainNpc.Constructed == constructors + 1 && frameLazy.type == plainType,
            $"constructors={constructors}/{PlainNpc.Constructed}");
    }

    static void Immunity(int type)
    {
        int custom = ModContent.BuffType<ProbeBuff>();
        bool?[] saved = (bool?[])NPCID.Sets.SpecificDebuffImmunity[type].Clone();
        try
        {
            NPCID.Sets.SpecificDebuffImmunity[type][BuffID.OnFire] = true;
            using var pin = New(type, out NPC npc);
            Report("immunity sets include registered mod buffs and inherited variants", npc.buffImmune.Length == BuffLoader.BuffCount &&
                NPCID.Sets.SpecificDebuffImmunity.Length == NPCLoader.NPCCount && npc.buffImmune[BuffID.OnFire3] && npc.buffImmune[custom],
                $"buffs={npc.buffImmune.Length} custom={custom} inherited={npc.buffImmune[custom]}");
            NPCID.Sets.SpecificDebuffImmunity[type][custom] = false;
            npc.SetDefaults(type);
            Report("a specific false overrides inherited immunity", !npc.buffImmune[custom] && npc.buffImmune[BuffID.OnFire3],
                $"custom={npc.buffImmune[custom]} variant={npc.buffImmune[BuffID.OnFire3]}");
            npc.buffImmune[BuffID.OnFire] = false;
            npc.AddBuff(BuffID.OnFire, 120);
            npc.AddBuff(custom, 120);
            npc.BecomeImmuneTo(BuffID.OnFire);
            npc.ClearImmuneToBuffs(out bool cleared);
            Report("BecomeImmuneTo and ClearImmuneToBuffs update active buffs and inherited immunity", cleared && npc.FindBuffIndex(BuffID.OnFire) < 0 &&
                npc.FindBuffIndex(custom) < 0 && npc.buffImmune[custom], $"cleared={cleared} custom={npc.buffImmune[custom]}");
            NPCID.Sets.ImmuneToRegularBuffs[type] = true;
            NPCID.Sets.SpecificDebuffImmunity[type][BuffID.OnFire] = false;
            NPCID.Sets.SpecificDebuffImmunity[type][custom] = null;
            npc.SetDefaults(type);
            Report("regular immunity preserves tags and inherits explicit vulnerabilities", npc.buffImmune[BuffID.Ichor] && !npc.buffImmune[310] &&
                !npc.buffImmune[BuffID.OnFire] && !npc.buffImmune[BuffID.OnFire3] && !npc.buffImmune[custom],
                $"tag={npc.buffImmune[310]} fire={npc.buffImmune[BuffID.OnFire]} variant={npc.buffImmune[BuffID.OnFire3]}");
            NPCID.Sets.SpecificDebuffImmunity[type][BuffID.OnFire3] = true;
            NPCID.Sets.ImmuneToWhipTags[type] = true;
            npc.SetDefaults(type);
            Report("regular and tag masks combine while specific true has final priority", npc.buffImmune[BuffID.Ichor] && npc.buffImmune[310] &&
                npc.buffImmune[BuffID.OnFire3] && !npc.buffImmune[BuffID.OnFire],
                $"tag={npc.buffImmune[310]} ichor={npc.buffImmune[BuffID.Ichor]} variant={npc.buffImmune[BuffID.OnFire3]}");
        }
        finally
        {
            NPCID.Sets.ImmuneToRegularBuffs[type] = NPCID.Sets.ImmuneToWhipTags[type] = false;
            NPCID.Sets.SpecificDebuffImmunity[type] = saved;
        }
    }

    static GameHandle New(int type, out NPC npc, NPCSpawnParams parameters = default)
    {
        npc = NPC.New();
        var pin = new GameHandle(npc.Pointer);
        npc.SetDefaults(type, parameters);
        return pin;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++; else fail++;
        Log.Info($"csnpcai {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
