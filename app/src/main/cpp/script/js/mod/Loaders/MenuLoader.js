// Os temas da tela de título (ModMenu), como o MenuLoader do tModLoader, nos
// pontos do celular:
//   - GUILogo.Draw (o logo do título; o do PC fica no DrawMenu): a troca de
//     tema, o Update e o logo do tema. Os dois desenhos do logo do jogo (o de
//     dia e o de noite, cada um com a sua transparência) são segurados: com o
//     centro, a rotação, a escala e a cor deles (a soma) o tema desenha o dele
//     uma vez, ou refaz os do jogo com o que o PreDrawLogo mudou;
//   - o "Tema do menu: <nome>" no rodapé do título, como o do tModLoader: um
//     toque passa para o próximo. O toque só chega ao Main.mouseLeft dentro
//     de uma região registrada no GUIInputRegionManager, como a dos botões do
//     jogo;
//   - DrawSunAndMoon: o sol e a lua do tema, só nos menus;
//   - a música (ModMusic) e o fundo (SurfaceBackgroundLoader) perguntam aqui.
// O tema escolhido e os já vistos ficam em BunnyLoader.menu.json, na pasta de
// saves do jogo. Sem esse arquivo (a primeira vez), abre no primeiro tema de
// mod; depois, um tema novo só aparece como "(1 novo)" no rodapé.
class MenuLoader {
    static List = [];               // os temas de mod, na ordem de registro
    static #vanilla = null;         // "Terraria": o título do jogo, sem mudança
    static #current = null;
    static #switchTo = null;
    static #ready = false;
    static #known = new Set();
    static #logo = null;            // os desenhos do logo do jogo neste quadro
    static #textures = new Map();   // menu -> { caminho: Asset }
    static #draw = null;

