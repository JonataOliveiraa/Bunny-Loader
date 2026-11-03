using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria.GameContent.UI;

public readonly partial struct EmoteBubble
{
    public ModEmoteBubble? ModEmoteBubble => EmoteBubbleLoader.InstanceOf(this);

    public static void MakePlayerEmote(Player player, int emoteId, bool syncBetweenClients = true)
    {
        if (Main.netMode == NetmodeID.Server || Main.netMode == NetmodeID.SinglePlayer || !syncBetweenClients || emoteId >= EmoteID.Count)
        {
            NewBubble(emoteId, WorldUIAnchor.New(player), 360);
            CheckForNPCsToReactToEmoteBubble(emoteId, player);
        }
        else
        {
            NetMessage.SendData(MessageID.Emoji, -1, -1, default, player.whoAmI, emoteId);
        }
    }
}
