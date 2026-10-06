class NPCLoader {
    static ByType = new Map();
    static Spawnable = [];   // os que têm SpawnChance

    static Of(npc) {
        return Entities.InstanceOf(npc, 'ModNPC', NPCLoader.ByType);
    }

    // Uma List<IItemDropRule> do jogo num array.
    static Rules(list) {
        const out = [];
        const count = list ? list.Count : 0;
        for (let i = 0; i < count; i++) out.push(list.get_Item(i));
        return out;
    }

    static Hook(cls, type) {
        const N = Terraria.NPC;
        const has = (name) => Hooks.Overrides(cls, ModNPC, name);
        const of = NPCLoader.Of;
        // Os hooks de um método só entram no JS para os tipos cuja classe o
        // escreve (os outros NPCs de mod passam direto, como os do jogo).
        const marked = (key, ...methods) => {
            if (methods.some(has)) bl.hookMarks.set(key, type);
            return { minType: FIRST_NPC, marks: key };
        };

        if (has('SendExtraAI') || has('ReceiveExtraAI')) ModNet.InstallEntity();

        TownNPCLoader.Hook(cls);
        NPCLoader.#PlanFor(cls);
        NPCEventHooks.Install(has, marked);
        NPCDrawHooks.Install(has, marked);
        if (has('CanBeHitByItem') || has('OnHitByItem')) PlayerCombatHooks.InstallNPCItems();
        if (has('OnHitByProjectile')) PlayerCombatHooks.InstallNPCProjectiles();
        if (['CanHitPlayer', 'ModifyHitPlayer', 'OnHitPlayer'].some(has)) PlayerLoader.InstallNPCContact();

        const chat = marked('npc.GetChat', 'GetChat');
        if (has('GetChat')) Hooks.Once('npc.GetChat', () => {
            N['string GetChat()'].hook((original, npc) => {
                const chat = original(npc);
                const m = of(npc);
                const text = m ? Safe.Run(m.constructor.name + '.GetChat', () => m.GetChat(npc)) : undefined;
                return typeof text === 'string' ? text : chat;
            }, chat);
        });

        const ai = marked('npc.AI', 'PreAI', 'AI', 'PostAI');
        if (has('PreAI') || has('AI') || has('PostAI')) Hooks.Once('npc.AI', () => {
            N['void AI()'].hook((original, npc) => {
                const m = of(npc);
                if (!m) return original(npc);

                // Sem closure nem rótulo montado por chamada: só o que a classe escreve.
                const plan = NPCLoader.#PlanOf(m);
                let go = true;
                if (plan.PreAI) try { go = m.PreAI(npc) !== false; } catch (e) { Safe.Report(plan.PreAI, e); }
                if (go) {
                    original(npc);
                    if (plan.AI) try { m.AI(npc); } catch (e) { Safe.Report(plan.AI, e); }
                }
                if (plan.PostAI) try { m.PostAI(npc); } catch (e) { Safe.Report(plan.PostAI, e); }
            }, ai);
        });

        const frame = marked('npc.FindFrame', 'FindFrame');
        if (has('FindFrame')) Hooks.Once('npc.FindFrame', () => {
            const heights = new Map();

            N['void FindFrame()'].hook((original, npc) => {
                const m = of(npc);
                if (!m || !Hooks.Overrides(m.constructor, ModNPC, 'FindFrame')) return original(npc);

                let height = heights.get(npc.type);
                if (height === undefined) {
                    const texture = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                    const frames = Math.max(1, Terraria.Main.npcFrameCount[npc.type]);
                    height = texture ? Math.floor(texture.Height / frames) : 0;
                    heights.set(npc.type, height);
                }
                Safe.Run(m.constructor.name + '.FindFrame', () => m.FindFrame(npc, height));
            }, frame);
        });

        const active = marked('npc.CheckActive', 'CheckActive');
        if (has('CheckActive')) Hooks.Once('npc.CheckActive', () => {
            N['void CheckActive()'].hook((original, npc) => {
                const m = of(npc);
                if (m && Safe.Run(m.constructor.name + '.CheckActive', () => m.CheckActive(npc)) === false) return;

                original(npc);
            }, active);
        });

        // No cliente de multijogador o drop é do servidor: PreKill e OnKill só lá.
        const kill = marked('npc.Kill', 'PreKill', 'OnKill');
        if (has('PreKill') || has('OnKill')) Hooks.Once('npc.Kill', () => {
            N['void NPCLoot()'].hook((original, npc) => {
                const m = of(npc);
                if (!m || Terraria.Main.netMode === 1) return original(npc);

                const n = m.constructor.name;
                if (Safe.Run(n + '.PreKill', () => m.PreKill(npc)) === false) return;

                original(npc);
                Safe.Run(n + '.OnKill', () => m.OnKill(npc));
            }, kill);
        });

        if (has('SpawnChance')) SpawnLoader.InstallPool();
    }

    // Os rótulos do log de erro da AI, montados uma vez por classe.
    static #plans = new Map();
    static #PlanOf(m) {
        return NPCLoader.#PlanFor(m.constructor);
    }

    static #PlanFor(cls) {
        let plan = NPCLoader.#plans.get(cls);
        if (plan) return plan;

        plan = {};
        for (const name of ['PreAI', 'AI', 'PostAI', 'OnSpawn', 'ResetEffects', 'OnHitByItem', 'OnHitByProjectile',
            'CanHitPlayer', 'ModifyHitPlayer', 'OnHitPlayer', 'CanBeHitByItem', 'ModifyIncomingHit', 'CheckDead',
            'ApplyDifficultyAndPlayerScaling', 'CanChat', 'ModifyActiveShop', 'ModifyNPCHappiness', 'PreDraw', 'PostDraw', 'DrawEffects', 'DrawBehind', 'GetAlpha',
            'BossHeadSlot', 'BossHeadRotation', 'BossHeadSpriteEffects']) {
            plan[name] = Hooks.Overrides(cls, ModNPC, name) ? cls.name + '.' + name : null;
        }
        NPCLoader.#plans.set(cls, plan);
        return plan;
    }

    static Has(m, name) {
        return !!m && !!NPCLoader.#PlanOf(m)[name];
    }

    static Call(npc, name, a, b, c, d) {
        if (!npc || npc.type < FIRST_NPC) return undefined;
        const m = NPCLoader.Of(npc);
        if (!m) return undefined;
        const label = NPCLoader.#PlanOf(m)[name];
        if (!label) return undefined;
        try {
            switch (arguments.length) {
                case 2: return m[name](npc);
                case 3: return m[name](npc, a);
                case 4: return m[name](npc, a, b);
                case 5: return m[name](npc, a, b, c);
                default: return m[name](npc, a, b, c, d);
            }
        } catch (e) { Safe.Report(label, e); }
    }
}

