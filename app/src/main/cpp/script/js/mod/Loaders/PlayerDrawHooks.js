class PlayerDrawLayer {
    constructor(name, method = null) { this.Name = name || this.constructor.name; this.Method = method; this.IsHidden = false; }
    get Visible() { return !this.IsHidden; }
    get FullName() { return this.Mod ? this.Mod.uuid + '/' + this.Name : this.Name; }
    SetStaticDefaults() {}
    GetDefaultPosition() { throw new TypeError(this.Name + '.GetDefaultPosition precisa retornar BeforeParent ou AfterParent'); }
    GetDefaultVisibility(drawInfo) { return true; }
    Draw(drawInfo) {}
    Hide() { this.IsHidden = true; }
    static BeforeParent(layer) { return { Before: layer }; }
    static AfterParent(layer) { return { After: layer }; }
    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof PlayerDrawLayer)) {
            throw new TypeError('PlayerDrawLayer.register(Classe): passe uma classe que estende PlayerDrawLayer');
        }
        if (!Hooks.Overrides(cls, PlayerDrawLayer, 'Draw') || !Hooks.Overrides(cls, PlayerDrawLayer, 'GetDefaultPosition')) {
            throw new TypeError(cls.name + ': implemente Draw e GetDefaultPosition');
        }
        const layer = new cls();
        PlayerDrawHooks.CheckLayer(layer, cls);
        Templates.Adopt(cls, layer);
        PlayerDrawHooks.AddLayer(layer, cls);
        return layer;
    }
}

const PlayerDrawLayers = Object.fromEntries([
    ['JimsCloak', '01_2_JimsCloak'], ['MountBehindPlayer', '02_MountBehindPlayer'], ['Carpet', '03_Carpet'],
    ['PortableStool', '03_PortableStool'], ['ElectrifiedDebuffBack', '04_ElectrifiedDebuffBack'],
    ['ForbiddenSetRing', '05_ForbiddenSetRing'], ['SafemanSun', '05_2_SafemanSun'], ['WebbedDebuffBack', '06_WebbedDebuffBack'],
    ['LeinforsHairShampoo', '07_LeinforsHairShampoo'], ['Backpacks', '08_Backpacks'], ['Tails', '08_1_Tails'],
    ['Wings', '09_Wings'], ['BackHair', '01_BackHair'], ['BackAcc', '10_BackAcc'], ['BackHead', '01_3_BackHead'],
    ['Balloons', '11_Balloons'], ['Skin', '12_Skin'], ['ArmorBackCoat', '13_ArmorBackCoat'], ['Leggings', '13_Leggings'],
    ['Shoes', '14_Shoes'], ['SkinLongCoat', '15_SkinLongCoat'], ['ArmorLongCoat', '16_ArmorLongCoat'],
    ['Torso', '17_Torso'], ['OffhandAcc', '18_OffhandAcc'], ['WaistAcc', '19_WaistAcc'], ['NeckAcc', '20_NeckAcc'],
    ['Head', '21_Head'], ['Magiluminescence', '21_1_Magiluminescence'], ['FaceAcc', '22_FaceAcc'],
    ['MountFront', '23_MountFront'], ['Pulley', '24_Pulley'], ['Shield', '25_Shield'], ['SolarShield', '26_SolarShield'],
    ['HeldItem', '27_HeldItem'], ['ArmOverItem', '28_ArmOverItem'], ['OnhandAcc', '29_OnhandAcc'],
    ['BladedGlove', '30_BladedGlove'], ['ProjectileOverArm', '31_ProjectileOverArm'], ['FrontAcc', '32_FrontAcc'],
    ['FrontAccFront', '32_FrontAcc_FrontPart'], ['FrontAccBack', '32_FrontAcc_BackPart'],
    ['JimsDroneRadio', 'JimsDroneRadio'], ['FrozenOrWebbedDebuff', '33_FrozenOrWebbedDebuff'], ['ElectrifiedDebuffFront', '34_ElectrifiedDebuffFront'],
    ['IceBarrier', '35_IceBarrier'], ['CTG', '36_CTG'], ['BeetleBuff', '37_BeetleBuff'], ['EyebrellaCloud', '38_EyebrellaCloud'],
].map(([name, suffix]) => [name, new PlayerDrawLayer(name, 'DrawPlayer_' + suffix)]));

