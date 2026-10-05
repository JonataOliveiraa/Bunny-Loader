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

    static DefersTileCollisionKill(p) {
        const moving = p.__projectileMovement;
        return !!(p.tileCollide && moving && moving.hasCollision && !moving.reported && ProjectileLoader.#Collided(p, moving));
    }

    static Hook(cls, type) {
        const Pr = Terraria.Projectile;
        const has = (name) => Hooks.Overrides(cls, ModProjectile, name);
        const self = { minType: FIRST_PROJECTILE };
        const of = ProjectileLoader.Of;
        // Os hooks de um método só entram no JS para os tipos cuja classe o
        // escreve: basta um projétil do mod ter o OnTileCollide para o
        // HandleMovement ser instalado, e os outros projéteis de mod passam direto.
        const marked = (key, ...methods) => {
            if (methods.some(has)) bl.hookMarks.set(key, type);
            return { minType: FIRST_PROJECTILE, marks: key };
        };

        if (has('SendExtraAI') || has('ReceiveExtraAI')) ModNet.InstallEntity();

        // Sempre: o AIType e o OnSpawn podem vir de qualquer projétil.
        Hooks.Once('proj.AI', () => {
            Pr['void AI()'].hook((original, p) => {
                const m = of(p);
                if (!m) return original(p);

                const plan = ProjectileLoader.#PlanOf(m);
                if (!m.__spawned) {
                    m.__spawned = true;
                    if (plan.OnSpawn) try { m.OnSpawn(p); } catch (e) { Safe.Report(plan.OnSpawn, e); }
                }

                let go = true;
                if (plan.PreAI) try { go = m.PreAI(p) !== false; } catch (e) { Safe.Report(plan.PreAI, e); }
                if (go) {
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
                    if (plan.AI) try { m.AI(p); } catch (e) { Safe.Report(plan.AI, e); }
                }

                if (plan.PostAI) try { m.PostAI(p); } catch (e) { Safe.Report(plan.PostAI, e); }
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

        ProjectileLoader.#SharedHooks(has, type, false);

        const onHitNPC = marked('proj.OnHitNPC', 'OnHitNPC');
        if (has('OnHitNPC')) Hooks.Once('proj.OnHitNPC', () => {
            Pr['void StatusNPC(int i)'].hook((original, p, i) => {
                original(p, i);

                const m = of(p);
                if (m) Safe.Run(m.constructor.name + '.OnHitNPC', () => m.OnHitNPC(p, Terraria.Main.npc[i]));
            }, onHitNPC);
        });

        const onHitPlayer = marked('proj.OnHitPlayer', 'OnHitPlayer');
        if (has('OnHitPlayer')) Hooks.Once('proj.OnHitPlayer', () => {
            Pr['void StatusPlayer(Player player)'].hook((original, p, player) => {
                original(p, player);

                const m = of(p);
                if (m) Safe.Run(m.constructor.name + '.OnHitPlayer', () => m.OnHitPlayer(p, player));
            }, onHitPlayer);
        });

    }

    static #globalGroups = new Set();

    static HookGlobal(cls) {
        ProjectileLoader.#SharedHooks((name) => Hooks.Overrides(cls, GlobalProjectile, name), 0, true);
    }

    static #SharedHooks(has, type, global) {
        const Pr = Terraria.Projectile;
        const install = (key, methods, fn, extra) => {
            const wants = methods.some(has);
            if (!wants) return;
            if (global) {
                ProjectileLoader.#globalGroups.add(key);
                bl.hookFlags.set(key + '.local', false);
                Hooks.Once(key + '.global', () => fn(Object.assign({ minType: 1 }, extra)));
            } else {
                bl.hookMarks.set(key, type);
                if (ProjectileLoader.#globalGroups.has(key)) return;
                Hooks.Once(key + '.local', () => {
                    bl.hookFlags.set(key + '.local', true);
                    fn(Object.assign({ minType: FIRST_PROJECTILE, marks: key, flag: key + '.local' }, extra));
                });
            }
        };
        install('proj.Movement', ['OnTileCollide', 'TileCollideStyle'], (filter) => {
            Entities.Define(Pr, '__projectileMovement');
            ProjectileLoader.#HookMovement(filter);
        });
        install('proj.CollideStyle', ['TileCollideStyle'], ProjectileLoader.#HookCollideStyle);
        install('proj.Kill', ['PreKill', 'OnKill', 'OnTileCollide'], ProjectileLoader.#HookKill);
        install('proj.Colliding', ['Colliding'], (filter) => {
            Pr['bool Colliding(Rectangle myRect, Rectangle targetRect)'].hook((original, p, mine, target) => {
                let result = globalProjectiles.First(p, 'Colliding', mine, target, true);
                if (result === undefined) result = ProjectileLoader.#Invoke(ProjectileLoader.Of(p), 'Colliding', p, mine, target);
                return typeof result === 'boolean' ? result : original(p, mine, target);
            }, filter);
        });
        install('proj.Damage', ['CanDamage', 'MinionContactDamage', 'Colliding'], ProjectileLoader.#HookDamage);
        install('proj.Hitbox', ['ModifyDamageHitbox'], (filter) => {
            Pr['Rectangle Damage_GetHitbox()'].hook((original, p) => {
                const box = original(p), m = ProjectileLoader.Of(p);
                const globals = globalProjectiles.For(p, 'ModifyDamageHitbox');
                if (!globals.length && !ProjectileLoader.#PlanHas(m, 'ModifyDamageHitbox')) return box;
                const copy = Rectangle.new(box.X, box.Y, box.Width, box.Height);
                ProjectileLoader.#Invoke(m, 'ModifyDamageHitbox', p, copy);
                if (!globals.length) return copy;
                const hitbox = new Ref(copy);
                for (const g of globals) globalProjectiles.Invoke(g, 'ModifyDamageHitbox', p, hitbox);
                return hitbox.value;
            }, filter);
        });
        install('proj.CanCutTiles', ['CanCutTiles', 'CutTiles'], (filter) => {
            Pr['bool CanCutTiles()'].hook((original, p) => {
                const cutting = ProjectileLoader.#cutting;
                if (cutting && cutting.address === bl.addressOf(p)) return true;
                let result = globalProjectiles.First(p, 'CanCutTiles', undefined, undefined, true);
                if (result === undefined) result = ProjectileLoader.#Invoke(ProjectileLoader.Of(p), 'CanCutTiles', p);
                return typeof result === 'boolean' ? result : original(p);
            }, filter);
        });
        install('proj.CutTiles', ['CutTiles'], (filter) => {
            Pr['void CutTiles()'].hook((original, p) => {
                const m = ProjectileLoader.Of(p), globals = globalProjectiles.For(p, 'CutTiles');
                if (!globals.length && !ProjectileLoader.#PlanHas(m, 'CutTiles')) return original(p);
                if (p['bool CanCutTiles()']() === false) return undefined;
                for (const g of globals) globalProjectiles.Invoke(g, 'CutTiles', p);
                ProjectileLoader.#Invoke(m, 'CutTiles', p);
                const outer = ProjectileLoader.#cutting;
                ProjectileLoader.#cutting = { address: bl.addressOf(p) };
                try { return original(p); }
                finally { ProjectileLoader.#cutting = outer; }
            }, filter);
        });
        install('proj.GetAlpha', global ? ['GetAlpha', 'PreDraw'] : ['GetAlpha'], (filter) => {
            Pr['Color GetAlpha(Color newColor)'].hook((original, p, color) => {
                const draw = ProjectileLoader.#drawLight;
                if (draw && draw.address === bl.addressOf(p)) color = draw.color;
                let result = globalProjectiles.First(p, 'GetAlpha', color);
                if (result == null) result = ProjectileLoader.#Invoke(ProjectileLoader.Of(p), 'GetAlpha', p, color);
                return result == null ? original(p, color) : result;
            }, filter);
        });
        install('proj.Latch', ['GrappleCanLatchOnTo'], (filter) => {
            Pr['bool AI_007_GrapplingHooks_CanTileBeLatchedOnTo(Tile theTile)'].hook((original, p, tile) => {
                const owner = Terraria.Main.player[p.owner];
                let result = ProjectileLoader.#Invoke(ProjectileLoader.Of(p), 'GrappleCanLatchOnTo', p, owner, tile);
                for (const g of globalProjectiles.For(p, 'GrappleCanLatchOnTo')) {
                    const value = globalProjectiles.Invoke(g, 'GrappleCanLatchOnTo', p, owner, tile);
                    if (value === false) return false;
                    if (value === true) result = true;
                }
                return typeof result === 'boolean' ? result : original(p, tile);
            }, filter);
        });
        install('proj.Grapple', ['CanUseGrapple', 'UseGrapple'], ProjectileLoader.#HookGrapple, { on: 0, field: 'shoot' });
        install('proj.Draw', ['PreDraw', 'PostDraw', 'PreDrawExtras'], ProjectileLoader.#HookDraw, { on: 0 });
        if (has('PreDrawExtras') || global && has('PreDraw')) Hooks.Once('proj.DrawExtras', ProjectileLoader.#HookDrawExtras);
        else if (!global && has('PreDraw')) Ready.Add(() => ProjectileLoader.#CheckExtras(ProjectileLoader.ByType.get(type).constructor));
    }

    static #cutting = null;

    static #HookDamage(filter) {
        Terraria.Projectile['void Damage()'].hook((original, p) => {
            const m = ProjectileLoader.Of(p);
            let allowed;
            for (const g of globalProjectiles.For(p, 'CanDamage')) {
                const result = globalProjectiles.Invoke(g, 'CanDamage', p);
                if (result === false) return undefined;
                if (result === true) allowed = true;
            }
            if (allowed !== true && ProjectileLoader.#Invoke(m, 'CanDamage', p) === false) return undefined;
            const contactGlobals = globalProjectiles.For(p, 'MinionContactDamage');
            const collisionGlobals = globalProjectiles.For(p, 'Colliding');
            if (!contactGlobals.length && !collisionGlobals.length && !ProjectileLoader.#PlanHas(m, 'MinionContactDamage')) return original(p);
            const type = p.type, pets = Terraria.Main.projPet;
            const complex = collisionGlobals.length ? Terraria.ID.ProjectileID.Sets.IsAComplexCollision : null;
            const wasPet = pets[type], wasComplex = complex && complex[type];
            let contact = false;
            if (wasPet) {
                contact = ProjectileLoader.#Invoke(m, 'MinionContactDamage', p) === true;
                if (!contact) for (const g of contactGlobals) {
                    if (globalProjectiles.Invoke(g, 'MinionContactDamage', p) === true) { contact = true; break; }
                }
            }
            const customCollision = !!complex && !wasComplex;
            if (!contact && !customCollision) return original(p);
            if (contact) pets[type] = false;
            if (customCollision) complex[type] = true;
            try { return original(p); }
            finally {
                if (contact) pets[type] = wasPet;
                if (customCollision) complex[type] = wasComplex;
            }
        }, filter);
    }

    static #PlanHas(m, method) { return !!m && !!ProjectileLoader.#PlanOf(m)[method]; }

    static #Invoke(m, method, p, a, b, c, d) {
        if (!m) return undefined;
        const label = ProjectileLoader.#PlanOf(m)[method];
        if (!label) return undefined;
        try {
            switch (ModProjectile.prototype[method].length) {
                case 2: return m[method](p, a);
                case 3: return m[method](p, a, b);
                case 5: return m[method](p, a, b, c, d);
                default: return m[method](p);
            }
        }
        catch (error) { Safe.Report(label, error); }
    }

    // O que a classe escreve, e os rótulos do log de erro, montados uma vez:
    // os hooks de todo quadro (AI, desenho) chamam só o que ela tem.
    static #plans = new Map();
    static #PlanOf(m) {
        const cls = m.constructor;
        let plan = ProjectileLoader.#plans.get(cls);
        if (plan) return plan;

        plan = {};
        for (const name of ['OnSpawn', 'PreAI', 'AI', 'PostAI', 'PreDrawExtras', 'PreDraw', 'PostDraw',
            'OnTileCollide', 'TileCollideStyle', 'PreKill', 'OnKill', 'Colliding', 'CanDamage', 'MinionContactDamage',
            'ModifyDamageHitbox', 'CanCutTiles', 'CutTiles', 'GetAlpha', 'CanUseGrapple', 'UseGrapple', 'GrappleCanLatchOnTo']) {
            plan[name] = Hooks.Overrides(cls, ModProjectile, name) ? cls.name + '.' + name : null;
        }
        ProjectileLoader.#plans.set(cls, plan);
        return plan;
    }

    static #HookMovement(filter) {
        const Pr = Terraria.Projectile;
        const handleMovement = Pr['void HandleMovement(Vector2 wetVelocity)'];

        handleMovement.hook((original, p, wet) => {
            const m = ProjectileLoader.Of(p);
            const hasCollision = ProjectileLoader.#PlanHas(m, 'OnTileCollide') || globalProjectiles.For(p, 'OnTileCollide').length > 0;
            const hasStyle = ProjectileLoader.#PlanHas(m, 'TileCollideStyle') || globalProjectiles.For(p, 'TileCollideStyle').length > 0;
            if (!p.tileCollide || !hasCollision && !hasStyle) return original(p, wet);

            const outer = p.__projectileMovement;
            const start = hasCollision ? Vector2.Clone(p.position) : null;
            const moving = { velocity: hasCollision ? Vector2.Clone(p.velocity) : null,
                kept: false, reported: false, pendingKill: false, style: null, bypass: false, hasCollision };
            const restore = hasStyle
                ? ProjectileLoader.#ApplyCollideStyle(m, p, moving) : null;

            p.__projectileMovement = moving;
            try {
                original(p, wet);
                if (hasCollision && !moving.reported && p.active && (moving.pendingKill ||
                    p.tileCollide && !moving.bypass && ProjectileLoader.#Collided(p, moving))) {
                    ProjectileLoader.#ReportCollision(m, p, moving);
                }
                if (moving.kept && p.active) p.position = Vector2.Add(start, p.velocity);
            } finally {
                p.__projectileMovement = outer;
                if (restore) restore();
            }
        }, filter);

        Pr['void UpdatePosition(Vector2 wetVelocity)'].hook((original, p, wet) => {
            const moving = p.__projectileMovement;
            if (!moving) return original(p, wet);

            // Sem colisão de bloco, mas a rampa do lacaio continua.
            if (moving.bypass) p.tileCollide = true;
            else if (moving.hasCollision && !moving.reported && p.active && p.tileCollide && ProjectileLoader.#Collided(p, moving)) {
                ProjectileLoader.#ReportCollision(ProjectileLoader.Of(p), p, moving);
            }
            return original(p, wet);
        }, Object.assign({}, filter, { whileIn: handleMovement }));
    }

    // O tamanho da caixa de colisão sai do GetCollisionParams, chamado mais
    // de uma vez por movimento: devolve o que o TileCollideStyle decidiu,
    // sem chamar o mod de novo.
    static #HookCollideStyle(filter) {
        const Pr = Terraria.Projectile;

        Pr['void GetCollisionParams(out Vector2 resizeAnchor, out int colWidth, out int colHeight)'].hook((original, p, anchor, width, height) => {
            original();

            const moving = p.__projectileMovement;
            const style = moving && moving.style;
            if (!style) return;

            anchor.value = style.anchor;
            width.value = style.width;
            height.value = style.height;
        }, Object.assign({}, filter, { whileIn: Pr['void HandleMovement(Vector2 wetVelocity)'] }));
    }

    // Antes do movimento: o mod recebe a caixa e o fallThrough que o jogo
    // usaria. O fallThrough volta pelo decidesManualFallThrough, a última
    // palavra do jogo; false no retorno desliga a colisão só neste movimento.
    static #ApplyCollideStyle(m, p, moving) {
        const anchor = new Ref(), width = new Ref(0), height = new Ref(0);
        p['void GetCollisionParams(out Vector2 resizeAnchor, out int colWidth, out int colHeight)'](anchor, width, height);
        const fallThrough = new Ref(ProjectileLoader.#VanillaFallThrough(p));

        let collide = ProjectileLoader.#Invoke(m, 'TileCollideStyle', p, width, height, fallThrough, anchor);
        if (collide !== false) for (const g of globalProjectiles.For(p, 'TileCollideStyle')) {
            if (globalProjectiles.Invoke(g, 'TileCollideStyle', p, width, height, fallThrough, anchor) === false) {
                collide = false;
                break;
            }
        }

        moving.style = { anchor: anchor.value, width: width.value | 0, height: height.value | 0 };
        moving.bypass = collide === false;

        const manual = p.decidesManualFallThrough, should = p.shouldFallThrough;
        p.decidesManualFallThrough = true;
        p.shouldFallThrough = !!fallThrough.value;
        if (moving.bypass) p.tileCollide = false;

        return () => {
            p.decidesManualFallThrough = manual;
            p.shouldFallThrough = should;
            if (moving.bypass) p.tileCollide = true;
        };
    }

    static #noFallThrough = new Set([9, 12, 13, 15, 24, 663, 665, 667, 677, 678, 679,
        688, 689, 690, 691, 692, 693, 1037, 1049, 1105]);

    static #VanillaFallThrough(p) {
        if (p.decidesManualFallThrough) return p.shouldFallThrough;

        const Main = Terraria.Main;
        const style = p.aiStyle, type = p.type;
        let fall = true;
        if (Main.projPet[type]) {
            const owner = Main.player[p.owner];
            fall = owner.position.Y + owner.height - 12 > p.position.Y + p.height;
        }
        if (type === 500 || type === 653 || type === 1018) {
            const owner = Main.player[p.owner];
            fall = owner.position.Y + owner.height > p.position.Y + p.height + 4;
        }
        fall = ((fall && type !== 378) || style === 62 || style === 66 || type === 317 || type === 373) &&
            type !== 281 && type !== 253 && style !== 53 || style === 197;
        if (ProjectileLoader.#noFallThrough.has(type)) fall = false;
        if (style === 10 && Terraria.ID.ProjectileID.Sets.FallingBlockDoesNotFallThroughPlatforms[type]) fall = false;
        if (style === 99 && p.ai[0] === -2) fall = false;
        if (type === 759) fall = true;
        if (type === 1020) {
            let rotation = p.rotation % (Math.PI * 2);
            if (rotation >= Math.PI) rotation -= Math.PI * 2;
            if (rotation < -Math.PI) rotation += Math.PI * 2;
            fall = rotation >= Math.fround(Math.PI / 2) || rotation < Math.fround(-Math.PI / 2);
        }
        return fall;
    }

    static #Collided(p, moving) {
        const v = p.velocity;
        return v.X !== moving.velocity.X || v.Y !== moving.velocity.Y;
    }

    static #ReportCollision(m, p, moving) {
        moving.reported = true;
        if (!moving.hasCollision) return;
        const oldVelocity = Vector2.Clone(moving.velocity);
        let allowed = globalProjectiles.AllCall(p, 'OnTileCollide', oldVelocity);
        if (allowed) allowed = ProjectileLoader.#Invoke(m, 'OnTileCollide', p, oldVelocity) !== false;
        if (!moving.pendingKill) return;
        if (allowed === false) moving.kept = true;
        else if (p.active) p['void Kill()']();
    }

    static #HookKill(filter) {
        Terraria.Projectile['void Kill()'].hook((original, p) => {
            if (!p.active) return original(p);
            const moving = p.__projectileMovement;
            if (ProjectileLoader.DefersTileCollisionKill(p)) {
                moving.pendingKill = true;
                return undefined;
            }

            const timeLeft = p.timeLeft;
            const m = ProjectileLoader.Of(p);
            if (!globalProjectiles.AllCall(p, 'PreKill', timeLeft) || ProjectileLoader.#Invoke(m, 'PreKill', p, timeLeft) === false) {
                p.active = false;
                return undefined;
            }

            ProjectileLoader.#Invoke(m, 'OnKill', p, timeLeft);
            globalProjectiles.Call(p, 'OnKill', timeLeft);
            return original(p);
        }, filter);
    }

    static #grappleSamples = new Map();

    static #HookGrapple(filter) {
        Terraria.Player['void FireGrapple(Item grappleItem)'].hook((original, player, item) => {
            const template = ProjectileLoader.ByType.get(item.shoot);
            const shoot = item.shoot;
            let allowed = ProjectileLoader.#Invoke(template, 'CanUseGrapple', player, shoot);
            for (const g of globalProjectiles.Templates('CanUseGrapple')) {
                if (g.__conditional) {
                    let sample = ProjectileLoader.#grappleSamples.get(shoot);
                    if (!sample) {
                        sample = Terraria.Projectile.new();
                        sample['void .ctor()']();
                        sample['void SetDefaults(int Type)'](shoot);
                        ProjectileLoader.#grappleSamples.set(shoot, sample);
                    }
                    if (globalProjectiles.Invoke(g, 'AppliesToEntity', sample, true) !== true) continue;
                }
                const result = globalProjectiles.Invoke(g, 'CanUseGrapple', shoot, player);
                if (typeof result === 'boolean') allowed = result;
            }
            if (allowed === false) return undefined;

            const value = ProjectileLoader.#Invoke(template, 'UseGrapple', player, shoot);
            const ref = new Ref(typeof value === 'number' ? value : shoot);
            for (const g of globalProjectiles.Templates('UseGrapple')) globalProjectiles.Invoke(g, 'UseGrapple', player, ref);
            const type = Number.isInteger(ref.value) && ref.value > 0 &&
                (ref.value < FIRST_PROJECTILE || bl.projectiles.isModProjectile(ref.value)) ? ref.value : shoot;
            if (type === shoot) return original(player, item);

            item.shoot = type;
            try {
                original(player, item);
            } finally {
                item.shoot = shoot;
            }
            return undefined;
        }, filter);
    }

    static #drawLight = null;

    static #HookDraw(filter) {
        const lightAt = Terraria.Lighting['Color GetColor(int x, int y)'];

        Terraria.Main['void DrawProjDirect(Projectile proj, Player overridePlayer)'].hook((original, main, p, player) => {
            const m = ProjectileLoader.Of(p);
            const pre = globalProjectiles.For(p, 'PreDraw'), post = globalProjectiles.For(p, 'PostDraw');
            if (!pre.length && !post.length && !ProjectileLoader.#PlanHas(m, 'PreDraw') &&
                !ProjectileLoader.#PlanHas(m, 'PostDraw') && !ProjectileLoader.#PlanHas(m, 'PreDrawExtras')) return original(main, p, player);
            let light;
            if (pre.length || post.length || ProjectileLoader.#PlanHas(m, 'PreDraw') || ProjectileLoader.#PlanHas(m, 'PostDraw')) {
                const c = p.Center;
                light = Number.isFinite(c.X) && Number.isFinite(c.Y)
                    ? lightAt(Math.floor(c.X / 16), Math.floor(c.Y / 16))
                    : Color.White;
            }

            const wantsExtras = ProjectileLoader.#Invoke(m, 'PreDrawExtras', p) !== false;
            const hasExtras = ProjectileLoader.#HasExtras(p);
            const extras = wantsExtras && hasExtras;
            let sprite = true;
            if (pre.length) {
                const ref = new Ref(light);
                for (const g of pre) {
                    if (globalProjectiles.Invoke(g, 'PreDraw', p, ref) === false) sprite = false;
                }
                light = ref.value;
            }
            if (sprite) sprite = ProjectileLoader.#Invoke(m, 'PreDraw', p, light) !== false;

            // Só uma das partes do desenho do jogo: precisa dos filtros. Sem
            // eles (a classe não tinha extras no SetDefaults), o PreDraw false
            // tira tudo, e o PreDrawExtras false não acontece (instala sempre).
            const split = ProjectileLoader.#extrasHooked && hasExtras && sprite !== extras;
            if (sprite || split) {
                const outerSplit = ProjectileLoader.#split, outerLight = ProjectileLoader.#drawLight;
                if (split) {
                    const own = Terraria.GameContent.TextureAssets.Projectile[p.type].Value;
                    ProjectileLoader.#split = { own: bl.addressOf(own), extras, sprite, reached: false };
                    // Os filtros de desenho só entram no JS agora (ver #HookDrawExtras).
                    bl.hookFlags.set('proj.split', true);
                }
                if (pre.length) ProjectileLoader.#drawLight = { address: bl.addressOf(p), color: light };
                try {
                    original(main, p, player);
                } finally {
                    if (split) {
                        ProjectileLoader.#split = outerSplit;
                        bl.hookFlags.set('proj.split', !!outerSplit);
                    }
                    ProjectileLoader.#drawLight = outerLight;
                }
            }
            ProjectileLoader.#Invoke(m, 'PostDraw', p, light);
            for (const g of post) globalProjectiles.Invoke(g, 'PostDraw', p, light);
            return undefined;
        }, filter);
    }

    // Os extras do DrawProj_DrawExtras do tModLoader: a linha de pesca e as
    // correntes das IAs de gancho (7), arpão (13), mangual (15) e o fio do ioiô (99).
    static #HasExtras(p) {
        const ai = p.aiStyle;
        return p.bobber || ai === 7 || ai === 13 || ai === 15 || ai === 99;
    }

    // Só uma das partes do desenho do jogo (extras sem sprite ou o contrário).
    static #split = null;
    static #extrasHooked = false;

    // Um projétil com PreDraw e sem PreDrawExtras: os filtros só se a IA dele
    // tiver extras (o PreDraw false deixa a corrente do jogo).
    static #CheckExtras(cls) {
        if (ProjectileLoader.#extrasHooked) return;
        for (const [type, inst] of ProjectileLoader.ByType) {
            if (inst.constructor !== cls) continue;
            const needs = Safe.Run(cls.name + ' (extras)', () => {
                const p = Terraria.Projectile.new();
                p['void .ctor()']();
                p['void SetDefaults(int Type)'](type);
                return ProjectileLoader.#HasExtras(p);
            });
            if (needs) Hooks.Once('proj.DrawExtras', ProjectileLoader.#HookDrawExtras);
            return;
        }
    }

    // No celular os extras estão dentro do DrawProjDirect, sem método à parte,
    // e vêm antes do sprite: até o primeiro desenho com a textura do próprio
    // projétil, é extra; dali em diante, sprite. Os quatro filtros só entram no
    // JS com a chave 'proj.split' ligada, só durante um desenho em duas partes:
    // antes, todo desenho de todo projétil de mod passava por três deles.
    static #HookDrawExtras() {
        ProjectileLoader.#extrasHooked = true;
        const gate = Terraria.Main['void DrawProjDirect(Projectile proj, Player overridePlayer)'];
        const pass = (texture) => {
            const split = ProjectileLoader.#split;
            if (!split) return true;
            if (!split.reached && bl.addressOf(texture) === split.own) split.reached = true;
            return split.reached ? split.sprite : split.extras;
        };
        const Main = Terraria.Main;
        const Batch = Microsoft.Xna.Framework.Graphics.SpriteBatch;
        Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)'].hook(
            (original, texture) => (pass(texture) ? original() : undefined), { whileIn: gate, flag: 'proj.split' });
        Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float worthless)'].hook(
            (original, texture) => (pass(texture) ? original() : undefined), { whileIn: gate, flag: 'proj.split' });
        Batch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
            (original, batch, texture) => (pass(texture) ? original() : undefined), { whileIn: gate, flag: 'proj.split' });
        Batch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)'].hook(
            (original, batch, texture) => (pass(texture) ? original() : undefined), { whileIn: gate, flag: 'proj.split' });
    }
}
