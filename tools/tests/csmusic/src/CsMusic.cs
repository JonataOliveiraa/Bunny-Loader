using System;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsMusic;

public sealed class ToneBiome : ModBiome
{
    public static bool Active;

    public override int Music => MusicLoader.GetMusicSlot(Mod, "Music/Tone");

    public override SceneEffectPriority Priority => SceneEffectPriority.Event;

    public override bool IsBiomeActive(Player player) => Active;
}

public sealed class VanillaEffect : ModSceneEffect
{
    public static bool Active;

    public override int Music => MusicID.Boss1;

    public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

    public override bool IsSceneEffectActive(Player player) => Active;
}

public sealed class CsMusicMod : Mod
{
    static int frames, pass, fail, slot;
    static bool done;
    static float savedVolume = -1;

    static ILog Log => ModContent.GetInstance<CsMusicMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (!done) return;
        if (savedVolume >= 0) Main.musicVolume = savedVolume;
        Log.Info($"csmusic END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        var mod = ModContent.GetInstance<CsMusicMod>();
        switch (f)
        {
            case 20:
                slot = MusicLoader.GetMusicSlot(mod, "Music/Tone");
                Report("GetMusicSlot gives a slot after the game's music and keeps it",
                    slot >= MusicID.Count && slot < MusicLoader.MusicCount && MusicLoader.GetMusicSlot(mod, "Music/Tone") == slot && MusicLoader.GetMusicSlot("CsMusic/Music/Tone") == slot,
                    $"slot={slot} count={MusicLoader.MusicCount}");
                Report("MusicExists finds the file with or without the extension, and a missing one gives 0",
                    MusicLoader.MusicExists(mod, "Music/Tone") && MusicLoader.MusicExists(mod, "Music/Tone.ogg") && !MusicLoader.MusicExists(mod, "Music/None") && MusicLoader.GetMusicSlot(mod, "Music/None") == 0,
                    "");
                if (Main.musicVolume < 0.2f)
                {
                    savedVolume = Main.musicVolume;
                    Main.musicVolume = 0.5f;
                }
                ToneBiome.Active = true;
                break;
            case 260:
                Report("the biome's mod music plays and the game's music is silenced",
                    MusicLoader.IsMusicPlaying(slot) && Main.newMusic == Main.curMusic && Main.musicFade[0] == 1f,
                    $"playing={MusicLoader.IsMusicPlaying(slot)} new={Main.newMusic} cur={Main.curMusic} fade0={Main.musicFade[0]}");
                VanillaEffect.Active = true;
                break;
            case 300:
                Report("a higher priority scene with a vanilla id asks the game for it", Main.newMusic == MusicID.Boss1, $"new={Main.newMusic}");
                VanillaEffect.Active = false;
                ToneBiome.Active = false;
                break;
            case 700:
                Report("leaving the biome fades the mod music out", !MusicLoader.IsMusicPlaying(slot), $"playing={MusicLoader.IsMusicPlaying(slot)} new={Main.newMusic}");
                done = true;
                break;
        }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csmusic {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
