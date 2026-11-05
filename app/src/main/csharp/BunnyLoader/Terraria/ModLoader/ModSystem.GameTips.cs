using System.Collections.Generic;

namespace Terraria.ModLoader;

public abstract partial class ModSystem
{
    public virtual void ModifyGameTipVisibility(IReadOnlyList<GameTipData> gameTips) { }
}
