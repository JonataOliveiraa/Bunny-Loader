using BunnyLoader.Hooks;

namespace Terraria.ModLoader.Core;

internal static class NetGates
{
    internal static void OnlineSendData()
    {
        HookGates.Define<On_NetMessage.hook_SendData>(() => HookGates.Static(Main.Class, "netMode"));
        HookGates.Want<On_NetMessage.hook_SendData>();
    }
}
