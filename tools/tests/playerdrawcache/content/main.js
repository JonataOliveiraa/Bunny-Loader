const Main = Terraria.Main;
const markerScale = 16;
let frames = 0, completed = false, pending = null;
const observations = { uncounted: 0, counted: 0, helper: 0, lost: 0, failed: 0 };
function log(message) { bl.log('playerdrawcache ' + message); }
function marker() {
    const data = Terraria.DataStructures.DrawData.new();
    data['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
        Terraria.GameContent.TextureAssets.MagicPixel.Value, Vector2.new(Main.screenWidth / 2 + 70, Main.screenHeight / 2 - 30), Rectangle.new(0, 0, 1, 1), Color.new(255, 0, 255, 255),
        0, Vector2.new(0, 0), markerScale, 0, 0);
    return data;
}
export class DrawCacheProbe extends ModPlayer {
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
        pending = { address: bl.addressOf(info), mode };
    }
}
Terraria.DataStructures.PlayerDrawLayers['void DrawPlayer_RenderAllLayers(PlayerDrawSet drawinfo, Vector2[] positionalOffsets)'].hook((original, info, offsets) => {
    original(info, offsets);
    const probe = pending;
    if (!probe || probe.address !== bl.addressOf(info)) return;
    pending = null;
    if (completed) return;
    let found = false;
    for (let i = 0; i < info.DrawDataCacheCount; i++) {
        const data = info.DrawDataCache[i], color = data.color;
        if (color.R === 255 && color.G === 0 && color.B === 255 && data.scale.X === markerScale) { found = true; break; }
    }
    if (probe.mode === 'uncounted') {
        if (found) observations.failed++;
        else observations.uncounted++;
    } else if (found) observations[probe.mode]++;
    else observations.lost++;
    if ((observations[probe.mode] || 0) === 1) log(probe.mode + ' cache_ativo=' + found + ' count=' + info.DrawDataCacheCount);
});
Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (completed || Main.gameMenu || index !== Main.myPlayer) return;
    if (++frames !== 260) return;
    completed = true;
    const passed = observations.uncounted > 0 && observations.counted > 0 && observations.helper > 0 && observations.lost === 0 && observations.failed === 0;
    log('FIM ' + JSON.stringify(observations) + ' passou=' + passed);
});
export default class DrawCacheTests extends Mod {}
