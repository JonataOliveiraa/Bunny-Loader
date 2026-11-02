using ExampleMod.Common.UI.ExampleCoinsUI;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Commands;

public class ExampleCoinsCommand : ModCommand
{
    public static LocalizedText DescriptionText { get; private set; }

    public override void SetStaticDefaults()
    {
        DescriptionText = Mod.GetLocalization($"Commands.{nameof(ExampleCoinsCommand)}.Description");
    }

    public override CommandType Type => CommandType.Chat;

    public override string Command => "coins";

    public override string Description => DescriptionText.Value;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        ModContent.GetInstance<ExampleCoinsUISystem>().ShowMyUI();
    }
}
