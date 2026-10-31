using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using CsNpcHit.Content;
using ExampleMod.Common.GlobalNPCs;
using ExampleMod.Content.Buffs;
using ExampleMod.Content.NPCs;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcHit;

public static class Checks
{
    static int pass, fail, frames;
    static bool done, tracing;
    static readonly List<string> trace = new();
    public static bool Arithmetic, Nested, PlayerVeto;
    public static bool? PlayerDecision, ItemDecision, PlayerCollision, ItemCollision;
    public static int SourceHits, RawPackets, RawDamage, RawCrit, LastGlobalDone;
    public static NPC.HitInfo LastGlobalHit;
    static ILog Log => ModContent.GetInstance<CsNpcHitMod>().Logger;

    public static void Reset() { pass = fail = frames = 0; done = false; Options(); }
    static void Options()
    {
        Arithmetic = Nested = PlayerVeto = tracing = false;
        PlayerDecision = ItemDecision = PlayerCollision = ItemCollision = null;
        SourceHits = RawPackets = RawDamage = RawCrit = LastGlobalDone = 0;
        trace.Clear();
    }
    public static void Mark(string stage) { if (tracing) trace.Add(stage); }

    public static void Frame(Player player)
    {
        if (done || ++frames != 120) return;
        done = true;
        try { Run(player); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally { Options(); }
        Log.Info($"csnpchit END pass={pass} fail={fail}");
    }

    sealed class Target : IDisposable
    {
        public readonly int Index;
        public NPC NPC;
        public ProbeNpc? Own => NPC.ModNPC as ProbeNpc;
        public AGlobalNpc A => NPC.GetGlobalNPC<AGlobalNpc>();
        public BGlobalNpc B => NPC.GetGlobalNPC<BGlobalNpc>();
        public Target(Player player, int type = -1)
        {
            if (type < 0) type = ModContent.NPCType<ProbeNpc>();
            Index = Terraria.NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 40, (int)player.Center.Y, type);
            if ((uint)Index >= 200u) throw new InvalidOperationException("No NPC slot for hit test");
            NPC = Main.npc[Index];
            NPC.life = NPC.lifeMax = 100000;
            NPC.defense = 10;
            NPC.townNPC = true;
            NPC.aiStyle = 0;
            NPC.damage = 0;
            NPC.noTileCollide = NPC.noGravity = true;
            NPC.position = new Vector2(player.position.X + player.width + 8, player.position.Y + player.height - NPC.height);
        }
        public void Dispose() => NPC.active = false;
    }

    static void Hit(Player player, Item item, Target target, int damage = 40, Rectangle? rectangle = null)
    {
        target.NPC.immune[player.whoAmI] = 0;
        player.direction = 1;
        player.itemAnimation = player.itemAnimationMax = 20;
        player.ProcessHitAgainstNPC(item, rectangle ?? target.NPC.Hitbox, damage, 4, target.Index);
    }