class NPCEventHooks {
    static Install(has, marked) {
        const N = Terraria.NPC;
        const spawn = marked('npc.OnSpawn', 'OnSpawn');
        if (has('OnSpawn')) Hooks.Once('npc.OnSpawn', () => {
            N['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'].hook(
                (original, source, x, y, type, start, ai0, ai1, ai2, ai3, target) => {
                    const index = original(source, x, y, type, start, ai0, ai1, ai2, ai3, target);
                    if (index >= 0 && index < 200) {
                        const npc = Terraria.Main.npc[index];
                        if (npc.active) NPCLoader.Call(npc, 'OnSpawn', source);
                    }
                    return index;
                }, { ...spawn, arg: 3 });
        });
        const reset = marked('npc.ResetEffects', 'ResetEffects', 'DrawBehind');
        if (has('ResetEffects') || has('DrawBehind')) Hooks.Once('npc.ResetEffects', () => {
            N['void UpdateNPC_BuffSetFlags(bool lowerBuffTime)'].hook((original, npc, lowerBuffTime) => {
                if (NPCLoader.Has(NPCLoader.Of(npc), 'DrawBehind')) NPCDrawHooks.Track(npc);
                NPCLoader.Call(npc, 'ResetEffects');
                original(npc, lowerBuffTime);
            }, reset);
        });
        const dead = marked('npc.CheckDead', 'CheckDead');
        if (has('CheckDead')) Hooks.Once('npc.CheckDead', () => {
            N['void checkDead()'].hook((original, npc) => {
                if (npc.active && npc.life <= 0 && NPCLoader.Call(npc, 'CheckDead') === false) return;
                original(npc);
            }, dead);
        });
        const chat = marked('npc.CanChat', 'CanChat');
        if (has('CanChat')) Hooks.Once('npc.CanChat', () => {
            for (const signature of ['bool get_CanTalk()', 'bool get_CanBeTalkedTo()']) {
                N[signature].hook((original, npc) => {
                    const value = NPCLoader.Call(npc, 'CanChat');
                    return typeof value === 'boolean' ? value : original(npc);
                }, chat);
            }
        });
        const incoming = marked('npc.ModifyIncomingHit', 'ModifyIncomingHit');
        if (has('ModifyIncomingHit')) Hooks.Once('npc.ModifyIncomingHit', () => {
            N['int StrikeNPC_Inner(int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)'].hook(
                (original, npc, damage, knockback, direction, crit, fromNet, owner) => {
                    if (fromNet) return original(npc, damage, knockback, direction, crit, fromNet, owner);
                    const modifiers = { damage, knockBack: knockback, hitDirection: direction, crit,
                        SourceDamage: StatModifier.Default, Knockback: StatModifier.Default,
                        SetCrit() { this.crit = true; }, DisableCrit() { this.crit = false; } };
                    NPCLoader.Call(npc, 'ModifyIncomingHit', modifiers);
                    const amount = Math.max(0, Math.floor(modifiers.SourceDamage.ApplyTo(modifiers.damage)));
                    const kb = Math.max(0, modifiers.Knockback.ApplyTo(modifiers.knockBack));
                    PlayerCombatHooks.RecordIncoming(npc, amount, kb, modifiers.hitDirection, modifiers.crit);
                    return original(npc, amount, kb, modifiers.hitDirection, modifiers.crit, fromNet, owner);
                }, incoming);
        });
        const scaling = marked('npc.Scaling', 'ApplyDifficultyAndPlayerScaling');
        if (has('ApplyDifficultyAndPlayerScaling')) Hooks.Once('npc.Scaling', () => {
            N['void ScaleStats_ByPlayerCount(int numPlayers)'].hook((original, npc, numPlayers) => {
                original(npc, numPlayers);
                const balance = new Ref(1), boost = new Ref(1);
                N['void GetStatScalingFactors(int numPlayers, out float balance, out float boost)'](numPlayers, balance, boost);
                const master = npc.difficulty >= Terraria.DataStructures.GameDifficultyLevel.Master;
                NPCLoader.Call(npc, 'ApplyDifficultyAndPlayerScaling', numPlayers, balance.value, master ? .85 : 1);
            }, scaling);
        });
        if (has('ModifyNPCHappiness')) Hooks.Once('npc.Happiness', () => {
            Terraria.GameContent.Personalities.AllPersonalitiesModifier['void ModifyShopPrice(HelperInfo info, ShopHelper shopHelperInstance)'].hook(
                (original, self, info, helper) => {
                    original(self, info, helper);
                    const npc = info.npc;
                    if (!npc || npc.type < FIRST_NPC || !NPCLoader.Has(NPCLoader.Of(npc), 'ModifyNPCHappiness')) return;
                    const player = info.player;
                    const biome = player.ZoneDungeon ? 8 : player.ZoneCorrupt ? 9 : player.ZoneCrimson ? 10
                        : player.ZoneGlowshroom ? 7 : player.ZoneHallow ? 6 : player.ZoneJungle ? 4
                        : player.ZoneSnow ? 2 : player.ZoneBeach ? 5 : player.ZoneDesert ? 3
                        : player.position.Y > Terraria.Main.worldSurface * 16 ? 1 : 0;
                    NPCLoader.Call(npc, 'ModifyNPCHappiness', player, biome, helper, info.nearbyNPCsByType);
                });
        });
    }
}

