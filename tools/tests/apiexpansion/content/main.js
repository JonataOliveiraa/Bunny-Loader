const { Main, Item, NPC, WorldItem } = Terraria;
const B = Terraria.GameContent.BannerSystem;
const ItemSlot = Terraria.UI.ItemSlot;
let ticks = 0, done = false, drawing = false, veto = false, nested = false;
let checks = 0, failures = 0, trace = [], draws = [], worldArgs, inventoryArgs;
let reloaded = false, saveComplete = false;
let pendingClaim = null;
let windTest = null, inWind = false;
function check(name, fn) {
    checks++;
    try { if (!fn()) throw Error('resultado falso'); bl.log('apiexpansion PASS ' + name); }
    catch (error) { failures++; bl.log('apiexpansion FAIL ' + name + ': ' + error + ' ' + error.stack); }
}
const near = (a, b) => Math.abs(a - b) < 0.001;
const same = (a, b) => bl.addressOf(a) === bl.addressOf(b);
export class DrawProbe extends ModItem {
    Texture = 'Probe'; HideFromModMenu = true;
    SetDefaults(item) { item.width = item.height = 16; item.scale = 1; item.maxStack = 1; }
    PreDrawInWorld(...args) {
        if (!drawing) return true;
        trace.push('own-pre'); worldArgs = args;
        args[4].value = 0.375; args[5].value = 0.625;
        return !veto;
    }
    PostDrawInWorld(...args) { if (drawing) { trace.push('own-post'); worldArgs = args; } }
    PreDrawInInventory(...args) {
        if (drawing) { trace.push('own-pre'); inventoryArgs = args; }
        return !drawing || !veto;
    }
    PostDrawInInventory(...args) { if (drawing) trace.push('own-post'); }
}
export class DrawGlobal extends GlobalItem {
    AppliesToEntity(item) { return item.type === ModContent.ItemType(DrawProbe); }
    PreDrawInWorld(...args) { if (drawing) { trace.push('global-pre'); args[4].value += 0.25; } return true; }
    PostDrawInWorld(...args) { if (drawing) trace.push('global-post'); }
    PreDrawInInventory(...args) {
        if (drawing) {
            trace.push('global-pre');
            if (nested) {
                nested = false;
                const old = drawing; drawing = false;
                try { icon(args[0]); } finally { drawing = old; }
            }
        }
        return true;
    }
    PostDrawInInventory(...args) { if (drawing) trace.push('global-post'); }
}
export class BannerProbe extends ModItem {
    Texture = 'Banner'; HideFromModMenu = true;
    SetDefaults(item) { this.DefaultToPlaceableTile(ModContent.TileType(BannerTile), 0); }
}
export class BannerTile extends ModBannerTile { Texture = 'BannerTile'; }
export class CloneProbe extends ModNPC {
    Texture = 'Slime'; HideFromModMenu = true; HideFromBestiary = true;
    SetDefaults(npc) {
        this.CloneDefaults(Terraria.ID.NPCID.BlueSlime);
        this.AIType = Terraria.ID.NPCID.BlueSlime;
        this.Banner = this.Type; this.BannerItem = ModContent.ItemType(BannerProbe);
    }
}
export class CloneRelative extends ModNPC {
    Texture = 'Slime'; HideFromModMenu = true; HideFromBestiary = true;
    SetDefaults(npc) { this.CloneDefaults(ModContent.NPCType(CloneProbe)); this.Banner = ModContent.NPCType(CloneProbe); }
}
export class CloneGlobal extends GlobalNPC {
    InstancePerEntity = true;
    SetDefaults(npc) { this.owner = npc.type; }
}
export class PersistenceProbe extends ModSystem {
    SaveWorldData(tag) { tag.saved = saveComplete; }
    LoadWorldData(tag) { reloaded = tag.GetBool('saved'); }
}
function make(type) {
    const item = Item.new(); item['void .ctor()']();
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null); return item;
}
function npc(type) {
    const n = NPC.new(); n['void .ctor()']();
    n['void SetDefaults(int Type, NPCSpawnParams spawnparams)'](type, null); return n;
}
function icon(item) {
    return ItemSlot['float DrawItemIcon(Item item, int context, SpriteBatch spriteBatch, Vector2 screenPositionForItemCenter, float scale, float sizeLimit, Color environmentColor, float itemFade, bool flip)'](
        item, 0, Main.spriteBatch, Vector2.new(320, 200), 0.75, 32, Color.new(70, 90, 110, 255), 1, false);
}
Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable<Rectangle> sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
    (original, self, texture, position, frame, color, rotation, origin, scale, effects, depth) => {
        if (drawing) draws.push({ position: [position.X, position.Y], frame: [frame?.Width, frame?.Height], rotation, scale });
        if (inWind && windTest) windTest.draws.push({ rotation, width: frame?.Width, height: frame?.Height });
        return original(self, texture, position, frame, color, rotation, origin, scale, effects, depth);
    });
