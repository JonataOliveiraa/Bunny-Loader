using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class CommandLoader
{
    internal static readonly IDictionary<string, List<ModCommand>> Commands = new Dictionary<string, List<ModCommand>>(StringComparer.OrdinalIgnoreCase);

    static readonly Color ErrorColor = new(255, 25, 25, 255);

    static readonly Color AliasColor = new(124, 252, 0, 255);

    public static bool Matches(CommandType commandType, CommandType callerType)
    {
        if ((commandType & CommandType.World) != 0)
        {
            if ((Main.netMode & 2) != 0)
                commandType |= CommandType.Server;
            else if (Main.netMode == 0)
                commandType |= CommandType.Chat;
        }

        return (callerType & commandType) != 0;
    }

    internal static void Add(ModCommand cmd)
    {
        if (!Commands.TryGetValue(cmd.Command, out List<ModCommand>? list))
            Commands.Add(cmd.Command, list = new List<ModCommand>());
        list.Add(cmd);
        CommandHooks.Install();
    }

    internal static bool GetCommand(CommandCaller caller, string name, out ModCommand? mc)
    {
        string? modName = null;
        if (name.Contains(':'))
        {
            string[] split = name.Split(':');
            modName = split[0];
            name = split[1];
        }

        mc = null;
        if (!Commands.TryGetValue(name, out List<ModCommand>? list))
            return false;
        list = list.Where(c => Matches(c.Type, caller.CommandType)).ToList();
        if (list.Count == 0)
            return false;
        if (modName != null)
        {
            Mod? mod = ModLoader.Mods.FirstOrDefault(m => string.Equals(m.Name, modName, StringComparison.OrdinalIgnoreCase));
            if (mod == null && ModLoader.TryGetMod(modName, out Mod found))
                mod = found;
            if (mod == null)
            {
                caller.Reply(Language.GetTextValue("BunnyLoader.CommandUnknownMod", modName), ErrorColor);
            }
            else
            {
                mc = list.SingleOrDefault(c => c.Mod == mod);
                if (mc == null)
                    caller.Reply(Language.GetTextValue("BunnyLoader.CommandMissingInMod", modName, name), ErrorColor);
            }
        }
        else if (list.Count > 1)
        {
            caller.Reply(Language.GetTextValue("BunnyLoader.CommandAmbiguous", name), ErrorColor);
            foreach (ModCommand c in list)
                caller.Reply(c.Mod.Name + ":" + c.Command, AliasColor);
        }
        else
        {
            mc = list[0];
        }

        return true;
    }

    internal static bool HandleCommand(string input, CommandCaller caller)
    {
        int sep = input.IndexOf(' ');
        string name = (sep >= 0 ? input[..sep] : input).ToLower();
        if (caller.CommandType != CommandType.Console)
        {
            if (name.Length == 0 || name[0] != '/')
                return false;
            name = name[1..];
        }

        if (!GetCommand(caller, name, out ModCommand? mc))
            return false;
        if (mc == null)
            return true;
        if (!mc.IsCaseSensitive)
            input = input.ToLower();
        string[] args = input.TrimEnd().Split(' ').Skip(1).ToArray();
        try
        {
            mc.Action(caller, input, args);
        }
        catch (Exception e)
        {
            if (e is UsageException { msg: not null } usage)
            {
                caller.Reply(usage.msg, usage.color);
            }
            else
            {
                if (e is not UsageException)
                    ItemHooks.Report($"{mc.Name}.Action", e);
                caller.Reply(Language.GetTextValue("BunnyLoader.CommandUsage", mc.Usage), ErrorColor);
            }
        }

        return true;
    }

    public static List<Tuple<string, string>> GetHelp(CommandType type)
    {
        var list = new List<Tuple<string, string>>();
        foreach (KeyValuePair<string, List<ModCommand>> entry in Commands)
        {
            List<ModCommand> matching = entry.Value.Where(mc => Matches(mc.Type, type)).ToList();
            foreach (ModCommand mc in matching)
                list.Add(Tuple.Create(matching.Count > 1 ? mc.Mod.Name + ":" + mc.Command : mc.Command, mc.Description));
        }

        return list;
    }
}
