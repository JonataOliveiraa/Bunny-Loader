// A cena de cada jogador, como no tModLoader: dos ModSceneEffect ativos (os
// ModBiome também), o de maior prioridade + peso dá cada canal: a música e os
// fundos de superfície e de subsolo, a água e o fundo do mapa.
class SceneEffectLoader {
    static List = [];
    static #keys = new Map();   // instância -> 'uuid/Classe' (desempate e log)

    // O Type de um ModSceneEffect é a posição aqui; o de um ModBiome, a
    // posição no BiomeLoader (como no tModLoader).
    static Add(inst) {
        if (!(inst instanceof ModBiome)) inst.Type = SceneEffectLoader.List.length;
        SceneEffectLoader.List.push(inst);
        SceneEffectLoader.#keys.set(inst, (bl.mod ? bl.mod.uuid : 'sem-mod') + '/' + inst.constructor.name);
        Hooks.Once('scene.fields', () => bl.defineField(Terraria.Player, 'CurrentSceneEffect'));
        BiomeLoader.Install();
        // A música da cena (a de ModBiome é 0, o silêncio, a não ser que o mod diga -1).
        ModMusic.Install();
        Hooks.Once('scene.map', () => Ready.Add(SceneEffectLoader.#InstallMap));
    }

    // O hook do fundo do mapa só entra se algum efeito tiver um.
    static #InstallMap() {
        for (const effect of SceneEffectLoader.List) {
            const path = Safe.Run(effect.constructor.name + '.MapBackground', () => effect.MapBackground);
            if (typeof path === 'string' && path !== '') return MapBackgroundLoader.Install();
        }
    }

    static KeyOf(inst) {
        return SceneEffectLoader.#keys.get(inst);
    }

    // Uma cena vazia: nenhum efeito, música -1 (a do jogo), fundos do jogo.
    static Empty() {
        const none = () => ({ value: -1, priority: SceneEffectPriority.None, from: null });
        return { anyActive: false, music: none(), surfaceBackground: none(), undergroundBackground: none(), waterStyle: none(),
                 mapBackground: { value: null, from: null }, active: [] };
    }

    // O SpecialVisuals roda para todos, ativo ou não (é onde se desliga um filtro).
    // Um efeito que lança fica inativo nesta avaliação.
    // Roda a cada quadro: os rótulos e o que a classe sobrescreve, uma vez só.
    static #plans = new Map();
    static #Plan(effect) {
        let plan = SceneEffectLoader.#plans.get(effect);
        if (!plan) {
            const cls = effect.constructor, name = cls.name;
            const own = (method) => (Hooks.Overrides(cls, ModSceneEffect, method) ? name + '.' + method : null);
            // O ModBiome sem IsSceneEffectActive próprio só lê a flag do IsBiomeActive.
            const biomeFlag = effect instanceof ModBiome && !Hooks.Overrides(cls, ModBiome, 'IsSceneEffectActive');
            plan = { active: name + '.IsSceneEffectActive', biomeFlag, visuals: own('SpecialVisuals'), weight: own('GetWeight') };
            SceneEffectLoader.#plans.set(effect, plan);
        }
        return plan;
    }

    // biomeFlags: as do UpdateBiomes deste quadro, se já calculadas.
    static UpdateSceneEffect(player, biomeFlags) {
        const result = SceneEffectLoader.Empty();
        const ranked = [];

        for (const effect of SceneEffectLoader.List) {
            const plan = SceneEffectLoader.#Plan(effect);
            const active = plan.biomeFlag && biomeFlags
                ? biomeFlags[effect.Type] === 1
                : Safe.Run(plan.active, () => effect.IsSceneEffectActive(player)) === true;
            if (plan.visuals) Safe.Run(plan.visuals, () => effect.SpecialVisuals(player, active));
            if (!active) continue;

            const weight = plan.weight ? Safe.Run(plan.weight, () => effect.GetWeight(player)) : effect.GetWeight(player);
            const clamped = Math.max(0, Math.min(1, Number(weight) || 0));
            ranked.push({ effect, rank: clamped + (effect.Priority | 0), key: SceneEffectLoader.KeyOf(effect) });
        }

        if (ranked.length) {
            // Maior primeiro; no empate, a chave, para a escolha não depender da ordem de carga.
            ranked.sort((a, b) => b.rank - a.rank || (a.key < b.key ? -1 : a.key > b.key ? 1 : 0));
            result.anyActive = true;
            result.active = ranked.map((r) => r.effect);

            // Cada canal vem do primeiro que o preenche (a música de um, o fundo de outro).
            for (const { effect } of ranked) {
                const name = effect.constructor.name;
                const priority = effect.Priority | 0;
                if (result.music.from === null) {
                    const music = effect.Music | 0;
                    if (music !== -1) result.music = { value: music, priority, from: effect };
                }
                if (result.surfaceBackground.from === null) {
                    const style = Safe.Run(name + '.SurfaceBackgroundStyle', () => effect.SurfaceBackgroundStyle);
                    if (style instanceof ModSurfaceBackgroundStyle) result.surfaceBackground = { value: style.Slot, priority, from: effect };
                }
                if (result.undergroundBackground.from === null) {
                    const style = Safe.Run(name + '.UndergroundBackgroundStyle', () => effect.UndergroundBackgroundStyle);
                    if (style instanceof ModUndergroundBackgroundStyle) result.undergroundBackground = { value: style.Slot, priority, from: effect };
                }
                if (result.waterStyle.from === null) {
                    const style = Safe.Run(name + '.WaterStyle', () => effect.WaterStyle);
                    if (style instanceof ModWaterStyle) result.waterStyle = { value: style.Slot, priority, from: effect };
                }
                if (result.mapBackground.from === null) {
                    const path = Safe.Run(name + '.MapBackground', () => effect.MapBackground);
                    if (typeof path === 'string' && path !== '') result.mapBackground = { value: path, from: effect };
                }
            }
        }

        player.CurrentSceneEffect = result;
        return result;
    }

    // A cena do jogador, ou uma vazia antes da primeira avaliação. Sem efeito
    // registrado o campo nem existe no Player (os fundos e a água perguntam
    // mesmo assim, se outro mod os instalou).
    static Of(player) {
        if (!SceneEffectLoader.List.length) return SceneEffectLoader.Empty();
        return (player && player.CurrentSceneEffect) || SceneEffectLoader.Empty();
    }
}
