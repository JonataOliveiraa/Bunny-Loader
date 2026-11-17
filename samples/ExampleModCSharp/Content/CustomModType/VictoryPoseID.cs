using System.Collections.Generic;

namespace ExampleMod.Content.CustomModType;

// Note: To fully understand this example, please start by reading https://github.com/tModLoader/tModLoader/tree/stable/ExampleMod/Content/CustomModType/README.md

/// <summary>
/// Manages content ID sets. We can use ID sets to store content-specific data.
/// </summary>
public class VictoryPoseID
{
    public static readonly Dictionary<string, int> Search = new();

    public static class Sets
    {
        public static bool[] NonBoss = new bool[VictoryPoseLoader.VictoryPoses.Count];
    }
}