    static void Run(Player player)
    {
        bool full = ModContent.TryFind<ModItem>(ModContent.GetInstance<CsNpcHitMod>().Name + "/HitItem", out _);
        Item item = Item.New();
        using var itemPin = new GameHandle(item.Pointer);
        item.SetDefaults(full ? ModContent.ItemType<HitItem>() : ItemID.CopperShortsword, default);
        item.crit = -10000;
        Decisions(player, item);
        Options();
        using (var target = new Target(player))
        {
            tracing = true;
            Hit(player, item, target);
            tracing = false;
            string actual = string.Join("|", trace);
            string expected = full
                ? "PCan|PCanItem|ICanG|ICan|NCanA|NCanB|NCan|PCollide|ICollideG|ICollide|NCollideA|NCollideB|NCollide|IncomingN|IncomingA|IncomingB|ModifyI|ModifyIG|ModifyN|ModifyA|ModifyB|ModifyP|ModifyPI|EffectN|OnI|OnIG|OnN|OnA|OnB|OnP|OnPI"
                : "NCanA|NCanB|NCan|NCollideA|NCollideB|NCollide|IncomingN|IncomingA|IncomingB|ModifyN|ModifyA|ModifyB|EffectN|OnN|OnA|OnB";
            Report("callbacks follow the local CombinedHooks order", actual == expected, actual);
            ProbeNpc own = target.Own!;
            Report("one accepted hit has one incoming, item and post callback", own.IncomingCalls == 1 && own.ModifyCalls == 1 && own.HitCalls == 1 &&
                target.A.Hits == 1 && target.B.Hits == 1 && (!full || SourceHits == 1), $"incoming={own.IncomingCalls} modify={own.ModifyCalls} on={own.HitCalls}");
            Report("post callbacks identify the attacker and weapon", own.Attacker == player && own.Weapon == item && own.Hit.DamageType == DamageClass.Melee);
            Report("hit effects receive the full item hit metadata", own.EffectCalls == 1 && own.Effect.DamageType == DamageClass.Melee &&
                own.Effect.SourceDamage == own.Hit.SourceDamage && own.Effect.Damage == own.Hit.Damage && own.Effect.Knockback == own.Hit.Knockback);
            int expectedRaw = (int)Math.Ceiling(own.Hit.Damage + target.NPC.defense / 2d);
            Report("message 28 retains raw pre-defense damage", RawPackets == 1 && RawDamage == expectedRaw && RawCrit == 0,
                $"packets={RawPackets} raw={RawDamage}/{expectedRaw} final={own.Hit.Damage} crit={RawCrit}");
        }
        if (full) SourcePriority(player, item);
        Damage(player, item, full);
        Examples(player, item);
        Options();
        using (var target = new Target(player, NPCID.BlueSlime))
        {
            int before = target.NPC.life;
            Hit(player, item, target);
            Report("a vanilla target receives the NPC global callbacks", target.NPC.ModNPC == null && target.A.Hits == 1 && target.B.Hits == 1 &&
                before - target.NPC.life == LastGlobalDone && LastGlobalDone > 0, $"on={target.A.Hits}/{target.B.Hits} damage={LastGlobalDone}");
        }
        Options();
        using (var target = new Target(player))
        {
            var far = new Rectangle(target.NPC.Hitbox.X - 500, target.NPC.Hitbox.Y - 500, 4, 4);
            for (int i = 0; i < 30; i++) Hit(player, item, target, rectangle: far);
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Hit(player, item, target, rectangle: far);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
            Report("warm candidate checks allocate no managed memory", bytes == 0 && target.Own!.HitCalls == 0, $"bytes={bytes} iterations=100");
            for (int i = 0; i < 30; i++) Hit(player, item, target);
            start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Hit(player, item, target);
            bytes = GC.GetAllocatedBytesForCurrentThread() - start;
            Report("warm accepted hit callbacks allocate no managed memory", bytes == 0 && target.Own!.HitCalls == 130,
                $"bytes={bytes} hits={target.Own!.HitCalls}");
        }
    }

    static void Decisions(Player player, Item item)
    {
        Options();
        using (var target = new Target(player))
        {
            target.A.Decision = true;
            target.B.Decision = false;
            tracing = true;
            bool? result = NPCLoader.CanBeHitByItem(target.NPC, player, item);
            tracing = false;
            Report("an NPC global veto stops later NPC checks", result == false && target.Own!.CanCalls == 0 && string.Join("|", trace) == "NCanA|NCanB");
            target.B.Decision = null;
            target.Own!.Decision = false;
            Report("a mod NPC veto wins over its globals", NPCLoader.CanBeHitByItem(target.NPC, player, item) == false);
            target.A.Decision = target.Own.Decision = null;
            Report("null preserves the vanilla decision", NPCLoader.CanBeHitByItem(target.NPC, player, item) == null);
            target.Own.Decision = false;
            int life = target.NPC.life;
            Hit(player, item, target);
            Report("NPC CanBeHitByItem false prevents the real strike", target.NPC.life == life && target.Own.HitCalls == 0);
        }
        using (var target = new Target(player))
        {
            target.NPC.friendly = true;
            int life = target.NPC.life;
            Hit(player, item, target);
            bool skipped = target.NPC.life == life;
            target.Own!.Decision = true;
            Hit(player, item, target);
            Report("true permits a friendly target and restores its flag", skipped && target.NPC.life < life && target.NPC.friendly && target.Own.HitCalls == 1);
        }
        using (var target = new Target(player))
        {
            var far = new Rectangle(target.NPC.Hitbox.X - 500, target.NPC.Hitbox.Y - 500, 4, 4);
            int life = target.NPC.life;
            Hit(player, item, target, rectangle: far);
            bool skipped = target.NPC.life == life;
            target.Own!.Collision = true;
            Hit(player, item, target, rectangle: far);
            Report("NPC collision true permits an out-of-reach attack", skipped && target.NPC.life < life && target.Own.HitCalls == 1);
            target.A.Collision = false;
            life = target.NPC.life;
            int ownCalls = target.Own.CollisionCalls;
            Hit(player, item, target);
            Report("NPC global collision false prevents the strike and later checks", target.NPC.life == life && target.Own.CollisionCalls == ownCalls);
        }
    }

