using Terraria.ModLoader;

namespace ExampleMod.Content.Walls
{
    public class ExampleGlobalWall : GlobalWall
    {
        public override bool WallFrame(int i, int j, int type, bool randomizeFrame, ref int style, ref int frameNumber) {
            return base.WallFrame(i, j, type, randomizeFrame, ref style, ref frameNumber);
        }
    }
}
