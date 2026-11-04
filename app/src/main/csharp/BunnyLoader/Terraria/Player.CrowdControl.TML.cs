using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct Player
{
    public void SetCCed() => PlayerCrowdControlHooks.Set(this);
}