    static void SourcePriority(Player player, Item item)
    {
        Options();
        using (var target = new Target(player))
        {
            PlayerVeto = true;
            Hit(player, item, target);
            Report("the player generic veto stops item and NPC decisions", target.Own!.CanCalls == 0 && target.Own.HitCalls == 0);
        }
        Options();
        using (var target = new Target(player))
        {
            PlayerDecision = true;
            ItemDecision = false;
            target.Own!.Decision = false;
            Hit(player, item, target);
            Report("the first player true has CombinedHooks priority", target.Own.HitCalls == 1 && target.Own.CanCalls == 1);
        }
        Options();
        using (var target = new Target(player))
        {
            ItemDecision = true;
            target.Own!.Decision = false;
            Hit(player, item, target);
            Report("an item true has priority over the NPC source", target.Own.HitCalls == 1 && target.Own.CanCalls == 1);
        }
        Options();
        using (var target = new Target(player))
        {
            ItemDecision = false;
            target.Own!.Decision = true;
            Hit(player, item, target);
            Report("an earlier item false skips the NPC source", target.Own.CanCalls == 0 && target.Own.HitCalls == 0);
        }
        Options();
        using (var target = new Target(player))
        {
            PlayerCollision = true;
            ItemCollision = target.Own!.Collision = false;
            var far = new Rectangle(target.NPC.Hitbox.X - 500, target.NPC.Hitbox.Y - 500, 4, 4);
            Hit(player, item, target, rectangle: far);
            Report("collision sources retain first-non-null priority", target.Own.HitCalls == 1 && target.Own.CollisionCalls == 1);
        }
        Options();
    }

    static void Damage(Player player, Item item, bool full)
    {
        Options();
        if (full)
        {
            using var target = new Target(player);
            Arithmetic = true;
            target.Own!.DefenseScale = 0.5f;
            target.Own.ArmorPenetration = 3;
            Hit(player, item, target);
            int expected = (int)((target.Own.Hit.SourceDamage - 1) * 0.5f + 1);
            Report("defense, penetration and source/final modifiers compose in order", target.Own.Hit.Damage == expected && target.Own.Done == expected,
                $"source={target.Own.Hit.SourceDamage} damage={target.Own.Hit.Damage}/{expected}");
        }
        Options();
        using (var target = new Target(player))
        {
            target.Own!.DisableCrit = false;
            target.Own.Crit = true;
            target.Own.Limit = 40;
            Hit(player, item, target, 1000);
            Report("a critical cap is applied once and retains critical metadata", target.Own.Done == 40 && target.Own.Hit.Crit &&
                target.Own.Effect.Crit && RawCrit == 1 && target.Own.HitCalls == 1, $"done={target.Own.Done} crit={target.Own.Hit.Crit} raw={RawDamage}");
        }
        using (var target = new Target(player))
        {
            target.Own!.Limit = 1;
            Hit(player, item, target, 1000);
            Report("a one-point non-critical cap is exact", target.Own.Done == 1 && target.Own.Hit.Damage == 1);
        }
        using (var target = new Target(player))
        {
            target.NPC.life = 5;
            target.Own!.Limit = 80;
            Hit(player, item, target, 1000);
            Report("overkill separates hit damage from health lost", target.Own.Done == 5 && target.Own.Hit.Damage == 80 && target.A.Hits == 1,
                $"hit={target.Own.Hit.Damage} done={target.Own.Done} life={target.NPC.life}");
        }
        using (var target = new Target(player))
        {
            target.NPC.immortal = true;
            Hit(player, item, target);
            Report("immortal targets still receive one post hook with zero health loss", target.Own!.Done == 0 && target.Own.HitCalls == 1 && target.NPC.life == 100000);
        }
        using (var root = new Target(player))
        using (var segment = new Target(player))
        {
            root.NPC.life = 7;
            segment.NPC.realLife = root.Index;
            segment.Own!.Limit = 80;
            Hit(player, item, segment, 1000);
            Report("segmented targets limit health lost to their real-life entity", segment.Own.Done == 7 && segment.Own.Hit.Damage == 80 && root.NPC.life <= 0,
                $"hit={segment.Own.Hit.Damage} done={segment.Own.Done} root={root.NPC.life}");
        }
        using (var target = new Target(player))
        {
            target.NPC.life = 250;
            target.Own!.Instant = true;
            Hit(player, item, target);
            Report("instant kill retains its sentinel and reports the target health lost", target.Own.Hit.InstantKill && target.Own.Hit.Damage == 1 &&
                target.Own.Effect.InstantKill && target.Own.Done == 250, $"hit={target.Own.Hit.Damage} done={target.Own.Done}");
        }
        Options();
        using (var target = new Target(player))
        {
            Nested = true;
            Hit(player, item, target);
            Report("a raw nested strike is not attributed to the item", target.Own!.HitCalls == 1 && target.Own.ModifyCalls == 1 &&
                target.Own.EffectCalls == 2 && target.Own.Effect.DamageType == DamageClass.Default && RawPackets == 2,
                $"on={target.Own.HitCalls} effects={target.Own.EffectCalls} packets={RawPackets}");
        }
        Options();
        foreach (var text in Main.combatText.AsSpan()) text.active = false;
        using (var target = new Target(player))
        {
            target.Own!.Hide = true;
            Hit(player, item, target);
            int count = CombatTexts();
            Report("HideCombatText suppresses the native damage number", count == 0 && target.Own.Hit.HideCombatText && target.Own.HitCalls == 1, $"texts={count}");
        }
        using (var target = new Target(player))
        {
            Hit(player, item, target);
            Report("combat text suppression is scoped to the strike", CombatTexts() > 0 && !target.Own!.Hit.HideCombatText);
        }
        using (var target = new Target(player))
        {
            target.Own!.DisableCrit = false;
            target.Own.Crit = true;
            target.Own.Limit = 3;
            Hit(player, item, target, 1000);
            Log.Info($"csnpchit LIMITATION raw critical quantization cap=3 actual={target.Own.Done} defense={target.NPC.defense}");
        }
    }

