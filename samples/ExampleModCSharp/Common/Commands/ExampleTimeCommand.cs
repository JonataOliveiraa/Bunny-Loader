using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Commands;

public class ExampleTimeCommand : ModCommand
{
    public static LocalizedText UsageText { get; private set; }

    public static LocalizedText DescriptionText { get; private set; }

    public static LocalizedText[] ErrorText { get; private set; }

    public override void SetStaticDefaults()
    {
        string key = $"Commands.{nameof(ExampleTimeCommand)}.";
        UsageText = Mod.GetLocalization($"{key}Usage");
        DescriptionText = Mod.GetLocalization($"{key}Description");
        ErrorText = Enumerable.Range(0, 2).Select(i => Mod.GetLocalization($"{key}Error_{i}")).ToArray();
    }

    public override CommandType Type => CommandType.World;

    public override string Command => "addTime";

    public override string Usage => UsageText.Value;

    public override string Description => DescriptionText.Value;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        const double cycleLength = Main.dayLength + Main.nightLength;
        if (args.Length == 0)
            throw new UsageException(ErrorText[0].Value);
        if (!int.TryParse(args[0], out int extraTime))
            throw new UsageException(ErrorText[1].Format(args[0]));

        double fullTime = Main.time;
        if (!Main.dayTime)
            fullTime += Main.dayLength;

        fullTime += extraTime;
        fullTime %= cycleLength;
        if (fullTime < 0)
            fullTime += cycleLength;

        Main.dayTime = fullTime < Main.dayLength;
        if (!Main.dayTime)
            fullTime -= Main.dayLength;
        Main.time = fullTime;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }
}
