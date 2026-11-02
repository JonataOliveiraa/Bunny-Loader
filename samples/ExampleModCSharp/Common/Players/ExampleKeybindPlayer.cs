using ExampleMod.Common.Systems;
using System;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleKeybindPlayer : ModPlayer
{
    private int LearningExampleKeybindHeldTimer;
    private int LearningExampleKeybindDoubleTapTimer;
    public static LocalizedText RandomBuffText { get; private set; }
    public static LocalizedText LearningExampleHeldText { get; private set; }
    public static LocalizedText LearningExampleDoubleTapText { get; private set; }

    public override void SetStaticDefaults()
    {
        RandomBuffText = Mod.GetLocalization($"{nameof(ExampleKeybindPlayer)}.RandomBuff");
        LearningExampleHeldText = Mod.GetLocalization($"{nameof(ExampleKeybindPlayer)}.LearningExampleHeld");
        LearningExampleDoubleTapText = Mod.GetLocalization($"{nameof(ExampleKeybindPlayer)}.LearningExampleDoubleTap");
    }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (KeybindSystem.RandomBuffKeybind.JustPressed)
        {
            int buff = Main.rand.Next(BuffID.Count);
            Player.AddBuff(buff, 600);
            Main.NewText(RandomBuffText.Format(Lang.GetBuffName(buff)));
        }

        if (KeybindSystem.LearningExampleKeybind.Current)
        {
            LearningExampleKeybindHeldTimer++;
            if (LearningExampleKeybindHeldTimer == 30)
                Main.NewText(LearningExampleHeldText);
        }
        else
        {
            LearningExampleKeybindHeldTimer = 0;
        }

        LearningExampleKeybindDoubleTapTimer = Math.Max(0, LearningExampleKeybindDoubleTapTimer - 1);
        if (KeybindSystem.LearningExampleKeybind.JustPressed)
        {
            if (LearningExampleKeybindDoubleTapTimer > 0)
                Main.NewText(LearningExampleDoubleTapText);
            else
                LearningExampleKeybindDoubleTapTimer = 15;
        }
    }
}
