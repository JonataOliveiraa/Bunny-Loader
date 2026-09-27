const globalItems = new GlobalRegistry(GlobalItem, () => Terraria.Item, '__globalItems', 'GetGlobalItem');
const globalNPCs = new GlobalRegistry(GlobalNPC, () => Terraria.NPC, '__globalNPCs', 'GetGlobalNPC');
const globalProjectiles = new GlobalRegistry(GlobalProjectile, () => Terraria.Projectile, '__globalProjectiles', 'GetGlobalProjectile');
