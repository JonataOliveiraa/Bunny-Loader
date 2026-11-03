using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using BestiaryEntry = Terraria.GameContent.Bestiary.BestiaryEntry;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using System.ComponentModel;

namespace CsVisual;

public sealed class VisualCloud : ModCloud
{
    internal static int Draws;

    public override float SpawnChance() => 0;

    public override bool Draw(List<DrawData> drawDataCache, Cloud cloud, int cloudIndex, ref DrawData drawData)
    {
        Draws++;
        return true;
    }
}

public sealed class VisualBiome : ModBiome
{
    public override bool IsBiomeActive(Player player) => false;
}

public sealed class VisualConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("Tips")]
    [DefaultValue(true)]
    public bool Flag;

    [Range(0, 10)]
    [DefaultValue(3)]
    public int Amount;

    [OptionStrings(new[] { "One", "Two", "Three" })]
    [DefaultValue("Two")]
    public string Choice = "Two";

    [DefaultValue(false)]
    public bool Plain;
}

public sealed class VisualNpc : ModNPC
{
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        NPC.friendly = true;
        SpawnModBiomes = [ModContent.GetInstance<VisualBiome>().Type];
    }
}

public sealed class CsVisualMod : Mod
{
    const int Clouds = 30, Bestiary = 540, Vanilla = 840, CloseBestiary = 1140, Config = 1200, End = 1800;

    static int frames, pass, fail, npcIndex = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsVisualMod>().Logger;

    public override void Load() => On_Main.DoUpdate += Update;

    static void Update(On_Main.orig_DoUpdate orig, Main self, Microsoft.Xna.Framework.GameTime gameTime)
    {
        orig(self, gameTime);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csvisual END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        Player player = Main.LocalPlayer;
        if (f <= Bestiary)
        {
            Main.dayTime = true;
            Main.time = 27000;
            Main.cloudAlpha = 0;
        }
        if (f >= Clouds && f < Bestiary) PlaceClouds();
        if (f == Clouds + 60) Log.Info("csvisual scene clouds");
        if (f == Bestiary - 120) SpawnNpc(player);
        if (f == Bestiary - 60) KillNpc(player, ModContent.NPCType<VisualNpc>());
        if (f == Bestiary)
        {
            Report("mod clouds were drawn through ModCloud.Draw", VisualCloud.Draws > 0, $"draws={VisualCloud.Draws}");
            OpenBestiary();
            Log.Info("csvisual scene bestiary");
        }
        if (f == Bestiary + 60)
        {
            var providers = new BunnyLoader.Interop.GameList(GUIInstance.Active.GUIBestiary.FilterProviders);
            nint mine = ModContent.GetInstance<VisualBiome>().ModBiomeBestiaryInfoElement.Element.Pointer;
            Report("the bestiary's spawn list holds the mod biome", !providers.IsNull && providers.Count == 1 && providers[0] == mine, $"providers={(providers.IsNull ? -1 : providers.Count)}");
        }
        if (f == Vanilla - 30) KillNpc(player, Terraria.ID.NPCID.BlueSlime);
        if (f == Vanilla)
        {
            var bestiary = GUIInstance.Active.GUIBestiary;
            BestiaryEntry slime = Main.BestiaryDB.FindEntryByNPCID(Terraria.ID.NPCID.BlueSlime);
            bestiary.SelectedEntry = slime;
            bestiary.RefreshSelectedStats();
            bestiary.UpdateBackgroundImages(slime);
            Log.Info("csvisual scene vanilla");
        }
        if (f == CloseBestiary)
        {
            GUIInstance.Active.GUIBestiary.Close();
            GUIInstance.Active.GUISettings.Visible = false;
        }
        if (f == Config)
        {
            ModContent.GetInstance<VisualConfig>().Open();
            Log.Info("csvisual scene config");
        }
        if (f == End) done = true;
    }

    static void PlaceClouds()
    {
        int type = ModContent.GetInstance<VisualCloud>().Type;
        var texture = Terraria.GameContent.TextureAssets.Cloud[type].Value;
        for (int k = 0; k < Main.cloud.Length; k++)
        {
            Cloud cloud = Main.cloud[k];
            if (cloud.IsNull || !cloud.active || cloud.type == type) continue;
            cloud.type = type;
            cloud.width = (int)(texture.Width * cloud.scale);
            cloud.height = (int)(texture.Height * cloud.scale);
        }
    }

    static void SpawnNpc(Player player, int type = -1)
    {
        npcIndex = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 40, (int)player.Center.Y, type < 0 ? ModContent.NPCType<VisualNpc>() : type);
    }

    static void KillNpc(Player player, int type)
    {
        for (int k = 0; k < 50; k++)
        {
            if (k > 0 || type != ModContent.NPCType<VisualNpc>()) SpawnNpc(player, type);
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
            NPC npc = Main.npc[npcIndex];
            npc.lastInteraction = Main.myPlayer;
            npc.playerInteraction[Main.myPlayer] = true;
            npc.life = 0;
            npc.checkDead();
        }
    }

    static void OpenBestiary()
    {
        BestiaryEntry entry = Main.BestiaryDB.FindEntryByNPCID(ModContent.NPCType<VisualNpc>());
        GUIInstance.Active.GUISettings.Visible = true;
        var bestiary = GUIInstance.Active.GUIBestiary;
        bestiary.Open();
        bestiary.SelectedEntry = entry;
        bestiary.RefreshSelectedStats();
        bestiary.UpdateBackgroundImages(entry);
        Report("the bestiary is open on the mod NPC", !entry.IsNull && bestiary.Enabled, $"entry={!entry.IsNull}");
        nint mine = ModContent.GetInstance<VisualBiome>().ModBiomeBestiaryInfoElement.Element.Pointer;
        bool listed = false;
        for (int k = 0; k < entry.Info.Count; k++)
            listed |= entry.Info[k].Pointer == mine;
        Report("the NPC's entry lists the mod biome", listed, $"info={entry.Info.Count}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csvisual {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
