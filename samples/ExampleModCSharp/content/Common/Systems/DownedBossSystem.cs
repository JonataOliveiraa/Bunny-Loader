using System.IO;
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

    public override void NetSend(BinaryWriter writer)
    {
        writer.WriteFlags(downedMinionBoss);
    }

    public override void NetReceive(BinaryReader reader)
    {
        reader.ReadFlags(out downedMinionBoss);
    }
}
