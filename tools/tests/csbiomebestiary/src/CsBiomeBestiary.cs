using System;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.Graphics.Capture;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBiomeBestiary;

public sealed class TestBiome : ModBiome
{
    public override bool IsBiomeActive(Player player) => false;
}

public sealed class WrongBiome : ModBiome
{
    public override bool IsBiomeActive(Player player) => false;
}

public sealed class UnusedBiome : ModBiome
{
    public override bool IsBiomeActive(Player player) => false;
}

public sealed class BiomeNpc : ModNPC
{
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        SpawnModBiomes = [ModContent.GetInstance<TestBiome>().Type, ModContent.GetInstance<WrongBiome>().Type];
    }
}

public sealed class CaptureWater : ModWaterStyle
{
    public override string Texture => "Water/TestWater";

    public override int ChooseWaterfallStyle() => 0;

    public override int GetSplashDust() => DustID.Water;

    public override int GetDropletGore() => GoreID.WaterDrip;
}

public sealed class CaptureScene : ModSceneEffect
{
    public static bool On;

    public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

    public override ModWaterStyle WaterStyle => ModContent.GetInstance<CaptureWater>();

    public override CaptureBiome.TileColorStyle TileColorStyle => CaptureBiome.TileColorStyle.Crimson;

    public override bool IsSceneEffectActive(Player player) => On;
}

public sealed class CsBiomeBestiaryMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsBiomeBestiaryMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csbiomebestiary END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        switch (f)
        {
            case 10:
                Bestiary();
                CaptureScene.On = true;
                break;
            case 40:
                Capture();
                CaptureScene.On = false;
                break;
            case 60:
                {
                    CaptureBiome biome = CaptureBiome.GetBiomeByWater();
                    Report("scene off: the capture biome is the game's", biome.IsNull || biome.WaterStyle < Main.maxLiquidTypes, biome.IsNull ? "null" : $"{biome.WaterStyle}");
                    done = true;
                    break;
                }
        }
    }

    static bool Contains(BestiaryEntry entry, ModBiome biome)
    {
        foreach (IBestiaryInfoElement element in entry.Info)
            if (element.Pointer == biome.ModBiomeBestiaryInfoElement.Element.Pointer) return true;
        return false;
    }

    static bool Filtered(ModBiome biome)
    {
        var filters = new GameList(Main.BestiaryDB._filters);
        for (int k = 0; k < filters.Count; k++)
        {
            var filter = new GameObject(filters[k]);
            if (Filters.ByInfoElement.Is(filter) && new Filters.ByInfoElement(filter.Pointer)._element.Pointer == biome.ModBiomeBestiaryInfoElement.Element.Pointer) return true;
        }
        return false;
    }

    static void Bestiary()
    {
        var test = ModContent.GetInstance<TestBiome>();
        var wrong = ModContent.GetInstance<WrongBiome>();
        var unused = ModContent.GetInstance<UnusedBiome>();
        Report("every biome has a bestiary element with its DisplayName key",
            !test.ModBiomeBestiaryInfoElement.IsNull && test.ModBiomeBestiaryInfoElement.GetDisplayNameKey() == test.DisplayName.Key && !unused.ModBiomeBestiaryInfoElement.IsNull,
            test.ModBiomeBestiaryInfoElement.GetDisplayNameKey());
        BestiaryEntry entry = Main.BestiaryDB.FindEntryByNPCID(ModContent.GetInstance<BiomeNpc>().Type);
        Report("SpawnModBiomes puts both biomes in the NPC's entry", !entry.IsNull && Contains(entry, test) && Contains(entry, wrong) && !Contains(entry, unused), "");
        Report("a filter for each biome an NPC uses, none for the unused one", Filtered(test) && Filtered(wrong) && !Filtered(unused), $"{Filtered(test)} {Filtered(wrong)} {Filtered(unused)}");
        test.ModBiomeBestiaryInfoElement.Element.GetDisplay(out Texture2D icon, out Rectangle frame);
        wrong.ModBiomeBestiaryInfoElement.Element.GetDisplay(out Texture2D shadow, out Rectangle shadowFrame);
        Report("the filter icon is the 30x30 BestiaryIcon; a wrong size keeps the shadow icon",
            icon.Width == 30 && icon.Height == 30 && frame.Width == 30 && shadow.Pointer != icon.Pointer && shadow.Width == 480,
            $"icon {icon.Width}x{icon.Height} frame {frame.Width} shadow {shadow.Width}x{shadow.Height} frame {shadowFrame.Width}");
        var background = test.ModBiomeBestiaryInfoElement.Element.GetBackgroundImage();
        var none = unused.ModBiomeBestiaryInfoElement.Element.GetBackgroundImage();
        Report("the portrait background is the 115x65 BackgroundPath; without one, the game's", background != null && none == null,
            $"background {(background == null ? "null" : "set")} unused {(none == null ? "null" : "set")}");
    }

    static void Capture()
    {
        CaptureBiome biome = CaptureBiome.GetBiomeByWater();
        int water = ModContent.GetInstance<CaptureWater>().Slot;
        Report("the capture biome takes the scene's water and TileColorStyle", !biome.IsNull && biome.WaterStyle == water && biome.TileColor == CaptureBiome.TileColorStyle.Crimson,
            biome.IsNull ? "null" : $"water={biome.WaterStyle} color={biome.TileColor} bg={biome.BackgroundIndex}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbiomebestiary {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
