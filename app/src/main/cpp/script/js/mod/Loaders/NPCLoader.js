// ModNPC e GlobalNPC nos mesmos hooks do jogo, como o NPCLoader do
// tModLoader: um hook por método, que chama o do NPC de mod e os Globais na
// ordem de lá. O filtro nativo é por tipo: cada hook só entra no JS para os
// tipos marcados (o NPC de mod que escreve o método, ou todos, quando algum
// Global o escreve).
class NPCLoader {
    static ByType = new Map();
    static Spawnable = [];   // os que têm SpawnChance

    static #everyType = new Set();   // as marcas que valem para todo tipo (algum Global)

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
        const has = (name) => Hooks.Overrides(cls, ModNPC, name);
        if (has('SendExtraAI') || has('ReceiveExtraAI')) ModNet.InstallEntity(23);
        for (const key of NPCLoader.#everyType) bl.hookMarks.set(key, type);

        TownNPCLoader.Hook(cls);
        NPCLoader.#PlanFor(cls);
        NPCLoader.#Install(has, type);
        CombatLoader.WantNPC(cls, ModNPC);

        // AIType pode ser configurado no SetDefaults, sem sobrescrever a AI.
        NPCLoader.InstallAI();
        if (has('SpawnChance')) SpawnLoader.InstallPool();
    }

    static HookGlobal(cls) {
        NPCLoader.#Install((name) => Hooks.Overrides(cls, GlobalNPC, name), -1);
        CombatLoader.WantNPC(cls, GlobalNPC);
    }

    // type -1: um Global, que vale para todo NPC.
    static #Mark(key, type) {
        if (type >= 0) {
            bl.hookMarks.set(key, type);
            return;
        }
        if (NPCLoader.#everyType.has(key)) return;
        NPCLoader.#everyType.add(key);
        const total = Math.max(FIRST_NPC + NPCLoader.ByType.size, Terraria.Main.npcFrameCount.length);
        for (let t = 0; t < total; t++) bl.hookMarks.set(key, t);
    }

    static #Install(has, type) {
        const marked = (key, ...methods) => {
            if (methods.some(has)) NPCLoader.#Mark(key, type);
            return { minType: 0, marks: key };
        };
        const N = Terraria.NPC;
        const of = NPCLoader.Of;
        const globals = globalNPCs;

        NPCEventHooks.Install(has, marked);
        NPCDrawHooks.Install(has, marked);

        const chat = marked('npc.GetChat', 'GetChat');
        if (has('GetChat')) Hooks.Once('npc.GetChat', () => {
            N['string GetChat()'].hook((original, npc) => {
                let text = original(npc);
                const own = NPCLoader.Call(npc, 'GetChat');
                if (typeof own === 'string') text = own;
                const list = globals.For(npc, 'GetChat');
                if (!list.length) return text;
                const ref = new Ref(text);
                for (const g of list) globals.Invoke(g, 'GetChat', npc, ref);
                return String(ref.value);
            }, chat);
        });

        const frame = marked('npc.FindFrame', 'FindFrame');
        if (has('FindFrame')) Hooks.Once('npc.FindFrame', () => {
            const heights = new Map();
            const heightOf = (npc) => {
                let height = heights.get(npc.type);
                if (height === undefined) {
                    const texture = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                    const frames = Math.max(1, Terraria.Main.npcFrameCount[npc.type]);
                    height = texture ? Math.floor(texture.Height / frames) : 0;
                    heights.set(npc.type, height);
                }
                return height;
            };
            N['void FindFrame()'].hook((original, npc) => {
                const m = of(npc);
                if (m && NPCLoader.Has(m, 'FindFrame')) NPCLoader.Call(npc, 'FindFrame', heightOf(npc));
                else original(npc);
                const list = globals.For(npc, 'FindFrame');
                if (list.length) {
                    const height = heightOf(npc);
                    for (const g of list) globals.Invoke(g, 'FindFrame', npc, height);
                }
            }, frame);
        });

        const active = marked('npc.CheckActive', 'CheckActive');
        if (has('CheckActive')) Hooks.Once('npc.CheckActive', () => {
            N['void CheckActive()'].hook((original, npc) => {
                if (NPCLoader.Call(npc, 'CheckActive') === false) return;
                if (!globals.AllCall(npc, 'CheckActive')) return;
                original(npc);
            }, active);
        });

