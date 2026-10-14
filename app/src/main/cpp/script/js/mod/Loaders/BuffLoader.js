class BuffLoader {
    static ByType = new Map();

    // Os buffs de mod ativos de um jogador ou NPC. Se o fn tirou o buff, o jogo
    // puxou os de trás uma posição, e o que veio para i ainda não rodou.
    static ForEach(entity, fn) {
        const types = entity.buffType;
        const times = entity.buffTime;

        for (let i = 0; i < types.length; i++) {
            const type = types[i];
            if (type < FIRST_BUFF || times[i] <= 0) continue;

            const m = BuffLoader.ByType.get(type);
            if (!m) continue;

            fn(m, i);
            if (types[i] !== type) i--;
        }
    }

    static Hook(cls) {
        const P = Terraria.Player;
        const has = (name) => Hooks.Overrides(cls, ModBuff, name);
        const byType = BuffLoader.ByType;

        // Dentro do UpdateBuffs: antes dos acessórios, como os buffs do jogo.
        if (has('UpdatePlayer')) Hooks.Once('buff.UpdatePlayer', () => {
            P['void UpdateBuffs(int i)'].hook((original, self, i) => {
                original(self, i);
                BuffLoader.ForEach(self, (m, index) =>
                    Safe.Run(m.constructor.name + '.UpdatePlayer', () => m.UpdatePlayer(self, index)));
            });
        });

        if (has('UpdateNPC')) Hooks.Once('buff.UpdateNPC', () => {
            Terraria.NPC['void UpdateNPC_BuffSetFlags(bool lowerBuffTime)'].hook((original, self, lower) => {
                original(self, lower);
                BuffLoader.ForEach(self, (m, index) =>
                    Safe.Run(m.constructor.name + '.UpdateNPC', () => m.UpdateNPC(self, index)));
            });
        });

        if (has('ApplyPlayer')) Hooks.Once('buff.ApplyPlayer', () => {
            P['bool AddBuff_ActuallyTryToAddTheBuff(int type, int time)'].hook((original, self, type, time) => {
                const ok = original(self, type, time);

                const m = ok ? byType.get(type) : undefined;
                if (m) Safe.Run(m.constructor.name + '.ApplyPlayer', () => m.ApplyPlayer(self, time));
                return ok;
            });
        });

        if (has('ReApplyPlayer')) Hooks.Once('buff.ReApplyPlayer', () => {
            P['bool AddBuff_TryUpdatingExistingBuffTime(int type, int time)'].hook((original, self, type, time) => {
                const m = byType.get(type);
                if (!m) return original(self, type, time);

                const index = self['int FindBuffIndex(int type)'](type);
                const renew = index < 0 || Safe.Run(m.constructor.name + '.ReApplyPlayer', () => m.ReApplyPlayer(self, time, index));
                if (renew === false) return true;   // "já estava": o jogo não põe outro

                return original(self, type, time);
            });
        });

        if (has('ApplyNPC') || has('ReApplyNPC')) Hooks.Once('buff.NPC', () => {
            Terraria.NPC['void AddBuff(int type, int time, bool quiet)'].hook((original, self, type, time, quiet) => {
                const m = byType.get(type);
                if (!m) return original(self, type, time, quiet);

                const index = self['int FindBuffIndex(int type)'](type);
                if (index >= 0) {
                    if (Safe.Run(m.constructor.name + '.ReApplyNPC', () => m.ReApplyNPC(self, time, index)) === false) return;
                    return original(self, type, time, quiet);
                }

                original(self, type, time, quiet);
                if (self['int FindBuffIndex(int type)'](type) >= 0) {
                    Safe.Run(m.constructor.name + '.ApplyNPC', () => m.ApplyNPC(self, time));
                }
            });
        });

        // O texto do buff tocado: o GUIBuffs pede o nome ao Lang.GetBuffName (no
        // Draw) e a dica ao Main.GetBuffTooltip (no ChangeBuffString).
        if (has('ModifyBuffText')) Hooks.Once('buff.Text', () => BuffLoader.#HookText(false));
        if (has('PreDraw') || has('PostDraw')) Hooks.Once('buff.Draw', BuffLoader.#HookDraw);

        // O toque no ícone da barra de buffs do jogador local.
        if (has('CanRemove') || has('OnRemove')) Hooks.Once('buff.Remove', () => {
            bl.classOf('', 'GUIBuffs')['void RemoveBuff(int buff)'].hook((original, self, index) => {
                const player = Terraria.Main.player[Terraria.Main.myPlayer];
                const type = player.buffType[index];
                const time = player.buffTime[index];
                const m = byType.get(type);
                if (!m) return original(self, index);

                const debuff = !!Terraria.Main.debuff[type];
                const can = Safe.Run(m.constructor.name + '.CanRemove', () => m.CanRemove(player, time, index, debuff));
                if (can === false) return;

                if (can === true && debuff) player['void DelBuff(int b)'](index);
                else original(self, index);

                if (player.buffType[index] !== type) {
                    Safe.Run(m.constructor.name + '.OnRemove', () => m.OnRemove(player, time, index));
                }
            });
        });
    }


    // Os GlobalBuff: para todo buff, também os do jogo.
    static HookGlobal(cls) {
        const P = Terraria.Player;
        const has = (name) => globalBuffs.Overrides(cls, name);

        if (has('UpdatePlayer')) Hooks.Once('gbuff.UpdatePlayer', () => {
            P['void UpdateBuffs(int i)'].hook((original, self, i) => {
                original(self, i);
                BuffLoader.#EachBuff(self, (type, index) =>
                    globalBuffs.Each('UpdatePlayer', (g) => g.UpdatePlayer(type, self, index)));
            });
        });

        if (has('UpdateNPC')) Hooks.Once('gbuff.UpdateNPC', () => {
            Terraria.NPC['void UpdateNPC_BuffSetFlags(bool lowerBuffTime)'].hook((original, self, lower) => {
                original(self, lower);
                BuffLoader.#EachBuff(self, (type, index) =>
                    globalBuffs.Each('UpdateNPC', (g) => g.UpdateNPC(type, self, index)));
            });
        });

        if (has('ReApplyPlayer')) Hooks.Once('gbuff.ReApplyPlayer', () => {
            P['bool AddBuff_TryUpdatingExistingBuffTime(int type, int time)'].hook((original, self, type, time) => {
                const index = self['int FindBuffIndex(int type)'](type);
                if (index >= 0 && globalBuffs.Any('ReApplyPlayer', (g) => g.ReApplyPlayer(type, self, time, index))) return true;

                return original(self, type, time);
            });
        });

        if (has('ReApplyNPC')) Hooks.Once('gbuff.ReApplyNPC', () => {
            Terraria.NPC['void AddBuff(int type, int time, bool quiet)'].hook((original, self, type, time, quiet) => {
                const index = self['int FindBuffIndex(int type)'](type);
                if (index >= 0 && globalBuffs.Any('ReApplyNPC', (g) => g.ReApplyNPC(type, self, time, index))) return undefined;

                return original(self, type, time, quiet);
            });
        });

        if (has('ModifyBuffText')) Hooks.Once('gbuff.Text', () => BuffLoader.#HookText(true));
        if (has('PreDraw') || has('PostDraw')) Hooks.Once('buff.Draw', BuffLoader.#HookDraw);

        if (has('RightClick')) Hooks.Once('gbuff.RightClick', () => {
            bl.classOf('', 'GUIBuffs')['void RemoveBuff(int buff)'].hook((original, self, index) => {
                const player = Terraria.Main.player[Terraria.Main.myPlayer];
                const type = player.buffType[index];
                if (type > 0 && !globalBuffs.All('RightClick', (g) => g.RightClick(type, index))) return undefined;

                return original(self, index);
            });
        });
    }

    // Os buffs ativos (a lista do jogo não tem buraco). O índice vai num Ref:
    // quem tira o buff diminui 1 dele, e o que veio para o lugar ainda roda.
    static #EachBuff(entity, fn) {
        const types = entity.buffType;
        const times = entity.buffTime;

        for (let i = 0; i < types.length; i++) {
            const type = types[i];
            if (type === 0) break;
            if (times[i] <= 0) continue;

            const index = new Ref(i);
            fn(type, index);
            if (Number.isInteger(index.value)) i = index.value;
        }
    }

    static #texting = false;   // dentro do ModifyBuffText: o nome e a dica saem do jogo
    static #allText = false;   // o gancho de todos os buffs (GlobalBuff) faz o dos de mod

    // { name, tip } depois do ModifyBuffText do buff e dos GlobalBuff, ou null.
    static #Text(type, name, tip) {
        const m = BuffLoader.ByType.get(type);
        const own = m && Hooks.Overrides(m.constructor, ModBuff, 'ModifyBuffText');
        const globals = globalBuffs.With('ModifyBuffText');
        if (!own && !globals.length) return null;

        const buffName = new Ref(name), buffTip = new Ref(tip), rare = new Ref(0);
        BuffLoader.#texting = true;
        try {
            if (own) Safe.Run(m.constructor.name + '.ModifyBuffText', () => m.ModifyBuffText(buffName, buffTip, rare));
            globalBuffs.Each('ModifyBuffText', (g) => g.ModifyBuffText(type, buffName, buffTip, rare));
        } finally {
            BuffLoader.#texting = false;
        }
        return { name: String(buffName.value ?? ''), tip: String(buffTip.value ?? '') };
    }

    static #HookText(all) {
        const Lang = Terraria.Lang;
        if (all) BuffLoader.#allText = true;
        const skip = () => BuffLoader.#texting || (!all && BuffLoader.#allText);

        Lang['string GetBuffName(int id)'].hook((original, id) => {
            const name = original(id);
            if (skip()) return name;
            const text = BuffLoader.#Text(id, name, Lang['string GetBuffDescription(int id)'](id));
            return text ? text.name : name;
        }, all ? {} : { minType: FIRST_BUFF, arg: 0 });

        Terraria.Main['string GetBuffTooltip(Player player, int buffType)'].hook((original, player, type) => {
            const tip = original(player, type);
            if (skip()) return tip;
            BuffLoader.#texting = true;
            let name;
            try {
                name = Lang['string GetBuffName(int id)'](type);
            } finally {
                BuffLoader.#texting = false;
            }
            const text = BuffLoader.#Text(type, name, tip);
            return text ? text.tip : tip;
        }, all ? {} : { minType: FIRST_BUFF, arg: 1 });
    }

    static #look = null;   // o ícone mudado deste ItemDraw

    // O ícone de cada buff ativo sai do GUIBuffs.ItemDraw (index < numActiveBuffs;
    // activeBuffs[index] é a posição na lista do jogador). O que o drawParams
    // muda entra nos desenhos dele (#Restyle).
    static #HookDraw() {
        const GUIBuffs = bl.classOf('', 'GUIBuffs');
        const itemDraw = GUIBuffs['void ItemDraw(ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)'];

        itemDraw.hook((original, self, layout, index, position, scale) => {
            if (index >= self.numActiveBuffs) return original(self, layout, index, position, scale);

            const Main = Terraria.Main;
            const buffIndex = self.activeBuffs[index];
            const type = Main.player[Main.myPlayer].buffType[buffIndex];
            const m = BuffLoader.ByType.get(type);
            const own = m && (Hooks.Overrides(m.constructor, ModBuff, 'PreDraw') || Hooks.Overrides(m.constructor, ModBuff, 'PostDraw'));
            if (!own && !globalBuffs.With('PreDraw').length && !globalBuffs.With('PostDraw').length) {
                return original(self, layout, index, position, scale);
            }

            BuffLoader.#Draw(m, type, buffIndex, position, () => original(self, layout, index, position, scale));
            return undefined;
        });

        Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
            (original, sb, texture, position, source, color, rotation, origin, scale, effects, depth) => {
                const look = BuffLoader.#look;
                if (!look || bl.addressOf(texture) !== look.texture) return original();

                return original(sb, look.newTexture || texture, Vector2.new(position.X + look.dx, position.Y + look.dy),
                    look.source || source, look.color || color, rotation, origin, scale, effects, depth);
            }, { whileIn: itemDraw, flag: 'buff.look' });

        Microsoft.Xna.Framework.Graphics.SpriteBatch['void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
            (original, sb, font, text, position, color, rotation, origin, scale, effects, depth) => {
                const look = BuffLoader.#look;
                if (!look || (!look.tx && !look.ty)) return original();

                return original(sb, font, text, Vector2.new(position.X + look.tx, position.Y + look.ty), color, rotation, origin, scale, effects, depth);
            }, { whileIn: itemDraw, flag: 'buff.look' });
    }

    // A ordem do tModLoader: PreDraw dos Global, depois o do ModBuff;
    // PostDraw do ModBuff, depois os dos Global.
    static #Draw(m, type, buffIndex, position, original) {
        const spriteBatch = Terraria.Main.spriteBatch;
        const texture = Terraria.GameContent.TextureAssets.Buff[type].Value;
        const w = texture.Width, h = texture.Height;
        const params = {
            Texture: texture,
            Position: Vector2.new(position.X, position.Y),
            TextPosition: Vector2.new(position.X, position.Y),
            SourceRectangle: Rectangle.new(0, 0, w, h),
            MouseRectangle: Rectangle.new(position.X - w / 2, position.Y - h / 2, w, h),
            DrawColor: Color.White,
        };
        const before = BuffLoader.#Snapshot(params);

        let draw = globalBuffs.All('PreDraw', (g) => g.PreDraw(spriteBatch, type, buffIndex, params));
        if (draw && m && Hooks.Overrides(m.constructor, ModBuff, 'PreDraw')) {
            draw = Safe.Run(m.constructor.name + '.PreDraw', () => m.PreDraw(spriteBatch, buffIndex, params)) !== false;
        }
        if (draw) BuffLoader.#DrawIcon(params, before, original);

        if (m && Hooks.Overrides(m.constructor, ModBuff, 'PostDraw')) {
            Safe.Run(m.constructor.name + '.PostDraw', () => m.PostDraw(spriteBatch, buffIndex, params));
        }
        globalBuffs.Each('PostDraw', (g) => g.PostDraw(spriteBatch, type, buffIndex, params));
    }

    static #DrawIcon(params, before, original) {
        const after = BuffLoader.#Snapshot(params);
        const changed = Object.keys(before).some((key) => before[key] !== after[key]);
        if (!changed) return original();

        BuffLoader.#look = {
            texture: before.texture,
            newTexture: after.texture !== before.texture ? params.Texture : null,
            dx: after.x - before.x, dy: after.y - before.y,
            tx: after.tx - before.tx, ty: after.ty - before.ty,
            source: after.source !== before.source ? params.SourceRectangle : null,
            color: after.color !== before.color ? params.DrawColor : null,
        };
        bl.hookFlags.set('buff.look', true);
        try {
            return original();
        } finally {
            BuffLoader.#look = null;
            bl.hookFlags.set('buff.look', false);
        }
    }

    // Em números: o struct que o mod mexe (Position += ...) é o mesmo.
    static #Snapshot(p) {
        const c = p.DrawColor, s = p.SourceRectangle;
        return {
            texture: bl.addressOf(p.Texture),
            x: p.Position.X, y: p.Position.Y,
            tx: p.TextPosition.X, ty: p.TextPosition.Y,
            source: s.X + ',' + s.Y + ',' + s.Width + ',' + s.Height,
            color: ((c.R << 24) | (c.G << 16) | (c.B << 8) | c.A) >>> 0,
        };
    }
}
