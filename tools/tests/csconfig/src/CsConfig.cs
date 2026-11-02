using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CsConfig;

public enum Speed
{
    Slow,
    Normal,
    Fast,
    Faster
}

public sealed class TestConfig : ModConfig
{
    public static TestConfig Instance = null!;
    public static int Loaded, Changed;

    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("General")]
    [DefaultValue(true)]
    public bool Enabled;

    [Range(60, 250)]
    [Increment(5)]
    [DefaultValue(100)]
    public int Amount;

    [Range(0f, 2f)]
    [Increment(0.25f)]
    [DefaultValue(5f)]
    public float Scale;

    [DefaultValue(Speed.Fast)]
    public Speed Pace;

    [OptionStrings(new[] { "Red", "Green", "Blue" })]
    [DefaultValue("Green")]
    public string Channel = "Red";

    [DefaultValue(typeof(Color), "10, 20, 30, 255")]
    public Color Tint;

    [ReloadRequired]
    public bool NeedsRestart;

    public List<int> Numbers = new() { 1, 2, 3 };

    public override void OnLoaded() => Loaded++;

    public override void OnChanged() => Changed++;
}

public sealed class CsConfigMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsConfigMod>().Logger;

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
        if (done) Log.Info($"csconfig END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        switch (f)
        {
            case 10:
                Logic();
                done = true;
                break;
        }
    }

    static void Logic()
    {
        TestConfig config = ModContent.GetInstance<TestConfig>();
        Report("the config is autoloaded, registered and set to Instance with OnLoaded", config != null && TestConfig.Instance == config && ConfigManager.GetConfig(ModContent.GetInstance<CsConfigMod>(), nameof(TestConfig)) == config && TestConfig.Loaded == 1,
            $"instance={TestConfig.Instance == config} loaded={TestConfig.Loaded}");
        string file = Path.Combine(ConfigManager.ModConfigPath, $"{config!.Mod.Name}_{nameof(TestConfig)}.json");
        bool fresh = !File.Exists(file) || config.Amount == 100;
        Report("DefaultValue, Range and OptionStrings give the defaults", !fresh || (config.Enabled && config.Amount == 100 && config.Scale == 2f && config.Pace == Speed.Fast && config.Channel == "Green" &&
            config.Tint.R == 10 && config.Tint.G == 20 && config.Tint.B == 30 && config.Numbers.Count == 3),
            $"enabled={config.Enabled} amount={config.Amount} scale={config.Scale} pace={config.Pace} channel={config.Channel} tint={config.Tint}");
        var pending = (TestConfig)config.Clone();
        pending.Amount = 150;
        pending.Pace = Speed.Slow;
        pending.Channel = "Blue";
        pending.Tint = new Color(1, 2, 3, 255);
        int changed = TestConfig.Changed;
        ConfigSaveResult result = config.SaveChanges(pending);
        Report("SaveChanges applies the values, calls OnChanged and writes the file", result == ConfigSaveResult.Success && config.Amount == 150 && config.Pace == Speed.Slow && config.Channel == "Blue" &&
            TestConfig.Changed == changed + 1 && File.Exists(file) && File.ReadAllText(file).Contains("\"Amount\": 150"),
            $"result={result} amount={config.Amount} changed={TestConfig.Changed - changed}");
        var restart = (TestConfig)config.Clone();
        restart.NeedsRestart = !config.NeedsRestart;
        ConfigSaveResult refused = config.SaveChanges(restart);
        Report("a ReloadRequired change is refused in the world", refused == ConfigSaveResult.NeedsReload && restart.NeedsRestart != config.NeedsRestart, $"result={refused}");
        var reread = (TestConfig)Activator.CreateInstance(typeof(TestConfig))!;
        typeof(ModConfig).GetProperty("Mod")!.SetValue(reread, config.Mod);
        typeof(ModConfig).GetProperty("Name")!.SetValue(reread, config.Name);
        typeof(ConfigManager).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { reread });
        Report("the saved file reads back into a new instance", reread.Amount == 150 && reread.Pace == Speed.Slow && reread.Channel == "Blue" && reread.Tint.R == 1 && reread.Numbers.Count == 3,
            $"amount={reread.Amount} pace={reread.Pace} channel={reread.Channel} tint={reread.Tint} numbers={reread.Numbers.Count}");
        var reset = (TestConfig)config.Clone();
        reset.Amount = 100;
        reset.Pace = Speed.Fast;
        reset.Channel = "Green";
        reset.Tint = new Color(10, 20, 30, 255);
        config.SaveChanges(reset);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csconfig {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