    static int CombatTexts()
    {
        int count = 0;
        foreach (var text in Main.combatText.AsSpan()) if (text.active) count++;
        return count;
    }

    static void Examples(Player player, Item item)
    {
        Options();
        using (var target = new Target(player))
        {
            int type = ModContent.BuffType<ExampleDefenseDebuff>();
            target.NPC.AddBuff(type, 120);
            target.NPC.UpdateNPC_BuffFlagsReset();
            target.NPC.UpdateNPC_BuffSetFlags(false);
            var global = target.NPC.GetGlobalNPC<DamageModificationGlobalNPC>();
            bool applied = global.exampleDefenseDebuff;
            Hit(player, item, target);
            int expected = Math.Max((int)(target.Own!.Hit.SourceDamage - 10 * ExampleDefenseDebuff.DefenseMultiplier / 2), 1);
            Report("the ported debuff reduces NPC defense in real item hits", applied && target.Own.Done == expected,
                $"applied={applied} damage={target.Own.Done}/{expected}");
            NPCLoader.ResetEffects(target.NPC);
            Report("the defense debuff state resets before buff updates", !global.exampleDefenseDebuff);
        }
        using (var target = new Target(player, NPCID.IchorSticker))
            Report("the example debuff inherits Ichor immunity", target.NPC.buffImmune[ModContent.BuffType<ExampleDefenseDebuff>()]);
        using (var target = new Target(player, ModContent.NPCType<PartyZombie>()))
        {
            var originalRandom = Main.rand;
            using var originalRandomPin = new GameHandle(originalRandom.Pointer);
            try
            {
                var seeded = Terraria.Utilities.UnifiedRandom.New(31415);
                using var randomPin = new GameHandle(seeded.Pointer);
                Main.rand = seeded;
                item.DamageType = DamageClass.Melee;
                Hit(player, item, target, 400);
                int ordinary = 100000 - target.NPC.life;
                target.NPC.life = 100000;
                seeded = Terraria.Utilities.UnifiedRandom.New(31415);
                using var magicRandomPin = new GameHandle(seeded.Pointer);
                Main.rand = seeded;
                item.DamageType = DamageClass.Magic;
                Hit(player, item, target, 400);
                int magic = 100000 - target.NPC.life;
                Report("PartyZombie reduces magic item damage by 75 percent", ordinary > 0 && magic == (int)(ordinary * 0.25f), $"ordinary={ordinary} magic={magic}");
            }
            finally { Main.rand = originalRandom; item.DamageType = DamageClass.Melee; }
        }
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++; else fail++;
        Log.Info($"csnpchit {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
