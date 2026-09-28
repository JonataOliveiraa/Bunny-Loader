class ProjectileLoader {
    static CLONED_FIELDS = [
        'width', 'height', 'ownerHitCheckDistance', 'counterweight', 'sentry', 'arrow', 'bobber',
        'numHits', 'netImportant', 'manualDirectionChange', 'decidesManualFallThrough',
        'shouldFallThrough', 'bannerIdToRespondTo', 'stopsDealingDamageAfterPenetrateHits',
        'localNPCHitCooldown', 'idStaticNPCHitCooldown', 'usesLocalNPCImmunity',
        'usesIDStaticNPCImmunity', 'usesOwnerMeleeHitCD', 'appliesImmunityTimeOnSingleHits',
        'noDropItem', 'minion', 'minionSlots', 'soundDelay', 'spriteDirection', 'melee', 'ranged',
        'magic', 'ownerHitCheck', 'drawLayer', 'usesOwnerLight', 'hide', 'ignoreWater', 'hostile',
        'reflected', 'extraUpdates', 'light', 'penetrate', 'tileCollide', 'aiStyle', 'alpha',
        'rotation', 'scale', 'timeLeft', 'friendly', 'damage', 'originalDamage', 'knockBack',
        'coldDamage', 'noEnchantments', 'noEnchantmentVisuals', 'trap', 'npcProj',
        'tagEffectType', 'bonusTagDamage', 'armorPenetration', 'bonusCritChance',
    ];

    static ByType = new Map();

    static Of(proj) {
        return Entities.InstanceOf(proj, 'ModProjectile', ProjectileLoader.ByType);
    }

    static Hook(cls) {
        const Pr = Terraria.Projectile;
        const has = (name) => Hooks.Overrides(cls, ModProjectile, name);
        const self = { minType: FIRST_PROJECTILE };
        const of = ProjectileLoader.Of;

        if (has('SendExtraAI') || has('ReceiveExtraAI')) ModNet.InstallEntity();

        // Sempre: o AIType e o OnSpawn podem vir de qualquer projétil.
        Hooks.Once('proj.AI', () => {
            Pr['void AI()'].hook((original, p) => {
                const m = of(p);
                if (!m) return original(p);

                const n = m.constructor.name;
                if (!m.__spawned) {
                    m.__spawned = true;
                    Safe.Run(n + '.OnSpawn', () => m.OnSpawn(p));
                }

                if (Safe.Run(n + '.PreAI', () => m.PreAI(p)) !== false) {
                    const aiType = m.AIType | 0;
                    if (aiType > 0) {
                        const type = p.type;
                        p.type = aiType;
                        try {
                            original(p);
                        } finally {
                            p.type = type;
                        }
                    } else {
                        original(p);
                    }
                    Safe.Run(n + '.AI', () => m.AI(p));
                }

                Safe.Run(n + '.PostAI', () => m.PostAI(p));
            }, self);
        });

        // O jogo recalcula o dano do lacaio e da sentinela a partir do
        // originalDamage, que ele só preenche nos tipos dele.
        Hooks.Once('proj.OriginalDamage', () => {
            Pr['void ApplyStatsFromSource(IEntitySource spawnSource)'].hook((original, p, source) => {
                original(p, source);
                if (!of(p) || p.originalDamage !== 0) return;

                const item = source && 'Item' in source ? source.Item : undefined;
                p.originalDamage = item && item.damage >= 0 ? item.damage : p.damage;
            }, self);
        });

        // Um Kill dentro do HandleMovement é o choque com bloco. O jogo já
        // empurrou o projétil mais uma velocidade; se o OnTileCollide o manteve
        // vivo, a posição volta ao começo mais a velocidade de agora.
        if (has('OnTileCollide')) Hooks.Once('proj.Movement', () => {
            Pr['void HandleMovement(Vector2 wetVelocity)'].hook((original, p, wet) => {
                const m = of(p);
                if (!m) return original(p, wet);

                const outer = m.__moving;
                const start = Vector2.Clone(p.position);
                const moving = { velocity: Vector2.Clone(p.velocity), kept: false };
                m.__moving = moving;
                try {
                    original(p, wet);
                    if (moving.kept && p.active) p.position = Vector2.Add(start, p.velocity);
                } finally {
                    m.__moving = outer;
                }
            }, self);

            // A atualização do próprio projétil: só nela um Kill fora do
            // movimento pode ser a IA matando ao bater. O Kill que vem de
            // fora (o UpdateMaxTurrets trocando a sentinela, um mod) não é choque.
            Pr['void Update(int i)'].hook((original, p, i) => {
                const m = of(p);
                if (!m) return original(p, i);

                const outer = m.__updating;
                m.__updating = true;
                try {
                    return original(p, i);
                } finally {
                    m.__updating = outer;
                }
            }, self);
        });

        if (has('PreKill') || has('OnKill') || has('OnTileCollide')) Hooks.Once('proj.Kill', ProjectileLoader.#HookKill);

        if (has('OnHitNPC')) Hooks.Once('proj.OnHitNPC', () => {
            Pr['void StatusNPC(int i)'].hook((original, p, i) => {
                original(p, i);

                const m = of(p);
                if (m) Safe.Run(m.constructor.name + '.OnHitNPC', () => m.OnHitNPC(p, Terraria.Main.npc[i]));
            }, self);
        });

        if (has('OnHitPlayer')) Hooks.Once('proj.OnHitPlayer', () => {
            Pr['void StatusPlayer(Player player)'].hook((original, p, player) => {
                original(p, player);

                const m = of(p);
                if (m) Safe.Run(m.constructor.name + '.OnHitPlayer', () => m.OnHitPlayer(p, player));
            }, self);
        });

        if (has('Colliding')) Hooks.Once('proj.Colliding', () => {
            Pr['bool Colliding(Rectangle myRect, Rectangle targetRect)'].hook((original, p, mine, target) => {
                const m = of(p);
                const r = m ? Safe.Run(m.constructor.name + '.Colliding', () => m.Colliding(p, mine, target)) : undefined;
                return typeof r === 'boolean' ? r : original(p, mine, target);
            }, self);
        });

        if (has('CanDamage')) Hooks.Once('proj.CanDamage', () => {
            Pr['void Damage()'].hook((original, p) => {
                const m = of(p);
                if (m && Safe.Run(m.constructor.name + '.CanDamage', () => m.CanDamage(p)) === false) return undefined;

                return original(p);
            }, self);
        });

        // O Damage do jogo sai no começo para todo Main.projPet: desligado só
        // durante a chamada, o lacaio fere ao encostar.
        if (has('MinionContactDamage')) Hooks.Once('proj.MinionContactDamage', () => {
            Pr['void Damage()'].hook((original, p) => {
                const m = of(p);
                const pet = Terraria.Main.projPet;
                const type = p.type;
                const contact = m && pet[type] &&
                    Safe.Run(m.constructor.name + '.MinionContactDamage', () => m.MinionContactDamage(p)) === true;
                if (!contact) return original(p);

                pet[type] = false;
                try {
                    return original(p);
                } finally {
                    pet[type] = true;
                }
            }, self);
        });

        if (has('ModifyDamageHitbox')) Hooks.Once('proj.Hitbox', () => {
            Pr['Rectangle Damage_GetHitbox()'].hook((original, p) => {
                const box = original(p);
                const m = of(p);
                if (!m) return box;

                const r = Rectangle.new(box.X, box.Y, box.Width, box.Height);
                Safe.Run(m.constructor.name + '.ModifyDamageHitbox', () => m.ModifyDamageHitbox(p, r));
                return r;
            }, self);
        });

        if (has('CanCutTiles')) Hooks.Once('proj.CanCutTiles', () => {
            Pr['bool CanCutTiles()'].hook((original, p) => {
                const m = of(p);
                const r = m ? Safe.Run(m.constructor.name + '.CanCutTiles', () => m.CanCutTiles(p)) : undefined;
                return typeof r === 'boolean' ? r : original(p);
            }, self);
        });

        if (has('CutTiles')) Hooks.Once('proj.CutTiles', () => {
            Pr['void CutTiles()'].hook((original, p) => {
                original(p);

                const m = of(p);
                if (m) Safe.Run(m.constructor.name + '.CutTiles', () => m.CutTiles(p));
            }, self);
        });

        if (has('GetAlpha')) Hooks.Once('proj.GetAlpha', () => {
            Pr['Color GetAlpha(Color newColor)'].hook((original, p, color) => {
                const m = of(p);
                const c = m ? Safe.Run(m.constructor.name + '.GetAlpha', () => m.GetAlpha(p, color)) : undefined;
                return c || original(p, color);
            }, self);
        });

        if (has('GrappleCanLatchOnTo')) Hooks.Once('proj.Latch', () => {
            Pr['bool AI_007_GrapplingHooks_CanTileBeLatchedOnTo(Tile theTile)'].hook((original, p, tile) => {
                const vanilla = original(p, tile);
                const m = of(p);
                if (!m) return vanilla;

                const owner = Terraria.Main.player[p.owner];
                const r = Safe.Run(m.constructor.name + '.GrappleCanLatchOnTo', () => m.GrappleCanLatchOnTo(p, owner, tile));
                return typeof r === 'boolean' ? r : vanilla;
            }, self);
        });

        if (has('CanUseGrapple') || has('UseGrapple')) Hooks.Once('proj.Grapple', ProjectileLoader.#HookGrapple);

        if (has('PreDraw') || has('PostDraw')) Hooks.Once('proj.Draw', ProjectileLoader.#HookDraw);
    }

    static #HookKill() {
        const solid = Terraria.Collision['bool SolidCollision(Vector2 Position, int Width, int Height)'];

        Terraria.Projectile['void Kill()'].hook((original, p) => {
            const m = ProjectileLoader.Of(p);
            if (!m || !p.active) return original(p);

            const n = m.constructor.name;

            // Morte por bloco: no movimento, o choque; fora dele mas na
            // atualização do projétil (IA que mata ao bater), um bloco logo à frente.
            if (p.tileCollide && Hooks.Overrides(m.constructor, ModProjectile, 'OnTileCollide')) {
                let hit = m.__moving ? m.__moving.velocity : undefined;
                if (!hit && m.__updating) {
                    const v = p.velocity;
                    const len = Math.hypot(v.X, v.Y) || 1;
                    const ahead = Vector2.new(p.position.X + v.X / len, p.position.Y + v.Y / len);
                    if (solid(ahead, p.width, p.height)) hit = Vector2.Clone(v);
                }

                if (hit && Safe.Run(n + '.OnTileCollide', () => m.OnTileCollide(p, hit)) === false) {
                    if (m.__moving) m.__moving.kept = true;
                    return undefined;
                }
            }

            const timeLeft = p.timeLeft;
            if (Safe.Run(n + '.PreKill', () => m.PreKill(p, timeLeft)) === false) {
                p.active = false;
                return undefined;
            }

            Safe.Run(n + '.OnKill', () => m.OnKill(p, timeLeft));
            return original(p);
        }, { minType: FIRST_PROJECTILE });
    }

    // O item de gancho lança o item.shoot: filtro nativo pelo shoot.
    static #HookGrapple() {
        Terraria.Player['void FireGrapple(Item grappleItem)'].hook((original, player, item) => {
            const template = ProjectileLoader.ByType.get(item.shoot);
            if (!template) return original(player, item);

            const n = template.constructor.name;
            const shoot = item.shoot;
            if (Safe.Run(n + '.CanUseGrapple', () => template.CanUseGrapple(player, shoot)) === false) return undefined;

            const t = Safe.Run(n + '.UseGrapple', () => template.UseGrapple(player, shoot));
            const type = typeof t === 'number' ? t : shoot;
            if (type === shoot) return original(player, item);

            item.shoot = type;
            try {
                original(player, item);
            } finally {
                item.shoot = shoot;
            }
            return undefined;
        }, { minType: FIRST_PROJECTILE, on: 0, field: 'shoot' });
    }

    static #HookDraw() {
        const lightAt = Terraria.Lighting['Color GetColor(int x, int y)'];

        Terraria.Main['void DrawProjDirect(Projectile proj, Player overridePlayer)'].hook((original, main, p, player) => {
            const m = ProjectileLoader.Of(p);
            if (!m) return original(main, p, player);

            // Posição NaN por um quadro (a lança ao trocar de item): luz cheia.
            const n = m.constructor.name;
            const c = p.Center;
            const light = Number.isFinite(c.X) && Number.isFinite(c.Y)
                ? lightAt(Math.floor(c.X / 16), Math.floor(c.Y / 16))
                : Color.White;

            if (Safe.Run(n + '.PreDraw', () => m.PreDraw(p, light)) === false) return undefined;

            original(main, p, player);
            Safe.Run(n + '.PostDraw', () => m.PostDraw(p, light));
            return undefined;
        }, { minType: FIRST_PROJECTILE, on: 0 });
    }
}
