// A API que os mods enxergam; o resto fica no escopo privado.
Object.assign(globalThis, {
    Mod, ModLoader, ModContent, ModLocalization,
    ModItem, ModRecipe, TooltipLine, EquipType, EquipLoader, EquipTexture,
    ModPrefix, PrefixCategory, PrefixLoader,
    ModSystem, TagCompound, ModSceneEffect, ModBiome,
    ModSurfaceBackgroundStyle, ModUndergroundBackgroundStyle, BackgroundTextureLoader,
    ModPlayer, ModBuff, ModTile, ModProjectile,
    ModNPC, NPCLoot, NPCSpawnInfo, NPCShop, NPCHappiness, AffectionLevel, ModGore,
    GlobalItem, GlobalNPC, GlobalProjectile, GlobalLoot,
    ModPacket, NetWriter, NetReader,
    SoundStyle, SoundEngine, SoundLimitBehavior, MusicLoader, SceneEffectPriority,
});
