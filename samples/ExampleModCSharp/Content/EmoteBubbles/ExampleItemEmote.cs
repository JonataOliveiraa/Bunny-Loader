using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace ExampleMod.Content.EmoteBubbles;

public class ExampleItemEmote : ModEmoteBubble
{
    public override void SetStaticDefaults() => AddToCategory(EmoteID.Category.Items);
}
