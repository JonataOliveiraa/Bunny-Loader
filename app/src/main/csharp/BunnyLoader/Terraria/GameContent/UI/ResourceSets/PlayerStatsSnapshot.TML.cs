using System;

namespace Terraria.GameContent.UI.ResourceSets;

public partial struct PlayerStatsSnapshot
{
    public PlayerStatsSnapshot(Player player) => this = New(player);

    public int AmountOfLifeHearts => LifePerSegment > 0f ? Math.Clamp((int)Math.Ceiling(LifeMax / LifePerSegment), 0, 20) : 0;

    public int AmountOfManaStars => ManaPerSegment > 0f ? Math.Clamp((int)Math.Ceiling(ManaMax / ManaPerSegment), 0, 20) : 0;
}
