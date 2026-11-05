using System;
using ExampleMod.Common.Players;
using ExampleMod.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod;

partial class ExampleMod
{
    public override object? Call(params object?[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty!");

        if (args[0] is string content)
        {
            switch (content)
            {
                case "downedMinionBoss":
                    return DownedBossSystem.downedMinionBoss;
                case "showMinionCount":
                    return Main.LocalPlayer.GetModPlayer<ExampleInfoDisplayPlayer>().showMinionCount;
                case "setMinionCount":
                    if (args[1] is not bool minionSet)
                        throw new Exception($"Expected an argument of type bool when setting minion count, but got type {args[1]?.GetType().Name} instead.");
                    Main.LocalPlayer.GetModPlayer<ExampleInfoDisplayPlayer>().showMinionCount = minionSet;
                    return true;
            }
        }

        if (args[0] is int contentInt && contentInt == 4)
            return ModContent.GetInstance<ExampleBiomeTileCount>().exampleBlockCount;

        return false;
    }
}
