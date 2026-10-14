// Onde ficam os botões de mod de cada layout de controles. O arquivo do jogo
// nunca os leva (ao salvar, viram espaços vazios e voltam logo depois): eles
// vão para BunnyLoader.touchbuttons.json, pelo nome ('ExampleMod/RandomBuff')
// e pelo espaço. Um botão de um mod que saiu fica guardado no espaço, que o
// jogo mostra vazio, e volta quando o mod volta.
class TouchButtonLayouts {
    static #saved;

    static Save(styles, run) {
        if (!TouchButtonLoader.Count) return run();
        Safe.Run('TouchButtons: guardar o layout', () => TouchButtonLayouts.#Capture(styles));
        return TouchButtonLayouts.WithoutMods(TouchButtonLayouts.#Profiles(styles), run);
    }

    // Roda `run` com as ações de mod tiradas dos perfis, e as devolve.
    static WithoutMods(profiles, run) {
        if (!TouchButtonLoader.Count) return run();
        const restore = TouchButtonLayouts.#Strip(profiles);
        try {
            return run();
        } finally {
            restore();
        }
    }

    static Apply(styles) {
        const T = TouchButtonLoader;
        if (!styles || !T.Count) return;

        const data = TouchButtonLayouts.#Saved().profiles;
        const unbound = T.Ids.unbound;
        for (const profile of TouchButtonLayouts.#Profiles(styles)) {
            const rows = data[String(profile.Id || '')];
            const controls = rows && rows.length ? profile.Controls : null;
            if (!controls) continue;

            const bySlot = new Map();
            TouchButtonLayouts.#Each(controls, (b, i) => { if (b) bySlot.set(TouchButtonLayouts.#SlotKey(b, i), b); });
            for (const row of rows) {
                const binding = bySlot.get(row.slot) || (row.index < controls.length ? controls[row.index] : null);
                if (!binding) continue;
                const main = row.action && T.ByKey(row.action);
                if (main && binding.Action === unbound) binding.Action = T.ActionOf(main);
                const groups = binding.GroupActions;
                if (!groups || !row.group) continue;
                row.group.forEach((key, j) => {
                    const entry = key && T.ByKey(key);
                    if (entry && j < groups.length && groups[j] === unbound) groups[j] = T.ActionOf(entry);
                });
            }
        }
    }

    static #File() {
        return bl.path.join(Terraria.Main.SavePath, 'BunnyLoader.touchbuttons.json');
    }

    static #Saved() {
        if (TouchButtonLayouts.#saved === undefined) {
            const file = TouchButtonLayouts.#File();
            let data = null;
            try {
                if (bl.file.exists(file)) data = JSON.parse(bl.file.read(file));
            } catch (e) {
                bl.error('TouchButtons: ' + file + ' is broken (' + e + '); starting with no mod buttons in the layouts');
            }
            TouchButtonLayouts.#saved = data && data.profiles && typeof data.profiles === 'object' ? data : { version: 1, profiles: {} };
        }
        return TouchButtonLayouts.#saved;
    }

    static #Capture(styles) {
        const T = TouchButtonLoader;
        const unbound = T.Ids.unbound;
        const old = TouchButtonLayouts.#Saved().profiles;
        const keyOf = (action) => { const e = T.ByAction(action); return e ? e.key : null; };
        // Vazio no jogo, mas com o botão de um mod ausente guardado: fica.
        const kept = (action, previous) => (action === unbound && previous && !T.ByKey(previous) ? previous : null);

        const profiles = {};
        const live = new Set();
        for (const profile of TouchButtonLayouts.#Profiles(styles)) {
            const id = String(profile.Id || '');
            if (!id) continue;
            live.add(id);
            const before = new Map((old[id] || []).map((r) => [r.slot, r]));
            const rows = [];
            TouchButtonLayouts.#Each(profile.Controls, (binding, i) => {
                if (!binding) return;
                const slot = TouchButtonLayouts.#SlotKey(binding, i);
                const prev = before.get(slot);
                const action = binding.Action;
                const main = T.IsModded(action) ? keyOf(action) : kept(action, prev && prev.action);
                const group = [];
                let any = !!main;
                TouchButtonLayouts.#Each(binding.GroupActions, (g, j) => {
                    const key = T.IsModded(g) ? keyOf(g) : kept(g, prev && prev.group && prev.group[j]);
                    group.push(key);
                    if (key) any = true;
                });
                if (any) rows.push({ slot, index: i, action: main, group });
            });
            if (rows.length) profiles[id] = rows;
        }
        for (const id of Object.keys(old)) if (!(id in profiles) && !live.has(id)) profiles[id] = old[id];

        TouchButtonLayouts.#saved = { version: 1, profiles };
        bl.file.write(TouchButtonLayouts.#File(), JSON.stringify(TouchButtonLayouts.#saved));
    }

    static #Strip(profiles) {
        const unbound = TouchButtonLoader.Ids.unbound;
        const undo = [];
        for (const profile of profiles) {
            TouchButtonLayouts.#Each(profile.Controls, (binding) => {
                if (!binding) return;
                const action = binding.Action;
                if (TouchButtonLoader.IsModded(action)) {
                    undo.push(() => { binding.Action = action; });
                    binding.Action = unbound;
                }
                const groups = binding.GroupActions;
                TouchButtonLayouts.#Each(groups, (g, j) => {
                    if (!TouchButtonLoader.IsModded(g)) return;
                    undo.push(() => { groups[j] = g; });
                    groups[j] = unbound;
                });
            });
        }
        return () => {
            for (let i = undo.length - 1; i >= 0; i--) Safe.Run('TouchButtons: devolver o botão', undo[i]);
        };
    }

    // Os perfis do jogo, mais o ativo (que pode não estar na lista).
    static #Profiles(styles) {
        const out = [];
        TouchButtonLayouts.#Each(styles && styles.Profiles, (p) => { if (p) out.push(p); });
        const active = styles && styles._activeProfile;
        if (active && !out.some((p) => bl.addressOf(p) === bl.addressOf(active))) out.push(active);
        return out;
    }

    static #SlotKey(binding, i) {
        return String(binding.SlotId || '#' + i);
    }

    static #Each(array, fn) {
        const n = array ? array.length : 0;
        for (let i = 0; i < n; i++) fn(array[i], i);
    }
}