        // No cliente de multijogador o drop é do servidor: PreKill e OnKill só lá.
        const kill = marked('npc.Kill', 'PreKill', 'OnKill');
        if (has('PreKill') || has('OnKill')) Hooks.Once('npc.Kill', () => {
            N['void NPCLoot()'].hook((original, npc) => {
                if (Terraria.Main.netMode === 1) return original(npc);
                if (!globals.AllCall(npc, 'PreKill')) return;
                if (NPCLoader.Call(npc, 'PreKill') === false) return;
                original(npc);
                NPCLoader.Call(npc, 'OnKill');
                globals.Call(npc, 'OnKill');
            }, kill);
        });
    }

    static #globalAI = false;

    static InstallAI(global = false) {
        if (global) {
            NPCLoader.#globalAI = true;
            bl.hookFlags.set('npc.AI.local', false);
            Hooks.Once('gnpc.AI', () => {
                Terraria.NPC['void AI()'].hook((original, npc) => NPCLoader.#AI(original, npc, true));
            });
        } else if (!NPCLoader.#globalAI) {
            Hooks.Once('npc.AI', () => {
                bl.hookFlags.set('npc.AI.local', true);
                Terraria.NPC['void AI()'].hook((original, npc) => NPCLoader.#AI(original, npc, false),
                    { minType: FIRST_NPC, flag: 'npc.AI.local' });
            });
        }
    }

    static #AI(original, npc, global) {
        const m = NPCLoader.Of(npc);
        const plan = m ? NPCLoader.#PlanOf(m) : null;
        let go = !global || globalNPCs.AllCall(npc, 'PreAI');

        if (go && plan && plan.PreAI) {
            try {
                go = m.PreAI(npc) !== false;
            }
            catch (e) {
                Safe.Report(plan.PreAI, e);
            }
        }

        if (go) {
            const aiType = m ? m.AIType | 0 : 0;

            if (aiType > 0) {
                const type = npc.type;
                npc.type = aiType;

                try {
                    original(npc);
                }
                finally {
                    npc.type = type;
                }
            } else {
                original(npc);
            }

            if (plan && plan.AI) {
                try {
                    m.AI(npc);
                }
                catch (e) {
                    Safe.Report(plan.AI, e);
                }
            }

            if (global) globalNPCs.Call(npc, 'AI');
        }

        if (plan && plan.PostAI) {
            try {
                m.PostAI(npc);
            }
            catch (e) {
                Safe.Report(plan.PostAI, e);
            }
        }

        if (global) globalNPCs.Call(npc, 'PostAI');
    }

    // Os rótulos do log de erro, montados uma vez por classe.
    static #plans = new Map();
    static #PlanOf(m) {
        return NPCLoader.#PlanFor(m.constructor);
    }

    static #PlanFor(cls) {
        let plan = NPCLoader.#plans.get(cls);
        if (plan) return plan;

        plan = {};
        for (const name of ['PreAI', 'AI', 'PostAI', 'OnSpawn', 'ResetEffects', 'CheckDead', 'CheckActive', 'FindFrame',
            'PreKill', 'OnKill', 'GetChat', 'ApplyDifficultyAndPlayerScaling', 'CanChat', 'ModifyActiveShop',
            'ModifyNPCHappiness', 'PreDraw', 'PostDraw', 'DrawEffects', 'DrawBehind', 'GetAlpha',
            'BossHeadSlot', 'BossHeadRotation', 'BossHeadSpriteEffects']) {
            plan[name] = Hooks.Overrides(cls, ModNPC, name) ? cls.name + '.' + name : null;
        }
        NPCLoader.#plans.set(cls, plan);
        return plan;
    }

    static Has(m, name) {
        return !!m && !!NPCLoader.#PlanOf(m)[name];
    }

    // O método do NPC de mod (se a classe o escreve); undefined sem ele.
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

    // O de mod e depois cada Global (o mesmo método e argumentos).
    static CallAll(npc, name, a, b, c, d) {
        NPCLoader.Call(npc, name, a, b, c, d);
        globalNPCs.Call(npc, name, a, b, c, d);
    }
}

