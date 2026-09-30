// As montarias de mod, como o MountLoader do tModLoader. O tipo sai no
// registro (MountID.Count cresce: é ele que o SetMount, o Initialize e o
// GetHeightBoost do celular conferem), para o buff e o item pedirem
// ModContent.MountType antes de tudo carregado. O MountData, as texturas e o
// SetStaticDefaults vêm com o jogo pronto (Ready, fase 'setup'), quando o
// Mount.mounts já existe e os tipos de buff e de poeira também.
class MountLoader {
    static ByType = new Map();
    static VanillaCount = 0;
    static #pending = [];
    static #specific = new Map();   // whoAmI -> dado da montaria (o _mountSpecificData)
    static #setting = null;         // o SetMount de mod em curso: { mod, player, called }
    static #dismounting = null;     // o Dismount de mod em curso: { skip }

    static SETS = ['CanUseHooks', 'CanDash', 'DoesNotOverrideBodyFrames', 'DoesNotOverrideLegFrames',
                   'DoesNotOverrideBackpackDraw', 'DoesNotOverrideWings', 'IsRollerSkates', 'Cart',
                   'IsTransformationMount', 'PlayerIsHidden', 'DontHoldItems', 'DontDismountWhenCCed'];

    static Add(inst) {
        const MountID = Terraria.ID.MountID;
        if (!MountLoader.VanillaCount) MountLoader.VanillaCount = MountID.Count;

        inst.Type = MountID.Count;
        MountID.Count = inst.Type + 1;
        MountLoader.ByType.set(inst.Type, inst);

        // Os arquivos agora, com o mod na pilha: no Ready ele já saiu.
        inst.__textures = {};
        for (const layer of Object.values(MountTextureType)) {
            const file = ModFiles.Texture(inst.Texture + '_' + layer);
            if (bl.file.exists(file)) inst.__textures[layer] = bl.mod.path + '/' + file;
        }

        MountLoader.#pending.push(inst);
        if (MountLoader.#pending.length === 1) Ready.Add(() => MountLoader.#Install(), 'setup');
        MountLoader.#Hook(inst.constructor);
    }

    static SpecificData(player) { return MountLoader.#specific.get(player.whoAmI); }
    static SetSpecificData(player, value) { MountLoader.#specific.set(player.whoAmI, value); }

    // O campo do MountData de cada camada: Back -> backTexture, FrontGlow -> frontTextureGlow.
    static #Field(layer) {
        return layer.startsWith('Back') ? 'backTexture' + layer.slice(4) : 'frontTexture' + layer.slice(5);
    }

    static #Install() {
        const pending = MountLoader.#pending.splice(0);
        if (!pending.length) return;

        const Mount = Terraria.Mount;
        const Sets = Terraria.ID.MountID.Sets;
        const total = Terraria.ID.MountID.Count;
        if (Mount.mounts.length < total) Mount.mounts = Mount.mounts.cloneResized(total);
        for (const name of MountLoader.SETS) {
            const set = Sets[name];
            if (set && set.length < total) Sets[name] = set.cloneResized(total);
        }

        for (const inst of pending) {
            const name = inst.constructor.name;
            const data = Mount.MountData.new();
            data['void .ctor()']();
            for (const [layer, file] of Object.entries(inst.__textures)) {
                Safe.Run(name + ' (' + layer + ')', () => { data[MountLoader.#Field(layer)] = bl.loadTextureAsset(file); });
            }
            inst.MountData = data;
            Mount.mounts[inst.Type] = data;
            Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        }

        // Buff com BuffID.Sets.MountType: o jogo monta o jogador sozinho
        // (UpdateBuffs), e sem tempo na tela e sem salvar, como o Main faz na
        // inicialização com os dele (e o tModLoader com os de mod).
        const mountOf = Terraria.ID.BuffID.Sets.MountType;
        const Main = Terraria.Main;
        for (const type of BuffLoader.ByType.keys()) {
            if (type >= mountOf.length || mountOf[type] === -1) continue;
            Main.buffNoTimeDisplay[type] = true;
            Main.buffNoSave[type] = true;
        }
        bl.log('montarias de mod: ' + pending.length + ' (tipos ' + pending[0].Type + '..' + (total - 1) + ')');
    }

    // O jogador montado neste Mount: o JumpHeight/JumpSpeed do celular não o
    // recebe (o tModLoader acrescentou o parâmetro).
    static #OwnerOf(mount) {
        const Main = Terraria.Main;
        const address = bl.addressOf(mount);
        const local = Main.player[Main.myPlayer];
        if (local && bl.addressOf(local.mount) === address) return local;
        for (let i = 0; i < 255; i++) {
            const p = Main.player[i];
            if (p && p.active && bl.addressOf(p.mount) === address) return p;
        }
        return null;
    }

    static #Hook(cls) {
        const has = (name) => Hooks.Overrides(cls, ModMount, name);
        const M = Terraria.Mount;
        const byType = MountLoader.ByType;
        // Só montaria de mod entra no JS: o _type do próprio Mount.
        const own = { minType: MountLoader.VanillaCount, field: '_type' };

        if (has('SetMount') || has('Dismount')) Hooks.Once('mount.SetMount', () => {
            // O SetMount do mod roda depois do FinalizeMountData, no lugar da
            // poeira (o DoSpawnDust), como no tModLoader; sem poeira
            // (ignoreEffect), no fim.
            M['void SetMount(int m, Player mountedPlayer, bool ignoreEffect)'].hook((original, self, m, player, ignoreEffect) => {
                const mod = byType.get(m);
                if (!mod || self._type === m) return original(self, m, player, ignoreEffect);

                MountLoader.#specific.delete(player.whoAmI);
                const setting = { mod, called: false };
                MountLoader.#setting = setting;
                try {
                    original(self, m, player, ignoreEffect);
                } finally {
                    MountLoader.#setting = null;
                }
                if (!setting.called && self._type === m) {
                    Safe.Run(mod.constructor.name + '.SetMount', () => mod.SetMount(player, new Ref(false)));
                }
            }, { minType: MountLoader.VanillaCount, arg: 0 });

            M['void DoSpawnDust(Player mountedPlayer, bool isDismounting)'].hook((original, self, player, isDismounting) => {
                const setting = MountLoader.#setting;
                if (!isDismounting && setting && !setting.called) {
                    setting.called = true;
                    const skip = new Ref(false);
                    Safe.Run(setting.mod.constructor.name + '.SetMount', () => setting.mod.SetMount(player, skip));
                    if (skip.value) return;
                }
                const dismounting = MountLoader.#dismounting;
                if (isDismounting && dismounting && dismounting.skip) return;
                original(self, player, isDismounting);
            }, own);

            M['void Dismount(Player mountedPlayer, bool ignoreEffect)'].hook((original, self, player, ignoreEffect) => {
                const mod = byType.get(self._type);
                const skip = new Ref(false);
                if (mod) Safe.Run(mod.constructor.name + '.Dismount', () => mod.Dismount(player, skip));
                MountLoader.#dismounting = { skip: !!skip.value };
                try {
                    original(self, player, ignoreEffect);
                } finally {
                    MountLoader.#dismounting = null;
                    MountLoader.#specific.delete(player.whoAmI);
                }
            }, own);
        });

        if (has('UpdateEffects')) Hooks.Once('mount.UpdateEffects', () => {
            M['void UpdateEffects(Player mountedPlayer)'].hook((original, self, player) => {
                const mod = byType.get(self._type);
                if (mod) Safe.Run(mod.constructor.name + '.UpdateEffects', () => mod.UpdateEffects(player));
                original(self, player);
            }, own);
        });

        if (has('UpdateFrame')) Hooks.Once('mount.UpdateFrame', () => {
            M['void UpdateFrame(Player mountedPlayer, int state, Vector2 velocity)'].hook((original, self, player, state, velocity) => {
                const mod = byType.get(self._type);
                if (mod && Safe.Run(mod.constructor.name + '.UpdateFrame', () => mod.UpdateFrame(player, state, velocity)) === false) return;
                original(self, player, state, velocity);
            }, own);
        });

        if (has('JumpHeight')) Hooks.Once('mount.JumpHeight', () => {
            M['int JumpHeight(float xVelocity)'].hook((original, self, xVelocity) => {
                const value = new Ref(original(self, xVelocity));
                const mod = byType.get(self._type);
                const player = mod && MountLoader.#OwnerOf(self);
                if (player) Safe.Run(mod.constructor.name + '.JumpHeight', () => mod.JumpHeight(player, value, xVelocity));
                return Math.trunc(value.value);
            }, own);
        });

        if (has('JumpSpeed')) Hooks.Once('mount.JumpSpeed', () => {
            M['float JumpSpeed(float xVelocity)'].hook((original, self, xVelocity) => {
                const value = new Ref(original(self, xVelocity));
                const mod = byType.get(self._type);
                const player = mod && MountLoader.#OwnerOf(self);
                if (player) Safe.Run(mod.constructor.name + '.JumpSpeed', () => mod.JumpSpeed(player, value, xVelocity));
                return value.value;
            }, own);
        });

        if (has('UseAbility')) Hooks.Once('mount.UseAbility', () => {
            M['void UseAbility(Player mountedPlayer, Vector2 mousePosition, bool toggleOn)'].hook((original, self, player, mouse, toggleOn) => {
                original(self, player, mouse, toggleOn);
                const mod = byType.get(self._type);
                if (mod) Safe.Run(mod.constructor.name + '.UseAbility', () => mod.UseAbility(player, mouse, toggleOn));
            }, own);
        });

        if (has('AimAbility')) Hooks.Once('mount.AimAbility', () => {
            M['bool AimAbility(Player mountedPlayer, Vector2 mousePosition)'].hook((original, self, player, mouse) => {
                const mod = byType.get(self._type);
                if (mod) Safe.Run(mod.constructor.name + '.AimAbility', () => mod.AimAbility(player, mouse));
                return original(self, player, mouse);
            }, own);
        });

        if (has('Draw')) Hooks.Once('mount.Draw', () => {
            M['void Draw(PlayerDrawSet playerDrawData, int drawType, Player drawPlayer, Vector2 Position, Color drawColor, SpriteEffects playerEffect, float shadow)'].hook(
                (original, self, set, drawType, player, position, drawColor, effect, shadow) => {
                    const mod = byType.get(self._type);
                    const done = mod && Safe.Run(mod.constructor.name + '.Draw',
                        () => MountLoader.#Draw(mod, self, set, drawType, player, position, drawColor, effect, shadow));
                    if (!done) original(self, set, drawType, player, position, drawColor, effect, shadow);
                }, own);
        });
    }

    static #LAYERS = [['backTexture', 'backTextureGlow'], ['backTextureExtra', 'backTextureExtraGlow'],
                      ['frontTexture', 'frontTextureGlow'], ['frontTextureExtra', 'frontTextureExtraGlow']];

    // O que o Mount.Draw do jogo desenharia numa montaria sem caso especial
    // (as de mod), em Ref para o ModMount.Draw. true: já resolvido (o mod pediu
    // para não desenhar, ou mudou algo e desenhamos com os valores dele);
    // false: o do jogo desenha.
    static #Draw(mod, mount, set, drawType, player, position, drawColor, effect, shadow) {
        const layer = MountLoader.#LAYERS[drawType];
        const data = mount._data;
        const asset = layer && data[layer[0]];
        const texture = asset && asset.Value;
        if (!texture) return false;
        const glowAsset = data[layer[1]];
        const glow = glowAsset ? glowAsset.Value : null;

        const screen = Terraria.Main.screenPosition;
        const xOffset = mount.XOffset * (player.direction <= 0 ? -1 : 1);
        const yOffset = mount.YOffset + mount.PlayerOffset;
        const frameHeight = Math.trunc(data.textureHeight / data.totalFrames);
        const glowColor = Color.new(drawColor.R * 0.25 + 191.25, drawColor.G * 0.25 + 191.25,
                                    drawColor.B * 0.25 + 191.25, drawColor.A * 0.25 + 191.25);
        let spriteEffects = effect;
        if (mount.Cart && Math.sign(player.velocity.X) === -player.direction) spriteEffects = effect ^ 1;

        const refs = {
            texture: new Ref(texture),
            glowTexture: new Ref(glow),
            drawPosition: new Ref(Vector2.new(Math.trunc(position.X - screen.X + player.width / 2 + xOffset),
                                              Math.trunc(position.Y - screen.Y + player.height / 2 + yOffset))),
            frame: new Ref(Rectangle.new(0, frameHeight * mount._frame, data.textureWidth, frameHeight - 2)),
            drawColor: new Ref(Color.new(drawColor.R, drawColor.G, drawColor.B, drawColor.A)),
            glowColor: new Ref(glowColor),
            rotation: new Ref(0),
            spriteEffects: new Ref(spriteEffects),
            drawOrigin: new Ref(mount.Origin),
            drawScale: new Ref(1),
        };
        const before = MountLoader.#Snapshot(refs);
        const r = refs;
        const draw = mod.Draw(set, drawType, player, r.texture, r.glowTexture, r.drawPosition, r.frame, r.drawColor,
                              r.glowColor, r.rotation, r.spriteEffects, r.drawOrigin, r.drawScale, shadow);
        if (draw === false) return true;
        if (MountLoader.#Snapshot(refs) === before) return false;

        // Mudou algo: o desenho do jogo com os valores do mod.
        const add = (tex, color) => {
            const d = MountLoader.NewDrawData(tex, r.drawPosition.value, r.frame.value, color, r.rotation.value,
                                              r.drawOrigin.value, r.drawScale.value, r.spriteEffects.value);
            d.shader = Terraria.Mount.currentShader;
            MountLoader.AddDrawData(set, d);
        };
        if (r.texture.value) add(r.texture.value, r.drawColor.value);
        if (r.glowTexture.value) {
            const c = r.glowColor.value, alpha = r.drawColor.value.A / 255;
            add(r.glowTexture.value, Color.new(c.R * alpha, c.G * alpha, c.B * alpha, c.A * alpha));
        }
        return true;
    }

    static #Snapshot(r) {
        const v = (x) => (x && typeof x === 'object' ? bl.addressOf(x) : x);
        const p = r.drawPosition.value, f = r.frame.value, c = r.drawColor.value, g = r.glowColor.value, o = r.drawOrigin.value;
        return [v(r.texture.value), v(r.glowTexture.value), p.X, p.Y, f.X, f.Y, f.Width, f.Height,
                c.R, c.G, c.B, c.A, g.R, g.G, g.B, g.A, r.rotation.value, r.spriteEffects.value,
                o.X, o.Y, r.drawScale.value].join(',');
    }

    // new DrawData(texture, position, sourceRect, color, rotation, origin, scale, effect).
    static NewDrawData(texture, position, sourceRect, color, rotation = 0, origin = null, scale = 1, effect = 0) {
        const d = Terraria.DataStructures.DrawData.new();
        d['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
            texture, position, sourceRect, color, rotation, origin || Vector2.new(0, 0), scale, effect, 0);
        return d;
    }

    // O playerDrawData.Add do tModLoader: o PlayerDrawSet do celular guarda num
    // array de tamanho fixo (o AddDrawData do jogo nem confere o fim).
    static AddDrawData(set, data) {
        const cache = set.DrawDataCache;
        const count = set.DrawDataCacheCount;
        if (!cache || count >= cache.length) return false;
        cache[count] = data;
        set.DrawDataCacheCount = count + 1;
        return true;
    }
}
