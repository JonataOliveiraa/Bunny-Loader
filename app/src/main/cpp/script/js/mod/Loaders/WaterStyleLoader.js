// As águas de mod, como o WaterStylesLoader do tModLoader, no código do celular:
//   - as tabelas por estilo crescem (LiquidRenderer._liquidTextures, as
//     TextureAssets.Liquid e LiquidSlope, e o liquidAlpha e o activeLiquidAlpha,
//     que o celular guarda no LocalUserGameState e confere a cada quadro);
//   - os laços do Main.DrawWaters vão até o último estilo de mod
//     (bl.tiles.setWaterStyleCount, content/tiles/ModWater.h): o jogo faz o
//     fade, a lista de estilos visíveis (a água atrás de rampas) e o desenho;
//   - o CalculateWaterStyle devolve a água da cena, como o tModLoader: só no
//     caso padrão do jogo (fonte, lua de sangue e os fundos de bioma do jogo
//     ganham) e com Priority BiomeLow ou maior;
//   - o respingo (Dust.dustWater), a gota (o NewGore do EmitLiquidDrops, que
//     no celular é 705 + estilo), a chuva (a variante e a textura), a luz
//     através da água (LightingEngine.UpdateLightDecay), a cachoeira
//     (WaterfallManager.Draw) e a tintura de bioma (a do cabelo).
class WaterStyleLoader {
    static List = [];
    static VanillaCount = 15;          // Main.maxLiquidTypes
    static #installed = false;
    static #counted = false;
    static #dripGores = new Set();     // gores de gota de mod já pingados (como o UpdateType do tModLoader)
    static #rainAssets = new Map();     // arquivo -> Asset<Texture2D> da chuva, ou null (falhou)
    static #raining = null;             // a água de mod do MakeRain em curso

    static get TotalCount() { return WaterStyleLoader.VanillaCount + WaterStyleLoader.List.length; }

    static Add(inst) {
        inst.Slot = WaterStyleLoader.TotalCount;
        const base = bl.mod.path + '/';
        const file = (name) => {
            const rel = ModFiles.Texture(name, bl.mod.path);
            return bl.file.exists(base + rel) ? base + rel : null;
        };
        inst.__files = { water: file(inst.Texture), block: file(inst.Texture + '_Block'), slope: file(inst.Texture + '_Slope') };
        WaterStyleLoader.List.push(inst);
        Ready.Add(WaterStyleLoader.#Install);
    }

    static Get(slot) {
        return WaterStyleLoader.List[slot - WaterStyleLoader.VanillaCount];
    }

    // O estilo da água que o jogo está usando, se for de mod.
    static Current() {
        return WaterStyleLoader.Get(Terraria.Main.waterStyle);
    }

    // Na thread do jogo, com o conteúdo pronto.
    static #Install() {
        if (WaterStyleLoader.#installed) return;
        WaterStyleLoader.#installed = true;

        const LR = Terraria.GameContent.Liquid.LiquidRenderer;
        const T = Terraria.GameContent.TextureAssets;
        const total = WaterStyleLoader.TotalCount;
        LR._liquidTextures = LR._liquidTextures.cloneResized(total);
        T.Liquid = T.Liquid.cloneResized(total);
        T.LiquidSlope = T.LiquidSlope.cloneResized(total);

        for (const style of WaterStyleLoader.List) {
            const name = style.constructor.name;
            const files = style.__files;
            if (!files.water || !files.block) bl.log(`água de mod ${name}: falta ${style.Texture}.png ou ${style.Texture}_Block.png`);
            Safe.Run('água ' + name, () => {
                // Sem a textura, a do estilo da floresta (0): nunca um espaço vazio que o desenho leria.
                LR._liquidTextures[style.Slot] = files.water ? bl.loadTextureAsset(files.water) : LR._liquidTextures[0];
                T.Liquid[style.Slot] = files.block ? bl.loadTextureAsset(files.block) : T.Liquid[0];
                T.LiquidSlope[style.Slot] = files.slope ? bl.loadTextureAsset(files.slope) : T.Liquid[style.Slot];
            });
        }

        WaterStyleLoader.#HookDraw();
        WaterStyleLoader.#HookEffects();
        if (WaterStyleLoader.List.some((s) => Hooks.Overrides(s.constructor, ModWaterStyle, 'BiomeHairColor'))) {
            WaterStyleLoader.#HookHairDye();
        }
        bl.log('águas de mod: ' + WaterStyleLoader.List.length + ' (estilos ' + WaterStyleLoader.VanillaCount + '..' + (total - 1) + ')');
    }

    // O liquidAlpha e o activeLiquidAlpha moram no LocalUserGameState, que pode
    // nascer de novo: conferidos antes de cada DrawWaters. Os laços só crescem
    // depois das tabelas.
    static #Ensure() {
        const Main = Terraria.Main;
        const total = WaterStyleLoader.TotalCount;
        if (Main.liquidAlpha.length < total) Main.liquidAlpha = Main.liquidAlpha.cloneResized(total);
        if (Main.activeLiquidAlpha.length < total) Main.activeLiquidAlpha = Main.activeLiquidAlpha.cloneResized(total);
        if (!WaterStyleLoader.#counted) {
            WaterStyleLoader.#counted = true;
            if (!bl.tiles.setWaterStyleCount(total)) bl.log('águas de mod: o Main.DrawWaters não aceitou os estilos de mod (ver o log do núcleo)');
        }
    }

