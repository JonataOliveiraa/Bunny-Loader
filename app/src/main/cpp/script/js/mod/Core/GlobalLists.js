const globalTiles = new GlobalList(GlobalTile, 'GlobalTile', (run) => GlobalTileLoader.Unlocked(run));
const globalBuffs = new GlobalList(GlobalBuff, 'GlobalBuff');
const globalBossBars = new GlobalList(GlobalBossBar, 'GlobalBossBar');
const globalRecipes = new GlobalList(GlobalRecipe, 'GlobalRecipe');
