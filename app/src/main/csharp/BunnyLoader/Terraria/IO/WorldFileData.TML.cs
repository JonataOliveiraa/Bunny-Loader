using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace Terraria.IO;

public readonly partial struct WorldFileData
{
    public bool TryGetHeaderData(ModSystem system, out TagCompound data) => SystemHooks.TryGetHeaderData(this, system, out data);

    public bool TryGetHeaderData<T>(out TagCompound data) where T : ModSystem => TryGetHeaderData(ModContent.GetInstance<T>(), out data);
}
