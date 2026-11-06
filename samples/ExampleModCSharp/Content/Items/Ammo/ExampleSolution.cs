using ExampleMod.Content.Dusts;
using ExampleMod.Content.Tiles;
using ExampleMod.Content.Tiles.Furniture;
using ExampleMod.Content.Walls;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Ammo
{
    public class ExampleSolution : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 99;
            ItemID.Sets.SortingPriorityTerraforming[Type] = 101;
        }

        public override void SetDefaults() {
            Item.DefaultToSolution(ModContent.ProjectileType<ExampleSolutionProjectile>());
        }

        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup) {
            itemGroup = ContentSamples.CreativeHelper.ItemGroup.Solutions;
        }
    }

    public class ExampleSolutionProjectile : ModProjectile
    {
        public static int ConversionType;

        public ref float Progress => ref Projectile.ai[0];

        public bool ShotFromTerraformer => Projectile.ai[1] == 1f;

        public override void SetStaticDefaults() {
            ConversionType = ModContent.GetInstance<ExampleSolutionConversion>().Type;
        }

        public override void SetDefaults() {
            Projectile.DefaultToSpray();
            Projectile.aiStyle = 0;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (Projectile.timeLeft > 133)
                Projectile.timeLeft = 133;

            if (Projectile.owner == Main.myPlayer) {
                int size = ShotFromTerraformer ? 3 : 2;
                Point tileCenter = Projectile.Center.ToTileCoordinates();
                WorldGen.Convert(tileCenter.X, tileCenter.Y, ConversionType, size);
            }

            int spawnDustTreshold = 7;
            if (ShotFromTerraformer)
                spawnDustTreshold = 3;

            if (Progress > (float)spawnDustTreshold) {
                float dustScale = 1f;
                int dustType = ModContent.DustType<ExampleSolutionDust>();

                if (Progress == spawnDustTreshold + 1)
                    dustScale = 0.2f;
                else if (Progress == spawnDustTreshold + 2)
                    dustScale = 0.4f;
                else if (Progress == spawnDustTreshold + 3)
                    dustScale = 0.6f;
                else if (Progress == spawnDustTreshold + 4)
                    dustScale = 0.8f;

                int dustArea = 0;
                if (ShotFromTerraformer) {
                    dustScale *= 1.2f;
                    dustArea = (int)(12f * dustScale);
                }

                Dust sprayDust = Dust.NewDustDirect(new Vector2(Projectile.position.X - dustArea, Projectile.position.Y - dustArea), Projectile.width + dustArea * 2, Projectile.height + dustArea * 2, dustType, Projectile.velocity.X * 0.4f, Projectile.velocity.Y * 0.4f, 100);
                sprayDust.noGravity = true;
                sprayDust.scale *= 1.75f * dustScale;
            }

            Progress++;
            Projectile.rotation += 0.3f * Projectile.direction;
        }
    }

    public class ExampleSolutionConversion : ModBiomeConversion
    {
        public static int WallType;
        public static int UnsafeWallType;
        public static int StoneType;
        public static int SandType;
        public static int ChairType;
        public static int WorkbenchType;

        public override void PostSetupContent() {
            WallType = ModContent.WallType<ExampleWall>();
            UnsafeWallType = ModContent.WallType<ExampleWallUnsafe>();
            StoneType = ModContent.TileType<ExampleBlock>();
            SandType = ModContent.TileType<ExampleSand>();
            ChairType = ModContent.TileType<ExampleChair>();
            WorkbenchType = ModContent.TileType<ExampleWorkbench>();

            for (int i = 0; i < WallLoader.WallCount; i++) {
                if (WallID.Sets.Conversion.Dirt[i] ||
                    WallID.Sets.Conversion.Grass[i] ||
                    WallID.Sets.Conversion.Stone[i] ||
                    WallID.Sets.Conversion.Sandstone[i] ||
                    WallID.Sets.Conversion.HardenedSand[i] ||
                    WallID.Sets.Conversion.Ice[i] ||
                    WallID.Sets.Conversion.NewWall1[i] ||
                    WallID.Sets.Conversion.NewWall2[i] ||
                    WallID.Sets.Conversion.NewWall3[i] ||
                    WallID.Sets.Conversion.NewWall4[i])
                    WallLoader.RegisterConversion(i, Type, ConvertWalls);
            }
            WallLoader.RegisterConversionFallback(WallType, WallID.Dirt, Type);
            WallLoader.RegisterConversionFallback(UnsafeWallType, WallID.DirtUnsafe, Type);

            TileLoader.RegisterSimpleConversion(TileID.Sand, Type, SandType);

            TileLoader.RegisterConversion(TileID.Stone, Type, ConvertStone);
            TileLoader.RegisterConversionFallback(StoneType, TileID.Stone, Type);

            TileLoader.RegisterConversion(TileID.Chairs, Type, ConvertChairs);
            TileLoader.RegisterConversion(TileID.WorkBenches, Type, ConvertWorkbenches);
        }

        public bool ConvertStone(int i, int j, int type, int conversionType) {
            int tileTypeAbove = -1;
            if (j > 1 && Main.tile[i, j - 1].HasTile)
                tileTypeAbove = Main.tile[i, j - 1].TileType;

            FindAndConvertTree(i, j, tileTypeAbove);

            WorldGen.ConvertTile(i, j, StoneType);
            return false;
        }

        public void FindAndConvertTree(int i, int j, int tileTypeAbove) {
            if (tileTypeAbove == -1)
                return;

            if (!TileID.Sets.CountsAsGemTree[tileTypeAbove])
                return;

            int treeBottom = j;
            int treeTop = treeBottom - 1;
            int treeCenterX = i;

            int treeFrameX = Main.tile[treeCenterX, treeTop].TileFrameX / 22;
            int treeFrameY = Main.tile[treeCenterX, treeTop].TileFrameY / 22;
            bool isTreeTrunk = (treeFrameX != 1 && treeFrameX != 2) || treeFrameY < 6;

            bool isTreeBranch = (treeFrameX == 3 && treeFrameY < 3) || (treeFrameX == 4 && treeFrameY >= 3 && treeFrameY < 6);
            if (isTreeBranch)
                return;

            if (!isTreeTrunk) {
                for (int x = treeCenterX - 1; x < treeCenterX + 2; x += 2) {
                    Tile topTile = Main.tile[x, treeTop];
                    if (!topTile.HasTile || !TileID.Sets.CountsAsGemTree[topTile.TileType])
                        continue;

                    treeFrameX = topTile.TileFrameX / 22;
                    treeFrameY = topTile.TileFrameY / 22;
                    isTreeTrunk = (treeFrameX != 1 && treeFrameX != 2) || treeFrameY < 6;

                    if (isTreeTrunk) {
                        treeCenterX = x;
                        break;
                    }
                }
            }

            while (treeTop >= 0 && Main.tile[treeCenterX, treeTop].HasTile && TileID.Sets.CountsAsGemTree[Main.tile[treeCenterX, treeTop].TileType])
                treeTop--;

            for (int x = treeCenterX - 1; x < treeCenterX + 2; x++) {
                for (int y = treeTop; y < treeBottom; y++) {
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && TileID.Sets.CountsAsGemTree[t.TileType])
                        t.TileType = TileID.Trees;
                }
            }

            for (int x = treeCenterX - 1; x < treeCenterX + 2; x++) {
                Tile t = Main.tile[x, treeBottom];
                if (t.HasTile && t.TileType == TileID.Stone)
                    t.TileType = (ushort)StoneType;
            }
        }

        public bool ConvertChairs(int i, int j, int type, int conversionType) {
            if (Main.tile[i, j + 1].HasTile && Main.tile[i, j + 1].TileType == TileID.Chairs)
                j++;

            Tile tileTop = Main.tile[i, j - 1];
            Tile tileBottom = Main.tile[i, j];

            tileTop.TileType = (ushort)ChairType;

            tileTop.TileFrameY = 0;
            tileBottom.TileFrameY = 18;

            WorldGen.ConvertTile(i, j, ChairType);
            return false;
        }

        public bool ConvertWorkbenches(int i, int j, int type, int conversionType) {
            if (Main.tile[i + 1, j].HasTile && Main.tile[i + 1, j].TileType == TileID.WorkBenches)
                i++;

            Tile tileLeft = Main.tile[i - 1, j];
            Tile tileRight = Main.tile[i, j];

            tileLeft.TileType = (ushort)WorkbenchType;

            tileLeft.TileFrameX = 0;
            tileRight.TileFrameX = 18;

            WorldGen.ConvertTile(i, j, WorkbenchType);
            return false;
        }

        public bool ConvertWalls(int i, int j, int type, int conversionType) {
            int wallType = Main.wallHouse[type] ? WallType : UnsafeWallType;
            WorldGen.ConvertWall(i, j, wallType);
            return false;
        }
    }
}
