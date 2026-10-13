// O gerenciador dos efeitos de tela: cria, acha, mata, e a cada quadro
// (depois do Main.DoDraw, com a câmera já no lugar) roda Update/Draw de cada
// efeito vivo e manda a lista ao núcleo. Sem efeito vivo, o quadro sai sem
// nada (e sem custo no desenho).
class ScreenEffects {
    static Layer = Object.freeze({ World: 0, Screen: 1 });
    // Desligado (a linha "Efeitos de tela" das Configurações), os efeitos
    // continuam vivendo e contando o tempo, só não desenham.
    static Enabled = true;
    static Frame = 0;

    static #active = [];
    static #shaders = new Map();   // classe -> ScreenShader
    static #submitted = false;
    static #lastTime = 0;

    // Na carga do mod (ContentAutoload) ou no primeiro Spawn. O shader vem,
    // nesta ordem: `static Source` da própria classe, o `Shader` que o
    // SetDefaults deu (ScreenShader ou caminho), o .frag ao lado do .js e,
    // por último, o `static Source` herdado (quem estende um pronto).
    static Register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModScreenEffect)) {
            throw new TypeError('ScreenEffects.Register(Class): pass the class, which extends ModScreenEffect');
        }
        if (ScreenEffects.#shaders.has(cls)) return ScreenEffects.#shaders.get(cls);

        const sample = new cls();
        sample.SetDefaults();
        const shader = ScreenEffects.#ResolveShader(cls, sample.Shader);
        ScreenEffects.#shaders.set(cls, shader);
        return shader;
    }

    static #ResolveShader(cls, declared) {
        if (Object.prototype.hasOwnProperty.call(cls, 'Source') && typeof cls.Source === 'string') {
            return ScreenShader.FromSource(cls.Source, cls.name);
        }
        if (declared instanceof ScreenShader) return declared;
        if (typeof declared === 'string') return ScreenShader.FromFile(declared);

        const file = ContentAutoload.FileOf(cls);
        const frag = file ? file.replace(/\.js$/i, '.frag') : null;
        if (frag && bl.file.exists(frag)) return ScreenShader.FromFile(frag);

        for (let owner = Object.getPrototypeOf(cls); owner && owner !== ModScreenEffect; owner = Object.getPrototypeOf(owner)) {
            if (Object.prototype.hasOwnProperty.call(owner, 'Source')) return ScreenEffects.Register(owner);
        }

        throw new Error(cls.name + ' has no shader: put ' + cls.name + '.frag next to its .js, set `static Source` (GLSL) or Shader in SetDefaults');
    }

    // center: posição no mundo (copiada), uma entidade (o efeito a segue e
    // morre com ela) ou null (tela inteira). options: as propriedades do
    // efeito em camelCase ({ radius, intensity, duration, key, ... }).
    static Spawn(cls, center = null, options = {}) {
        const shader = ScreenEffects.Register(cls);

        if (options.key !== undefined && options.key !== null) {
            const existing = ScreenEffects.Find(options.key);
            if (existing && existing.constructor === cls) {
                ScreenEffects.#Place(existing, center);
                ScreenEffects.#Apply(existing, options);
                existing.Age = 0;
                return existing;
            }
            if (existing) existing.Kill();
        }

        const fx = new cls();
        fx.SetDefaults();
        fx.Shader = shader;
        ScreenEffects.#Place(fx, center);
        ScreenEffects.#Apply(fx, options);
        fx.OnSpawn();

        ScreenEffects.#active.push(fx);
        ScreenEffects.#active.sort((a, b) => a.Order - b.Order);
        ScreenEffects.#Install();
        return fx;
    }

    static Find(key) {
        return ScreenEffects.#active.find((fx) => fx.Key === key && fx.Active) ?? null;
    }

    static Kill(key) {
        const fx = ScreenEffects.Find(key);
        if (fx) fx.Kill();
        return fx !== null;
    }

    static KillAll() {
        for (const fx of ScreenEffects.#active) fx.Kill();
    }

    static get Active() {
        return ScreenEffects.#active.filter((fx) => fx.Active);
    }

    static #Place(fx, center) {
        if (center && typeof center === 'object' && 'active' in center && center.Center) {
            fx.Follow = center;
            fx.FollowType = 'type' in center ? center.type : undefined;
            center = center.Center;
        }
        fx.Center = center ? { X: center.X, Y: center.Y } : null;
    }

    static #Apply(fx, options) {
        for (const key of Object.keys(options)) {
            const name = key.charAt(0).toUpperCase() + key.slice(1);
            if (!(name in fx)) throw new TypeError(fx.constructor.name + ': unknown option \'' + key + '\'');

            fx[name] = name === 'Uniforms' ? Object.assign({}, fx.Uniforms, options[key]) : options[key];
        }
    }

    static #Install() {
        Hooks.Once('screen.effects', () => {
            Terraria.Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
                original(self, gameTime);
                ScreenEffects.#Tick();
            });
        });
    }

    // Ticks de jogo (1/60 s) desde o quadro anterior: o tempo dos efeitos
    // anda igual a 45, 60 ou 90 fps, e para com o jogo pausado.
    static #Delta() {
        const now = Date.now();
        const elapsed = ScreenEffects.#lastTime ? (now - ScreenEffects.#lastTime) / (1000 / 60) : 1;
        ScreenEffects.#lastTime = now;
        if (Terraria.Main.gamePaused) return 0;
        return Math.min(Math.max(elapsed, 0), 3);
    }

    static #Tick() {
        const active = ScreenEffects.#active;
        if (active.length === 0) {
            if (ScreenEffects.#submitted) bl.screen.submit([]);
            ScreenEffects.#submitted = false;
            ScreenEffects.#lastTime = 0;
            return;
        }

        ScreenEffects.Frame++;
        ScreenSpace.BeginFrame(ScreenEffects.Frame);
        const delta = ScreenEffects.#Delta();
        const draws = [];
        for (const fx of active) ScreenEffects.#Step(fx, delta, draws);

        ScreenEffects.#active = active.filter((fx) => fx.Active);
        bl.screen.submit(draws);
        ScreenEffects.#submitted = true;
    }

    static #Step(fx, delta, draws) {
        const name = fx.constructor.name;
        try {
            if (fx.Active && fx.Follow) {
                // O jogo reusa o índice de um projétil/NPC morto: outro tipo no
                // mesmo lugar não é mais a entidade seguida.
                const gone = !fx.Follow.active || (fx.FollowType !== undefined && fx.Follow.type !== fx.FollowType);
                if (gone) fx.Kill();
                else fx.Center = { X: fx.Follow.Center.X, Y: fx.Follow.Center.Y };
            }
            if (fx.Active) {
                fx.Age += delta;
                fx.Update();
                if (fx.Duration > 0 && fx.Age >= fx.Duration) fx.Kill();
            }
            if (fx.Active && ScreenEffects.Enabled && fx.ShouldDraw()) fx.Draw(new ScreenCommands(fx, draws));
        } catch (e) {
            Safe.Report('screen effect ' + name, e);
            fx.Kill();
        }
        if (!fx.Active && !fx.Ended) {
            fx.Ended = true;
            Safe.Run('screen effect ' + name + '.OnKill', () => fx.OnKill());
        }
    }
}
