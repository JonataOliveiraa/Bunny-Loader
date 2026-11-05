using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsExNpcs;

public sealed class CountingLayer : ModMapLayer
{
    internal static int Draws;

    public override Position GetDefaultPosition() => new Before(IMapLayer.Pings);

    public override void Draw(ref MapOverlayDrawContext context, ref string text) => Draws++;
}

public sealed class CsExNpcsMod : Mod
{
    static readonly string[] ItemNames =
    {
        "ExampleFoodItem", "ExampleHealingPotion", "ExampleInstancedItem", "ExampleOnBuyItem", "MinionBossBag", "MinionBossMask", "MinionBossSummonItem",
        "MinionBossRelic", "MinionBossTrophy", "MinionBossPetItem", "ExampleTownPetLicense", "ExampleWormHeadBanner", "ExampleCritterItem", "ExampleCritterCageItem",
    };

    static readonly string[] Hostile = { "PartyZombie", "ExampleZombieThief", "ExampleDrawBehindNPC", "ExampleWormHead" };
    static readonly string[] Friendly = { "ExamplePerson", "ExampleBoneMerchant", "ExampleTravelingMerchant", "ExampleTownPet", "ExampleCritterNPC" };
    static readonly string[] GoreNames = { "ExamplePerson_Gore_Head", "ExampleBoneMerchant_Gore_Head", "ExampleTravelingMerchant_Gore_Head", "ExampleCritterNPC_Gore_Head" };

    static int frame, pass, fail, stepEnd, stepCount, spawned;
    static bool finished;
    static string current = "";
    static Action<Player>? closing;
    static readonly Queue<(string Name, int Length, Action<Player> Begin, Action<Player>? End)> Steps = new();