class NPCEventHooks {
    static Install(has, marked) {
        const N = Terraria.NPC;
        const globals = globalNPCs;
        const spawn = marked('npc.OnSpawn', 'OnSpawn');
        if (has('OnSpawn')) Hooks.Once('npc.OnSpawn', () => {
            N['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'].hook(
                (original, source, x, y, type, start, ai0, ai1, ai2, ai3, target) => {
                    const index = original(source, x, y, type, start, ai0, ai1, ai2, ai3, target);
                    if (index >= 0 && index < 200) {
                        const npc = Terraria.Main.npc[index];
                        if (npc.active) NPCLoader.CallAll(npc, 'OnSpawn', source);
                    }
                    return index;
                }, { ...spawn, arg: 3 });
        });
        const reset = marked('npc.ResetEffects', 'ResetEffects', 'DrawBehind');
        if (has('ResetEffects') || has('DrawBehind')) Hooks.Once('npc.ResetEffects', () => {
            N['void UpdateNPC_BuffSetFlags(bool lowerBuffTime)'].hook((original, npc, lowerBuffTime) => {
                if (NPCLoader.Has(NPCLoader.Of(npc), 'DrawBehind')) NPCDrawHooks.Track(npc);
                NPCLoader.CallAll(npc, 'ResetEffects');
                original(npc, lowerBuffTime);
            }, reset);
        });
        const dead = marked('npc.CheckDead', 'CheckDead');
        if (has('CheckDead')) Hooks.Once('npc.CheckDead', () => {
            N['void checkDead()'].hook((original, npc) => {
                if (npc.active && npc.life <= 0) {
                    if (NPCLoader.Call(npc, 'CheckDead') === false) return;
                    if (!globals.AllCall(npc, 'CheckDead')) return;
                }
                original(npc);
            }, dead);
        });
        // ModNPC.CanChat devolve bool (o padrão é o townNPC); o Global, bool?:
        // false impede, true força.
        const chat = marked('npc.CanChat', 'CanChat');
        if (has('CanChat')) Hooks.Once('npc.CanChat', () => {
            for (const signature of ['bool get_CanTalk()', 'bool get_CanBeTalkedTo()']) {
                N[signature].hook((original, npc) => {
                    let value = NPCLoader.Call(npc, 'CanChat');
                    for (const g of globals.For(npc, 'CanChat')) {
                        const own = globals.Invoke(g, 'CanChat', npc);
                        if (own === false) return false;
                        if (own === true) value = true;
                    }
                    return typeof value === 'boolean' ? value : original(npc);
                }, chat);
            }
        });
        const scaling = marked('npc.Scaling', 'ApplyDifficultyAndPlayerScaling');
        if (has('ApplyDifficultyAndPlayerScaling')) Hooks.Once('npc.Scaling', () => {
            N['void ScaleStats_ByPlayerCount(int numPlayers)'].hook((original, npc, numPlayers) => {
                original(npc, numPlayers);
                const balance = new Ref(1), boost = new Ref(1);
                N['void GetStatScalingFactors(int numPlayers, out float balance, out float boost)'](numPlayers, balance, boost);
                const master = npc.difficulty >= Terraria.DataStructures.GameDifficultyLevel.Master;
                NPCLoader.CallAll(npc, 'ApplyDifficultyAndPlayerScaling', numPlayers, balance.value, master ? .85 : 1);
            }, scaling);
        });
        if (has('ModifyNPCHappiness')) Hooks.Once('npc.Happiness', () => {
            Terraria.GameContent.Personalities.AllPersonalitiesModifier['void ModifyShopPrice(HelperInfo info, ShopHelper shopHelperInstance)'].hook(
                (original, self, info, helper) => {
                    original(self, info, helper);
                    const npc = info.npc;
                    if (!npc) return;
                    const own = NPCLoader.Has(NPCLoader.Of(npc), 'ModifyNPCHappiness');
                    const list = globals.For(npc, 'ModifyNPCHappiness');
                    if (!own && !list.length) return;
                    const player = info.player;
                    const biome = player.ZoneDungeon ? 8 : player.ZoneCorrupt ? 9 : player.ZoneCrimson ? 10
                        : player.ZoneGlowshroom ? 7 : player.ZoneHallow ? 6 : player.ZoneJungle ? 4
                        : player.ZoneSnow ? 2 : player.ZoneBeach ? 5 : player.ZoneDesert ? 3
                        : player.position.Y > Terraria.Main.worldSurface * 16 ? 1 : 0;
                    NPCLoader.Call(npc, 'ModifyNPCHappiness', player, biome, helper, info.nearbyNPCsByType);
                    for (const g of list) {
                        try { g.ModifyNPCHappiness(npc, player, biome, helper, info.nearbyNPCsByType); }
                        catch (e) { Safe.Report(g.constructor.name + '.ModifyNPCHappiness', e); }
                    }
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
        const globals = globalNPCs;
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
                    NPCLoader.CallAll(npc, 'DrawEffects', light);
                    // Os Globais antes do de mod no Pre, depois no Post (como o tModLoader).
                    let go = globals.AllCall(npc, 'PreDraw', batch, screen, light.value);
                    if (go) go = NPCLoader.Call(npc, 'PreDraw', batch, screen, light.value) !== false;
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
                    NPCLoader.CallAll(npc, 'PostDraw', batch, screen, light.value);
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
                for (const g of globals.For(npc, 'GetAlpha')) {
                    const value = globals.Invoke(g, 'GetAlpha', npc, color);
                    if (value !== null && value !== undefined) return value;
                }
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
                    NPCLoader.CallAll(npc, name, value);
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
