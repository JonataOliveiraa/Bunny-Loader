using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Ammo
{
    public class ExampleHellSolution : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 99;
            ItemID.Sets.SortingPriorityTerraforming[Type] = 101;
        }

        public override void SetDefaults() {
            Item.DefaultToSolution(ModContent.ProjectileType<ExampleHellSolutionProjectile>());
        }

        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup) {
            itemGroup = ContentSamples.CreativeHelper.ItemGroup.Solutions;
        }
    }

    public class ExampleHellSolutionProjectile : ModProjectile
    {
        public static int ConversionType;

        public ref float Progress => ref Projectile.ai[0];

        public bool ShotFromTerraformer => Projectile.ai[1] == 1f;

        public override void SetStaticDefaults() {
            ConversionType = ModContent.GetInstance<ExampleHellSolutionConversion>().Type;
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
                short dustType = Main.rand.NextBool() ? DustID.DirtSpray : DustID.CrimsonSpray;

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

    public class ExampleHellSolutionConversion : ModBiomeConversion
    {
        public override void PostSetupContent() {
            TileLoader.RegisterConversion(TileID.Grass, Type, HellifyGrass);
            TileLoader.RegisterConversion(TileID.GolfGrass, Type, HellifyGrass);

            TileLoader.RegisterConversion(TileID.Dirt, Type, TileID.Ash);
            TileLoader.RegisterConversion(TileID.Stone, Type, TileID.Ash);
            TileLoader.RegisterConversion(TileID.Sand, Type, TileID.Ash);

            TileLoader.RegisterConversion(TileID.ClayBlock, Type, TileID.Ash);

            WallLoader.RegisterConversion(WallID.DirtUnsafe, Type, HellifyWall);
            WallLoader.RegisterConversion(WallID.GrassUnsafe, Type, HellifyWall);
            WallLoader.RegisterConversion(WallID.Stone, Type, HellifyWall);
        }

        public bool HellifyGrass(int i, int j, int type, int conversionType) {
            int tileTypeAbove = -1;
            if (j > 1 && Main.tile[i, j - 1].HasTile)
                tileTypeAbove = Main.tile[i, j - 1].TileType;

            FindAndConvertTree(i, j, tileTypeAbove);

            if (tileTypeAbove == TileID.Plants ||
                tileTypeAbove == TileID.CorruptPlants ||
                tileTypeAbove == TileID.CrimsonPlants ||
                tileTypeAbove == TileID.HallowedPlants ||
                tileTypeAbove == TileID.Plants2 ||
                tileTypeAbove == TileID.HallowedPlants2) {
                Tile tileAbove = Main.tile[i, j - 1];

                if (j > 2 && (tileTypeAbove == TileID.Plants2 || tileTypeAbove == TileID.HallowedPlants2)) {
                    Tile tileTwiceAbove = Main.tile[i, j - 2];
                    if (tileTwiceAbove.TileType == tileTypeAbove) {
                        tileTwiceAbove.HasTile = false;
                        tileTwiceAbove.TileType = 0;
                    }

                    tileAbove.TileFrameY = 0;
                }

                tileAbove.TileType = TileID.AshPlants;

                tileAbove.TileFrameX = (short)(WorldGen.genRand.Next(10) * 18);
            }

            WorldGen.ConvertTile(i, j, TileID.AshGrass);
            return false;
        }

        public void FindAndConvertTree(int i, int j, int tileTypeAbove) {
            if (tileTypeAbove == -1)
                return;

            if (!TileID.Sets.IsATreeTrunk[tileTypeAbove])
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
                    if (!topTile.HasTile || !TileID.Sets.IsATreeTrunk[topTile.TileType])
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

            while (treeTop >= 0 && Main.tile[treeCenterX, treeTop].HasTile && TileID.Sets.IsATreeTrunk[Main.tile[treeCenterX, treeTop].TileType])
                treeTop--;

            for (int x = treeCenterX - 1; x < treeCenterX + 2; x++) {
                for (int y = treeTop; y < treeBottom; y++) {
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && TileID.Sets.IsATreeTrunk[t.TileType])
                        t.TileType = TileID.TreeAsh;
                }
            }

            for (int x = treeCenterX - 1; x < treeCenterX + 2; x++) {
                Tile t = Main.tile[x, treeBottom];
                if (t.HasTile && TileID.Sets.Conversion.Grass[t.TileType] || TileID.Sets.Conversion.GolfGrass[t.TileType])
                    t.TileType = TileID.AshGrass;
            }
        }

        public bool HellifyWall(int i, int j, int type, int conversionType) {
            WorldGen.ConvertWall(i, j, WallID.Lava1Echo + WorldGen.genRand.Next(3));
            return false;
        }
    }
}
