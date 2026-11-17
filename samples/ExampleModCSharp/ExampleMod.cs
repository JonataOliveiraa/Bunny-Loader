using Terraria.ModLoader;

namespace ExampleMod;

// This is a partial class, meaning some of its parts were split into other files. See ExampleMod.*.cs for other portions.
// The class is partial to organize similar code together to clarify what is related.
public partial class ExampleMod : Mod
{
    public const string AssetPath = $"{nameof(ExampleMod)}/Assets/";
    // The Load() method can be used for loading content and assets, but for organization reasons it is recommended that you instead use ModSystem/ModType/ILoadable Load() hooks.
}
