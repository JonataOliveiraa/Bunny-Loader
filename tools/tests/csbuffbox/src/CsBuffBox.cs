using System;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBuffBox;

public sealed class CsBuffBoxMod : Mod
{
    static int frame, shots, draws, tooltips, descriptions, changes, names, overs, zooms;
    static bool showing;

    static ILog Log => ModContent.GetInstance<CsBuffBoxMod>().Logger;

    static string Flat(string? s) => (s ?? "null").Replace("\n", "\n");

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.MouseText_DrawBuffTooltip += (On_Main.orig_MouseText_DrawBuffTooltip orig, Main self, string? cursorText, string? buffString, ref int X, ref int Y) =>
        {
            if (showing && ++draws <= 3) Log.Info($"csbuffbox draw cursorText=[{Flat(cursorText)}] buffString=[{Flat(buffString)}] X={X} Y={Y}");
            orig(self, cursorText, buffString, ref X, ref Y);
        };
        On_Main.GetBuffTooltip += (orig, player, type) =>
        {
            string? tip = orig(player, type);
            if (showing && ++tooltips <= 3) Log.Info($"csbuffbox GetBuffTooltip {type} -> [{Flat(tip)}]");
            return tip;
        };
        On_Lang.GetBuffDescription += (orig, id) =>
        {
            string? tip = orig(id);
            if (showing && ++descriptions <= 3) Log.Info($"csbuffbox GetBuffDescription {id} -> [{Flat(tip)}]");
            return tip;
        };
        On_Lang.GetBuffName += (orig, id) =>
        {
            string? name = orig(id);
            if (showing && ++names <= 3) Log.Info($"csbuffbox GetBuffName {id} -> [{Flat(name)}]");
            return name;
        };
        On_GUIBuffs.ItemOver += (orig, self, index) =>
        {
            if (showing && ++overs <= 3) Log.Info($"csbuffbox ItemOver {index} buffString=[{Flat(Main.buffString)}]");
            orig(self, index);
        };
        On_Main.MouseTextHackZoom_string_string += (orig, self, text, buffTooltip) =>
        {
            if (showing && ++zooms <= 3) Log.Info($"csbuffbox HackZoom text=[{Flat(text)}] tip=[{Flat(buffTooltip)}]");
            orig(self, text, buffTooltip);
        };
        On_Main.MouseTextHackZoom_string_int_byte_string += (orig, self, text, rarity, diff, buffTooltip) =>
        {
            if (showing && ++zooms <= 3) Log.Info($"csbuffbox HackZoom4 text=[{Flat(text)}] tip=[{Flat(buffTooltip)}]");
            orig(self, text, rarity, diff, buffTooltip);
        };
        On_GUIBuffs.ChangeBuffString += (orig, self, type) =>
        {
            if (showing && ++changes <= 3) Log.Info($"csbuffbox ChangeBuffString {type} before=[{Flat(Main.buffString)}]");
            orig(self, type);
            if (showing && changes <= 3) Log.Info($"csbuffbox ChangeBuffString {type} after=[{Flat(Main.buffString)}]");
        };
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame < 0) return;
        frame++;
        try
        {
            if (frame == 30)
            {
                for (int k = 0; k < self.buffType.Length; k++) self.buffType[k] = 0;
                showing = true;
                self.AddBuff(BuffID.Poisoned, 5 * 60 * 60);
            }
            if (frame == 80)
            {
                Log.Info($"csbuffbox direct tooltip=[{Flat(Main.GetBuffTooltip(self, BuffID.Poisoned))}] description=[{Flat(Lang.GetBuffDescription(BuffID.Poisoned))}] name=[{Flat(Lang.GetBuffName(BuffID.Poisoned))}]");
                Log.Info($"csbuffbox after direct calls tooltips={tooltips} descriptions={descriptions} names={names}");
            }
            if (frame == 90) Log.Info($"csbuffbox FHOLD {++shots} 0.136 0.031 4000");
            if (frame == 1400) Log.Info($"csbuffbox SHOT {++shots} the buff tooltip box");
            if (frame == 1500)
            {
                showing = false;
                Log.Info($"csbuffbox counts draws={draws} tooltips={tooltips} descriptions={descriptions} changes={changes} names={names} overs={overs} zooms={zooms} buffString=[{Flat(Main.buffString)}]");
                frame = -1;
                Log.Info("csbuffbox END pass=1 fail=0");
            }
        }
        catch (Exception e)
        {
            frame = -1;
            Log.Info($"csbuffbox END pass=0 fail=1 {e}");
        }
    }
}
