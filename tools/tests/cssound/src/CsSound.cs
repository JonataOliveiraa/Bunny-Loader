using System;
using log4net;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CsSound;

public sealed class BeepItem : ModItem
{
    public override string Texture => "BeepItem";

    public override void SetDefaults()
    {
        Item.width = Item.height = 20;
        Item.useStyle = 1;
        Item.useTime = Item.useAnimation = 20;
        Item.UseSound = CsSoundMod.Beep;
    }
}

public sealed class CsSoundMod : Mod
{
    internal static readonly SoundStyle Beep = new("CsSound/Sounds/Beep") { Volume = 0.8f, PitchVariance = 0.1f };
    static readonly SoundStyle Single = new("CsSound/Sounds/Beep") { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, Volume = 0.5f };
    static readonly SoundStyle Variants = new("CsSound/Sounds/Beep", 3);
    static readonly SoundStyle Missing = new("CsSound/Sounds/Missing");

    static int frames, pass, fail, markers;
    static bool done, counting;

    static ILog Log => ModContent.GetInstance<CsSoundMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(self, ++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"cssound END pass={pass} fail={fail}");
    }

    static void Step(Player p, int f)
    {
        switch (f)
        {
            case 10:
                Styles();
                LegacySoundStyle warm = Beep;
                warm = Single;
                warm = Variants;
                warm = Missing;
                On_LegacySoundPlayer.PlaySound += (orig, self, type, x, y, style, volumeScale, pitchOffset) =>
                {
                    if (counting && type == 1000) markers++;
                    return orig(self, type, x, y, style, volumeScale, pitchOffset);
                };
                break;
            case 90:
                Playing(p);
                break;
            case 100:
                {
                    SlotId first = SoundEngine.PlaySound(Single);
                    SlotId second = SoundEngine.PlaySound(Single);
                    Report("MaxInstances 1 with IgnoreNew refuses a second play while the first is on", first.Value > 0 && second.Value == 0, $"{first.Value} {second.Value}");
                    done = true;
                    break;
                }
        }
    }

    static void Styles()
    {
        var plain = new SoundStyle("CsSound/Sounds/Beep");
        Report("tModLoader's defaults: volume 1, one instance, replace the oldest, sound type",
            plain.Volume == 1f && plain.MaxInstances == 1 && plain.SoundLimitBehavior == SoundLimitBehavior.ReplaceOldest && plain.Type == SoundType.Sound && plain.SoundPath == "CsSound/Sounds/Beep",
            $"{plain.Volume} {plain.MaxInstances} {plain.SoundLimitBehavior} {plain.Type}");
        var range = plain with { PitchRange = (-0.4f, 0.2f) };
        Report("PitchRange sets Pitch and PitchVariance", Math.Abs(range.Pitch + 0.1f) < 1e-4f && Math.Abs(range.PitchVariance - 0.6f) < 1e-4f, $"{range.Pitch} {range.PitchVariance}");
        Report("WithVolumeScale and WithPitchOffset; volume is clamped", plain.WithVolumeScale(0.5f).Volume == 0.5f && plain.WithPitchOffset(0.3f).Pitch == 0.3f && (plain with { Volume = 3f }).Volume == 1f, "");
        Report("numberOfVariants gives the suffixes 1..n", Variants.Variants.Length == 3 && Variants.Variants[0] == 1 && Variants.Variants[2] == 3, $"{Variants.Variants.Length}");
        LegacySoundStyle a = Beep, b = new SoundStyle("CsSound/Sounds/Beep") { Volume = 0.8f, PitchVariance = 0.1f }, c = Single;
        Report("a mod style becomes one marker per path and options", a.SoundId == 1000 && a.Pointer == b.Pointer && a.Pointer != c.Pointer, $"id={a.SoundId}");
        var item = Item.New();
        item.SetDefaults(ModContent.ItemType<BeepItem>(), default);
        Report("Item.UseSound takes a mod SoundStyle", item.UseSound.SoundId == 1000 && item.UseSound.Pointer == a.Pointer, $"id={item.UseSound.SoundId}");
    }

    static void Playing(Player p)
    {
        Report("the sound volume is on", Main.soundVolume > 0, $"{Main.soundVolume}");
        SlotId here = SoundEngine.PlaySound(Beep, p.Center);
        Report("PlaySound with a position on screen is accepted", here.Value > 0, $"{here.Value}");
        SlotId far = SoundEngine.PlaySound(Beep, p.Center + new Vector2(100000, 0));
        Report("PlaySound beyond the hearing distance plays nothing", far.Value == 0, $"{far.Value}");
        SlotId variant = SoundEngine.PlaySound(Variants);
        Report("a style with variants plays one of them", variant.Value > 0, $"{variant.Value}");
        SlotId missing = SoundEngine.PlaySound(Missing);
        Report("a missing file plays nothing and does not throw", missing.Value == 0, $"{missing.Value}");
        counting = true;
        var item = Item.New();
        item.SetDefaults(ModContent.ItemType<BeepItem>(), default);
        SoundEngine.PlaySound(item.UseSound, p.Center);
        SoundEngine.PlaySound(item.UseSound);
        counting = false;
        Report("the game's SoundEngine routes the marker to the mod sound", markers == 2, $"markers={markers}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cssound {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
