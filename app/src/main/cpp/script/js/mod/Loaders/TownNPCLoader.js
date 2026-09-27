// Moradores: aparência (festa, shimmer, retrato), conversa, mudança, nomes e ataque.
class TownNPCLoader {
    static LOOK_SUFFIXES = ['_Party', '_Shimmer', '_Shimmer_Party', '_Portrait', '_Shimmer_Portrait'];
    static MERCHANT = 17;
    static TRAVELLING_MERCHANT = 368;

    // TownNPCMood.<Classe> de cada cultura; o {0} vira {BiomeName}/{NPCName}.
    static MoodTexts(className) {
        const byKey = new Map();

        for (const culture of Lang.CULTURES) {
            const json = Lang.File(culture);
            const moods = json && json.TownNPCMood && json.TownNPCMood[className];
            if (!moods || typeof moods !== 'object') continue;

            for (const [key, text] of Object.entries(moods)) {
                if (typeof text !== 'string') continue;

                const slot = /Biome$/.test(key) ? '{BiomeName}' : /NPC$/.test(key) ? '{NPCName}' : '{0}';
                if (!byKey.has(key)) byKey.set(key, {});
                byKey.get(key)[culture] = text.replace(/\{0\}/g, slot);
            }
        }
        return byKey;
    }

    // As texturas ao lado da Texture que existem, em caminho absoluto: no
    // onContentReady quem chama já não é o mod.
    static LookFiles(base) {
        const files = {};
        for (const suffix of TownNPCLoader.LOOK_SUFFIXES) {
            const rel = ModFiles.Texture(base + suffix);
            if (bl.file.exists(rel)) files[suffix] = bl.mod.path + '/' + rel;
        }
        return files;
    }

    // Um perfil do jogo (textura por festa e shimmer) e o retrato da conversa.
    static SetupLooks(inst, files) {
        const type = inst.Type;
        const has = (suffix) => suffix in files;
        const asset = (suffix) => bl.loadTextureAsset(files[suffix]);

        if (has('_Party') || has('_Shimmer')) {
            const Profiles = Terraria.GameContent.TownNPCProfiles;
            const head = bl.npcs.headSlot(type);
            const shimmerHead = bl.npcs.headSlot(type, true);
            const profile = Profiles['ITownNPCProfile LegacyWithSimpleShimmer(string subPath, int headIdNormal, int headIdShimmered, bool uniquePartyTexture, bool uniquePartyTextureShimmered)'](
                inst.constructor.name, head, shimmerHead >= 0 ? shimmerHead : head, true, true);

            const normal = Terraria.GameContent.TextureAssets.Npc[type];
            const plain = profile._profiles[0], shimmer = profile._profiles[1];
            plain._defaultNoAlt = normal;
            plain._defaultParty = has('_Party') ? asset('_Party') : normal;
            shimmer._defaultNoAlt = has('_Shimmer') ? asset('_Shimmer') : normal;
            shimmer._defaultParty = has('_Shimmer_Party') ? asset('_Shimmer_Party') : shimmer._defaultNoAlt;
            Profiles.Instance._townNPCProfiles.Add(type, profile);
        }

        if (has('_Portrait')) {
            const Sets = Terraria.ID.NPCID.Sets;
            const portrait = (suffix) => {
                const p = Sets.BasicPortrait('Images/TownNPCs/Portraits/Portrait_Guide');
                p._image = asset(suffix);
                return p;
            };

            let provider = Sets.PrioritizedPortrait();
            if (has('_Shimmer_Portrait')) {
                // A condição "depois do shimmer" do Guia vale para qualquer morador.
                const condition = Sets.NPCPortraits.get_Item(22)._entries.get_Item(0).Condition;
                provider = provider.With(condition, portrait('_Shimmer_Portrait'));
            }
            Sets.NPCPortraits.Add(type, provider.Default(portrait('_Portrait')));
        }
    }

    static CountTownNPCs() {
        const npcs = Terraria.Main.npc;
        let count = 0;
        for (let i = 0; i < npcs.length - 1; i++) {
            const npc = npcs[i];
            if (npc.active && npc.townNPC && npc.type !== TownNPCLoader.TRAVELLING_MERCHANT) count++;
        }
        return count;
    }

