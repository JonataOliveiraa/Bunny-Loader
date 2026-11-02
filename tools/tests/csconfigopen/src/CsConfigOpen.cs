using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CsConfigOpen;

public sealed class LongConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public bool First;
    public bool Second;
    public bool Third;
    public bool Fourth;
    public bool Fifth;
    public bool Sixth;
    public bool Seventh;
    public bool Eighth;

    [Header("Target")]
    [DefaultValue(true)]
    public bool TargetOption;

    public bool Ninth;
    public bool Tenth;
    public bool Eleventh;
    public bool Twelfth;
}

public sealed class CsConfigOpenMod : Mod
{
    static int frames;
    static bool opened;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || opened || ++frames < 60)
            return;
        opened = true;
        var mod = ModContent.GetInstance<CsConfigOpenMod>();
        ModContent.GetInstance<LongConfig>().Open(onClose: () => mod.Logger.Info($"csconfigopen closed ingameOptions={Main.ingameOptionsWindow}"), scrollToOption: nameof(LongConfig.TargetOption));
        mod.Logger.Info($"csconfigopen opened ingameOptions={Main.ingameOptionsWindow}");
        mod.Logger.Info("csconfigopen END pass=1 fail=0");
    }
}
