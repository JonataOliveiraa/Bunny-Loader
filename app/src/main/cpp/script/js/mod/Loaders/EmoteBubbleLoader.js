// Os emotes de mod, como o EmoteBubbleLoader do tModLoader. No celular o
// EmoteID.Count é constante (151) e o jogo recorta o emote de uma folha só
// (TextureAssets.Extra[48]): o de mod sairia fora dela. Então a bolha de mod é
// desenhada aqui (o fundo da folha do jogo, o emote da textura do mod), e no
// menu de emotes (GUIEmotesWindow) a textura da folha é trocada pela do mod
// só para o ícone da entrada: o EmoteDraw desenha a bolha da folha, chama o
// GetFrame e lê a textura de novo para o ícone.
//
// Multijogador: o emote que o jogador manda (msg 120) é recusado pelo jogo
// acima de 150; o de mod não chega aos outros (e não quebra nada).
class EmoteBubbleLoader {
    static ByType = new Map();
    static VanillaCount = 151;
    static #pending = [];
    static #categories = new Map();   // categoria -> [ModEmoteBubble]
    static #drawingMenu = null;       // o emote de mod que o menu está desenhando
    static #live = new Set();         // IDs das bolhas de mod (nascidas aqui ou desenhadas)

    // Desligado por ora (2026-09-30): no celular do usuário os emotes de mod não
    // funcionam. A classe é ignorada, sem tipo e sem nenhum hook.
    static Enabled = false;
    static #warned = false;