    // O NPC com quem o jogador local conversa, e o ModNPC dele.
    static TalkingTo() {
        const Main = Terraria.Main;
        const player = Main.player[Main.myPlayer];
        const i = player ? player.talkNPC : -1;
        const npc = i >= 0 && i < Main.npc.length - 1 ? Main.npc[i] : null;
        const m = npc ? NPCLoader.Of(npc) : undefined;
        return m ? { npc, m } : null;
    }

    static Hook(cls) {
        const has = (name) => Hooks.Overrides(cls, ModNPC, name);

        if (has('CanTownNPCSpawn')) Hooks.Once('npc.TownSpawn', TownNPCLoader.#HookSpawn);

        if (has('CheckConditions')) Hooks.Once('npc.TownRoom', () => {
            const WorldGen = Terraria.WorldGen;

            WorldGen['bool CheckSpecialTownNPCSpawningConditions(int type)'].hook((original, type) => {
                const m = NPCLoader.ByType.get(type);
                if (!m) return original(type);

                const room = () => m.CheckConditions(WorldGen.roomX1, WorldGen.roomX2, WorldGen.roomY1, WorldGen.roomY2);
                return Safe.Run(m.constructor.name + '.CheckConditions', room) !== false;
            });
        });

        if (has('SetNPCNameList')) Hooks.Once('npc.Names', () => {
            Terraria.NPC['string getNewNPCName(int npcType)'].hook((original, type) => {
                const m = NPCLoader.ByType.get(type);
                const names = m ? Safe.Run(m.constructor.name + '.SetNPCNameList', () => m.SetNPCNameList()) : undefined;
                if (!Array.isArray(names) || names.length === 0) return original(type);

                return String(names[Math.floor(Math.random() * names.length)]);
            });
        });

        if (has('SetChatButtons') || has('OnChatButtonClicked')) Hooks.Once('npc.ChatButtons', TownNPCLoader.#HookChatButtons);

        if (has('TownNPCAttackProj')) Hooks.Once('npc.TownAttack', TownNPCLoader.#HookAttack);
    }

    // O jogo zera Main.townNPCCanSpawn e marca os dele a cada checagem; os de
    // mod são marcados logo depois.
    static #HookSpawn() {
        const Main = Terraria.Main;
        const WorldGen = Terraria.WorldGen;
        const anyNPCs = Terraria.NPC['bool AnyNPCs(int Type)'];

        Main['void UpdateTime_SpawnTownNPCs(bool forceUpdate)'].hook((original, force) => {
            original(force);
            if (Main.netMode === 1 || Main.checkForSpawns !== 0) return;

            let towns = -1;
            for (const [type, m] of NPCLoader.ByType) {
                if (!Hooks.Overrides(m.constructor, ModNPC, 'CanTownNPCSpawn') || m.NPCHeadSlot() < 0 || anyNPCs(type)) continue;

                if (towns < 0) towns = TownNPCLoader.CountTownNPCs();
                if (Safe.Run(m.constructor.name + '.CanTownNPCSpawn', () => m.CanTownNPCSpawn(towns)) !== true) continue;

                Main.townNPCCanSpawn[type] = true;
                if (WorldGen.prioritizedTownNPCType === 0) WorldGen.prioritizedTownNPCType = type;
            }
        });
    }

    // O SetupButtonText da conversa é um switch pelo tipo: para o morador de mod
    // ele roda como se fosse o Mercador, e o texto vem do SetChatButtons.
    static #HookChatButtons() {
        const Dialogue = bl.classOf('', 'GUINPCDialogue');

        Dialogue['void SetupButtonText(ref string focusText, ref Texture2D option1Tex, ref string focusText3, ref Texture2D option2Tex, ref int cost, ref bool showHappiness)'].hook(
            (original, self, text1, tex1, text2, tex2, cost, happy) => {
                const talk = TownNPCLoader.TalkingTo();
                if (!talk) return original(self, text1, tex1, text2, tex2, cost, happy);

                const type = talk.npc.type;
                talk.npc.type = TownNPCLoader.MERCHANT;
                try {
                    original(self, text1, tex1, text2, tex2, cost, happy);
                } finally {
                    talk.npc.type = type;
                }

                const buttons = { button: '', button2: '' };
                Safe.Run(talk.m.constructor.name + '.SetChatButtons', () => talk.m.SetChatButtons(talk.npc, buttons));

                const icon = tex1.value;
                text1.value = buttons.button || '';
                tex1.value = buttons.button ? icon : null;
                text2.value = buttons.button2 || '';
                tex2.value = buttons.button2 ? icon : null;
                cost.value = 0;
            });

        const clicked = (first) => (original, self, ...args) => {
            const talk = TownNPCLoader.TalkingTo();
            if (!talk) return original(self, ...args);

            const n = talk.m.constructor.name;
            const shopName = Safe.Run(n + '.OnChatButtonClicked', () => talk.m.OnChatButtonClicked(talk.npc, first));
            if (typeof shopName !== 'string') return undefined;

            const shop = NPCShop.get(talk.npc.type, shopName);
            if (shop) shop.Open();
            else bl.log('NPCShop: ' + n + ' pediu a loja "' + shopName + '", nao registrada');
            return undefined;
        };
        Dialogue['void Option1Clicked(int healCost)'].hook(clicked(true));
        Dialogue['void Option2Clicked()'].hook(clicked(false));
    }

    // A IA 7 leva o morador ao ataque (ai[0] 10 arremesso, 12 tiro, 14 magia) e
    // conta localAI[3], mas o projétil sai de um switch pelo tipo: para tipo de
    // mod, o tiro sai daqui, no inimigo à vista mais perto.
    static #HookAttack() {
        const Main = Terraria.Main;
        const newProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

        Terraria.NPC['void AI()'].hook((original, npc) => {
            original(npc);

            const m = NPCLoader.Of(npc);
            if (!m || !npc.townNPC || Main.netMode === 1) return;

            const state = npc.ai[0];
            if (state !== 10 && state !== 12 && state !== 14) return;

            const n = m.constructor.name;
            const attack = { projType: 0, attackDelay: 1, damage: npc.damage, knockback: 3, speed: 10,
                             gravityCorrection: 0, randomOffset: 0 };
            Safe.Run(n + '.TownNPCAttackProj', () => m.TownNPCAttackProj(npc, attack));
            if (npc.localAI[3] !== attack.attackDelay || attack.projType <= 0) return;

            Safe.Run(n + '.TownNPCAttackStrength', () => m.TownNPCAttackStrength(npc, attack));
            Safe.Run(n + '.TownNPCAttackProjSpeed', () => m.TownNPCAttackProjSpeed(npc, attack));

            const here = npc.Center;
            const target = TownNPCLoader.#NearestTarget(npc, here);
            let dx = npc.spriteDirection, dy = 0;
            if (target) {
                const c = target.Center;
                dx = c.X - here.X;
                dy = c.Y - attack.gravityCorrection - here.Y;
                const len = Math.hypot(dx, dy) || 1;
                dx /= len;
                dy /= len;
            }

            let vx = dx * attack.speed, vy = dy * attack.speed;
            if (attack.randomOffset) {
                vx += (Math.random() * 2 - 1) * attack.randomOffset;
                vy += (Math.random() * 2 - 1) * attack.randomOffset;
            }

            const damage = npc['int GetAttackDamage_ForTownNPC(float normalDamage)'](attack.damage);
            const i = newProjectile(npc.GetSpawnSource_ForProjectile(), here.X + npc.spriteDirection * 16, here.Y - 2,
                                    vx, vy, attack.projType, damage, attack.knockback, Main.myPlayer, 0, 0, 0, null);
            const proj = Main.projectile[i];
            if (proj) {
                proj.npcProj = true;
                proj.noDropItem = true;
            }
        }, { minType: FIRST_NPC });
    }

    static #NearestTarget(npc, here) {
        const canHit = Terraria.Collision['bool CanHit(Vector2 Position1, int Width1, int Height1, Vector2 Position2, int Width2, int Height2)'];
        const npcs = Terraria.Main.npc;

        let target = null;
        let best = Terraria.ID.NPCID.Sets.DangerDetectRange[npc.type] || 700;
        for (let i = 0; i < npcs.length - 1; i++) {
            const other = npcs[i];
            if (!other.active || other.friendly || other.damage <= 0 || other.townNPC) continue;

            const c = other.Center;
            const distance = Math.hypot(c.X - here.X, c.Y - here.Y);
            if (distance < best && canHit(npc.position, npc.width, npc.height, other.position, other.width, other.height)) {
                best = distance;
                target = other;
            }
        }
        return target;
    }
}
