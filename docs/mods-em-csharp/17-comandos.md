# Chat commands

A `ModCommand` is written as in tModLoader: the name typed after `/`, who may
run it and what it does.

```csharp
using Terraria;
using Terraria.ModLoader;

public class HealCommand : ModCommand
{
    public override string Command => "heal";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/heal [amount]";

    public override string Description => "Heals the player";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        int amount = 100;
        if (args.Length > 0 && !int.TryParse(args[0], out amount))
            throw new UsageException("The amount must be a number.");
        caller.Player.Heal(amount);
        caller.Reply($"Healed {amount}.");
    }
}
```

The player types `/heal 50` in the game's chat. `args` has the words after the
name, lowercased unless `IsCaseSensitive` is true; `input` is the whole line.

## Who runs it

| `Type` | Runs |
|---|---|
| `Chat` | on the device of whoever typed it, in single player and in multiplayer |
| `Server` | on the host, for any player who types it (not in single player) |
| `World` | `Chat` in single player, `Server` in multiplayer: the commands that change the world |
| `Console` | kept for tModLoader code; the phone has no server console |

`caller.Player` is the player who typed it, and `caller.Reply(text, color)`
answers in that player's chat (from the host, it goes to that player only).

## Errors and help

- `throw new UsageException("message")` answers with the message, in red (or
  the color given). Without a message, or with any other exception, the
  player sees `Usage: <Usage>`.
- The game's `/help` lists the mod commands after its own, with the
  `Description`. `CommandLoader.GetHelp(CommandType)` gives the same list.
- Two mods with the same command: the player picks one with
  `/<Mod>:<command>` (the mod's internal name, in any case); typing just
  `/<command>` lists the choices.

Example: `samples/ExampleModCSharp/Common/Commands` (`/item`, `/addTime` and
`/setSomeNumber`, ported from tModLoader). Test pack: `tools/tests/csmodcommand`.
