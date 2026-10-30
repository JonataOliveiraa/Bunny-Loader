using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using CsNpc.Content;
using ExampleMod.Content.NPCs;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpc;

public static class Checks
{
    static int pass, fail, frames, visible = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsNpcMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        visible = -1;
        done = false;
    }

    public static void Frame(Player player)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60)
            {
                Run(player);
                visible = NPC.NewNPC(default, (int)player.Center.X + 100, (int)player.Center.Y, ModContent.NPCType<PartyZombie>());
                if ((uint)visible < 200u)
                {
                    NPC shown = Main.npc[visible];
                    shown.friendly = true;
                    shown.aiStyle = -1;
                    shown.velocity = Vector2.Zero;
                    shown.noGravity = true;
                    shown.noTileCollide = true;
                }
                Log.Info("csnpc PartyZombie visible for screenshot");
            }
            if (frames != 240) return;
            Report("the ExampleMod NPC stays active and keeps its frame", (uint)visible < 200u && Main.npc[visible].active && Main.npc[visible].frame.Height > 0,
                $"index={visible}");
            if ((uint)visible < 200u) Main.npc[visible].active = false;
            done = true;
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csnpc END pass={pass} fail={fail}");
    }

    static void Run(Player player)
    {
        int type = ModContent.NPCType<TestNpc>();
        TestNpc template = ModContent.GetInstance<TestNpc>();
        Report("registered NPC types and lookups agree before and after content setup", type >= NPCID.Count && CsNpcMod.CountDuringLoad == NPCLoader.NPCCount &&
            NPCLoader.GetNPC(type) == template && ModContent.GetModNPC(type) == template && ModContent.Find<ModNPC>("CsNpc/TestNpc") == template &&
            NPCLoader.GetNPC(NPCID.Bunny) == null && NPCLoader.GetNPC(-1) == null && NPCLoader.GetNPC(int.MaxValue) == null,
            $"type={type} count={NPCLoader.NPCCount} loadCount={CsNpcMod.CountDuringLoad}");
        Report("static setup has a bound template and grown game tables", CsNpcMod.ContentReady && TestNpc.TemplateBound && TestNpc.StaticTablesReady &&
            TestNpc.StaticCalls == 1 && SharedGlobalNpc.StaticCalls == 1 && ReferenceEquals(template.NPC.ModNPC, template),
            $"bound={TestNpc.TemplateBound} tables={TestNpc.StaticTablesReady} static={TestNpc.StaticCalls} globalStatic={SharedGlobalNpc.StaticCalls}");
        string name = Lang.GetNPCNameValue(type) ?? "";
        var texture = TextureAssets.Npc[type].Value;
        Report("localized names, search and texture reach the game", (name == "Test NPC" || name == "NPC de Teste") && NPCID.Search.GetId("CsNpc/TestNpc") == type &&
            texture.Width > 0 && texture.Height > 0 && template.NPC.frame.Height == texture.Height / Main.npcFrameCount[type],
            $"name='{name}' texture={texture.Width}x{texture.Height} frame={template.NPC.frame.Height}");

        using var firstPin = New(type, out NPC first);
        using var secondPin = New(type, out NPC second);
        var a = (TestNpc)first.ModNPC!;
        var b = (TestNpc)second.ModNPC!;
        Report("SetDefaults creates independent constructor instances", first.active && first.life == 120 && first.defLifeMax == 120 && first.damage == 43 &&
            first.defDamage == 43 && first.defense == 7 && a.Value == 4 && b.Value == 4 && template.Value == 99 && !ReferenceEquals(a, b) &&
            ReferenceEquals(a, first.ModNPC) && a.NPC == first,
            $"life={first.life}/{first.defLifeMax} damage={first.damage}/{first.defDamage} defense={first.defense} values={a.Value},{b.Value},{template.Value}");
        EarlyGlobalNpc early = first.GetGlobalNPC<EarlyGlobalNpc>();
        LateGlobalNpc late = first.GetGlobalNPC<LateGlobalNpc>();
        Report("early defaults run before late AppliesToEntity", early.DefaultsCalls == 1 && early.SawModDefaults && late.SeenDamage == 43,
            $"early={early.DefaultsCalls} sawMod={early.SawModDefaults} lateDamage={late.SeenDamage}");
        early.Value = 31;
        a.Value = 32;
        Report("per-entity globals are isolated and shared globals keep their template", second.GetGlobalNPC<EarlyGlobalNpc>().Value == 9 && b.Value == 4 &&
            ReferenceEquals(first.GetGlobalNPC<SharedGlobalNpc>(), second.GetGlobalNPC<SharedGlobalNpc>()) &&
            ReferenceEquals(first.GetGlobalNPC<SharedGlobalNpc>(), ModContent.GetInstance<SharedGlobalNpc>()) && first.EntityGlobals.Count == 3 &&
            ReferenceEquals(first.GetGlobalNPC(early), early) && first.TryGetGlobalNPC(early, out EarlyGlobalNpc found) && ReferenceEquals(found, early),
            $"globals={first.EntityGlobals.Count} otherValue={second.GetGlobalNPC<EarlyGlobalNpc>().Value}");

        a.Values.Add(32);
        NPC copy = new(first.Clone().Pointer);
        using var copyPin = new GameHandle(copy.Pointer);
        var copyMod = (TestNpc)copy.ModNPC!;
        copyMod.Values.Add(33);
        copy.GetGlobalNPC<EarlyGlobalNpc>().Value++;
        Report("NPC.Clone clones mod state and per-entity globals", copy != first && copyMod.Value == 32 && copyMod.NPC == copy &&
            !ReferenceEquals(copyMod, a) && a.Values.Count == 2 && copyMod.Values.Count == 3 && early.Value == 31 &&
            copy.GetGlobalNPC<EarlyGlobalNpc>().Value == 32 && ReferenceEquals(copy.GetGlobalNPC<SharedGlobalNpc>(), first.GetGlobalNPC<SharedGlobalNpc>()),
            $"values={a.Values.Count},{copyMod.Values.Count} globals={early.Value},{copy.GetGlobalNPC<EarlyGlobalNpc>().Value}");

        first.SetDefaults(type);
        Report("reusing the same NPC resets mod and global instances", first.ModNPC is TestNpc fresh && fresh.Value == 4 && !ReferenceEquals(fresh, a) &&
            first.GetGlobalNPC<EarlyGlobalNpc>().Value == 9 && !ReferenceEquals(first.GetGlobalNPC<EarlyGlobalNpc>(), early),
            $"value={((TestNpc)first.ModNPC!).Value} global={first.GetGlobalNPC<EarlyGlobalNpc>().Value}");
        first.SetDefaults(NPCID.Bunny);
        bool absentThrows = false;
        try { first.GetGlobalNPC<EarlyGlobalNpc>(); }
        catch (KeyNotFoundException) { absentThrows = true; }
        Report("vanilla defaults clear the mod and conditional globals", first.ModNPC == null && !first.TryGetGlobalNPC(out EarlyGlobalNpc _) && absentThrows &&
            first.TryGetGlobalNPC(out SharedGlobalNpc _) && first.EntityGlobals.Count == 1,
            $"modNull={first.ModNPC == null} globals={first.EntityGlobals.Count} missingThrows={absentThrows}");

        using var clonedPin = New(ModContent.NPCType<ClonedNpc>(), out NPC cloned);
        var clonedMod = (ClonedNpc)cloned.ModNPC!;
        Report("CloneNewInstances copies template state through Clone", clonedMod.Value == 67 && clonedMod.Values.Count == 2 &&
            !ReferenceEquals(clonedMod.Values, ModContent.GetInstance<ClonedNpc>().Values),
            $"value={clonedMod.Value} listCount={clonedMod.Values.Count}");

        using var copiedPin = New(ModContent.NPCType<CopiedNpc>(), out NPC copied);
        var copiedMod = (CopiedNpc)copied.ModNPC!;
        Report("CloneDefaults inside SetDefaults copies mod defaults and restores identity", copied.type == copiedMod.Type && copied.life == 130 && copied.damage == 43 &&
            copiedMod.KeptInstance && copiedMod.KeptInteraction && !copied.TryGetGlobalNPC(out EarlyGlobalNpc _),
            $"type={copied.type} life={copied.life} damage={copied.damage} instance={copiedMod.KeptInstance} interaction={copiedMod.KeptInteraction}");

        second.playerInteraction[7] = true;
        var savedMod = second.ModNPC;
        var savedGlobal = second.GetGlobalNPC<EarlyGlobalNpc>();
        second.CloneDefaults(NPCID.Zombie);
        Report("CloneDefaults preserves type, globals and interaction outside defaults", second.type == type && second.netID == type &&
            ReferenceEquals(second.ModNPC, savedMod) && ReferenceEquals(second.GetGlobalNPC<EarlyGlobalNpc>(), savedGlobal) && second.playerInteraction[7] && second.aiStyle == 3,
            $"type={second.type} netId={second.netID} aiStyle={second.aiStyle} interaction={second.playerInteraction[7]}");

        using var masterPin = New(ModContent.NPCType<VanillaCloneNpc>(), out NPC master, new NPCSpawnParams { difficultyOverride = GameDifficultyLevel.Master });
        using var vanillaPin = New(NPCID.Zombie, out NPC vanilla, new NPCSpawnParams { difficultyOverride = GameDifficultyLevel.Master });
        Report("cloned vanilla stats receive the requested difficulty once", master.lifeMax == vanilla.lifeMax && master.damage == vanilla.damage &&
            master.defLifeMax == vanilla.defLifeMax && master.difficulty == GameDifficultyLevel.Master,
            $"life={master.lifeMax}/{vanilla.lifeMax} damage={master.damage}/{vanilla.damage} difficulty={master.difficulty}");
        using var scaledPin = New(type, out NPC scaled, new NPCSpawnParams { sizeScaleOverride = 2f });
        Report("spawn size overrides reach mod dimensions", scaled.width == 48 && scaled.height == 52 && scaled.scale == 2f,
            $"size={scaled.width}x{scaled.height} scale={scaled.scale}");

        bool rejected = false;
        try { copied.CloneDefaults(copied.type); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Report("CloneDefaults refuses self-copy before mutating the NPC", rejected && copied.life == 130 && ReferenceEquals(copied.ModNPC, copiedMod), $"rejected={rejected}");
        using var lazyPin = New(NPCID.Bunny, out NPC lazy);
        lazy.type = type;
        Report("direct type assignment binds one lazy mod instance", lazy.ModNPC is TestNpc lazyMod && ReferenceEquals(lazyMod, lazy.ModNPC) && lazyMod.NPC == lazy,
            $"bound={lazy.ModNPC != null}");

        int index = NPC.NewNPC(default, (int)player.Center.X + 60, (int)player.Center.Y, type);
        bool spawned = (uint)index < 200u && Main.npc[index].active && Main.npc[index].ModNPC is TestNpc && Main.npc[index].life == 120;
        if ((uint)index < 200u) Main.npc[index].active = false;
        Report("NewNPC enters the same defaults and instance path", spawned, $"index={index}");
        using var examplePin = New(ModContent.NPCType<PartyZombie>(), out NPC example);
        Report("the ported PartyZombie has its original base stats and texture", example.ModNPC is PartyZombie && example.width == 18 && example.height == 40 &&
            example.defLifeMax == 200 && example.defDamage == 14 && TextureAssets.Npc[example.type].Value.Height > 40 &&
            Main.npcFrameCount[example.type] == Main.npcFrameCount[NPCID.Zombie],
            $"size={example.width}x{example.height} life={example.defLifeMax} damage={example.defDamage} frames={Main.npcFrameCount[example.type]}");
    }

    static GameHandle New(int type, out NPC npc, NPCSpawnParams spawnParams = default)
    {
        npc = NPC.New();
        var pin = new GameHandle(npc.Pointer);
        npc.SetDefaults(type, spawnParams);
        return pin;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csnpc {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