    static get CurrentMenu() { return MenuLoader.#current; }

    static Add(menu) {
        MenuLoader.List.push(menu);
        MenuLoader.#Install();
    }

    static FullName(menu) {
        if (menu === MenuLoader.#vanilla) return 'Terraria';
        const mod = menu.Mod;
        return (mod ? mod.id || mod.uuid : '?') + '/' + menu.constructor.name;
    }

    // O slot da música do tema (-1: a do jogo). A do jogo fica na geração de
    // mundo com semente especial e no menu 3000, como no tModLoader.
    static MenuMusic() {
        const menu = MenuLoader.#Active();
        if (!menu) return -1;

        const W = Terraria.WorldGen;
        if (W.drunkWorldGen || W.remixWorldGen || W.tenthAnniversaryWorldGen || Terraria.Main.menuMode === 3000) return -1;

        const music = Safe.Run(menu.constructor.name + '.Music', () => menu.Music);
        return Number.isInteger(music) ? music : -1;
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

    static #Name(menu) {
        const name = Safe.Run(menu.constructor.name + '.DisplayName', () => menu.DisplayName);
        if (name) return String(name);
        return menu.Mod ? menu.Mod.name : menu.constructor.name;
    }

    static #All() {
        return [MenuLoader.#vanilla, ...MenuLoader.List];
    }

    static #File() {
        return bl.path.join(Terraria.Main.SavePath, 'BunnyLoader.menu.json');
    }

    // O tema salvo; sem arquivo, o primeiro de mod disponível.
    static #Restore() {
        let saved = null;
        const text = Safe.Run('ModMenu: ler o tema salvo', () => bl.file.read(MenuLoader.#File()));
        if (text) {
            try {
                const data = JSON.parse(text) || {};
                saved = typeof data.LastSelectedModMenu === 'string' ? data.LastSelectedModMenu : null;
                if (Array.isArray(data.KnownMenuThemes)) for (const n of data.KnownMenuThemes) MenuLoader.#known.add(String(n));
            } catch (e) {
                bl.log('ModMenu: ' + MenuLoader.#File() + ' esta quebrado (' + e + '); fica o tema do jogo');
                saved = 'Terraria';
            }
        }

        const all = MenuLoader.#All();
        let pick = saved !== null ? all.find((m) => MenuLoader.FullName(m) === saved && MenuLoader.#Available(m)) : null;
        if (!pick && saved === null) pick = MenuLoader.List.find((m) => MenuLoader.#Available(m));
        MenuLoader.#switchTo = pick || MenuLoader.#vanilla;
        MenuLoader.#current = MenuLoader.#vanilla;
    }

    static #Save() {
        const data = { LastSelectedModMenu: MenuLoader.FullName(MenuLoader.#current), KnownMenuThemes: [...MenuLoader.#known] };
        Safe.Run('ModMenu: gravar o tema', () => bl.file.write(MenuLoader.#File(), JSON.stringify(data)));
    }

    // O próximo tema disponível (o do jogo conta), como o OffsetModMenu.
    static #Offset(offset) {
        const all = MenuLoader.#All();
        let i = all.indexOf(MenuLoader.#current);
        for (let n = 0; n < all.length; n++) {
            i = (i + offset + all.length) % all.length;
            if (MenuLoader.#Available(all[i])) {
                MenuLoader.#switchTo = all[i];
                return;
            }
        }
    }

    // Uma vez por quadro dos menus, antes do logo.
    static #Tick() {
        if (!MenuLoader.#current) MenuLoader.#Restore();

        const current = MenuLoader.#current;
        if (current !== MenuLoader.#vanilla && !MenuLoader.#switchTo && !MenuLoader.#Available(current)) {
            MenuLoader.#switchTo = MenuLoader.#vanilla;
        }

        const next = MenuLoader.#switchTo;
        MenuLoader.#switchTo = null;
        if (next && next !== current) {
            Safe.Run(current.constructor.name + '.OnDeselected', () => current.OnDeselected());
            MenuLoader.#current = next;
            Safe.Run(next.constructor.name + '.OnSelected', () => next.OnSelected());
            MenuLoader.#known.add(MenuLoader.FullName(next));
            MenuLoader.#Save();
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

    // "Tema do menu: <nome>" no rodapé do título.
    static #DrawSwitch() {
        const Main = Terraria.Main;
        if (Main.menuMode !== 0 || !MenuLoader.List.length) return;

        const pt = String(ModLocalization.ActiveCultureName || '').startsWith('pt');
        const fresh = MenuLoader.List.filter((m) => !MenuLoader.#known.has(MenuLoader.FullName(m)) && MenuLoader.#Available(m)).length;
        let text = (pt ? 'Tema do menu' : 'Menu Theme') + ': ' + MenuLoader.#Name(MenuLoader.#current);
        if (fresh) text += pt ? ' (' + fresh + (fresh > 1 ? ' novos)' : ' novo)') : ' (' + fresh + ' New)';

        const font = Terraria.GameContent.FontAssets.MouseText.Value;
        const size = font['Vector2 MeasureString(string text)'](text);
        const x = Math.floor(Main.screenWidth / 2 - size.X / 2);
        const y = Math.floor(Main.screenHeight - 2 - size.Y);
        const w = Math.ceil(size.X), h = Math.ceil(size.Y);

        const mx = Main.mouseX, my = Main.mouseY;
        const over = GUIInputRegionManager.Instance['bool RegisterInputRegion(Rectangle rect)'](Rectangle.new(x, y, w, h)) &&
            mx >= x && mx < x + w && my >= y && my < y + h;
        if (over && Main.mouseLeft && Main.mouseLeftRelease) {
            Main.mouseLeftRelease = false;
            SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
            MenuLoader.#Offset(1);
        }

        const color = over && Main.mouseLeft ? Main.OurFavoriteColor : Color.new(120, 120, 120, 76);
        Terraria.Utils['Vector2 DrawBorderString(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float anchorx, float anchory, int maxCharactersDisplayed)'](
            Main.spriteBatch, text, Vector2.new(x, y), color, 1, 0, 0, -1);
    }

    // O sol e a lua do tema no lugar dos do jogo, só durante o desenho.
    static #DrawSunAndMoon(original) {
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
            ModMusic.Install();

            const logoDraw = GUILogo['void Draw()'];
            logoDraw.hook((original, self) => {
                if (!MenuLoader.#ready) return original(self);

                Safe.Run('ModMenu (troca)', () => MenuLoader.#Tick());
                const menu = MenuLoader.#current;
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
                Safe.Run('ModMenu (rodapé)', () => MenuLoader.#DrawSwitch());
            });

            Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
                (original, sb, texture, position, source, color, rotation, origin, scale, effects, depth) => {
                    const list = MenuLoader.#logo;
                    if (!list) return original();
                    list.push({ texture, position, source, color, rotation, origin, scale, effects, depth });
                }, { whileIn: logoDraw });

            Terraria.Main['void DrawSunAndMoon(SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)'].hook(
                (original) => MenuLoader.#DrawSunAndMoon(original));
        });
    }
}
