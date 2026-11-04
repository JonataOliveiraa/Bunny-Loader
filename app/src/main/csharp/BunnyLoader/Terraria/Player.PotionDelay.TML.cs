using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct Player
{
    public ref StatModifier PotionDelayModifier => ref PlayerPotionDelayHooks.Get(this);
}
