// Os temas da tela de título (ModMenu), como o MenuLoader do tModLoader, nos
// pontos do celular:
//   - GUILogo.Draw (o logo do título; o do PC fica no DrawMenu): a troca de
//     tema, o Update e o logo do tema. Os dois desenhos do logo do jogo (o de
//     dia e o de noite, cada um com a sua transparência) são segurados: com o
//     centro, a rotação, a escala e a cor deles (a soma) o tema desenha o dele
//     uma vez, ou refaz os do jogo com o que o PreDrawLogo mudou;
//   - DrawSunAndMoon: o sol e a lua do tema, só nos menus;
//   - o fundo (SurfaceBackgroundLoader) pergunta aqui.
// Não há escolha: vale o último tema de mod disponível (o do mod carregado
// por último), e sem nenhum, o título do jogo. A música dos menus é sempre a
// do jogo (a do tema tocava junto com ela).
class MenuLoader {
    static List = [];               // os temas de mod, na ordem de registro
    static #vanilla = null;         // "Terraria": o título do jogo, sem mudança
    static #current = null;
    static #ready = false;
    static #logo = null;            // os desenhos do logo do jogo neste quadro
    static #sky = false;            // a chave 'menu.sky' (o sol e a lua do tema)
    static #textures = new Map();   // menu -> { caminho: Asset }
    static #draw = null;

    static get CurrentMenu() { return MenuLoader.#current; }

    static Add(menu) {
        MenuLoader.List.push(menu);
        MenuLoader.#Install();
    }

    // O estilo de fundo do tema (-1: o do jogo). O da semente especial fica.
    static BackgroundSlot() {
        const menu = MenuLoader.#Active();
        if (!menu || Terraria.WorldGen.drunkWorldGen) return -1;

        const style = Safe.Run(menu.constructor.name + '.MenuBackgroundStyle', () => menu.MenuBackgroundStyle);
        return style && SurfaceBackgroundLoader.Get(style.Slot) === style ? style.Slot : -1;
    }

    // O tema de mod escolhido, nos menus; null com o do jogo.
    static #Active() {
        const menu = MenuLoader.#current;
        return menu && menu !== MenuLoader.#vanilla && Terraria.Main.gameMenu ? menu : null;
    }

    static #Available(menu) {
        if (menu === MenuLoader.#vanilla) return true;

        const value = Safe.Run(menu.constructor.name + '.IsAvailable', () =>
            typeof menu.IsAvailable === 'function' ? menu.IsAvailable() : menu.IsAvailable);
        return value !== false && value !== undefined;
    }

    // O último tema de mod disponível; sem nenhum, o do jogo.
    static #Pick() {
        for (let i = MenuLoader.List.length - 1; i >= 0; i--) {
            if (MenuLoader.#Available(MenuLoader.List[i])) return MenuLoader.List[i];
        }
        return MenuLoader.#vanilla;
    }

    // Uma vez por quadro dos menus, antes do logo.
    static #Tick() {
        const current = MenuLoader.#current || MenuLoader.#vanilla;
        const next = MenuLoader.#Pick();
        MenuLoader.#current = next;
        if (next !== current) {
            Safe.Run(current.constructor.name + '.OnDeselected', () => current.OnDeselected());
            Safe.Run(next.constructor.name + '.OnSelected', () => next.OnSelected());
        }

