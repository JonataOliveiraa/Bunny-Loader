using System.Collections.Generic;

namespace ExampleMod.Content.CustomModType;

public class VictoryPoseID
{
    public static readonly Dictionary<string, int> Search = new();

    public static class Sets
    {
        public static bool[] NonBoss = new bool[VictoryPoseLoader.VictoryPoses.Count];
    }
}