    // O fundo de bioma do jogo que escolhe a água dele (o switch do CalculateWaterStyle).
    static #VANILLA_BG = new Set([1, 2, 3, 4, 5, 6, 7, 8, 13, 14]);

    static #Choose(vanilla, ignoreFountains) {
        const Main = Terraria.Main;
        if (Main.gameMenu) return vanilla;

        const scene = SceneEffectLoader.Of(Main.LocalPlayer).waterStyle;
        if (!scene || scene.priority < SceneEffectPriority.BiomeLow || !WaterStyleLoader.Get(scene.value)) return vanilla;
        if (!ignoreFountains && Main.SceneMetrics.ActiveFountainColor >= 0) return vanilla;
        if (Main.bloodMoon && !Main.dayTime) return vanilla;
        if (WaterStyleLoader.#VANILLA_BG.has(Main.bgStyle)) return vanilla;
        return scene.value;
    }

    static #HookDraw() {
        const Main = Terraria.Main;

        Main['int CalculateWaterStyle(bool ignoreFountains)'].hook((original, ignoreFountains) => {
            const vanilla = original(ignoreFountains);
            return Safe.Run('água: escolha', () => WaterStyleLoader.#Choose(vanilla, ignoreFountains)) ?? vanilla;
        });

        Main['void DrawWaters(bool isBackground)'].hook((original, self, isBackground) => {
            Safe.Run('água: tabelas', () => WaterStyleLoader.#Ensure());
            return original(self, isBackground);
        });

        // A cachoeira de cada água de mod visível, como o DrawWaterfall do
        // WaterStylesLoader, depois das do jogo.
        const WF = Terraria.WaterfallManager;
        WF['void Draw(SpriteBatch spriteBatch)'].hook((original, self, spriteBatch) => {
            original(self, spriteBatch);
            if (Main.gameMenu) return;
            const alpha = Main.liquidAlpha;
            for (const style of WaterStyleLoader.List) {
                if (!(style.Slot < alpha.length) || !(alpha[style.Slot] > 0)) continue;
                const waterfall = Safe.Run(style.constructor.name + '.ChooseWaterfallStyle', () => style.ChooseWaterfallStyle()) | 0;
                self['void DrawWaterfall(SpriteBatch spriteBatch, int Style, float Alpha)'](spriteBatch, waterfall, alpha[style.Slot]);
                WaterfallStyleLoader.Light(self, waterfall);
            }
        });
    }

    static #HookEffects() {
        const Main = Terraria.Main;

        Terraria.Dust['int dustWater()'].hook((original) => {
            const style = WaterStyleLoader.Current();
            if (!style) return original();
            return Safe.Run(style.constructor.name + '.GetSplashDust', () => style.GetSplashDust()) ?? original();
        });

        // A gota: o jogo pede 705 + estilo (um gore que não é gota). A de mod
        // nasce como a gota d'água (706) e troca o tipo depois, como o
        // UpdateType do tModLoader; o Gore.Update dela roda como 706.
        const TD = Terraria.GameContent.Drawing.TileDrawing;
        const emit = TD['void EmitLiquidDrops(int j, int i, byte liquid, ushort typeCache)'];
        emit.hook((original, self, j, i, liquid, typeCache) => original(self, j, i, liquid, typeCache));
        const Gore = Terraria.Gore;
        Gore['int NewGore(Vector2 Position, Vector2 Velocity, int Type, float Scale)'].hook((original, position, velocity, type, scale) => {
            const style = WaterStyleLoader.Current();
            if (!style || type !== 705 + Main.waterStyle) return original(position, velocity, type, scale);

            const drip = Safe.Run(style.constructor.name + '.GetDropletGore', () => style.GetDropletGore()) | 0;
            if (drip < Terraria.ID.GoreID.Count) return original(position, velocity, drip, scale);

            const index = original(position, velocity, 706, scale);
            if (index >= 0 && index < Main.gore.length) Main.gore[index].type = drip;
            WaterStyleLoader.#dripGores.add(drip);
            return index;
        }, { whileIn: emit });
        // Só gores de mod entram no JS (o filtro é nativo).
        Gore['void Update()'].hook((original, self) => {
            const type = self.type;
            if (!WaterStyleLoader.#dripGores.has(type)) return original(self);
            self.type = 706;
            try {
                return original(self);
            } finally {
                if (self.active) self.type = type;
            }
        }, { minType: Terraria.ID.GoreID.Count });

        WaterStyleLoader.#HookRain();

        // A luz através da água, com o 0,91 da luz no ar, como o tModLoader. No
        // celular os dois motores (o novo e o antigo, do Retro) usam um LightMap.
        const r = new Ref(0), g = new Ref(0), b = new Ref(0);
        const Vector3 = Microsoft.Xna.Framework.Vector3;
        const decay = (original, self) => {
            original(self);
            const style = WaterStyleLoader.Current();
            if (!style || !Hooks.Overrides(style.constructor, ModWaterStyle, 'LightColorMultiplier')) return;
            Safe.Run(style.constructor.name + '.LightColorMultiplier', () => {
                style.LightColorMultiplier(r, g, b);
                const v = Vector3.new();
                v['void .ctor(float x, float y, float z)'](r.value * 0.91, g.value * 0.91, b.value * 0.91);
                self._workingLightMap.LightDecayThroughWater = v;
            });
        };
        Terraria.Graphics.Light.LightingEngine['void UpdateLightDecay()'].hook(decay);
        Terraria.Graphics.Light.LegacyLighting['void UpdateLightDecay()'].hook(decay);
    }

    // A chuva de água de mod, uma entrada no JS por quadro (era uma por gota,
    // a cada quadro: centenas com chuva forte, e o jogo travava).
    //   - O tipo da gota é 3 × Main.waterStyle + 0..2 (Rain.NewRain), e o
    //     desenho lê a coluna type × 4 da TextureAssets.Rain. Com a água de mod,
    //     o MakeRain roda com o estilo 0: as gotas novas saem nas colunas 0..2,
    //     as três variantes da textura de chuva do mod (ou as da floresta).
    //   - O DrawRain desenha com a TextureAssets.Rain trocada pela textura do
    //     GetRainTexture da água atual, e a devolve.
    //   - O GetRainVariant só entra se a água o mudar (o padrão é o sorteio de
    //     0..2 do jogo), gota a gota, só dentro do MakeRain com água de mod.
    //   - O NewRainForced (a do Eyebrella, rara) não passa pelo MakeRain.
    // Como no TL Pro: as gotas que já caíam ao trocar de água são desenhadas
    // com a textura nova até sumirem.
    static #HookRain() {
        const Main = Terraria.Main;
        const Rain = Terraria.Rain;
        const T = Terraria.GameContent.TextureAssets;
        const variant = (style) => (Safe.Run(style.constructor.name + '.GetRainVariant', () => style.GetRainVariant()) | 0) & 0xFF;
        const custom = WaterStyleLoader.List.some((s) => Hooks.Overrides(s.constructor, ModWaterStyle, 'GetRainVariant'));

        const make = Rain['void MakeRain()'];
        make.hook((original) => {
            const style = WaterStyleLoader.Current();
            if (!style) return original();
            const water = Main.waterStyle;
            Main.waterStyle = 0;
            WaterStyleLoader.#raining = style;
            try {
                return original();
            } finally {
                Main.waterStyle = water;
                WaterStyleLoader.#raining = null;
            }
        });
        if (custom) {
            Rain['int NewRain(Vector2 Position, Vector2 Velocity)'].hook((original, position, velocity) => {
                const index = original(position, velocity);
                const style = WaterStyleLoader.#raining;
                if (style && index >= 0 && index < Main.rain.length && Hooks.Overrides(style.constructor, ModWaterStyle, 'GetRainVariant')) {
                    Main.rain[index].type = variant(style);
                }
                return index;
            }, { whileIn: make });
        }
        Rain['int NewRainForced(Vector2 Position, Vector2 Velocity)'].hook((original, position, velocity) => {
            const index = original(position, velocity);
            const style = WaterStyleLoader.Current();
            if (style && index >= 0 && index < Main.rain.length) Main.rain[index].type = variant(style);
            return index;
        });

        const textured = WaterStyleLoader.List.filter((s) => Hooks.Overrides(s.constructor, ModWaterStyle, 'GetRainTexture'));
        if (!textured.length) return;
        for (const style of textured) WaterStyleLoader.#RainAsset(style);   // carrega já, na thread do jogo
        Main['void DrawRain()'].hook((original, self) => {
            const style = WaterStyleLoader.Current();
            const own = style ? WaterStyleLoader.#RainAsset(style) : null;
            if (!own) return original(self);
            const rain = T.Rain;
            T.Rain = own;
            try {
                return original(self);
            } finally {
                T.Rain = rain;
            }
        });
    }