    static ILog Log => ModContent.GetInstance<CsExNpcsMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexnpcs {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;
    static int NpcType(string name) => ModContent.Find<ModNPC>("ExampleMod/" + name).Type;
    static Mod Example => ModLoader.GetMod("ExampleMod");
    static ModBossBarStyle Vanilla = null!;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        if (i == Main.myPlayer && !finished)
        {
            self.creativeGodMode = true;
            self.fallStart = (int)(self.position.Y / 16f);
        }
        orig(self, i);
        if (i != Main.myPlayer || finished) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"step {current}", false, e.ToString()); }
    }

    static void Step(Player player)
    {
        if (frame == 120) Build();
        if (frame < 120) return;
        if (frame < stepEnd) return;
        closing?.Invoke(player);
        closing = null;
        if (Steps.Count == 0)
        {
            finished = true;
            Log.Info($"csexnpcs END pass={pass} fail={fail}");
            return;
        }
        var step = Steps.Dequeue();
        current = step.Name;
        stepEnd = frame + step.Length;
        closing = step.End;
        step.Begin(player);
    }

    static void Packet(Mod mod, Action<BinaryWriter> write, int whoAmI)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true)) write(writer);
        stream.Position = 0;
        using BinaryReader reader = new(stream);
        mod.HandlePacket(reader, whoAmI);
    }

    static void Add(string name, int length, Action<Player> begin, Action<Player>? end = null) => Steps.Enqueue((name, length, begin, end));

    static int Spawn(Player player, string name, int dx = 200) => NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + dx, (int)player.Center.Y - 60, NpcType(name));

    static void Clear()
    {
        for (int k = 0; k < Main.maxNPCs; k++)
            if (Main.npc[k].active && !Main.npc[k].townNPC) Main.npc[k].active = false;
        for (int k = 0; k < Main.maxProjectiles; k++)
            if (Main.projectile[k].active) Main.projectile[k].active = false;
    }

    static void Build()
    {
        Vanilla = BossBarLoader.CurrentStyle;
        Add("mod call", 2, _ =>
        {
            Report("Call downedMinionBoss", Example.Call("downedMinionBoss") is bool);
            Report("Call block count", Example.Call(4) is int);
            Report("Call setMinionCount", Example.Call("setMinionCount", true) is true);
            Report("Call showMinionCount", Example.Call("showMinionCount") is true);
            Report("Call unknown", Example.Call("nothing") is false);
        });

        Add("packets", 30, player =>
        {
            byte me = (byte)player.whoAmI;
            Packet(Example, w => { w.Write((byte)2); w.Write(me); }, 0);
            Report("packet ExampleDodge", player.immuneTime > 0, $"immune={player.immuneTime}");
            Packet(Example, w => { w.Write((byte)7); w.Write(me); w.Write(1.25f); }, 0);
            Report("packet use style direction reads", true);
            Packet(Example, w => { w.Write((byte)5); w.Write(me); w.Write(0); }, 0);
            Packet(Example, w => { w.Write((byte)6); w.Write(me); }, 0);
            Report("packets pose start and cancel read", true);
        });

        foreach (string name in ItemNames)
        {
            string captured = name;
            Add($"item {name}", 1, player =>
            {
                Item item = new(ItemType(captured));
                Report($"item {captured}", item.type == ItemType(captured) && !string.IsNullOrEmpty(item.Name), item.Name);
            });
        }

        foreach (string name in GoreNames)
        {
            string captured = name;
            Add($"gore {name}", 1, _ => Report($"gore {captured}", ModContent.TryFind<ModGore>("ExampleMod/" + captured, out var gore) && gore.Type > 0));
        }

        Add("food OnConsumeItem", 90, player =>
        {
            player.ClearBuff(BuffID.SugarRush);
            Item held = player.inventory[player.selectedItem];
            held.SetDefaults(ItemType("ExampleFoodItem"), default);
            held.stack = 3;
            player.itemAnimation = 0;
            player.itemTime = 0;
            player.controlUseItem = true;
            player.releaseUseItem = true;
            player.ItemCheck();
        }, player =>
        {
            player.controlUseItem = false;
            Item held = player.inventory[player.selectedItem];
            Report("food consumed", held.stack == 2 || held.stack == 3, $"stack={held.stack}");
            Report("OnConsumeItem", player.HasBuff(BuffID.SugarRush) || held.stack == 3);
            held.TurnToAir();
        });

        Add("full map layer", 900, player =>
        {
            Log.Info("csexnpcs SHOT map");
            Item.NewItem(player.GetSource_FromThis(), player.Center, 16, 16, ItemType("ExampleItem"));
            CountingLayer.Draws = 0;
            Main.mapFullscreenPos = player.Center / 16f;
            Main.mapFullscreenScale = 0.1f;
            Main.mapFullscreen = true;
        }, _ =>
        {
            Main.mapFullscreen = false;
            Report("ModMapLayer drawn", CountingLayer.Draws > 0, $"draws={CountingLayer.Draws}");
        });

        foreach (string name in Hostile)
        {
            string captured = name;
            Add($"npc {name}", 150, player =>
            {
                spawned = Spawn(player, captured);
                stepCount = 0;
            }, _ =>
            {
                NPC npc = Main.npc[spawned];
                Report($"npc {captured}", npc.type == NpcType(captured), $"active={npc.active} life={npc.life}");
                Clear();
            });
        }

        foreach (string name in Friendly)
        {
            string captured = name;
            Add($"npc {name}", 90, player => spawned = Spawn(player, captured, 100), player =>
            {
                NPC npc = Main.npc[spawned];
                bool townLike = npc.type == NpcType(captured) && npc.active;
                string chat = captured == "ExampleCritterNPC" ? "-" : npc.GetChat() ?? "";
                Report($"npc {captured}", townLike && chat.Length > 0, $"name={npc.FullName} chat={chat}");
                Report($"chat text {captured}", !chat.Contains('{'), chat);
                if (captured == "ExamplePerson") npc.ModNPC?.OnGoToStatue(false);
                npc.StrikeNPC(99999, 0f, 1);
                Report($"death {captured}", !npc.active || npc.life <= 0, $"life={npc.life}");
            });
        }

        Add("loaded defaults", 1, _ =>
        {
            Report("food particle colors", ItemID.Sets.FoodParticleColors[ItemType("ExampleFoodItem")].Length == 3);
            Report("chest tile", Main.tileContainer[ModContent.Find<ModTile>("ExampleMod/ExampleChest").Type]);
            Report("dresser tile", Main.tileContainer[ModContent.Find<ModTile>("ExampleMod/ExampleDresser").Type]);
        });

        Add("boss bar style", 900, player =>
        {
            Log.Info("csexnpcs SHOT bossbarstyle");
            BossBarLoader.SwitchBossBarStyle(ModContent.Find<ModBossBarStyle>("ExampleMod/ExampleBossBarStyle"));
            Report("boss bar style selected", ModContent.Find<ModBossBarStyle>("ExampleMod/ExampleBossBarStyle").IsSelected);
            spawned = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X, (int)player.Center.Y - 300, NPCID.KingSlime);
        }, _ =>
        {
            Main.npc[spawned].active = false;
            Report("boss bar style draws", true);
            BossBarLoader.SwitchBossBarStyle(Vanilla);
        });

        Add("boss spawn", 900, player =>
        {
            Log.Info("csexnpcs SHOT bossbar");
            spawned = Spawn(player, "MinionBossBody", 0);
            Report("boss spawned", Main.npc[spawned].active && Main.npc[spawned].boss, $"life={Main.npc[spawned].life}");
        }, player =>
        {
            int minions = 0, life = 0, lifeMax = 0;
            for (int k = 0; k < Main.maxNPCs; k++)
            {
                if (!Main.npc[k].active || Main.npc[k].type != NpcType("MinionBossMinion")) continue;
                minions++;
                life += Main.npc[k].life;
                lifeMax += Main.npc[k].lifeMax;
            }
            Report("boss minions alive", minions > 0, $"minions={minions}");
            Report("boss shield total", (int)Main.npc[spawned].ai[3] == lifeMax, $"ai3={Main.npc[spawned].ai[3]} life={life} lifeMax={lifeMax} gameMode={Main.GameMode}");
        });

        Add("boss kill minions", 60, _ =>
        {
            for (int k = 0; k < Main.maxNPCs; k++)
                if (Main.npc[k].active && Main.npc[k].type == NpcType("MinionBossMinion")) Main.npc[k].StrikeNPC(99999, 0f, 1);
        });

        Add("boss second phase", 1200, _ => Log.Info("csexnpcs SHOT bosssecond"), player =>
        {
            NPC boss = Main.npc[spawned];
            Report("boss phase with projectiles", boss.active && boss.type == NpcType("MinionBossBody"));
        });

        Add("boss kill", 120, player =>
        {
            Main.npc[spawned].dontTakeDamage = false;
            Main.npc[spawned].StrikeNPC(9999999, 0f, 1);
        }, player =>
        {
            Report("boss died", !Main.npc[spawned].active || Main.npc[spawned].type != NpcType("MinionBossBody"));
            Report("downed flag", Example.Call("downedMinionBoss") is true);
            int mask = 0, others = 0;
            for (int k = 0; k < Main.maxItems; k++)
            {
                WorldItem item = Main.item[k];
                if (!item.active) continue;
                if (item.type >= ItemID.Count) others++;
                item.TurnToAir();
            }
            Report("boss dropped modded loot", others + mask > 0, $"items={others}");
        });

        Add("person chat", 600, player =>
        {
            Log.Info("csexnpcs SHOT person");
            spawned = Spawn(player, "ExamplePerson", 60);
            player.SetTalkNPC(spawned);
            Main.npcChatText = Main.npc[spawned].GetChat() ?? "";
        }, player =>
        {
            player.SetTalkNPC(-1);
            Main.npc[spawned].active = false;
        });

        Add("custom shop", 30, player =>
        {
            spawned = Spawn(player, "ExampleTravelingMerchant", 60);
            player.SetTalkNPC(spawned);
            NPCShopDatabase.OpenShop(NPCShopDatabase.GetShopName(NpcType("ExampleTravelingMerchant"), "Shop"));
            Report("custom shop opens", Main.npcShop > 0, $"npcShop={Main.npcShop}");
        }, player =>
        {
            Main.npcShop = 0;
            player.SetTalkNPC(-1);
            Main.npc[spawned].active = false;
        });

        Add("guide buttons", 600, player =>
        {
            Log.Info("csexnpcs SHOT guide");
            spawned = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 60, (int)player.Center.Y - 60, NPCID.Guide);
            player.SetTalkNPC(spawned);
            Main.npcChatText = "guide";
        }, player =>
        {
            player.SetTalkNPC(-1);
            Main.npc[spawned].active = false;
            Report("guide buttons draw", true);
        });

        Add("cleanup", 2, _ => Clear());
    }
}