class NPCDrawHooks {
    static #behind = new Map();
    static #light = null;

    static Track(npc) {
        NPCDrawHooks.#behind.set(npc.whoAmI, npc);
    }

    static Install(has, marked) {
        const N = Terraria.NPC;
        const draw = marked('npc.Draw', 'PreDraw', 'PostDraw', 'DrawEffects');
        if (has('PreDraw') || has('PostDraw') || has('DrawEffects')) Hooks.Once('npc.Draw', () => {
            for (const signature of [
                'void DrawNPCDirect(SpriteBatch mySpriteBatch, NPC rCurrentNPC, bool behindTiles, Vector2 screenPos)',
                'void DrawNPCDirect(SpriteBatch mySpriteBatch, NPC rCurrentNPC, bool behindTiles, Vector2 screenPos, LightMap lightMap, ref Rectangle lightRegion)'
            ]) {
                Terraria.Main[signature].hook((original, self, batch, npc, behind, screen, map, region) => {
                    const center = npc.Center;
                    const light = new Ref(Terraria.Lighting['Color GetColor(int x, int y)'](Math.floor(center.X / 16), Math.floor(center.Y / 16)));
                    const before = NPCDrawHooks.#ColorKey(light.value);
                    NPCLoader.Call(npc, 'DrawEffects', light);
                    const go = NPCLoader.Call(npc, 'PreDraw', batch, screen, light.value) !== false;
                    if (go) {
                        const outer = NPCDrawHooks.#light;
                        NPCDrawHooks.#light = NPCDrawHooks.#ColorKey(light.value) !== before ? { npc, color: light.value } : null;
                        bl.hookFlags.set('npc.DrawColor', !!NPCDrawHooks.#light);
                        try {
                            if (region === undefined) original(self, batch, npc, behind, screen);
                            else original(self, batch, npc, behind, screen, map, region);
                        } finally {
                            NPCDrawHooks.#light = outer;
                            bl.hookFlags.set('npc.DrawColor', !!outer);
                        }
                    }
                    NPCLoader.Call(npc, 'PostDraw', batch, screen, light.value);
                }, { ...draw, on: 1 });
            }
            N['Color GetNPCColorTintedByBuffs(Color npcColor)'].hook((original, npc, color) => {
                const light = NPCDrawHooks.#light;
                return original(npc, light && bl.addressOf(npc) === bl.addressOf(light.npc) ? light.color : color);
            }, { ...draw, flag: 'npc.DrawColor' });
        });
        const alpha = marked('npc.GetAlpha', 'GetAlpha');
        if (has('GetAlpha')) Hooks.Once('npc.GetAlpha', () => {
            N['Color GetAlpha(Color newColor)'].hook((original, npc, color) => {
                const value = NPCLoader.Call(npc, 'GetAlpha', color);
                return value === null || value === undefined ? original(npc, color) : value;
            }, alpha);
        });
        for (const [name, signature] of [
            ['BossHeadSlot', 'int GetBossHeadTextureIndex()'],
            ['BossHeadRotation', 'float GetBossHeadRotation()'],
            ['BossHeadSpriteEffects', 'SpriteEffects GetBossHeadSpriteEffects()']
        ]) {
            const filter = marked('npc.' + name, name);
            if (has(name)) Hooks.Once('npc.' + name, () => {
                N[signature].hook((original, npc) => {
                    const value = new Ref(original(npc));
                    NPCLoader.Call(npc, name, value);
                    return value.value;
                }, filter);
            });
        }
        if (has('DrawBehind')) Hooks.Once('npc.DrawBehind', () => {
            Terraria.Main['void CacheNPCDraws()'].hook((original, self) => {
                original(self);
                for (const [index, npc] of NPCDrawHooks.#behind) {
                    if (!npc.active || npc.whoAmI !== index || !NPCLoader.Has(NPCLoader.Of(npc), 'DrawBehind')) {
                        NPCDrawHooks.#behind.delete(index);
                        continue;
                    }
                    NPCLoader.Call(npc, 'DrawBehind', index);
                }
            });
        });
    }

    static #ColorKey(color) {
        return color.PackedValue;
    }
}