    // O Asset<Texture2D> do GetRainTexture (caminho no mod ou o Asset), ou null (a do jogo).
    static #RainAsset(style) {
        const got = Safe.Run(style.constructor.name + '.GetRainTexture', () => style.GetRainTexture());
        if (!got) return null;
        if (typeof got !== 'string') return got;

        const name = ModFiles.Texture(ModFiles.TextureName(got), style.Mod);
        const file = (style.Mod ? style.Mod.path + '/' : '') + name;
        let asset = WaterStyleLoader.#rainAssets.get(file);
        if (asset === undefined) {
            asset = null;
            try {
                asset = bl.loadTextureAsset(file);
            } catch (e) {
                bl.log(`água de mod ${style.constructor.name}: sem a chuva ${name} (${e})`);
            }
            WaterStyleLoader.#rainAssets.set(file, asset);
        }
        return asset;
    }

    // A tintura de bioma (o item 1983): o celular a tem numa lambda do
    // DyeInitializer.LoadLegacyHairdyes (a 7ª, b__5_6), que anda a cor do
    // cabelo um ponto por quadro até a da água. Com água de mod, o alvo é o
    // BiomeHairColor dela (no Shimmer, o do jogo).
    static #HookHairDye() {
        const lambdas = Terraria.Initializers.DyeInitializer['<>c'];
        lambdas['Color <LoadLegacyHairdyes>b__5_6(Player player, Color newColor, ref bool lighting)'].hook(
            (original, self, player, newColor, lighting) => {
                const style = WaterStyleLoader.Current();
                if (!style || player.ZoneShimmer) return original(self, player, newColor, lighting);

                const target = Safe.Run(style.constructor.name + '.BiomeHairColor', () => style.BiomeHairColor());
                if (!target) return original(self, player, newColor, lighting);
                const now = player.hairDyeColor;
                const from = now.A === 0 ? target : now;
                const step = (a, b) => (a > b ? a - 1 : a < b ? a + 1 : a);
                return Color.new(step(from.R, target.R), step(from.G, target.G), step(from.B, target.B), from.A);
            });
    }
}
