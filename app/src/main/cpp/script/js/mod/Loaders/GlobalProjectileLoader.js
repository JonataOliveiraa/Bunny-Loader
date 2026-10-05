class GlobalProjectileLoader {
    static Hook(cls) {
        const Pr = Terraria.Projectile;
        const registry = globalProjectiles;
        const has = (name) => Hooks.Overrides(cls, GlobalProjectile, name);

        if (has('NetSend') || has('NetReceive')) ModNet.InstallEntity();

        if (has('SetDefaults') || registry.cached) Hooks.Once('gproj.SetDefaults', () => {
            Pr['void SetDefaults(int Type)'].hook((original, p, type) => {
                original(p, type);
                if (!(p.type > 0)) return;

                if (registry.cached) registry.Attach(p, true);
                DamageClassLoader.Defaulting(p, () => registry.Each(p, 'SetDefaults', (g) => g.SetDefaults(p)));
            });
        });

        if (has('OnSpawn')) Hooks.Once('gproj.OnSpawn', () => {
            Pr['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'].hook(
                (original, source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier) => {
                    const i = original(source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);
                    if (i < 0 || i >= 1000) return i;

                    const p = Terraria.Main.projectile[i];
                    if (p.active) registry.Each(p, 'OnSpawn', (g) => g.OnSpawn(p, source));
                    return i;
                });
        });

        if (has('PreAI') || has('AI') || has('PostAI')) Hooks.Once('gproj.AI', () => {
            Pr['void AI()'].hook((original, p) => {
                if (registry.All(p, 'PreAI', (g) => g.PreAI(p))) {
                    original(p);
                    registry.Each(p, 'AI', (g) => g.AI(p));
                }
                registry.Each(p, 'PostAI', (g) => g.PostAI(p));
            });
        });

        if (has('PreKill') || has('OnKill')) Hooks.Once('gproj.Kill', () => {
            Pr['void Kill()'].hook((original, p) => {
                if (!p.active) return original(p);
                if (ProjectileLoader.DefersTileCollisionKill(p)) return original(p);

                const timeLeft = p.timeLeft;
                if (!registry.All(p, 'PreKill', (g) => g.PreKill(p, timeLeft))) {
                    p.active = false;
                    return undefined;
                }

                registry.Each(p, 'OnKill', (g) => g.OnKill(p, timeLeft));
                return original(p);
            });
        });

        if (has('OnHitNPC')) HitLoader.ProjectileHitsNPC();

        if (has('OnHitPlayer')) Hooks.Once('gproj.OnHitPlayer', () => {
            Pr['void StatusPlayer(Player player)'].hook((original, p, player) => {
                original(p, player);
                registry.Each(p, 'OnHitPlayer', (g) => g.OnHitPlayer(p, player));
            });
        });
    }
}
