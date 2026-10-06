const Main = Terraria.Main;
const totals = { visible: 0, hidden: 0, invisible: 0, reparent: 0, reordered: 0, cyclic: 0, late: 0, idle: 0, failed: 0 };
const colors = { before: [255, 23, 71], after: [41, 255, 83], child: [37, 89, 255], head: [251, 223, 41], late: [227, 41, 251] };
let frames = 0, completed = false, pending = null, staticCalls = 0, positionCalls = 0;
const phase = () => completed ? 'visible' : frames < 120 ? 'visible' : frames < 170 ? 'hidden' : frames < 220 ? 'invisible' :
    frames < 270 ? 'reparent' : frames < 320 ? 'reordered' : frames < 370 ? 'cyclic' : frames < 420 ? 'late' : 'idle';
function eligible(info) { return !Main.gameMenu && info.drawPlayer.whoAmI === Main.myPlayer && info.shadow === 0 && frames >= 60; }
function marker(info, name, offset) {
    const color = colors[name], data = Terraria.DataStructures.DrawData.new();
    data['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
        Terraria.GameContent.TextureAssets.MagicPixel.Value,
        Vector2.new(Main.screenWidth / 2 + 70 + offset, Main.screenHeight / 2 - 30), Rectangle.new(0, 0, 1, 1),
        Color.new(color[0], color[1], color[2], 255), 0, Vector2.new(0, 0), 16, 0, 0);
    data.shader = info.cWings;
    if (!ModPlayer.AddDrawData(info, data)) throw Error('cache recusou ' + name);
}
export class BeforeWings extends PlayerDrawLayer {
    SetStaticDefaults() { staticCalls++; }
    GetDefaultPosition() { positionCalls++; return PlayerDrawLayer.BeforeParent(PlayerDrawLayers.Wings); }
    GetDefaultVisibility(info) { return eligible(info) && phase() !== 'idle'; }
    Draw(info) { marker(info, 'before', 0); }
}
export class AfterWings extends PlayerDrawLayer {
    SetStaticDefaults() { staticCalls++; }
    GetDefaultPosition() { positionCalls++; return PlayerDrawLayer.AfterParent(PlayerDrawLayers.Wings); }
    GetDefaultVisibility(info) { return eligible(info) && !['invisible', 'idle'].includes(phase()); }
    Draw(info) { marker(info, 'after', 20); }
}
export class WingsChild extends PlayerDrawLayer {
    SetStaticDefaults() { staticCalls++; }
    GetDefaultPosition() { positionCalls++; return PlayerDrawLayer.AfterParent(AfterWings); }
    Draw(info) { marker(info, 'child', 40); }
}
export class AfterHead extends PlayerDrawLayer {
    SetStaticDefaults() { staticCalls++; }
    GetDefaultPosition() { positionCalls++; return PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head); }
    GetDefaultVisibility(info) { return eligible(info) && phase() !== 'idle'; }
    Draw(info) { marker(info, 'head', 60); }
}
class LateWings extends PlayerDrawLayer {
    SetStaticDefaults() { staticCalls++; }
    GetDefaultPosition() { positionCalls++; return PlayerDrawLayer.AfterParent(AfterWings); }
    GetDefaultVisibility(info) { return eligible(info) && phase() === 'late'; }
    Draw(info) { marker(info, 'late', 80); }
}
class DrawProbe extends ModPlayer {
    ModifyDrawInfo(player, info) { if (eligible(info)) pending = { address: bl.addressOf(info), phase: phase() }; }
    HideDrawLayers(player) { if (phase() === 'hidden' && player.whoAmI === Main.myPlayer) PlayerDrawLayers.Wings.Hide(); }
    ModifyDrawLayerOrdering(player, positions) {
        if (player.whoAmI !== Main.myPlayer) return;
        if (phase() === 'reparent') positions.set(AfterWings, PlayerDrawLayer.AfterParent(AfterHead));
        if (phase() === 'reordered') positions.set(PlayerDrawLayers.Wings, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head));
        if (phase() === 'cyclic') positions.set(AfterWings, PlayerDrawLayer.AfterParent(WingsChild));
    }
}
ModPlayer.register(DrawProbe);
const gate = Terraria.Graphics.Renderers.LegacyPlayerRenderer['void DrawPlayer_UseNormalLayers(PlayerDrawSet drawinfo)'];
gate.hook((original, info) => {
    if (eligible(info) && phase() === 'visible') {
        if (!bl.hookFlags.get('player.DrawLayer.Wings') || !bl.hookFlags.get('player.DrawLayer.Head') || bl.hookFlags.get('player.DrawLayer.Skin')) totals.failed++;
    }
    if (eligible(info) && phase() === 'idle' && bl.hookFlags.get('player.DrawLayers')) totals.failed++;
    original(info);
});
Terraria.DataStructures.PlayerDrawLayers['void DrawPlayer_RenderAllLayers(PlayerDrawSet drawinfo, Vector2[] positionalOffsets)'].hook((original, info, offsets) => {
    original(info, offsets);
    const probe = pending;
    if (!probe || probe.address !== bl.addressOf(info)) return;
    pending = null;
    if (completed) return;
    const found = {};
    for (let index = 0; index < info.DrawDataCacheCount; index++) {
        const data = info.DrawDataCache[index], color = data.color;
        if (data.scale.X !== 16) continue;
        for (const name of Object.keys(colors)) {
            const rgb = colors[name];
            if (color.R === rgb[0] && color.G === rgb[1] && color.B === rgb[2]) {
                if (found[name] !== undefined || data.shader !== info.cWings) totals.failed++;
                found[name] = index;
            }
        }
    }
    const order = Object.keys(found).sort((a, b) => found[a] - found[b]).join(',');
    const expected = { visible: 'before,after,child,head', hidden: 'head', invisible: 'before,head', reparent: 'before,head,after,child',
        reordered: 'head,before,after,child', cyclic: 'before,head', late: 'before,after,child,late,head', idle: '' };
    if (order === expected[probe.phase]) totals[probe.phase]++;
    else { totals.failed++; if (totals.failed < 5) bl.log('playerdrawlayers ORDEM ' + probe.phase + ': ' + order); }
});
Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (completed || Main.gameMenu || index !== Main.myPlayer) return;
    frames++;
    if (frames === 370) PlayerDrawLayer.register(LateWings);
    if (frames !== 470) return;
    completed = true;
    const passed = Object.keys(totals).filter(name => name !== 'failed').every(name => totals[name] > 0) && totals.failed === 0 && staticCalls === 5 && positionCalls === 9 &&
        !!ModContent.GetInstance(AfterWings) && !bl.hookFlags.get('player.DrawLayers');
    bl.log('playerdrawlayers FIM ' + JSON.stringify({ ...totals, staticCalls, positionCalls, passed }));
});
export default class PlayerDrawLayerTests extends Mod {}
