using System.Linq;
using ExampleMod.Common.Configs.ModConfigShowcases;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Commands;

public class ModifyServerConfigValueCommand : ModCommand
{
    public static LocalizedText UsageText { get; private set; }

    public static LocalizedText DescriptionText { get; private set; }

    public static LocalizedText[] ErrorText { get; private set; }

    public override void SetStaticDefaults()
    {
        string key = $"Commands.{nameof(ModifyServerConfigValueCommand)}.";
        UsageText = Mod.GetLocalization($"{key}Usage");
        DescriptionText = Mod.GetLocalization($"{key}Description");
        ErrorText = Enumerable.Range(0, 2).Select(i => Mod.GetLocalization($"{key}Error_{i}")).ToArray();
    }

    public override CommandType Type => CommandType.World;

    public override string Command => "setSomeNumber";

    public override string Usage => UsageText.Value;

    public override string Description => DescriptionText.Value;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 0)
            throw new UsageException(ErrorText[0].Value);
        if (!int.TryParse(args[0], out int userInput))
            throw new UsageException(ErrorText[1].Format(args[0]));

        var config = ModContent.GetInstance<ModConfigShowcaseAcceptClientChanges>();
        config.SomeNumber = userInput;
        config.SaveChanges();
    }
}
