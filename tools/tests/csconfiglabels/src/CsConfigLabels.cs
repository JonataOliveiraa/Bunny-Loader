using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CsConfigLabels;

public enum LongChoice
{
    First,
    Second,
    Third
}

public enum ManyChoices
{
    One,
    Two,
    Three,
    Four,
    Five,
    Six
}

public sealed class LongLabels : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public bool ToggleWithAVeryLongLabelThatDoesNotFitBeforeTheOnAndOffButtonsOfThisRow;

    [Range(0, 100)]
    public int SliderWithAVeryLongLabelThatDoesNotFitBeforeTheTrackAndTheValueOfThisRow;

    public LongChoice RadioWithAVeryLongLabelThatDoesNotFitBeforeTheThreeChoicesOfThisRow;

    [DrawTicks]
    public ManyChoices DropdownWithAVeryLongLabelThatDoesNotFitBeforeTheButtonOfThisRow;

    public Color ColorWithAVeryLongLabelThatDoesNotFitBeforeTheSwatchAndTheHexOfThisRow = Color.Red;

    public ItemDefinition DefinitionWithAVeryLongLabelThatDoesNotFitBeforeTheNameOfThisRow = new(ItemID.Meowmere);

    public bool Short;

    [Range(0, 100)]
    public int ShortSlider;
}

public sealed class CsConfigLabelsMod : Mod
{
    const double StepSeconds = 8;

    static readonly Stopwatch clock = new();
    static int step;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUISettingsPauseMenu.Draw += Draw;
    }

    static ModConfig Labels => ModContent.Find<ModConfig>("ExampleMod/ModConfigShowcaseLabels");

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || step != 0)
            return;
        if (!clock.IsRunning)
            clock.Start();
        if (clock.Elapsed.TotalSeconds < 2)
            return;
        step = 1;
        clock.Restart();
        Labels.Open(scrollToOption: "LocalizedLabel", centerScrolledOption: false);
        Shot("labels");
    }

    static void Draw(On_GUISettingsPauseMenu.orig_Draw orig, GUISettingsPauseMenu self)
    {
        if (step > 0 && step < 5 && clock.Elapsed.TotalSeconds >= StepSeconds)
        {
            clock.Restart();
            Advance();
        }

        orig(self);
    }

    static void Shot(string name) => ModContent.GetInstance<CsConfigLabelsMod>().Logger.Info("csconfiglabels SHOT " + name);

    static void ShowTip()
    {
        Type menu = typeof(ModConfig).Assembly.GetType("Terraria.ModLoader.Config.ConfigMenu")!;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        Type tipType = menu.GetNestedType("Tip", flags)!;
        object tip = Activator.CreateInstance(tipType, true)!;
        tipType.GetField("Title", flags)!.SetValue(tip, "A very long title that has to wrap because it does not fit on one line of the panel");
        tipType.GetField("Text", flags)!.SetValue(tip, "[c/FF55AA:Use to disable the] [c/22a2dd:Example Sword] [c/FF55AA:feature, and this text is long enough to wrap to a second line inside the panel]");
        menu.GetField("tip", flags)!.SetValue(null, tip);
    }

    static void Advance()
    {
        switch (step++)
        {
            case 1:
                Labels.Open(scrollToOption: "Header:CoolHeader", centerScrolledOption: false);
                Shot("headers");
                break;
            case 2:
                ModContent.GetInstance<LongLabels>().Open();
                Shot("kinds");
                break;
            case 3:
                ShowTip();
                Shot("tip");
                break;
            case 4:
                ModContent.GetInstance<CsConfigLabelsMod>().Logger.Info("csconfiglabels END pass=1 fail=0");
                break;
        }
    }
}