        const menu = MenuLoader.#current;
        Safe.Run(menu.constructor.name + '.Update', () => menu.Update(Terraria.Main.menuMode === 0));
    }

    // A textura de Logo, SunTexture ou MoonTexture: o caminho no mod do tema
    // (ou 'OutroMod/...'), ou o Asset direto.
    static #Texture(menu, key) {
        const value = Safe.Run(menu.constructor.name + '.' + key, () => menu[key]);
        if (!value) return null;
        if (typeof value !== 'string') return value;

        let cache = MenuLoader.#textures.get(menu);
        if (!cache) MenuLoader.#textures.set(menu, cache = new Map());
        if (cache.has(value)) return cache.get(value);

        let asset = null;
        Safe.Run(menu.constructor.name + '.' + key, () => {
            const file = (menu.Mod && ContentLookup.FindTexture(menu.Mod.uuid + '/' + value)) || ContentLookup.FindTexture(value);
            if (!file) throw new Error("não achei a textura '" + value + "'");
            asset = ContentLookup.RequestFile(file);
        });
        cache.set(value, asset);
        return asset;
    }

    static #Blit(texture, position, source, color, rotation, origin, scale, effects, depth) {
        const draw = MenuLoader.#draw || (MenuLoader.#draw =
            Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)']);
        draw(Terraria.Main.spriteBatch, texture, position, source, color, rotation, origin, scale, effects, depth);
    }

    // O logo do tema com o que o jogo ia desenhar (captured: os desenhos dele).
    static #DrawLogo(menu, captured) {
        if (!captured.length) return;

        const first = captured[0];
        let r = 0, g = 0, b = 0, a = 0;
        for (const d of captured) {
            r += d.color.R;
            g += d.color.G;
            b += d.color.B;
            a += d.color.A;
        }
        const base = Color.new(Math.min(255, r), Math.min(255, g), Math.min(255, b), Math.min(255, a));

        const center = new Ref(), rotation = new Ref(), scale = new Ref(), color = new Ref();
        center.value = Vector2.new(first.position.X, first.position.Y);
        rotation.value = first.rotation;
        scale.value = first.scale;
        color.value = base;

        const sb = Terraria.Main.spriteBatch;
        const name = menu.constructor.name;
        const draw = Safe.Run(name + '.PreDrawLogo', () => menu.PreDrawLogo(sb, center, rotation, scale, color)) !== false;
        const pos = Vector2.new(Number(center.value.X), Number(center.value.Y));
        const rot = Number(rotation.value), sc = Number(scale.value), col = color.value;

        if (draw) {
            const asset = MenuLoader.#Texture(menu, 'Logo');
            const logo = asset ? asset.Value : null;
            if (logo) {
                MenuLoader.#Blit(logo, pos, null, col, rot, Vector2.new(logo.Width / 2, logo.Height / 2), sc, 0, 0);
            } else {
                // O logo do jogo, com a mesma proporção entre o de dia e o de noite.
                const same = col.R === base.R && col.G === base.G && col.B === base.B && col.A === base.A;
                for (const d of captured) {
                    const c = same ? d.color : Color.Multiply(col, a > 0 ? d.color.A / a : 1 / captured.length);
                    MenuLoader.#Blit(d.texture, pos, d.source, c, rot, d.origin, sc, d.effects, d.depth);
                }
            }
        }
        Safe.Run(name + '.PostDrawLogo', () => menu.PostDrawLogo(sb, pos, rot, sc, col));
    }

    // O sol e a lua do tema no lugar dos do jogo, só durante o desenho.
    static #DrawSunAndMoon(original) {
        // Entrou no mundo: o logo não é mais desenhado para religar a chave
        // até a volta aos menus.
        if (!Terraria.Main.gameMenu) {
            MenuLoader.#sky = false;
            bl.hookFlags.set('menu.sky', false);
            return original();
        }
        const menu = MenuLoader.#Active();
        const sun = menu ? MenuLoader.#Texture(menu, 'SunTexture') : null;
        const moon = menu ? MenuLoader.#Texture(menu, 'MoonTexture') : null;
        if (!sun && !moon) return original();

        const Main = Terraria.Main;
        const T = Terraria.GameContent.TextureAssets;
        const moons = T.Moon;
        const index = Main.moonType >= 0 && Main.moonType < moons.length ? Main.moonType : Math.max(0, Math.min(8, Main.moonType));
        const oldSun = T.Sun, oldMoon = moons[index], oldPhase = Main.moonPhase;
        try {
            if (sun) T.Sun = sun;
            if (moon) {
                moons[index] = moon;
                Main.moonPhase = 0;   // um quadro só: o de cima
            }
            return original();
        } finally {
            if (sun) T.Sun = oldSun;
            if (moon) {
                moons[index] = oldMoon;
                Main.moonPhase = oldPhase;
            }
        }
    }

    static #Install() {
        Hooks.Once('menu', () => {
            const vanilla = MenuLoader.#vanilla = Object.create(ModMenu.prototype);
            vanilla.DisplayName = 'Terraria';

            Ready.Add(() => { MenuLoader.#ready = true; });

            const logoDraw = GUILogo['void Draw()'];
            logoDraw.hook((original, self) => {
                if (!MenuLoader.#ready) return original(self);

                Safe.Run('ModMenu (troca)', () => MenuLoader.#Tick());
                const menu = MenuLoader.#current;
                // O sol e a lua do tema só entram no JS com um tema de mod nos menus.
                const sky = !!menu && menu !== vanilla;
                if (sky !== MenuLoader.#sky) {
                    MenuLoader.#sky = sky;
                    bl.hookFlags.set('menu.sky', sky);
                }
                if (!menu || menu === vanilla) {
                    original(self);
                } else {
                    MenuLoader.#logo = [];
                    let captured;
                    try {
                        original(self);
                    } finally {
                        captured = MenuLoader.#logo;
                        MenuLoader.#logo = null;
                    }
                    Safe.Run(menu.constructor.name + ' (logo)', () => MenuLoader.#DrawLogo(menu, captured));
                }
            });

            Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
                (original, sb, texture, position, source, color, rotation, origin, scale, effects, depth) => {
                    const list = MenuLoader.#logo;
                    if (!list) return original();
                    list.push({ texture, position, source, color, rotation, origin, scale, effects, depth });
                }, { whileIn: logoDraw });

            // Com a chave desligada (dentro do mundo, ou o tema do jogo), o
            // desenho do céu de todo quadro nem entra no JS.
            Terraria.Main['void DrawSunAndMoon(SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)'].hook(
                (original) => MenuLoader.#DrawSunAndMoon(original), { flag: 'menu.sky' });
        });
    }
}
