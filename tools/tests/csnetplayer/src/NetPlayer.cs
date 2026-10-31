using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CsNetPlayer;

public class NetPlayer : ModPlayer
{
    public static readonly HashSet<int> Connects = new(), Disconnects = new();
    public static int NewPlayerSyncs, HostSyncs;

    public int Value, Received, Sends;

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode == 3 && Player.whoAmI != Main.myPlayer) HostSyncs++;
        ModPacket packet = Mod.GetPacket();
        packet.Write(CsNetPlayerMod.Sync);
        packet.Write((byte)Player.whoAmI);
        packet.Write(Value);
        packet.Write(newPlayer);
        packet.Send(toWho, fromWho);
    }

    public override void CopyClientState(ModPlayer targetCopy) => ((NetPlayer)targetCopy).Value = Value;

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if (((NetPlayer)clientPlayer).Value == Value) return;
        Sends++;
        SyncPlayer(-1, Main.myPlayer, false);
    }

    public override void PlayerConnect() => Connects.Add(Player.whoAmI);

    public override void PlayerDisconnect() => Disconnects.Add(Player.whoAmI);
}
