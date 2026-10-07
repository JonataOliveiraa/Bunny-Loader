class GlobalProjectile extends GlobalType {
    SetDefaults(projectile) {}
    OnSpawn(projectile, source) {}
    PreAI(projectile) { return true; }
    AI(projectile) {}
    PostAI(projectile) {}
    PreKill(projectile, timeLeft) { return true; }
    OnKill(projectile, timeLeft) {}
    // Golpes (como no ModProjectile).
    CanHitNPC(projectile, target) { return null; }
    ModifyHitNPC(projectile, target, modifiers) {}
    OnHitNPC(projectile, target, hit, damageDone) {}
    CanHitPvp(projectile, target) { return true; }
    CanHitPlayer(projectile, target) { return true; }
    ModifyHitPlayer(projectile, target, modifiers) {}
    OnHitPlayer(projectile, target, info) {}
    PreDraw(projectile, lightColor) { return true; }
    PostDraw(projectile, lightColor) {}
    GetAlpha(projectile, lightColor) { return undefined; }
    Colliding(projectile, projHitbox, targetHitbox) { return undefined; }
    CanDamage(projectile) { return undefined; }
    ModifyDamageHitbox(projectile, hitbox) {}
    OnTileCollide(projectile, oldVelocity) { return true; }
    TileCollideStyle(projectile, width, height, fallThrough, hitboxCenterFrac) { return true; }
    MinionContactDamage(projectile) { return false; }
    CanCutTiles(projectile) { return undefined; }
    CutTiles(projectile) {}
    CanUseGrapple(type, player) { return undefined; }
    UseGrapple(player, type) {}
    GrappleCanLatchOnTo(projectile, player, tile) { return undefined; }
    NetSend(projectile, writer) {}
    NetReceive(projectile, reader) {}

    static register(cls) {
        const inst = globalProjectiles.Register(cls, 'GlobalProjectile');
        GlobalProjectileLoader.Hook(cls);
        return inst;
    }
}
