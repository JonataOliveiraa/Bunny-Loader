using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Commands;

public class ExampleItemCommand : ModCommand
{
    public static LocalizedText UsageText { get; private set; }

    public static LocalizedText DescriptionText { get; private set; }

    public static LocalizedText[] ErrorText { get; private set; }

    public override void SetStaticDefaults()
    {
        string key = $"Commands.{nameof(ExampleItemCommand)}.";
        UsageText = Mod.GetLocalization($"{key}Usage");
        DescriptionText = Mod.GetLocalization($"{key}Description");
        ErrorText = Enumerable.Range(0, 3).Select(i => Mod.GetLocalization($"{key}Error_{i}")).ToArray();
    }

    public override CommandType Type => CommandType.Chat;

    public override string Command => "item";

    public override string Usage => UsageText.Value;

    public override string Description => DescriptionText.Value;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 0)
            throw new UsageException(ErrorText[0].Value);

        if (!int.TryParse(args[0], out int type))
        {
            string name = args[0].Replace("_", " ");
            for (int k = 1; k < ItemLoader.ItemCount; k++)
            {
                if (name.ToLower() == Lang.GetItemNameValue(k)?.ToLower())
                {
                    type = k;
                    break;
                }
            }
        }

        if (type <= 0 || type >= ItemLoader.ItemCount)
            throw new UsageException(ErrorText[1].Format(ItemLoader.ItemCount));

        int stack = 1;
        if (args.Length >= 2 && !int.TryParse(args[1], out stack))
            throw new UsageException(ErrorText[2].Format(args[1]));

        caller.Player.QuickSpawnItem(caller.Player.GetSource_FromThis(), type, stack);
    }
}