Terraria.GameContent.Drawing.TileDrawing['void DrawMultiTileVinesInWind(Vector2 screenPosition, Vector2 offSet, int topLeftX, int topLeftY, int sizeX, int sizeY)'].hook(
    (original, self, screen, offset, x, y, width, height) => {
        const interested = windTest && x === windTest.x && y === windTest.y;
        if (interested) { windTest.calls++; windTest.size = [width, height]; inWind = true; }
        try { return original(self, screen, offset, x, y, width, height); }
        finally { if (interested) inWind = false; }
    });
function startWindTest() {
    const x = Math.floor(Main.LocalPlayer.Center.X / 16) + 4, y = Math.floor(Main.LocalPlayer.Center.Y / 16) - 5;
    const saved = [];
    for (let row = -1; row < 3; row++) {
        const tile = Main.tile['Tile get_Item(int x, int y)'](x, y + row);
        saved.push({ tile, type: tile.type, frameX: tile.frameX, frameY: tile.frameY, active: tile['bool active()']() });
        tile.type = row < 0 ? Terraria.ID.TileID.Dirt : ModContent.TileType(BannerTile);
        tile.frameX = 0; tile.frameY = row < 0 ? 0 : row * 18;
        tile['void active(bool active)'](true);
    }
    windTest = { x, y, saved, frames: 0, calls: 0, draws: [], size: [], wind: Main.windSpeedCurrent,
        target: Main.windSpeedTarget, enabled: Main.SettingsEnabled_TilesSwayInWind };
    Main.SettingsEnabled_TilesSwayInWind = true;
    Main.windSpeedCurrent = Main.windSpeedTarget = 0.8;
}
function run() {
    const type = ModContent.ItemType(DrawProbe), item = make(type);
    check('default banner strength and threshold', () => Terraria.ID.ItemID.Sets.BannerStrength[type].Enabled && Terraria.ID.ItemID.Sets.KillsToBanner[type] === 50);
    Main.spriteBatch['void Begin(SpriteSortMode sortMode, bool defferedBatch)'](0, true);
    try {
        drawing = true; trace = []; draws = []; veto = false;
        const scale = icon(item);
        check('inventory native stage order', () => trace.join(',') === 'global-pre,own-pre,own-post,global-post');
        check('inventory register and stack arguments', () => {
            const a = inventoryArgs;
            bl.log('apiexpansion TRACE inventory ' + JSON.stringify({ p: [a[2].X, a[2].Y], f: [a[3].Width, a[3].Height], o: [a[6].X, a[6].Y], scale: a[7], color: [a[4].R, a[4].G, a[4].B] }));
            return same(a[0], item) && same(a[1], Main.spriteBatch) && a[3].Width === 32 && a[3].Height === 32 && near(a[7], scale) && near(a[6].X, 16) && near(a[6].Y, 16);
        });
        check('inventory native sprite draws', () => draws.length === 1 && near(draws[0].scale, scale));
        veto = true; trace = []; draws = [];
        const canceledScale = icon(item);
        check('inventory veto skips native sprite and retains post and return', () => draws.length === 0 && near(scale, canceledScale) && trace.join(',') === 'global-pre,own-pre,own-post,global-post');
        veto = false; nested = true; trace = []; draws = [];
        icon(item);
        check('recursive draw in callback does not recursively dispatch', () => trace.join(',') === 'global-pre,own-pre,own-post,global-post');

        const world = WorldItem.new(); world['void .ctor(Item item)'](item);
        world.position = Vector2.new(Main.screenPosition.X + 320, Main.screenPosition.Y + 200);
        trace = []; draws = [];
        Main['void DrawItem(WorldItem item, int whoami)'](Main.instance, world, 137);
        check('world native stage order and modified refs', () => trace.join(',') === 'own-pre,global-pre,own-post,global-post' && near(worldArgs[4], 0.625) && near(worldArgs[5], 0.625));
        check('world register and stack arguments reach native draw', () => same(worldArgs[0], world) && worldArgs[6] === 137 && draws.some(d => near(d.rotation, 0.625) && near(d.scale, 0.625)));
        veto = true; trace = []; draws = [];
        Main['void DrawItem(WorldItem item, int whoami)'](Main.instance, world, 137);
        check('world veto skips native sprite and retains post', () => draws.length === 0 && trace.join(',') === 'own-pre,global-pre,own-post,global-post');
    } finally { drawing = false; Main.spriteBatch['void End()'](); }

    const base = npc(Terraria.ID.NPCID.BlueSlime), clone = npc(ModContent.NPCType(CloneProbe));
    bl.log('apiexpansion TRACE stats ' + JSON.stringify({ base: [base.type, base.lifeMax, base.damage, base.defense, base.aiStyle], clone: [clone.type, clone.lifeMax, clone.damage, clone.defense, clone.aiStyle] }));
    check('CloneDefaults native stats without double difficulty scaling', () => clone.lifeMax === base.lifeMax && clone.damage === base.damage && clone.defense === base.defense && clone.aiStyle === base.aiStyle);
    check('CloneDefaults retains mod identity', () => clone.type === ModContent.NPCType(CloneProbe) && clone.netID === clone.type && same(clone.ModNPC.NPC, clone));
    const relative = npc(ModContent.NPCType(CloneRelative));
    check('CloneDefaults of another mod NPC', () => relative.lifeMax === base.lifeMax && relative.damage === base.damage && relative.type === ModContent.NPCType(CloneRelative) && same(relative.ModNPC.NPC, relative));
    const originalMode = Main.GameMode;
    try {
        for (const mode of [0, 1, 2]) {
            Main.GameMode = mode;
            check('CloneDefaults difficulty mode ' + mode, () => {
                const source = npc(Terraria.ID.NPCID.BlueSlime), target = npc(ModContent.NPCType(CloneProbe));
                return source.lifeMax === target.lifeMax && source.damage === target.damage && source.defense === target.defense;
            });
        }
    } finally { Main.GameMode = originalMode; }
    check('CloneDefaults native participation and instance preservation', () => {
        const m = clone.ModNPC, global = clone.GetGlobalNPC(CloneGlobal);
        clone.playerInteraction[Main.myPlayer] = true;
        m.CloneDefaults(Terraria.ID.NPCID.BlueSlime);
        return clone.playerInteraction[Main.myPlayer] && clone.ModNPC === m && same(m.NPC, clone) && clone.type === ModContent.NPCType(CloneProbe)
            && clone.GetGlobalNPC(CloneGlobal) === global && global.owner === clone.type;
    });
    const banner = clone.type, bannerItem = ModContent.ItemType(BannerProbe);
    const example = new Ref();
    if (ModContent.TryFind(ModNPC, 'examplemod/ExampleSlimeNPC', example)) check('ExampleMod slime has its registered banner item and tile', () => {
        const enemy = example.value;
        const itemType = ModContent.ItemType('examplemod/ExampleSlimeBanner');
        const sample = make(itemType);
        return enemy.Banner === enemy.Type && B.NPCtoBanner(enemy.Type) === enemy.Type && B.BannerToItem(enemy.Type) === itemType
            && sample.createTile === ModContent.TileType('examplemod/ExampleEnemyBannerTile') && sample.placeStyle === 1;
    });
    if (reloaded) check('native world reload restores banner sidecar count, claim and notification', () => B.killCount[banner] === 50 && B.claimableBanners[banner] === 1 && B.newBanner[banner]);
    bl.log('apiexpansion TRACE banners ' + JSON.stringify({ banner, item: bannerItem, fromNPC: B.NPCtoBanner(clone.type), fromRelative: B.NPCtoBanner(relative.type), toItem: B.BannerToItem(banner), toNPC: B.BannerToNPC(banner), template: [ModContent.GetInstance(CloneProbe).Banner, ModContent.GetInstance(CloneProbe).BannerItem] }));
    check('native banner mapping and shared counter identity', () => B.NPCtoBanner(clone.type) === banner && B.NPCtoBanner(relative.type) === banner && B.BannerToItem(banner) === bannerItem && B.BannerToNPC(banner) === clone.type);
    B.killCount[banner] = 49; B.claimableBanners[banner] = 0; B.newBanner[banner] = false;
    check('50th kill uses native claimable banner flow', () => {
        B.AddNPCKillBy(relative.type, Main.myPlayer);
        bl.log('apiexpansion TRACE kill ' + JSON.stringify({ count: B.killCount[banner], claim: B.claimableBanners[banner], fresh: B.newBanner[banner] }));
        return B.killCount[banner] === 50 && B.claimableBanners[banner] === 1 && B.newBanner[banner];
    });
    check('mobile banner crafting lists the mod banner', () => {
        const gui = bl.classOf('', 'GUIInstance').Active.GUIBannerCrafting;
        gui['void UpdateFilter(bool force)'](true);
        return gui.FilteredItems.Contains(banner);
    });
    const stream = System.IO.MemoryStream.new(); stream['void .ctor()']();
    const writer = System.IO.BinaryWriter.new(); writer['void .ctor(Stream output)'](stream);
    check('native world banner serialization keeps vanilla size and live arrays', () => {
        B.Save(writer); const length = stream.Length; stream.Position = 0;
        const reader = System.IO.BinaryReader.new(); reader['void .ctor(Stream input)'](stream);
        const count = reader.ReadInt32();
        bl.log('apiexpansion TRACE save length=' + length + ' count=' + count);
        return count === B.MaxBannerTypes && B.killCount[banner] === 50;
    });
    check('native network banner packet includes extended state', () => {
        const packet = B.NetBannersModule.WriteFullState();
        const reader = packet.Reader;
        reader.BaseStream.Position = 6;
        const count = reader.ReadInt32();
        bl.log('apiexpansion TRACE packet capacity=' + packet.Length + ' count=' + count);
        const valid = count === B.killCount.length;
        packet.Recycle(); return valid;
    });
    writer.Close(); stream.Close();
    check('placed banner native damage effect', () => {
        Main.SceneMetrics.NPCBannerBuff[banner] = true;
        const effect = new Ref();
        const found = Main.LocalPlayer['bool GetBannerBuffEffect(NPC npc, out ItemID.BannerEffect effect)'](clone, effect);
        Main.SceneMetrics.NPCBannerBuff[banner] = false;
        return found && effect.value.Enabled;
    });
    check('banner tile resolves style and activates native effect', () => {
        const x = Math.floor(Main.LocalPlayer.Center.X / 16), y = Math.floor(Main.LocalPlayer.Center.Y / 16) - 8;
        const tile = Main.tile['Tile get_Item(int x, int y)'](x, y);
        const saved = { type: tile.type, frameX: tile.frameX, frameY: tile.frameY, active: tile['bool active()']() };
        try {
            tile.type = ModContent.TileType(BannerTile); tile.frameX = 0; tile.frameY = 0; tile['void active(bool active)'](true);
            Main.SceneMetrics.NPCBannerBuff[banner] = false;
            ModContent.GetInstance(BannerTile).NearbyEffects(x, y, false);
            const effect = new Ref();
            return Main.SceneMetrics.NPCBannerBuff[banner] && Main.LocalPlayer['bool GetBannerBuffEffect(NPC npc, out ItemID.BannerEffect effect)'](clone, effect) && effect.value.Enabled;
        } finally {
            tile.type = saved.type; tile.frameX = saved.frameX; tile.frameY = saved.frameY; tile['void active(bool active)'](saved.active);
            Main.SceneMetrics.NPCBannerBuff[banner] = false;
        }
    });
    if (!reloaded && !failures) {
        saveComplete = true;
        Terraria.IO.WorldFile['void InternalSaveWorld(bool useCloudSaving, bool resetTime)'](false, false);
        bl.log('apiexpansion TRACE saved isolated world for reload check');
    }
    pendingClaim = { banner, item: bannerItem, frames: 0 };
    B.RequestBannerClaim(banner, 1);
    startWindTest();
}
Main['void DrawInterface(GameTime gameTime)'].hook((original, self, time) => {
    original(self, time);
    if (windTest && ++windTest.frames >= 120) {
        check('native MultiTileVine renderer draws a complete mod banner', () => windTest.calls > 0 && windTest.size.join(',') === '1,3' && windTest.draws.length >= 3 && windTest.draws.every(d => d.width > 0 && d.height > 0));
        check('native banner wind supplies varying rotation', () => new Set(windTest.draws.map(d => d.rotation.toFixed(5))).size > 1);
        bl.log('apiexpansion TRACE wind calls=' + windTest.calls + ' draws=' + windTest.draws.length + ' rotations=' + JSON.stringify(windTest.draws.slice(0, 6)));
        for (const s of windTest.saved) { s.tile.type = s.type; s.tile.frameX = s.frameX; s.tile.frameY = s.frameY; s.tile['void active(bool active)'](s.active); }
        Main.windSpeedCurrent = windTest.wind; Main.windSpeedTarget = windTest.target;
        Main.SettingsEnabled_TilesSwayInWind = windTest.enabled;
        windTest = null;
        bl.log('apiexpansion WIND FIM checks=' + checks + ' falhas=' + failures);
    }
    if (pendingClaim && ++pendingClaim.frames >= 30) {
        check('native request and loopback award the banner item', () => {
            const item = pendingClaim.item;
            let found = Main.mouseItem.type === item;
            const inventory = Main.LocalPlayer.inventory;
            for (let i = 0; i < inventory.length; i++) if (inventory[i].type === item && inventory[i].stack > 0) found = true;
            for (let i = 0; i < Main.item.length; i++) if (Main.item[i].type === item && Main.item[i].stack > 0) found = true;
            return B.claimableBanners[pendingClaim.banner] === 0 && found;
        });
        pendingClaim = null;
        bl.log('apiexpansion FIM checks=' + checks + ' falhas=' + failures);
    }
    if (done || Main.gameMenu || ++ticks < 120) return;
    done = true;
    try { run(); } catch (error) { failures++; bl.log('apiexpansion FAIL EXEC ' + error + ' ' + error.stack); }
    if (!pendingClaim) bl.log('apiexpansion FIM checks=' + checks + ' falhas=' + failures);
});
export default class ApiExpansionTests extends Mod {}
