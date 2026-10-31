using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsNpcHit;

public sealed class CsNpcHitMod : Mod
{
    public override void Load()
    {
        On_Player.Update += Update;
        On_NetMessage.SendData += SendData;
    }

    static void Update(On_Player.orig_Update orig, Player player, int index)
    {
        orig(player, index);
        if (Main.gameMenu) Checks.Reset();
        else if (index == Main.myPlayer) Checks.Frame(player);
    }

    static void SendData(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, NetworkText text,
        int number, float number2, float number3, float number4, int number5, int number6, int number7)
    {
        if (msgType == 28) { Checks.RawPackets++; Checks.RawDamage = (int)number2; Checks.RawCrit = number5; }
        orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
    }
}
