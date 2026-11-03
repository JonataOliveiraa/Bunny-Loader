using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Pets.ExamplePet;

public class ExamplePetProjectile : ModProjectile
{
    public override void SetStaticDefaults()
    {
        Main.projFrames[Type] = 4;
        Main.projPet[Type] = true;

        ProjectileID.Sets.CharacterPreviewAnimations[Type] = ProjectileID.Sets.SimpleLoop(0, Main.projFrames[Type], 6)
            .WithOffset(-10, -20f)
            .WithSpriteDirection(-1)
            .WithCode(DelegateMethods.CharacterPreview.Float);
    }

    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.ZephyrFish);

        AIType = ProjectileID.ZephyrFish;
    }

    public override bool PreAI()
    {
        Player player = Main.player[Projectile.owner];

        player.zephyrfish = false;

        return true;
    }

    public override void AI()
    {
        Player player = Main.player[Projectile.owner];

        if (!player.dead && player.HasBuff(ModContent.BuffType<ExamplePetBuff>()))
        {
            Projectile.timeLeft = 2;
        }
    }
}
