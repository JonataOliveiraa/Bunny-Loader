using System.Collections.Generic;

namespace Terraria.ModLoader;

public interface IEntityWithGlobals<TGlobal> where TGlobal : GlobalType
{
    IReadOnlyList<TGlobal> EntityGlobals { get; }
}
