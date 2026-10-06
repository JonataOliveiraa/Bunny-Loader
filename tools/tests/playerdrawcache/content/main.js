const Main = Terraria.Main;
const markerScale = 16;
let frames = 0, completed = false, pending = null;
const observations = { uncounted: 0, counted: 0, helper: 0, reordered: 0, unchanged: 0, cyclic: 0, lost: 0, failed: 0 };
function log(message) { bl.log('playerdrawcache ' + message); }
function marker(color = Color.new(255, 0, 255, 255), offset = 0) {
    const data = Terraria.DataStructures.DrawData.new();
    data['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
        Terraria.GameContent.TextureAssets.MagicPixel.Value, Vector2.new(Main.screenWidth / 2 + 70 + offset, Main.screenHeight / 2 - 30), Rectangle.new(0, 0, 1, 1), color,
        0, Vector2.new(0, 0), markerScale, 0, 0);
    return data;
}
class DrawCacheProbe extends ModPlayer {
    ModifyDrawInfo(player, info) {
        if (Main.gameMenu || player.whoAmI !== Main.myPlayer || info.shadow !== 0 || frames < 60) return;
        const mode = completed ? 'helper' : frames < 100 ? 'uncounted' : frames < 180 ? 'counted' : 'helper';
        if (mode === 'helper' && typeof ModPlayer.AddDrawData !== 'function') return;
        const data = marker(), start = info.DrawDataCacheCount;
        if (mode === 'helper') {
            if (!ModPlayer.AddDrawData(info, data)) throw Error('helper recusou cache valido');
        } else {
            info.DrawDataCache[start] = data;
            if (mode === 'counted') info.DrawDataCacheCount = start + 1;
        }
        pending = { address: bl.addressOf(info), mode, reordered: frames >= 180 && frames < 220,
            unchanged: frames >= 220 && frames < 240, cyclic: frames >= 240 && frames < 250 };
    }
    ModifyDrawLayerOrdering(player, positions) {
        if (frames >= 180 && frames < 220) positions.set(PlayerDrawLayers.Skin, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head));
        else if (frames >= 220 && frames < 240) positions.set(PlayerDrawLayers.Skin, PlayerDrawLayer.BeforeParent(PlayerDrawLayers.Head));
        else if (frames >= 240 && frames < 250) {
            positions.set(PlayerDrawLayers.Skin, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head));
            positions.set(PlayerDrawLayers.Head, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Skin));
        }
    }
}
ModPlayer.register(DrawCacheProbe);
const gate = Terraria.Graphics.Renderers.LegacyPlayerRenderer['void DrawPlayer_UseNormalLayers(PlayerDrawSet drawinfo)'];
for (const [layer, color, offset] of [[PlayerDrawLayers.Skin, Color.new(255, 63, 31, 255), 20], [PlayerDrawLayers.Head, Color.new(31, 63, 255, 255), 40]]) {
    Terraria.DataStructures.PlayerDrawLayers['void ' + layer.Method + '(PlayerDrawSet drawinfo)'].hook((original, info) => {
        original(info);
        if (Main.gameMenu || info.drawPlayer.whoAmI !== Main.myPlayer || info.shadow !== 0 || frames < 60) return;
        if (!ModPlayer.AddDrawData(info, marker(color, offset))) throw Error('marcador de camada recusado');
    }, { whileIn: gate });
}
Terraria.DataStructures.PlayerDrawLayers['void DrawPlayer_RenderAllLayers(PlayerDrawSet drawinfo, Vector2[] positionalOffsets)'].hook((original, info, offsets) => {
    original(info, offsets);
    const probe = pending;
    if (!probe || probe.address !== bl.addressOf(info)) return;
    pending = null;
    if (completed) return;
    let found = false, skinIndex = -1, headIndex = -1;
    for (let i = 0; i < info.DrawDataCacheCount; i++) {
        const data = info.DrawDataCache[i], color = data.color;
        if (data.scale.X !== markerScale) continue;
        if (color.R === 255 && color.G === 0 && color.B === 255) found = true;
        if (color.R === 255 && color.G === 63 && color.B === 31) skinIndex = i;
        if (color.R === 31 && color.G === 63 && color.B === 255) headIndex = i;
    }
    if (probe.mode === 'uncounted') {
        if (found) observations.failed++;
        else observations.uncounted++;
    } else if (found) {
        observations[probe.mode]++;
        const validLayers = skinIndex >= 0 && headIndex >= 0;
        if (probe.reordered) { if (validLayers && headIndex < skinIndex) observations.reordered++; else observations.failed++; }
        if (probe.unchanged) { if (validLayers && skinIndex < headIndex) observations.unchanged++; else observations.failed++; }
        if (probe.cyclic) { if (validLayers && skinIndex < headIndex) observations.cyclic++; else observations.failed++; }
    }
    else observations.lost++;
    if ((observations[probe.mode] || 0) === 1) log(probe.mode + ' cache_ativo=' + found + ' count=' + info.DrawDataCacheCount);
});
Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (completed || Main.gameMenu || index !== Main.myPlayer) return;
    if (++frames !== 260) return;
    completed = true;
    const passed = observations.uncounted > 0 && observations.counted > 0 && observations.helper > 0 && observations.reordered > 0 &&
        observations.unchanged > 0 && observations.cyclic > 0 && observations.lost === 0 && observations.failed === 0;
    log('FIM ' + JSON.stringify(observations) + ' passou=' + passed);
});
export default class DrawCacheTests extends Mod {}
