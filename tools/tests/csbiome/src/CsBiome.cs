using System;
using System.IO;
using System.Reflection;
using BunnyLoader.Interop;
using log4net;
using Terraria;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBiome;

public sealed class CsBiomeMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsBiomeMod>().Logger;

    static readonly Assembly Api = typeof(ModBiome).Assembly;

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
        try { Step(self, ++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csbiome END pass={pass} fail={fail}");
    }

    static object? Call(string type, string method, params object[] args) =>
        Api.GetType(type, true)!.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args);

    static void Step(Player p, int f)
    {
        switch (f)
        {
            case 20:
                Registration();
                break;
            case 40:
                Report("SpecialVisuals runs while the effect is inactive", TestBiome.VisualsInactive > 0 && TestBiome.Enters == 0, $"visuals={TestBiome.Visuals} inactive={TestBiome.VisualsInactive} enters={TestBiome.Enters}");
                TestBiome.Active = true;
                break;
            case 70:
                Entered(p);
                EventEffect.Active = true;
                break;
            case 90:
                Scene(p, 7, SceneEffectPriority.Event, "the event effect outranks the biome for music");
                EventEffect.Active = false;
                Shopping(p);
                Network(p);
                break;
            case 110:
                Scene(p, 5, SceneEffectPriority.BiomeLow, "the biome's music comes back when the event ends");
                Call("Terraria.ModLoader.BiomeLoader", "LeaveWorld");
                Report("leaving the world calls OnLeave and clears the flags", TestBiome.Leaves == 1 && !p.InModBiome<TestBiome>() && !p.CurrentSceneEffect.anyActive,
                    $"leaves={TestBiome.Leaves} in={p.InModBiome<TestBiome>()} any={p.CurrentSceneEffect.anyActive}");
                break;
            case 130:
                Report("the biome is entered again on the next update", TestBiome.Enters == 2 && p.InModBiome<TestBiome>(), $"enters={TestBiome.Enters}");
                TestBiome.Throw = true;
                break;
            case 150:
                Report("an IsBiomeActive that throws counts as false and leaves the biome", TestBiome.Leaves == 2 && !p.InModBiome<TestBiome>(), $"leaves={TestBiome.Leaves}");
                TestBiome.Throw = false;
                TestBiome.Active = false;
                done = true;
                break;
        }
    }

    static void Registration()
    {
        var biome = ModContent.GetInstance<TestBiome>();
        var other = ModContent.GetInstance<OtherBiome>();
        var effect = ModContent.GetInstance<EventEffect>();
        Report("biomes and scene effects get their types", BiomeLoader.Count == 2 && SceneEffectLoader.Count == 3 && other.Type == 0 && biome.Type == 1 && effect.Type == 0,
            $"biomes={BiomeLoader.Count} effects={SceneEffectLoader.Count} types={biome.Type},{other.Type},{effect.Type}");
        Report("a biome without localization gets tModLoader's default names",
            biome.DisplayName.Value == "Test Biome" && biome.DisplayName.Key == "Mods.CsBiome.Biomes.TestBiome.DisplayName",
            $"key={biome.DisplayName.Key} value={biome.DisplayName.Value}");
        Report("BestiaryIcon and BackgroundPath follow the class path", biome.BestiaryIcon == "CsBiome/TestBiome_Icon" && biome.BackgroundPath == "CsBiome/TestBiome_Background",
            $"{biome.BestiaryIcon} {biome.BackgroundPath}");
        Report("ModBiome defaults: BiomeLow priority and silence", other.Priority == SceneEffectPriority.BiomeLow && other.Music == 0 && effect.GetWeight(Main.LocalPlayer) == 0.5f, "");
    }

    static void Entered(Player p)
    {
        Report("OnEnter once, OnInBiome every update, InModBiome true",
            TestBiome.Enters == 1 && TestBiome.InBiome >= 20 && p.InModBiome<TestBiome>() && p.InModBiome(ModContent.GetInstance<TestBiome>()) && !p.InModBiome<OtherBiome>(),
            $"enters={TestBiome.Enters} inBiome={TestBiome.InBiome}");
        Scene(p, 5, SceneEffectPriority.BiomeLow, "the active biome sets the scene's music");
    }

    static void Scene(Player p, int music, SceneEffectPriority priority, string name)
    {
        var scene = p.CurrentSceneEffect;
        Report(name, scene.anyActive && scene.music.value == music && scene.music.priority == priority, $"any={scene.anyActive} music={scene.music.value} priority={scene.music.priority}");
    }

    static void Shopping(Player p)
    {
        NPCHappiness.Get(NPCID.Guide).SetBiomeAffection<MushroomBiome>(AffectionLevel.Like).SetBiomeAffection<TestBiome>(AffectionLevel.Love);
        var proxy = (AShoppingBiome)Call("Terraria.ModLoader.BiomeLoader", "ShoppingOf", ModContent.GetInstance<TestBiome>())!;
        var profile = Main.ShopHelper._database.GetByNPCID(NPCID.Guide);
        int mine = 0, mushroom = 0;
        var modifiers = new GameList(profile.ShopModifiers);
        for (int i = 0; i < modifiers.Count; i++)
        {
            var value = new GameObject(modifiers.Get<IntPtr>(i));
            if (!BiomePreferenceListTrait.Is(value)) continue;
            var preferences = new GameList(new BiomePreferenceListTrait(value.Pointer)._preferences);
            for (int j = 0; j < preferences.Count; j++)
            {
                var entry = new BiomePreferenceListTrait.BiomePreference(preferences.Get<IntPtr>(j));
                if (entry.Biome.Pointer == proxy.Pointer && entry.Affection == AffectionLevel.Love) mine++;
                else if (MushroomBiome.Is(new GameObject(entry.Biome.Pointer)) && entry.Affection == AffectionLevel.Like) mushroom++;
            }
        }
        Report("SetBiomeAffection with a ModBiome adds its own entry next to the vanilla mushroom one", mine == 1 && mushroom >= 1, $"mine={mine} mushroom={mushroom}");
        bool inside = new MushroomBiome(proxy.Pointer).IsInBiome(p);
        Report("the shopping biome answers with the player's biome flag", inside && proxy.NameKey == "Mods.CsBiome.Biomes.TestBiome.TownNPCDialogueName",
            $"inside={inside} key={proxy.NameKey}");
    }

    static void Network(Player p)
    {
        byte[] packet = (byte[])Call("Terraria.ModLoader.Core.BiomeHooks", "Write", Main.myPlayer)!;
        bool[] flags = (bool[])Call("Terraria.ModLoader.BiomeLoader", "FlagsOf", Main.myPlayer)!;
        Array.Clear(flags);
        using (var reader = new BinaryReader(new MemoryStream(packet)))
        {
            reader.ReadByte();
            Call("Terraria.ModLoader.Core.BiomeHooks", "Receive", reader, Main.myPlayer);
        }
        Report("the biome packet carries the flags by key", p.InModBiome<TestBiome>() && !p.InModBiome<OtherBiome>(), $"bytes={packet.Length}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbiome {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