    static Add(inst) {
        if (!EmoteBubbleLoader.Enabled) {
            if (!EmoteBubbleLoader.#warned) {
                EmoteBubbleLoader.#warned = true;
                bl.log('emotes de mod desligados por ora: ' + inst.constructor.name + ' e os outros ficam de fora');
            }
            return;
        }
        inst.Type = EmoteBubbleLoader.VanillaCount + EmoteBubbleLoader.ByType.size;
        EmoteBubbleLoader.ByType.set(inst.Type, inst);

        const file = ModFiles.Texture(inst.Texture);
        inst.__file = bl.file.exists(file) ? bl.mod.path + '/' + file : null;
        EmoteBubbleLoader.#pending.push(inst);
        if (EmoteBubbleLoader.#pending.length === 1) Ready.Add(() => EmoteBubbleLoader.#Install(), 'setup');
        EmoteBubbleLoader.#Hook(inst.constructor);
    }

    static AddToCategory(inst, categoryId) {
        if (!EmoteBubbleLoader.#categories.has(categoryId)) EmoteBubbleLoader.#categories.set(categoryId, []);
        const list = EmoteBubbleLoader.#categories.get(categoryId);
        if (!list.includes(inst)) list.push(inst);
    }

    static #Name(inst) { return inst.constructor.name; }

    static #Install() {
        const pending = EmoteBubbleLoader.#pending.splice(0);
        for (const inst of pending) {
            Safe.Run(EmoteBubbleLoader.#Name(inst) + ' (textura)', () => {
                if (!inst.__file) throw new Error('sem a textura ' + inst.Texture);
                inst.__asset = bl.loadTextureAsset(inst.__file);
            });
            Safe.Run(EmoteBubbleLoader.#Name(inst) + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        }
        if (pending.length) {
            bl.log('emotes de mod: ' + pending.length + ' (tipos ' + pending[0].Type + '..' +
                   (EmoteBubbleLoader.VanillaCount + EmoteBubbleLoader.ByType.size - 1) + ')');
        }
    }

    // Um método do ModEmoteBubble com this.EmoteBubble apontando a bolha.
    static #Call(inst, bubble, method, fn) {
        inst.EmoteBubble = bubble;
        try {
            return Safe.Run(EmoteBubbleLoader.#Name(inst) + '.' + method, fn);
        } finally {
            inst.EmoteBubble = null;
        }
    }

    static #Hook(cls) {
        const has = (name) => Hooks.Overrides(cls, ModEmoteBubble, name);
        const UI = Terraria.GameContent.UI;
        const Bubble = UI.EmoteBubble;
        const byType = EmoteBubbleLoader.ByType;
        const own = { minType: EmoteBubbleLoader.VanillaCount, field: 'emote' };

        Hooks.Once('emote.draw', () => {
            Bubble['void Draw(SpriteBatch sb)'].hook((original, self, sb) => {
                const inst = byType.get(self.emote);
                if (!inst || !inst.__asset) return original(self, sb);
                EmoteBubbleLoader.#live.add(self.ID);   // também as que vieram pela rede
                EmoteBubbleLoader.#DrawBubble(inst, self, sb);
            }, own);
        });

        Hooks.Once('emote.menu', () => EmoteBubbleLoader.#HookMenu());

        if (has('OnSpawn') || has('UpdateFrame')) Hooks.Once('emote.OnSpawn', () => {
            const spawned = (id) => {
                const bubble = Bubble.byID.ContainsKey(id) ? Bubble.byID.get_Item(id) : null;
                const inst = bubble && byType.get(bubble.emote);
                if (!inst) return;
                EmoteBubbleLoader.#live.add(id);
                EmoteBubbleLoader.#Call(inst, bubble, 'OnSpawn', () => inst.OnSpawn());
            };
            Bubble['int NewBubble(int emoticon, WorldUIAnchor bubbleAnchor, int time)'].hook((original, emote, anchor, time) => {
                const id = original(emote, anchor, time);
                spawned(id);
                return id;
            }, { minType: EmoteBubbleLoader.VanillaCount, arg: 0 });
            Bubble['int NewBubbleNPC(WorldUIAnchor bubbleAnchor, int time, WorldUIAnchor other)'].hook((original, anchor, time, other) => {
                const id = original(anchor, time, other);
                spawned(id);
                return id;
            });
        });

        // O UpdateFrame do tModLoader: o tempo de vida corre, a animação é do
        // mod. No celular o Update de cada bolha está embutido no UpdateAll:
        // o quadro das de mod é guardado antes, e depois o mod decide (true:
        // o passo do jogo, dois quadros a cada 8).
        if (has('UpdateFrame')) Hooks.Once('emote.UpdateFrame', () => {
            Bubble['void UpdateAll()'].hook((original) => {
                const live = EmoteBubbleLoader.#live;
                if (!live.size) return original();
                const byID = Bubble.byID, saved = [];
                for (const id of live) {
                    const bubble = byID.ContainsKey(id) ? byID.get_Item(id) : null;
                    const inst = bubble && byType.get(bubble.emote);
                    if (!inst) { live.delete(id); continue; }
                    if (Hooks.Overrides(inst.constructor, ModEmoteBubble, 'UpdateFrame')) saved.push([bubble, inst, bubble.frame, bubble.frameCounter]);
                }
                original();
                for (const [bubble, inst, frame, counter] of saved) {
                    bubble.frame = frame;
                    bubble.frameCounter = counter;
                    if (EmoteBubbleLoader.#Call(inst, bubble, 'UpdateFrame', () => inst.UpdateFrame()) === false) continue;
                    if (bubble.lifeTime > 0 && ++bubble.frameCounter >= 8) {
                        bubble.frameCounter = 0;
                        if (++bubble.frame >= 2) bubble.frame = 0;
                    }
                }
            });
        });
    }

    // A bolha como o EmoteBubble.Draw do jogo desenha, com o emote do mod.
    static #DrawBubble(inst, bubble, sb) {
        const Main = Terraria.Main;
        const sheet = Terraria.GameContent.TextureAssets.Extra[48].Value;
        const effectRef = new Ref(0);
        const at = bubble['Vector2 GetPosition(out SpriteEffects effect)'](effectRef);
        let position = Vector2.new(Math.floor(at.X), Math.floor(at.Y));
        let effect = effectRef.value;

        const showing = !(bubble.lifeTime < 6 || bubble.lifeTimeStart - bubble.lifeTime < 6);
        const back = Terraria.Utils['Rectangle Frame(Texture2D tex, int horizontalFrames, int verticalFrames, int frameX, int frameY, int sizeOffsetX, int sizeOffsetY)'](
            sheet, 8, Terraria.GameContent.UI.EmoteBubble.EMOTE_SHEET_VERTICAL_FRAMES, showing ? 1 : 0, 0, 0, 0);
        const origin = Vector2.new(Math.trunc(back.Width / 2), back.Height);
        if (Main.player[Main.myPlayer].gravDir === -1) {
            origin.Y = 0;
            effect |= 2;   // SpriteEffects.FlipVertically
            position = Main['Vector2 ReverseGravitySupport(Vector2 pos, float height)'](position, 0);
        }

        const texture = inst.__asset.Value;
        const half = Math.trunc(texture.Width / 2);
        const frame = EmoteBubbleLoader.#Call(inst, bubble, 'GetFrame', () => inst.GetFrame()) ||
            Rectangle.new(half * bubble.frame, 0, half, texture.Height);

        const draw = EmoteBubbleLoader.#Call(inst, bubble, 'PreDraw', () => inst.PreDraw(sb, texture, position, frame, origin, effect));
        if (draw !== false) {
            const signature = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
            sb[signature](sheet, position, back, Color.White, 0, origin, 1, effect, 0);
            if (showing) sb[signature](texture, position, frame, Color.White, 0, origin, 1, effect, 0);
        }
        EmoteBubbleLoader.#Call(inst, bubble, 'PostDraw', () => inst.PostDraw(sb, texture, position, frame, origin, effect));
    }

    static #CATEGORY_METHODS = ['GetEmotesGeneral', 'GetEmotesRPS', 'GetEmotesItems', 'GetEmotesBiomesAndEvents',
                                'GetEmotesTownNPCs', 'GetEmotesCritters', 'GetEmotesBosses'];

    static #HookMenu() {
        const Window = GUIEmotesWindow;

        // Os de mod no fim de cada categoria, se liberados.
        EmoteBubbleLoader.#CATEGORY_METHODS.forEach((name, category) => {
            Window['void ' + name + '(List`1 emotes)'].hook((original, self, list) => {
                original(self, list);
                for (const inst of EmoteBubbleLoader.#categories.get(category) || []) {
                    if (!inst.__asset) continue;
                    if (Safe.Run(EmoteBubbleLoader.#Name(inst) + '.IsUnlocked', () => inst.IsUnlocked()) !== false) list.Add(inst.Type);
                }
            });
        });

        // A entrada de mod: o menu desenha a bolha branca da folha do jogo (o
        // GetFrame devolve a bolha vazia, linha 0, coluna 1) e, no Draw dessa
        // bolha, o ícone do mod vai por cima, com a mesma posição, origem e
        // escala. Os quadros da folha do jogo já têm a bolha; os de mod (do
        // tModLoader também) são só o ícone.
        const emoteDraw = Window['void EmoteDraw(ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)'];
        emoteDraw.hook((original, self, grid, index, position, scale) => {
            const entries = self._emoteEntries;
            const inst = entries && index >= 0 && index < entries.Count ? EmoteBubbleLoader.ByType.get(entries.get_Item(index)) : null;
            if (!inst || !inst.__asset) return original(self, grid, index, position, scale);

            EmoteBubbleLoader.#drawingMenu = { inst, frame: null, drawing: false };
            try {
                original(self, grid, index, position, scale);
            } finally {
                EmoteBubbleLoader.#drawingMenu = null;
            }
        });

        Window['Rectangle GetFrame(int emoteIndex)'].hook((original, self, emoteIndex) => {
            const frame = original(self, emoteIndex);
            const menu = EmoteBubbleLoader.#drawingMenu;
            if (!menu) return frame;
            const inst = menu.inst;
            // Qual dos dois quadros (a animação do menu): o do jogo diz.
            const anim = Math.trunc(frame.X / frame.Width) % 2;
            const own = Safe.Run(EmoteBubbleLoader.#Name(inst) + '.GetFrameInEmoteMenu', () => inst.GetFrameInEmoteMenu(anim, self._frameCounter));
            const texture = inst.__asset.Value;
            const half = Math.trunc(texture.Width / 2);
            menu.frame = own || Rectangle.new(anim * half, 0, half, texture.Height);
            return Rectangle.new(frame.Width, 0, frame.Width, frame.Height);
        });

        const DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
        Microsoft.Xna.Framework.Graphics.SpriteBatch[DRAW].hook(
            (original, self, texture, position, source, color, rotation, origin, scale, effects, depth) => {
                original(self, texture, position, source, color, rotation, origin, scale, effects, depth);
                const menu = EmoteBubbleLoader.#drawingMenu;
                if (!menu || !menu.frame || menu.drawing || !source || source.Y !== 0 || source.X !== source.Width) return;
                menu.drawing = true;
                try {
                    self[DRAW](menu.inst.__asset.Value, position, menu.frame, color, rotation, origin, scale, effects, depth);
                } finally {
                    menu.drawing = false;
                }
            }, { whileIn: emoteDraw });
    }
}
