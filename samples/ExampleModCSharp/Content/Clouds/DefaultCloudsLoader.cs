using Terraria.ModLoader;

namespace ExampleMod.Content.Clouds;

public class DefaultCloudsLoader : ICustomAutoload
{
    public static void Autoload(Mod mod)
    {
        CloudLoader.AddCloudFromTexture(mod, "ExampleMod/Content/Clouds/ExampleCloud", spawnChance: 0.1f, rareCloud: false);
    }
}
