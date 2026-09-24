// ExampleBulletProjectile: o tiro. Voa com a IA de flecha do jogo (aiStyle 1;
// o ExMod usa ProjAIStyleID.Arrow, outro enum dele que este Terraria nao tem),
// atravessa 5 inimigos e cada um so leva dano uma vez.
//
// Projetil ainda nao tem classe (ModProjectile): registra com bl.projectiles,
// e o tipo sai deste modulo para a bala que o dispara.
export const ExampleBulletProjectile = bl.projectiles.register({
    name: 'ExampleBulletProjectile',
    texture: 'Textures/Projectiles/ExampleBulletProjectile.png',
    displayName: { 'pt-BR': 'Bala de Exemplo', 'en-US': 'Example Bullet' },
    setDefaults(projectile) {
        projectile.width = 8;
        projectile.height = 8;
        projectile.scale = 1;
        projectile.aiStyle = 1;
        projectile.friendly = true;
        projectile.hostile = false;
        projectile.ranged = true;
        projectile.penetrate = 5;
        projectile.timeLeft = 600;
        projectile.light = 0.5;
        projectile.ignoreWater = true;
        projectile.tileCollide = true;
        projectile.extraUpdates = 1;
        projectile.usesLocalNPCImmunity = true;
        projectile.localNPCHitCooldown = -1;
    },
});
