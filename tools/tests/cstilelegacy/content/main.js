import { Block } from './Content/Tiles/Block.js';
const Main = Terraria.Main;
let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player,index);
    if (done || Main.gameMenu || index !== Main.myPlayer || ++frames !== 120) return;
    done = true;
    let fail = 0;
    try {
        const type = ModContent.TileType(Block);
        Terraria.WorldGen['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](64,64,type,true,true,-1,0);
        const tile = Main.tile['Tile get_Item(int x, int y)'](64,64);
        tile.frameX = 18; tile.frameY = 36;
        if (!tile['bool active()']() || tile.type !== type) fail++;
        Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);
        bl.log('cstilelegacy saved Block type=' + type + ' at 64,64');
    } catch (error) { fail++; bl.log('cstilelegacy run: FAIL ' + error); }
    bl.log('cstilelegacy END pass=' + (fail === 0 ? 1 : 0) + ' fail=' + fail);
});
export default class CsTileLegacy extends Mod {}
