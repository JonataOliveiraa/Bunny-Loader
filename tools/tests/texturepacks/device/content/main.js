const cfg = JSON.parse(bl.file.read('expectations.json'));
let frames = 0, done = false;
Terraria.Main['void DoUpdate(GameTime gameTime)'].hook((original, self, time) => {
    original(self, time);
    if (done || ++frames < 120) return;
    done = true;
    let failed = 0;
    for (const row of cfg.rows) {
        try {
            const asset = Terraria.GameContent.TextureAssets[row.table][row.index];
            if (cfg.vanilla) self['void LoadItem(int i)'](row.index);
            const value = asset.Value;
            if (!value) throw new Error('Value is null');
            const source = value._sourceLoadAsset;
            if (cfg.vanilla) {
                if (value.Width !== 30 || value.Height !== 20) throw new Error('vanilla size '+value.Width+'x'+value.Height);
                if (!value.PackedEntry) throw new Error('vanilla atlas entry is missing');
            } else {
                if (value.Width !== row.width || value.Height !== row.height) throw new Error('size '+value.Width+'x'+value.Height+' expected '+row.width+'x'+row.height);
                if (source) throw new Error('asset still uses vanilla source: '+source);
                const unity = value._unityTexture;
                if (!unity) throw new Error('no Unity texture');
            }
            bl.log('texturedevice '+cfg.name+' '+row.table+'['+row.index+']: ok ('+value.Width+'x'+value.Height+')');
        } catch(e) { failed++; bl.log('texturedevice '+cfg.name+' '+row.table+'['+row.index+']: FAILED '+e); }
    }
    bl.log('texturedevice FIM '+cfg.name+': '+failed+' failure(s), '+cfg.rows.length+' checks');
});
export default class TextureDeviceTest extends Mod {}
