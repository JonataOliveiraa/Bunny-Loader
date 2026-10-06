const cfg = JSON.parse(bl.file.read('expectations.json'));
const TextureAssets = Terraria.GameContent.TextureAssets;
const Asset = ReLogic.Content.Asset.makeGeneric(Microsoft.Xna.Framework.Graphics.Texture2D);
let frames = 0, done = false, captured;

Terraria.Main['void DoUpdate(GameTime gameTime)'].hook((original, self, time) => {
    original(self, time);
    if (done || ++frames < 120) return;
    done = true;
    let failures = 0, checks = 0;
    const check = (name, callback) => {
        checks++;
        try { callback(); bl.log('namedtexture '+cfg.name+' '+name+': ok'); }
        catch(e) { failures++; bl.log('namedtexture '+cfg.name+' '+name+': FAILED '+e); }
    };
    const verify = (asset, row) => {
        const value = asset.Value;
        if (!value) throw new Error('Value is null');
        if (cfg.vanilla) {
            if (value._unityTexture && !value.PackedEntry && !value._sourceLoadAsset) throw new Error('custom texture remains');
        } else {
            if (value.Width !== row.width || value.Height !== row.height) throw new Error('size '+value.Width+'x'+value.Height+' expected '+row.width+'x'+row.height);
            if (!value._unityTexture) throw new Error('Unity texture is null');
            if (value._sourceLoadAsset || value.PackedEntry) throw new Error('vanilla source/atlas remains');
        }
    };
    for (const row of cfg.rows) {
        check(row.path, () => {
            const asset = row.field ? (row.index === undefined ? TextureAssets[row.field] : TextureAssets[row.field][row.index]) : Asset.new();
            if (!row.field) asset['void .ctor(string name)'](row.path);
            if (!row.field || cfg.vanilla) asset['void ActionUnityLoad()']();
            verify(asset, row);
            if (!row.field) { asset['void ActionUnityLoad()'](); verify(asset, row); }
        });
    }
    if (captured) check('InventoryBack reference preserved', () => {
        if (bl.addressOf(TextureAssets.InventoryBack) !== bl.addressOf(captured)) throw new Error('Asset object was replaced');
    });
    bl.log('namedtexture FIM '+cfg.name+': '+failures+' failures, '+checks+' checks');
    if (cfg.world) Terraria.Main.playerInventory = true;
});

export default class NamedTextureDeviceTest extends Mod {
    Load() { captured = TextureAssets.InventoryBack; }
}
