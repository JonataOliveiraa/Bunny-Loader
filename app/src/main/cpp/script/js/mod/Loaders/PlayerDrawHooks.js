class PlayerDrawLayer {
    constructor(name, method) { this.Name = name; this.Method = method; this.IsHidden = false; }
    Hide() { this.IsHidden = true; }
    static BeforeParent(layer) { return { Before: layer }; }
    static AfterParent(layer) { return { After: layer }; }
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
    static #drawing = null;
    static #colors = ['colorHair', 'colorEyeWhites', 'colorEyes', 'colorHead', 'colorBodySkin', 'colorLegs', 'colorShirt',
        'colorUnderShirt', 'colorPants', 'colorShoes', 'colorArmorHead', 'colorArmorBody', 'colorArmorLegs', 'colorMount'];
    static #tints = { colorEyes: 'eyeColor', colorHead: 'skinColor', colorBodySkin: 'skinColor', colorLegs: 'skinColor',
        colorShirt: 'shirtColor', colorUnderShirt: 'underShirtColor', colorPants: 'pantsColor', colorShoes: 'shoeColor' };

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
        want(cls, ['ModifyScreenPosition'], 'player.ScreenPosition', () => {
            Terraria.Main['void DoDraw_UpdateCameraPosition()'].hook((original) => {
                original();
                const player = Terraria.Main.player[Terraria.Main.myPlayer];
                if (player && !Terraria.Main.gameMenu) PlayerLoader.Call(player, 'ModifyScreenPosition');
            });
        });
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
        const layers = Object.values(PlayerDrawLayers);
        gate.hook((original, info) => {
            const outer = PlayerDrawHooks.#drawing, hidden = [];
            for (const layer of layers) {
                if (layer.IsHidden) hidden.push(layer);
                layer.IsHidden = false;
            }
            const positions = PlayerLoader.Has('ModifyDrawLayerOrdering') ? new Map() : null;
            const scope = { info, address: bl.addressOf(info), segments: null, depth: 0, filtered: false };
            PlayerDrawHooks.#drawing = scope;
            try {
                if (positions) {
                    PlayerLoader.Call(info.drawPlayer, 'ModifyDrawLayerOrdering', positions);
                    if (positions.size) scope.segments = [];
                }
                PlayerLoader.Call(info.drawPlayer, 'HideDrawLayers', info);
                scope.filtered = !!scope.segments || layers.some(layer => layer.IsHidden);
                bl.hookFlags.set('player.DrawLayers', scope.filtered);
                original(info);
                if (scope.segments) PlayerDrawHooks.#Reorder(info, scope.segments, positions);
            } finally {
                PlayerDrawHooks.#drawing = outer;
                bl.hookFlags.set('player.DrawLayers', !!outer && outer.filtered);
                for (const layer of layers) layer.IsHidden = false;
                for (const layer of hidden) layer.IsHidden = true;
            }
        });
        for (const layer of layers) {
            Terraria.DataStructures.PlayerDrawLayers['void ' + layer.Method + '(PlayerDrawSet drawinfo)'].hook((original, info) => {
                const scope = PlayerDrawHooks.#drawing;
                if (!scope || scope.depth || scope.address !== bl.addressOf(info)) return original(info);
                const start = scope.segments ? info.DrawDataCacheCount : 0;
                scope.depth++;
                const hidden = layer.IsHidden || (layer === PlayerDrawLayers.FrontAccFront || layer === PlayerDrawLayers.FrontAccBack) && PlayerDrawLayers.FrontAcc.IsHidden;
                try { if (!hidden) original(info); }
                finally { scope.depth--; }
                if (scope.segments) scope.segments.push({ layer, start, end: info.DrawDataCacheCount });
            }, { whileIn: gate, flag: 'player.DrawLayers' });
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

    static #Reorder(info, segments, positions) {
        const edges = segments.map(() => new Set()), degrees = segments.map(() => 0);
        const named = (value) => typeof value === 'string' ? PlayerDrawLayers[value] : value;
        for (const [key, position] of positions) {
            const layer = named(key), before = named(position.Before), after = named(position.After);
            if (!layer || !before && !after) continue;
            segments.forEach((source, index) => {
                if (source.layer !== layer) return;
                segments.forEach((target, other) => {
                    const from = before && target.layer === before ? index : after && target.layer === after ? other : -1;
                    const to = from === index ? other : index;
                    if (from < 0 || from === to || edges[from].has(to)) return;
                    edges[from].add(to);
                    degrees[to]++;
                });
            });
        }
        const ordered = [], used = new Set();
        while (ordered.length < segments.length) {
            const index = degrees.findIndex((degree, i) => degree === 0 && !used.has(i));
            if (index < 0) { Safe.Once('player.DrawLayerCycle', 'ModifyDrawLayerOrdering: ordem ciclica ignorada'); return; }
            used.add(index);
            ordered.push(segments[index]);
            for (const next of edges[index]) degrees[next]--;
        }
        const source = info.DrawDataCache.cloneResized(info.DrawDataCache.length);
        let index = segments.length ? segments[0].start : 0;
        for (const segment of ordered) {
            for (let i = segment.start; i < segment.end; i++) info.DrawDataCache[index++] = source[i];
        }
    }
}
