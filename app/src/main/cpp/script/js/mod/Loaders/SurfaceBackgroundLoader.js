// Os fundos de superfície de mod, como o SurfaceBackgroundStylesLoader do
// tModLoader, nos pontos que a B5.0 mediu no celular (tudo na thread do jogo,
// um DrawSurfaceBG por quadro desenhado):
//   - GetPreferredBGStyleForPlayer: o estilo da cena entra pela Priority, nos
//     mesmos degraus do tModLoader (BiomeHigh ganha de tudo; BiomeMedium da
//     selva, da neve e da floresta; BiomeLow só da floresta);
//   - UpdateBGVisibility_BackLayer: com o estilo de mod, a camada de trás
//     sobe a dele (o jogo subiria a da floresta); FrontLayer: depois do jogo,
//     o ModifyFarFades do estilo;
//   - a camada de longe depois do Step1 das montanhas, a do meio depois do
//     Step2 e a da frente antes do GetFogPower (logo depois das árvores do
//     fundo do jogo). Desenho direto no SpriteBatch, com o lote já aberto.
// Nos menus, só o fundo do tema da tela de título (ModMenu.MenuBackgroundStyle).
class SurfaceBackgroundLoader {
    static List = [];
    static #step1 = null;       // { top, push } do Step1 deste quadro
    static #draw = null;

    // Lido uma vez: o getter atravessava a ponte a cada Get (vários por quadro).
    static #vanillaCount = 0;
    static get VanillaCount() {
        return SurfaceBackgroundLoader.#vanillaCount || (SurfaceBackgroundLoader.#vanillaCount = Terraria.ID.SurfaceBackgroundID.Count);
    }

    static Add(inst) {
        inst.Slot = SurfaceBackgroundLoader.VanillaCount + SurfaceBackgroundLoader.List.length;
        SurfaceBackgroundLoader.List.push(inst);
        SurfaceBackgroundLoader.#Install();
    }

    static Get(slot) {
        return SurfaceBackgroundLoader.List[slot - SurfaceBackgroundLoader.VanillaCount];
    }

