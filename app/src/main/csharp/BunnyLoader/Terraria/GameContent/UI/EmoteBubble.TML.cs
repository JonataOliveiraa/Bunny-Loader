using Terraria.ModLoader;

namespace Terraria.GameContent.UI;

public readonly partial struct EmoteBubble
{
    public ModEmoteBubble? ModEmoteBubble => EmoteBubbleLoader.InstanceOf(this);
}
