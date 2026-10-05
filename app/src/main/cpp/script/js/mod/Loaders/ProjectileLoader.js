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
        // O DrawProjDirect recebe o projétil no parâmetro 0 (o self é o Main).
        const markedParam = (key, ...methods) => Object.assign(marked(key, ...methods), { on: 0 });

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
                    // Só a IA do jogo marca o Kill como possível choque (ver
                    // #HookKill): o Kill do próprio mod no AI não é.
                    const aiType = m.AIType | 0;
                    const type = p.type;
                    const outer = m.__vanillaAI;
                    m.__vanillaAI = true;
                    if (aiType > 0) p.type = aiType;
                    try {
                        original(p);
                    } finally {
                        p.type = type;
                        m.__vanillaAI = outer;
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

        // O movimento é a fronteira do TileCollideStyle e do OnTileCollide:
        // o GetCollisionParams e o UpdatePosition só entram no JS dentro dele.
        const movement = marked('proj.Movement', 'OnTileCollide', 'TileCollideStyle');
        if (has('OnTileCollide') || has('TileCollideStyle')) Hooks.Once('proj.Movement', () => ProjectileLoader.#HookMovement(movement));

        const collideStyle = marked('proj.CollideStyle', 'TileCollideStyle');
        if (has('TileCollideStyle')) Hooks.Once('proj.CollideStyle', () => ProjectileLoader.#HookCollideStyle(collideStyle));

        const kill = marked('proj.Kill', 'PreKill', 'OnKill', 'OnTileCollide');
        if (has('PreKill') || has('OnKill') || has('OnTileCollide')) Hooks.Once('proj.Kill', () => ProjectileLoader.#HookKill(kill));

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

        const colliding = marked('proj.Colliding', 'Colliding');
        if (has('Colliding')) Hooks.Once('proj.Colliding', () => {
            Pr['bool Colliding(Rectangle myRect, Rectangle targetRect)'].hook((original, p, mine, target) => {
                const m = of(p);
                const r = m ? Safe.Run(m.constructor.name + '.Colliding', () => m.Colliding(p, mine, target)) : undefined;
                return typeof r === 'boolean' ? r : original(p, mine, target);
            }, colliding);
        });

        const canDamage = marked('proj.CanDamage', 'CanDamage');
        if (has('CanDamage')) Hooks.Once('proj.CanDamage', () => {
            Pr['void Damage()'].hook((original, p) => {
                const m = of(p);
                if (m && Safe.Run(m.constructor.name + '.CanDamage', () => m.CanDamage(p)) === false) return undefined;

                return original(p);
            }, canDamage);
        });

        // O Damage do jogo sai no começo para todo Main.projPet: desligado só
        // durante a chamada, o lacaio fere ao encostar.
        const contactFilter = marked('proj.MinionContactDamage', 'MinionContactDamage');
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
            }, contactFilter);
        });

        const hitbox = marked('proj.Hitbox', 'ModifyDamageHitbox');
        if (has('ModifyDamageHitbox')) Hooks.Once('proj.Hitbox', () => {
            Pr['Rectangle Damage_GetHitbox()'].hook((original, p) => {
                const box = original(p);
                const m = of(p);
                if (!m) return box;

                const r = Rectangle.new(box.X, box.Y, box.Width, box.Height);
                Safe.Run(m.constructor.name + '.ModifyDamageHitbox', () => m.ModifyDamageHitbox(p, r));
                return r;
            }, hitbox);
        });

        const canCut = marked('proj.CanCutTiles', 'CanCutTiles');
        if (has('CanCutTiles')) Hooks.Once('proj.CanCutTiles', () => {
            Pr['bool CanCutTiles()'].hook((original, p) => {
                const m = of(p);
                const r = m ? Safe.Run(m.constructor.name + '.CanCutTiles', () => m.CanCutTiles(p)) : undefined;
                return typeof r === 'boolean' ? r : original(p);
            }, canCut);
        });

        const cut = marked('proj.CutTiles', 'CutTiles');
        if (has('CutTiles')) Hooks.Once('proj.CutTiles', () => {
            Pr['void CutTiles()'].hook((original, p) => {
                original(p);

                const m = of(p);
                if (m) Safe.Run(m.constructor.name + '.CutTiles', () => m.CutTiles(p));
            }, cut);
        });

        const alpha = marked('proj.GetAlpha', 'GetAlpha');
        if (has('GetAlpha')) Hooks.Once('proj.GetAlpha', () => {
            Pr['Color GetAlpha(Color newColor)'].hook((original, p, color) => {
                const m = of(p);
                const c = m ? Safe.Run(m.constructor.name + '.GetAlpha', () => m.GetAlpha(p, color)) : undefined;
                return c || original(p, color);
            }, alpha);
        });

        const latch = marked('proj.Latch', 'GrappleCanLatchOnTo');
        if (has('GrappleCanLatchOnTo')) Hooks.Once('proj.Latch', () => {
            Pr['bool AI_007_GrapplingHooks_CanTileBeLatchedOnTo(Tile theTile)'].hook((original, p, tile) => {
                const vanilla = original(p, tile);
                const m = of(p);
                if (!m) return vanilla;

                const owner = Terraria.Main.player[p.owner];
                const r = Safe.Run(m.constructor.name + '.GrappleCanLatchOnTo', () => m.GrappleCanLatchOnTo(p, owner, tile));
                return typeof r === 'boolean' ? r : vanilla;
            }, latch);
        });

        if (has('CanUseGrapple') || has('UseGrapple')) Hooks.Once('proj.Grapple', ProjectileLoader.#HookGrapple);

        const draw = markedParam('proj.Draw', 'PreDraw', 'PostDraw', 'PreDrawExtras');
        if (has('PreDraw') || has('PostDraw') || has('PreDrawExtras')) Hooks.Once('proj.Draw', () => ProjectileLoader.#HookDraw(draw));
        // Os filtros de desenho do PreDrawExtras passam por todo SpriteBatch.Draw:
        // só com o PreDrawExtras, ou com um PreDraw num projétil que tem extras
        // (conferido depois do SetDefaults dele).
        if (has('PreDrawExtras')) Hooks.Once('proj.DrawExtras', ProjectileLoader.#HookDrawExtras);
        else if (has('PreDraw')) Ready.Add(() => ProjectileLoader.#CheckExtras(cls));
    }

    // O que a classe escreve, e os rótulos do log de erro, montados uma vez:
    // os hooks de todo quadro (AI, desenho) chamam só o que ela tem.
    static #plans = new Map();
    static #PlanOf(m) {
        const cls = m.constructor;
        let plan = ProjectileLoader.#plans.get(cls);
        if (plan) return plan;

        plan = {};
        for (const name of ['OnSpawn', 'PreAI', 'AI', 'PostAI', 'PreDrawExtras', 'PreDraw', 'PostDraw']) {
            plan[name] = Hooks.Overrides(cls, ModProjectile, name) ? cls.name + '.' + name : null;
        }
        ProjectileLoader.#plans.set(cls, plan);
        return plan;
    }

    // Um Kill dentro do HandleMovement é o choque com bloco. O jogo já
    // empurrou o projétil mais uma velocidade; se o OnTileCollide o manteve
    // vivo, a posição volta ao começo mais a velocidade de agora.
    static #HookMovement(filter) {
        const Pr = Terraria.Projectile;
        const handleMovement = Pr['void HandleMovement(Vector2 wetVelocity)'];

        handleMovement.hook((original, p, wet) => {
            const m = ProjectileLoader.Of(p);
            if (!m) return original(p, wet);

            const outer = m.__moving;
            const start = Vector2.Clone(p.position);
            const moving = { velocity: Vector2.Clone(p.velocity), kept: false, reported: false, style: null, bypass: false };
            const restore = p.tileCollide && Hooks.Overrides(m.constructor, ModProjectile, 'TileCollideStyle')
                ? ProjectileLoader.#ApplyCollideStyle(m, p, moving) : null;

            m.__moving = moving;
            try {
                original(p, wet);
                if (moving.kept && p.active) p.position = Vector2.Add(start, p.velocity);
            } finally {
                m.__moving = outer;
                if (restore) restore();
            }
        }, filter);

        // Roda depois do choque e da resposta do jogo, antes de o projétil
        // andar. O lacaio (correctSlopeCollision) pula a resposta inteira e
        // nunca chega ao Kill: o OnTileCollide dele sai daqui, todo quadro com
        // choque, como no tModLoader. Velocidade trocada nele já vale agora.
        Pr['void UpdatePosition(Vector2 wetVelocity)'].hook((original, p, wet) => {
            const m = ProjectileLoader.Of(p);
            const moving = m ? m.__moving : null;
            if (!moving) return original(p, wet);

            // Sem colisão de bloco, mas a rampa do lacaio continua.
            if (moving.bypass) p.tileCollide = true;
            else if (!moving.reported && p.active && p.tileCollide && ProjectileLoader.#Collided(p, moving)) {
                moving.reported = true;
                if (Hooks.Overrides(m.constructor, ModProjectile, 'OnTileCollide')) {
                    const hit = Vector2.Clone(moving.velocity);
                    Safe.Run(m.constructor.name + '.OnTileCollide', () => m.OnTileCollide(p, hit));
                }
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

            const m = ProjectileLoader.Of(p);
            const style = m && m.__moving ? m.__moving.style : null;
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

        const collide = Safe.Run(m.constructor.name + '.TileCollideStyle',
            () => m.TileCollideStyle(p, width, height, fallThrough, anchor));

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

    // O começo do HandleMovement para um tipo de mod (os casos por número
    // são todos de tipos do jogo). Conferido no desmontado do celular.
    static #VanillaFallThrough(p) {
        if (p.decidesManualFallThrough) return p.shouldFallThrough;

        const Main = Terraria.Main;
        const style = p.aiStyle;
        let fall = true;
        if (Main.projPet[p.type]) {
            const owner = Main.player[p.owner];
            fall = owner.position.Y + owner.height - 12 > p.position.Y + p.height;
        }
        if (style === 62 || style === 66 || style === 197) fall = true;
        if (style === 53) fall = false;
        if (style === 10 && Terraria.ID.ProjectileID.Sets.FallingBlockDoesNotFallThroughPlatforms[p.type]) fall = false;
        if (style === 99 && p.ai[0] === -2) fall = false;
        return fall;
    }

    static #Collided(p, moving) {
        const v = p.velocity;
        return v.X !== moving.velocity.X || v.Y !== moving.velocity.Y;
    }

    static #HookKill(filter) {
        const solid = Terraria.Collision['bool SolidCollision(Vector2 Position, int Width, int Height)'];

        Terraria.Projectile['void Kill()'].hook((original, p) => {
            const m = ProjectileLoader.Of(p);
            if (!m || !p.active) return original(p);

            const n = m.constructor.name;

            // Morte por bloco: no movimento, o choque; fora dele mas dentro da
            // IA do jogo (a que mata ao bater), um bloco logo à frente. Só a
            // IA: o resto da atualização também mata (o timeLeft acabando, o
            // penetrate zerado no Damage), e no chão o bloco está sempre à
            // frente; o OnTileCollide false o deixava vivo, sem OnKill.
            // O Kill que vem de fora (o UpdateMaxTurrets trocando a
            // sentinela, um mod) também não é choque.
            if (p.tileCollide && Hooks.Overrides(m.constructor, ModProjectile, 'OnTileCollide')) {
                let hit = m.__moving ? m.__moving.velocity : undefined;
                if (m.__moving) m.__moving.reported = true;
                if (!hit && m.__vanillaAI) {
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
        }, filter);
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

    static #HookDraw(filter) {
        const lightAt = Terraria.Lighting['Color GetColor(int x, int y)'];

        Terraria.Main['void DrawProjDirect(Projectile proj, Player overridePlayer)'].hook((original, main, p, player) => {
            const m = ProjectileLoader.Of(p);
            if (!m) return original(main, p, player);

            const plan = ProjectileLoader.#PlanOf(m);
            // Posição NaN por um quadro (a lança ao trocar de item): luz cheia.
            // Só o PreDraw e o PostDraw recebem a luz.
            let light;
            if (plan.PreDraw || plan.PostDraw) {
                const c = p.Center;
                light = Number.isFinite(c.X) && Number.isFinite(c.Y)
                    ? lightAt(Math.floor(c.X / 16), Math.floor(c.Y / 16))
                    : Color.White;
            }

            // Como no tModLoader: os extras (correntes, linha, fio) antes do
            // PreDraw, e o PostDraw mesmo com o PreDraw false.
            let wantsExtras = true;
            if (plan.PreDrawExtras) try { wantsExtras = m.PreDrawExtras(p) !== false; } catch (e) { Safe.Report(plan.PreDrawExtras, e); }
            const hasExtras = ProjectileLoader.#HasExtras(p);
            const extras = wantsExtras && hasExtras;
            let sprite = true;
            if (plan.PreDraw) try { sprite = m.PreDraw(p, light) !== false; } catch (e) { Safe.Report(plan.PreDraw, e); }

            // Só uma das partes do desenho do jogo: precisa dos filtros. Sem
            // eles (a classe não tinha extras no SetDefaults), o PreDraw false
            // tira tudo, e o PreDrawExtras false não acontece (instala sempre).
            const split = ProjectileLoader.#extrasHooked && hasExtras && sprite !== extras;
            if (sprite || split) {
                if (split) {
                    const own = Terraria.GameContent.TextureAssets.Projectile[p.type].Value;
                    ProjectileLoader.#split = { own: bl.addressOf(own), extras, sprite, reached: false };
                    // Os filtros de desenho só entram no JS agora (ver #HookDrawExtras).
                    bl.hookFlags.set('proj.split', true);
                }
                try {
                    original(main, p, player);
                } finally {
                    if (split) {
                        ProjectileLoader.#split = null;
                        bl.hookFlags.set('proj.split', false);
                    }
                }
            }
            if (plan.PostDraw) try { m.PostDraw(p, light); } catch (e) { Safe.Report(plan.PostDraw, e); }
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
