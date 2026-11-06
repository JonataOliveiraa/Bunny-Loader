using ExampleMod.Content.Items.Placeable;
using ExampleMod.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{
    public abstract class ExampleSandBallProjectile : ModProjectile
    {
        public override string Texture => "ExampleMod/Content/Projectiles/ExampleSandBallProjectile";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.FallingBlockDoesNotFallThroughPlatforms[Type] = true;
            ProjectileID.Sets.ForcePlateDetection[Type] = true;
        }
    }

    public class ExampleSandBallFallingProjectile : ExampleSandBallProjectile
    {
        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            ProjectileID.Sets.FallingBlockTileItem[Type] = new(ModContent.TileType<ExampleSand>(), ModContent.ItemType<ExampleSandBlock>());
        }

        public override void SetDefaults() {
            Projectile.CloneDefaults(ProjectileID.EbonsandBallFalling);
        }
    }

    public class ExampleSandBallGunProjectile : ExampleSandBallProjectile
    {
        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            ProjectileID.Sets.FallingBlockTileItem[Type] = new(ModContent.TileType<ExampleSand>());
        }

        public override void SetDefaults() {
            Projectile.CloneDefaults(ProjectileID.EbonsandBallGun);
            AIType = ProjectileID.EbonsandBallGun;
        }
    }
}
