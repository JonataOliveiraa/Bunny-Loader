const Main = Terraria.Main, scale = 16;
const states = new Map();
let pending = null;

function state(player) {
    let result = states.get(player.whoAmI);
    if (!result || result.address !== bl.addressOf(player)) {
        result = { address: bl.addressOf(player), uncounted: 0, counted: 0, helper: 0, reordered: 0, lost: 0, wrong: 0 };
        states.set(player.whoAmI, result);
    }
    return result;
}

export function addMarker(player, info, elapsed) {
    if (Main.gameMenu || !(Main.netMode & 3) || info.shadow !== 0 || elapsed < 60) return;
    const mode = elapsed < 100 ? 'uncounted' : elapsed < 180 ? 'counted' : 'helper';
    const data = Terraria.DataStructures.DrawData.new();
    const color = player.whoAmI === 0 ? Color.new(255, 0, 255, 255) : Color.new(0, 256 - player.whoAmI, 255, 255);
    data['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
        Terraria.GameContent.TextureAssets.MagicPixel.Value,
        Vector2.new(info.Position.X - Main.screenPosition.X + (player.whoAmI === 0 ? -50 : 70), info.Position.Y - Main.screenPosition.Y - 30),
        Rectangle.new(0, 0, 1, 1), color, 0, Vector2.new(0, 0), scale, 0, 0);
    const count = info.DrawDataCacheCount;
    if (mode === 'helper') {
        if (!ModPlayer.AddDrawData(info, data)) throw Error('cache valido recusado no multiplayer');
    } else {
        info.DrawDataCache[count] = data;
        if (mode === 'counted') info.DrawDataCacheCount = count + 1;
    }
    pending = { address: bl.addressOf(info), index: player.whoAmI, mode, reordered: elapsed >= 180 && elapsed < 220 };
}

export function reorderMarker(positions, elapsed) {
    if (elapsed >= 180 && elapsed < 220) positions.set(PlayerDrawLayers.Skin, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head));
}

Terraria.DataStructures.PlayerDrawLayers['void DrawPlayer_RenderAllLayers(PlayerDrawSet drawinfo, Vector2[] positionalOffsets)'].hook((original, info, offsets) => {
    original(info, offsets);
    const probe = pending;
    if (!probe || probe.address !== bl.addressOf(info)) return;
    pending = null;
    const player = info.drawPlayer, result = state(player);
    let found = false, wrong = false;
    for (let i = 0; i < info.DrawDataCacheCount; i++) {
        const data = info.DrawDataCache[i], color = data.color;
        if (data.scale.X !== scale || color.B !== 255) continue;
        const index = color.R === 255 && color.G === 0 ? 0 : color.R === 0 && color.G > 0 ? 256 - color.G : -1;
        if (index < 0) continue;
        if (index === player.whoAmI) found = true;
        else wrong = true;
    }
    if (probe.index !== player.whoAmI || wrong) result.wrong++;
    if (probe.mode === 'uncounted') {
        if (found) result.lost++;
        else result.uncounted++;
    } else if (found) {
        result[probe.mode]++;
        if (probe.reordered) result.reordered++;
    } else result.lost++;
});

export function checkDrawing(player, remote, check) {
    for (const [name, target] of [['local', player], ['remoto', Main.player[remote]]]) {
        const result = state(target);
        check('DrawDataCache ' + name + ' sem contador reproduz sobrescrita', () => result.uncounted > 0);
        check('DrawDataCache ' + name + ' contador manual chega ao renderer', () => result.counted > 0);
        check('DrawDataCache ' + name + ' helper chega ao renderer', () => result.helper > 0);
        check('DrawDataCache ' + name + ' preserva prefixo na reordenacao', () => result.reordered > 0);
        check('DrawDataCache ' + name + ' sem perdas ou dados de outro jogador', () => result.lost === 0 && result.wrong === 0);
        bl.log('mpmodplayer DRAW jogador=' + target.whoAmI + ' local=' + Main.myPlayer + ' ' + JSON.stringify(result));
    }
}

export function checkRejoinedDrawing(player, remote, check) {
    for (const [name, target] of [['local', player], ['remoto', Main.player[remote]]]) {
        const result = state(target);
        check('reconexao DrawDataCache ' + name + ' chega ao renderer', () => result.helper > 0);
        check('reconexao DrawDataCache ' + name + ' sem perdas ou mistura', () => result.lost === 0 && result.wrong === 0);
        bl.log('mpmodplayer REJOIN_DRAW jogador=' + target.whoAmI + ' local=' + Main.myPlayer + ' ' + JSON.stringify(result));
    }
}
