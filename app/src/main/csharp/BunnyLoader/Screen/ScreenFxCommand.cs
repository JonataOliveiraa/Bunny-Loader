using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace BunnyLoader;

internal sealed class ScreenFxCommand : ModCommand
{
    static readonly Color ErrorColor = new(255, 25, 25, 255);

    public override string Command => "fx";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/fx list | spawn <Effect> [radius] [ticks] | kill [key|Effect] | reload | watch | debug | on | off | stats";

    public override string Description => Text("FxDescription");

    internal static void Install() => ItemHooks.Once("ScreenFxCommand", () => CommandLoader.Add(new ScreenFxCommand()));

    static string Text(string key, params object[] args) => Language.GetTextValue("BunnyLoader." + key, args);

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        void Say(string key, params object[] values) => caller.Reply(Text(key, values));
        string what = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
        switch (what)
        {
            case "list":
            {
                var active = ScreenEffects.Active;
                if (active.Count == 0)
                {
                    Say("FxNone");
                    return;
                }
                foreach (ModScreenEffect fx in active) caller.Reply(ScreenDebug.Describe(fx));
                return;
            }
            case "spawn":
            {
                Type? type = args.Length > 1 ? ScreenEffects.Classes.FirstOrDefault(t => t.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase)) : null;
                if (type == null)
                {
                    caller.Reply(Text("FxUnknown", string.Join(", ", ScreenEffects.Classes.Select(t => t.Name))), ErrorColor);
                    return;
                }
                float radius = args.Length > 2 && float.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) && r > 0 ? r : -1;
                float ticks = args.Length > 3 && float.TryParse(args[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t) && t >= 0 ? t : -1;
                ScreenEffects.Spawn(type, caller.Player, fx =>
                {
                    if (radius > 0) fx.Radius = radius;
                    if (ticks >= 0) fx.Duration = ticks;
                });
                Say("FxSpawned", type.Name);
                return;
            }
            case "kill":
            {
                string? target = args.Length > 1 ? args[1].ToLowerInvariant() : null;
                int killed = 0;
                foreach (ModScreenEffect fx in ScreenEffects.Active)
                {
                    string key = fx.Key?.ToLowerInvariant() ?? "";
                    if (target == null || target == "all" || key == target || fx.GetType().Name.ToLowerInvariant() == target)
                    {
                        fx.Kill();
                        killed++;
                    }
                }
                Say("FxKilled", killed);
                return;
            }
            case "reload":
            {
                int files = ScreenGraphics.Shader.Files.Count;
                Say("FxReloaded", ScreenEffects.Reload(), files);
                return;
            }
            case "watch":
                ScreenEffects.HotReload = !ScreenEffects.HotReload;
                Say(ScreenEffects.HotReload ? "FxWatchOn" : "FxWatchOff");
                return;
            case "debug":
                ScreenEffects.Debug = !ScreenEffects.Debug;
                Say(ScreenEffects.Debug ? "FxDebugOn" : "FxDebugOff");
                return;
            case "on":
            case "off":
                ScreenEffectsSetting.Set(what == "on");
                Say(what == "on" ? "FxOn" : "FxOff");
                return;
            case "stats":
            {
                ScreenStats s = ScreenEffects.Stats;
                Say("FxStats", s.GpuSamples > 0 ? $"{s.GpuAverageMs:0.00} ms" : "-", s.Passes, s.Frames, s.MarkerReady ? "ok" : "-");
                return;
            }
            default:
                throw new UsageException();
        }
    }
}
