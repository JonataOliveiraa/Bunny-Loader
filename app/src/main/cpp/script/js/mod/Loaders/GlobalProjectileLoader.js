class GlobalProjectileLoader {
    static Hook(cls) {
        const Pr = Terraria.Projectile;
        const registry = globalProjectiles;
        const has = (name) => Hooks.Overrides(cls, GlobalProjectile, name);

        if (has('NetSend') || has('NetReceive')) ModNet.InstallEntity(27);
        ProjectileLoader.HookGlobal(cls);

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
                    if (p.active) registry.Call(p, 'OnSpawn', source);
                    return i;
                });
        });

        if (has('PreAI') || has('AI') || has('PostAI')) Hooks.Once('gproj.AI', () => {
            Pr['void AI()'].hook((original, p) => {
                if (registry.AllCall(p, 'PreAI')) {
                    original(p);
                    registry.Call(p, 'AI');
                }
                registry.Call(p, 'PostAI');
            });
        });

        CombatLoader.WantProjectile(cls, GlobalProjectile);
    }
}
