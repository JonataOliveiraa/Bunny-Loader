using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.Systems;

public class DownedBossSystem : ModSystem
{
    public static bool downedMinionBoss = false;

    public override void ClearWorld()
    {
        downedMinionBoss = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (downedMinionBoss)
        {
            tag["downedMinionBoss"] = true;
        }
    }

    public override void LoadWorldData(TagCompound tag)
    {
        downedMinionBoss = tag.ContainsKey("downedMinionBoss");
    }
}