    // Os arrays por estilo crescem até os de mod. Conferido a cada quadro: o
    // celular guarda os alphas no LocalUserGameState, que pode nascer de novo.
    static #Ensure() {
        const Main = Terraria.Main;
        const Sets = Terraria.ID.SurfaceBackgroundID.Sets;
        const total = SurfaceBackgroundLoader.VanillaCount + SurfaceBackgroundLoader.List.length;
        if (Main.bgAlphaFrontLayer.length < total) Main.bgAlphaFrontLayer = Main.bgAlphaFrontLayer.cloneResized(total);
        if (Main.bgAlphaFarBackLayer.length < total) Main.bgAlphaFarBackLayer = Main.bgAlphaFarBackLayer.cloneResized(total);
        if (Sets.IsForest.length < total) Sets.IsForest = Sets.IsForest.cloneResized(total);
        if (Sets.IsDesertVariant.length < total) Sets.IsDesertVariant = Sets.IsDesertVariant.cloneResized(total);
    }

    // O estilo pela cena do jogador local, com o que o jogo escolheu.
    static #Choose(vanilla) {
        const Main = Terraria.Main;
        if (Main.gameMenu) return vanilla;

        const scene = SceneEffectLoader.Of(Main.LocalPlayer).surfaceBackground;
        if (!scene || !SurfaceBackgroundLoader.Get(scene.value)) return vanilla;

        const P = SceneEffectPriority;
        const forest = vanilla === 0 || vanilla === 10 || vanilla === 11 || vanilla === 12;
        if (scene.priority >= P.BiomeHigh) return scene.value;
        if (scene.priority >= P.BiomeMedium && (forest || vanilla === 3 || vanilla === 7)) return scene.value;
        if (scene.priority >= P.BiomeLow && forest) return scene.value;
        return vanilla;
    }

    static #Install() {
        Hooks.Once('bg.surface', () => {
            const Main = Terraria.Main;

            // Os arrays só precisam crescer com um estilo de mod em uso (o
            // escolhido agora ou o que ainda está na tela).
            Main['int GetPreferredBGStyleForPlayer()'].hook((original) => {
                const vanilla = original();
                const style = Safe.Run('fundo: escolha', () => SurfaceBackgroundLoader.#Choose(vanilla)) ?? vanilla;
                const count = SurfaceBackgroundLoader.VanillaCount;
                if (style >= count || Main.bgStyle >= count) Safe.Run('fundo: arrays', () => SurfaceBackgroundLoader.#Ensure());
                return style;
            });

            const MODIFY = 'void DrawBG_ModifyBGFarBackLayerAlpha(int desiredBG, Nullable`1 desiredBG2, Nullable`1 transitionAmountOverride)';
            Main['void UpdateBGVisibility_BackLayer(Nullable`1 targetBiomeOverride, Nullable`1 transitionAmountOverride)'].hook((original, self, target, amount) => {
                // Nos menus, o fundo do tema (ModMenu), como o tModLoader faz
                // depois da escolha do fundo do título.
                if (Main.gameMenu && target === null) {
                    const menu = Safe.Run('fundo: tema do menu', () => MenuLoader.BackgroundSlot());
                    if (menu >= 0) {
                        Safe.Run('fundo: arrays', () => SurfaceBackgroundLoader.#Ensure());
                        Main.bgStyle = menu;
                    }
                }
                const style = target !== null ? target : Main.bgStyle;
                if (SurfaceBackgroundLoader.Get(style)) self[MODIFY](style, null, amount);
                else original(self, target, amount);
            });
            Main['void UpdateBGVisibility_FrontLayer(Nullable`1 targetBiomeOverride, Nullable`1 transitionAmountOverride)'].hook((original, self, target, amount) => {
                original(self, target, amount);
                if (Main.gameMenu && !SurfaceBackgroundLoader.#InMenu()) return;
                const style = SurfaceBackgroundLoader.Get(target !== null ? target : Main.bgStyle);
                if (style) {
                    const fades = Main.bgAlphaFrontLayer;
                    const speed = amount !== null ? amount : Main.backgroundLayerTransitionSpeed;
                    Safe.Run(style.constructor.name + '.ModifyFarFades', () => style.ModifyFarFades(fades, speed));
                }
            });

            Main['void DrawSurfaceBG_BackMountainsStep1(float backgroundTopMagicNumber, float bgGlobalScaleMultiplier, int pushBGTopHack)'].hook((original, self, top, scale, push) => {
                original(self, top, scale, push);
                SurfaceBackgroundLoader.#step1 = { top, push };
                SurfaceBackgroundLoader.#DrawBack(self, 'ChooseFarTexture');
            });
            Main['void DrawSurfaceBG_BackMountainsStep2(int pushBGTopHack)'].hook((original, self, push) => {
                original(self, push);
                SurfaceBackgroundLoader.#DrawBack(self, 'ChooseMiddleTexture');
            });
            Main['float DrawSurfaceBG_GetFogPower()'].hook((original) => {
                const step1 = SurfaceBackgroundLoader.#step1;
                SurfaceBackgroundLoader.#step1 = null;
                if (step1) SurfaceBackgroundLoader.#DrawClose(Main.instance, step1);
                return original();
            });
        });
    }

    // Nos menus, só com o fundo de um tema (ModMenu).
    static #InMenu() {
        return (Safe.Run('fundo: tema do menu', () => MenuLoader.BackgroundSlot()) ?? -1) >= 0;
    }

    static #Texture(slot) {
        const Textures = Terraria.GameContent.TextureAssets;
        if (!Number.isInteger(slot) || slot < 0 || slot >= Textures.Background.length) return null;
        const asset = Textures.Background[slot];
        return asset ? asset.Value : null;
    }

    static #Blit(texture, x, y, w, h, color, scale) {
        const draw = SurfaceBackgroundLoader.#draw || (SurfaceBackgroundLoader.#draw =
            Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)']);
        draw(Terraria.Main.spriteBatch, texture, Vector2.new(x, y), Rectangle.new(0, 0, w, h), color, 0, Vector2.new(0, 0), scale, 0, 0);
    }

    // Longe e meio: pela posição, escala e repetições que o jogo deixou na
    // última camada dele (como o tModLoader), com a transparência da camada de
    // trás do estilo.
    static #DrawBack(self, choose) {
        const Main = Terraria.Main;
        if (SurfaceBackgroundLoader.List.length === 0 || (Main.gameMenu && !SurfaceBackgroundLoader.#InMenu())) return;

        const fades = Main.bgAlphaFarBackLayer;
        for (const style of SurfaceBackgroundLoader.List) {
            const alpha = style.Slot < fades.length ? fades[style.Slot] : 0;
            if (!(alpha > 0)) continue;

            const name = style.constructor.name;
            const slot = Safe.Run(name + '.' + choose, () => style[choose]());
            const texture = SurfaceBackgroundLoader.#Texture(slot);
            if (!texture) continue;

            Safe.Run(name + ' (desenho)', () => {
                const color = Color.Multiply(Main.ColorOfSurfaceBackgroundsBase, alpha);
                Main.ColorOfSurfaceBackgroundsModified = color;
                const w = Main.backgroundWidth[slot], h = Main.backgroundHeight[slot];
                const loops = self.bgLoops, startX = self.bgStartX, topY = self.bgTopY;
                const step = Main.bgWidthScaled, scale = Main.bgScale;
                for (let k = 0; k < loops; k++) SurfaceBackgroundLoader.#Blit(texture, startX + step * k, topY, w, h, color, scale);
            });
        }
    }

    // A da frente, como o DrawCloseBackground do tModLoader. A altura segue a
    // das árvores do fundo do jogo: a × (posição da tela) + b + ajuste.
    static #DrawClose(self, step1) {
        const Main = Terraria.Main;
        if (SurfaceBackgroundLoader.List.length === 0 || (Main.gameMenu && !SurfaceBackgroundLoader.#InMenu())) return;

        const fades = Main.bgAlphaFrontLayer;
        for (const style of SurfaceBackgroundLoader.List) {
            const alpha = style.Slot < fades.length ? fades[style.Slot] : 0;
            if (!(alpha > 0)) continue;

            const name = style.constructor.name;
            if (Safe.Run(name + '.PreDrawCloseBackground', () => style.PreDrawCloseBackground(Main.spriteBatch)) === false) continue;

            const scale = new Ref(), parallax = new Ref(), a = new Ref(), b = new Ref();
            scale.value = 1.25;
            parallax.value = 0.37;
            a.value = 1800;
            b.value = 1750;
            const slot = Safe.Run(name + '.ChooseCloseTexture', () => style.ChooseCloseTexture(scale, parallax, a, b));
            const texture = SurfaceBackgroundLoader.#Texture(slot);
            if (!texture) continue;

            Safe.Run(name + ' (desenho da frente)', () => {
                const bgScale = Number(scale.value) * 2;
                const bgParallax = Number(parallax.value);
                const w = Main.backgroundWidth[slot], h = Main.backgroundHeight[slot];
                const width = Math.floor(w * bgScale);
                if (!(width > 0) || !(bgParallax > 0)) return;

                Terraria.Graphics.Effects.SkyManager.Instance['void DrawToDepth(SpriteBatch spriteBatch, float minDepth)'](Main.spriteBatch, 1 / bgParallax);
                const x = Main.screenPosition.X * bgParallax;
                const startX = Math.floor(-(x - width * Math.round(x / width)) - width / 2);
                const topY = Math.floor(step1.top * Number(a.value) + Number(b.value)) + self.scAdj + step1.push;
                const loops = Math.floor(Main.screenWidth / width) + 2;
                if (Main.screenPosition.Y >= Main.worldSurface * 16 + 16) return;

                const color = Color.Multiply(Main.ColorOfSurfaceBackgroundsBase, alpha);
                Main.ColorOfSurfaceBackgroundsModified = color;
                for (let k = 0; k < loops; k++) SurfaceBackgroundLoader.#Blit(texture, startX + width * k, topY, w, h, color, bgScale);
            });
        }
    }
}
