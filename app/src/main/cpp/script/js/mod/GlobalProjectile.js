class GlobalProjectile extends GlobalType {
    SetDefaults(projectile) {}
    OnSpawn(projectile, source) {}
    PreAI(projectile) { return true; }
    AI(projectile) {}
    PostAI(projectile) {}
    PreKill(projectile, timeLeft) { return true; }
    OnKill(projectile, timeLeft) {}
    OnHitNPC(projectile, target) {}
    OnHitPlayer(projectile, target) {}
    NetSend(projectile, writer) {}
    NetReceive(projectile, reader) {}

    static register(cls) {
        const inst = globalProjectiles.Register(cls, 'GlobalProjectile');
        GlobalProjectileLoader.Hook(cls);
        return inst;
    }
}
