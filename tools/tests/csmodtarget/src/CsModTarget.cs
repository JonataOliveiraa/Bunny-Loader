using System;
using Terraria.ModLoader;

namespace CsModTarget;

public sealed class CsModTargetMod : Mod
{
    public override void Load() => TargetApi.Loaded = true;

    public override object? Call(params object?[] args)
    {
        if (args.Length == 0 || args[0] is not string command) throw new ArgumentException("Call: the first argument is the command");
        switch (command)
        {
            case "Add":
                return (int)args[1]! + (int)args[2]!;
            case "Echo":
                return args[1];
            case "Throw":
                throw new InvalidOperationException("csmodtarget Throw (expected)");
        }
        return null;
    }
}