class PlayerDrawHooks {
    static #cameraPlayer = false;
    static #cameraSystem = false;
    static #drawing = null;
    static #nativeLayers = Object.values(PlayerDrawLayers);
    static #native = new Set(PlayerDrawHooks.#nativeLayers);
    static #enabled = new Set();
    static #custom = [];
    static #byClass = new Map();
    static #byName = new Map();
    static #revision = 0;
    static #defaults = null;
    static #colors = ['colorHair', 'colorEyeWhites', 'colorEyes', 'colorHead', 'colorBodySkin', 'colorLegs', 'colorShirt',
        'colorUnderShirt', 'colorPants', 'colorShoes', 'colorArmorHead', 'colorArmorBody', 'colorArmorLegs', 'colorMount'];
    static #tints = { colorEyes: 'eyeColor', colorHead: 'skinColor', colorBodySkin: 'skinColor', colorLegs: 'skinColor',
        colorShirt: 'shirtColor', colorUnderShirt: 'underShirtColor', colorPants: 'pantsColor', colorShoes: 'shoeColor' };

    static CheckLayer(layer, cls) {
        if (typeof layer.Name !== 'string' || !layer.Name) throw new TypeError(cls.name + ': nome de camada invalido');
        const key = (bl.mod ? bl.mod.uuid + '/' : '') + layer.Name;
        if (PlayerDrawHooks.#byClass.has(cls) || PlayerDrawHooks.#byName.has(key)) throw new TypeError(key + ' ja foi registrado');
    }

    static AddLayer(layer, cls) {
        const entry = { layer, initialized: false, label: layer.FullName };
        PlayerDrawHooks.#custom.push(entry);
        PlayerDrawHooks.#byClass.set(cls, layer);
        PlayerDrawHooks.#byName.set(layer.FullName, layer);
        PlayerDrawHooks.#revision++;
        Ready.Add(() => PlayerDrawHooks.#Initialize(entry));
        Hooks.Once('player.DrawLayers', PlayerDrawHooks.#Layers);
    }

    static #Initialize(entry) {
        if (entry.initialized) return;
        entry.initialized = true;
        try { entry.layer.SetStaticDefaults(); }
        catch (error) { Safe.Report(entry.label + '.SetStaticDefaults', error); }
        PlayerDrawHooks.#revision++;
    }

    static InstallCamera(forPlayer) {
        if (forPlayer) PlayerDrawHooks.#cameraPlayer = true;
        else PlayerDrawHooks.#cameraSystem = true;
        Hooks.Once('camera.ScreenPosition', () => {
            const Main = Terraria.Main;
            Main['void DoDraw_UpdateCameraPosition()'].hook(original => {
                original();
                if (Main.gameMenu) return;
                if (PlayerDrawHooks.#cameraPlayer) {
                    const player = Main.player[Main.myPlayer];
                    if (player) PlayerLoader.Call(player, 'ModifyScreenPosition');
                }
                if (PlayerDrawHooks.#cameraSystem) SystemLoader.Call('ModifyScreenPosition');
            });
        });
    }

    static #Resolve(value) {
        if (typeof value === 'function') return PlayerDrawHooks.#byClass.get(value);
        if (value instanceof PlayerDrawLayer) return PlayerDrawHooks.#native.has(value) || PlayerDrawHooks.#byClass.get(value.constructor) === value ? value : null;
        if (typeof value !== 'string') return null;
        const named = PlayerDrawLayers[value] || PlayerDrawHooks.#byName.get(value);
        if (named) return named;
        let found = null;
        for (const entry of PlayerDrawHooks.#custom) {
            if (entry.layer.Name !== value) continue;
            if (found) return null;
            found = entry.layer;
        }
        return found;
    }

    static #Position(layer, value) {
        if (!value || typeof value !== 'object' || !!value.Before === !!value.After) {
            Safe.Once('player.DrawPosition:' + layer.FullName, layer.FullName + ': posicao de camada invalida');
            return null;
        }
        const before = !!value.Before, parent = PlayerDrawHooks.#Resolve(before ? value.Before : value.After);
        if (!parent) {
            Safe.Once('player.DrawParent:' + layer.FullName, layer.FullName + ': camada pai nao registrada');
            return null;
        }
        return { parent, before };
    }

    static #Plan(entries, positions) {
        const relations = new Map(), children = new Map(), roots = new Map(), normalized = new Map();
        const registered = new Set(entries.map(entry => entry.layer));
        for (const [key, value] of positions) {
            const layer = PlayerDrawHooks.#Resolve(key);
            if (!registered.has(layer)) continue;
            try {
                const position = PlayerDrawHooks.#Position(layer, value);
                if (position && (PlayerDrawHooks.#native.has(position.parent) || registered.has(position.parent))) {
                    relations.set(layer, position);
                    normalized.set(layer, position.before ? { Before: position.parent } : { After: position.parent });
                }
            } catch (error) { Safe.Report(layer.FullName + '.Position', error); }
        }
        const visiting = new Set();
        const rootOf = layer => {
            if (roots.has(layer)) return roots.get(layer);
            if (visiting.has(layer)) {
                Safe.Once('player.DrawCustomCycle', 'PlayerDrawLayer: ordem ciclica ignorada');
                return null;
            }
            const position = relations.get(layer);
            if (!position) return null;
            visiting.add(layer);
            const root = PlayerDrawHooks.#byClass.has(position.parent.constructor) ? rootOf(position.parent) : position.parent;
            visiting.delete(layer);
            roots.set(layer, root);
            return root;
        };
        for (const entry of entries) {
            const layer = entry.layer;
            if (!rootOf(layer)) continue;
            const position = relations.get(layer);
            let group = children.get(position.parent);
            if (!group) { group = { before: [], after: [] }; children.set(position.parent, group); }
            (position.before ? group.before : group.after).push(layer);
        }
        return { entries, layers: PlayerDrawHooks.#nativeLayers.concat(entries.map(entry => entry.layer)), positions: normalized, relations, children, roots };
    }

    static #DefaultPlan() {
        if (PlayerDrawHooks.#defaults && PlayerDrawHooks.#defaults.revision === PlayerDrawHooks.#revision) return PlayerDrawHooks.#defaults;
        const entries = PlayerDrawHooks.#custom.slice(), positions = new Map();
        for (const entry of entries) PlayerDrawHooks.#Initialize(entry);
        const revision = PlayerDrawHooks.#revision;
        for (const entry of entries) {
            try { positions.set(entry.layer, entry.layer.GetDefaultPosition()); }
            catch (error) { Safe.Report(entry.label + '.GetDefaultPosition', error); }
        }
        const plan = PlayerDrawHooks.#Plan(entries, positions);
        plan.revision = entries.length === PlayerDrawHooks.#custom.length ? revision : -1;
        PlayerDrawHooks.#defaults = plan;
        return plan;
    }

    static #DrawChildren(info, plan, parent, side) {
        const group = plan && plan.children.get(parent);
        if (!group) return;
        for (const layer of group[side]) {
            if (layer.IsHidden) continue;
            PlayerDrawHooks.#DrawChildren(info, plan, layer, 'before');
            try { layer.Draw(info); }
            catch (error) { Safe.Report(layer.FullName + '.Draw', error); }
            PlayerDrawHooks.#DrawChildren(info, plan, layer, 'after');
        }
    }

    static #Ordering(defaults, positions) {
        if (!positions.size) return { plan: defaults.positions.size ? PlayerDrawHooks.#Plan(defaults.entries, positions) : defaults, positions };
        const custom = new Map(), native = new Map();
        for (const [key, value] of positions) {
            const layer = PlayerDrawHooks.#Resolve(key);
            if (PlayerDrawHooks.#native.has(layer)) native.set(layer, value);
            else if (layer) custom.set(layer, value);
        }
        let same = custom.size === defaults.positions.size;
        if (same) for (const [layer, value] of custom) {
            const previous = defaults.positions.get(layer);
            try {
                if (!previous || !value || value.Before !== previous.Before || value.After !== previous.After) { same = false; break; }
            } catch { same = false; break; }
        }
        const plan = same ? defaults : PlayerDrawHooks.#Plan(defaults.entries, custom);
        const ordering = new Map();
        for (const [layer, value] of native) {
            try {
                const position = PlayerDrawHooks.#Position(layer, value);
                if (!position) continue;
                const parent = PlayerDrawHooks.#native.has(position.parent) ? position.parent : plan.roots.get(position.parent);
                if (parent && parent !== layer) ordering.set(layer, position.before ? { Before: parent } : { After: parent });
            } catch (error) { Safe.Report(layer.FullName + '.Position', error); }
        }
        return { plan, positions: ordering };
    }

    static #VisibleChildren(plan, parent) {
        const group = plan.children.get(parent);
        return !!group && (group.before.some(layer => !layer.IsHidden) || group.after.some(layer => !layer.IsHidden));
    }

    static #FilterLayers(enabled) {
        for (const layer of PlayerDrawHooks.#enabled) if (!enabled.has(layer)) bl.hookFlags.set('player.DrawLayer.' + layer.Name, false);
        for (const layer of enabled) if (!PlayerDrawHooks.#enabled.has(layer)) bl.hookFlags.set('player.DrawLayer.' + layer.Name, true);
        PlayerDrawHooks.#enabled = enabled;
        bl.hookFlags.set('player.DrawLayers', enabled.size > 0);
    }

    static Install(cls) {
        const want = PlayerLoader.Wants;
        want(cls, ['DrawEffects', 'ModifyDrawInfo'], 'player.DrawInfo', () => {
            Terraria.DataStructures.PlayerDrawSet['void BoringSetup(Player player, ref Vector2 drawPosition, float shadowOpacity, float rotation, ref Vector2 rotationOrigin, Projectile overrideHeldProjectile)'].hook(
                (original, info, player, position, shadow, rotation, origin, projectile) => {
                    original(info, player, position, shadow, rotation, origin, projectile);
                    if (PlayerLoader.Has('DrawEffects')) PlayerDrawHooks.#Effects(player, info);
                    PlayerLoader.Call(player, 'ModifyDrawInfo', info);
                });
        });
        want(cls, ['DrawPlayer'], 'player.DrawPlayer', () => {
            Terraria.Graphics.Renderers.LegacyPlayerRenderer['void DrawPlayer(Camera camera, Player drawPlayer, Vector2 position, float rotation, Vector2 rotationOrigin, float shadow, float scale, Vector2[] positionalOffsets)'].hook(
                (original, renderer, camera, player, position, rotation, origin, shadow, scale, offsets) => {
                    original(renderer, camera, player, position, rotation, origin, shadow, scale, offsets);
                    PlayerLoader.Call(player, 'DrawPlayer', camera);
                });
        });
        want(cls, ['TransformDrawData'], 'player.TransformDrawData', () => {
            Terraria.DataStructures.PlayerDrawLayers['void DrawPlayer_TransformDrawData(PlayerDrawSet drawinfo, Vector2[] positionalOffsets)'].hook((original, info, offsets) => {
                original(info, offsets);
                PlayerLoader.Call(info.drawPlayer, 'TransformDrawData', info);
            });
        });
        want(cls, ['HideDrawLayers', 'ModifyDrawLayerOrdering'], 'player.DrawLayers', PlayerDrawHooks.#Layers);
        want(cls, ['ModifyScreenPosition'], 'player.ScreenPosition', () => PlayerDrawHooks.InstallCamera(true));
        want(cls, ['ModifyZoom'], 'player.Zoom', () => {
            Terraria.Graphics.SpriteViewMatrix['void set_Zoom(Vector2 value)'].hook((original, matrix, value) => {
                const Main = Terraria.Main;
                if (Main.gameMenu || bl.addressOf(matrix) !== bl.addressOf(Main.GameViewMatrix)) return original(matrix, value);
                const zoom = new Ref(value.X);
                PlayerLoader.Call(Main.player[Main.myPlayer], 'ModifyZoom', zoom);
                const factor = Number.isFinite(zoom.value) && zoom.value > 0 && value.X > 0 ? zoom.value / value.X : 1;
                if (factor === 1) return original(matrix, value);
                return original(matrix, Vector2.new(value.X * factor, value.Y * factor));
            });
        });
    }

    static #Layers() {
        const renderer = Terraria.Graphics.Renderers.LegacyPlayerRenderer;
        const gate = renderer['void DrawPlayer_UseNormalLayers(PlayerDrawSet drawinfo)'];
        const layers = PlayerDrawHooks.#nativeLayers;
        const copy = bl.classOf('System', 'Array')['void Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)'];
        gate.hook((original, info) => {
            const defaults = PlayerDrawHooks.#DefaultPlan();
            const outer = PlayerDrawHooks.#drawing, hidden = [], active = defaults.layers;
            for (const layer of active) {
                if (layer.IsHidden) hidden.push(layer);
                layer.IsHidden = false;
            }
            const scope = { address: bl.addressOf(info), segments: null, depth: 0, plan: defaults, enabled: new Set() };
            PlayerDrawHooks.#drawing = scope;
            try {
                for (const entry of defaults.entries) {
                    try { entry.layer.IsHidden = !entry.layer.GetDefaultVisibility(info); }
                    catch (error) { entry.layer.IsHidden = true; Safe.Report(entry.label + '.GetDefaultVisibility', error); }
                }
                let positions = null;
                if (PlayerLoader.Has('ModifyDrawLayerOrdering')) {
                    const requested = new Map();
                    for (const [layer, value] of defaults.positions) requested.set(layer, { ...value });
                    PlayerLoader.Call(info.drawPlayer, 'ModifyDrawLayerOrdering', requested);
                    const ordering = PlayerDrawHooks.#Ordering(defaults, requested);
                    scope.plan = ordering.plan;
                    positions = ordering.positions;
                    if (positions.size) scope.segments = [];
                }
                PlayerLoader.Call(info.drawPlayer, 'HideDrawLayers', info);
                if (scope.segments) scope.enabled = PlayerDrawHooks.#native;
                else for (const layer of layers) if (layer.IsHidden || scope.plan.children.size && PlayerDrawHooks.#VisibleChildren(scope.plan, layer)) scope.enabled.add(layer);
                PlayerDrawHooks.#FilterLayers(scope.enabled);
                original(info);
                if (scope.segments) PlayerDrawHooks.#Reorder(info, scope.segments, positions, copy);
            } finally {
                PlayerDrawHooks.#drawing = outer;
                if (outer) PlayerDrawHooks.#FilterLayers(outer.enabled);
                else bl.hookFlags.set('player.DrawLayers', false);
                for (const layer of active) layer.IsHidden = false;
                for (const layer of hidden) layer.IsHidden = true;
            }
        });
        for (const layer of layers) {
            Terraria.DataStructures.PlayerDrawLayers['void ' + layer.Method + '(PlayerDrawSet drawinfo)'].hook((original, info) => {
                const scope = PlayerDrawHooks.#drawing;
                if (!scope || scope.address !== bl.addressOf(info)) return original(info);
                const record = !!scope.segments && scope.depth === 0;
                const start = record ? info.DrawDataCacheCount : 0;
                scope.depth++;
                const hidden = layer.IsHidden || (layer === PlayerDrawLayers.FrontAccFront || layer === PlayerDrawLayers.FrontAccBack) && PlayerDrawLayers.FrontAcc.IsHidden;
                try {
                    if (!hidden) {
                        PlayerDrawHooks.#DrawChildren(info, scope.plan, layer, 'before');
                        original(info);
                        PlayerDrawHooks.#DrawChildren(info, scope.plan, layer, 'after');
                    }
                }
                finally { scope.depth--; }
                if (record) scope.segments.push({ layer, start, end: info.DrawDataCacheCount });
            }, { whileIn: gate, flag: 'player.DrawLayer.' + layer.Name });
        }
    }

    static #Channel(value) { return Math.max(0, Math.min(255, Math.trunc(Number.isFinite(value) ? value : 0))); }

    static #Effects(player, info) {
        const r = new Ref(1), g = new Ref(1), b = new Ref(1), a = new Ref(1), bright = new Ref(false);
        PlayerLoader.Call(player, 'DrawEffects', info, r, g, b, a, bright);
        if (r.value === 1 && g.value === 1 && b.value === 1 && a.value === 1 && !bright.value) return;
        for (const field of PlayerDrawHooks.#colors) {
            let color = info[field];
            if (bright.value) {
                const tint = field === 'colorHair' ? player['Color GetHairColor(bool useLighting)'](false)
                    : PlayerDrawHooks.#tints[field] ? player[PlayerDrawHooks.#tints[field]] : Color.new(255, 255, 255, 255);
                color = player['Color GetImmuneAlpha(Color newColor, float alphaReduction)'](tint, info.shadow);
            }
            info[field] = Color.new(PlayerDrawHooks.#Channel(color.R * r.value), PlayerDrawHooks.#Channel(color.G * g.value),
                PlayerDrawHooks.#Channel(color.B * b.value), PlayerDrawHooks.#Channel(color.A * a.value));
        }
    }

    static #Reorder(info, segments, positions, copy) {
        const edges = new Array(segments.length), degrees = new Array(segments.length).fill(0);
        const named = (value) => typeof value === 'string' ? PlayerDrawLayers[value] : value;
        for (const [key, position] of positions) {
            const layer = named(key), before = named(position.Before), after = named(position.After);
            if (!layer || !before && !after) continue;
            for (let index = 0; index < segments.length; index++) {
                if (segments[index].layer !== layer) continue;
                for (let other = 0; other < segments.length; other++) {
                    const from = before && segments[other].layer === before ? index : after && segments[other].layer === after ? other : -1;
                    const to = from === index ? other : index;
                    if (from < 0 || from === to) continue;
                    const outgoing = edges[from] || (edges[from] = new Set());
                    if (outgoing.has(to)) continue;
                    outgoing.add(to);
                    degrees[to]++;
                }
            }
        }
        const ordered = [];
        while (ordered.length < segments.length) {
            let index = 0;
            while (index < degrees.length && degrees[index] !== 0) index++;
            if (index === degrees.length) { Safe.Once('player.DrawLayerCycle', 'ModifyDrawLayerOrdering: ordem ciclica ignorada'); return; }
            degrees[index] = -1;
            ordered.push(segments[index]);
            if (edges[index]) for (const next of edges[index]) degrees[next]--;
        }
        let index = segments.length ? segments[0].start : 0;
        let changed = false;
        for (const segment of ordered) {
            if (segment.end > segment.start && segment.start !== index) changed = true;
            index += segment.end - segment.start;
        }
        if (!changed) return;
        const cache = info.DrawDataCache, source = cache.cloneResized(index);
        index = segments[0].start;
        for (let i = 0; i < ordered.length; i++) {
            const start = ordered[i].start;
            let end = ordered[i].end;
            while (i + 1 < ordered.length && ordered[i + 1].start === end) end = ordered[++i].end;
            const length = end - start;
            if (length && start !== index) copy(source, start, cache, index, length);
            index += length;
        }
    }
}
