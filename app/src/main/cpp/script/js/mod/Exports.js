// A API que os mods enxergam; o resto fica no escopo privado.
Object.assign(globalThis, {
    Mod, ModLoader, ModContent, ModLocalization,
    ModItem, ItemLoot, ModRecipe, TooltipLine, DrawableTooltipLine, ModRarity, RarityLoader, EquipType, EquipLoader, EquipTexture,
    ModPrefix, PrefixCategory, PrefixLoader,
    ModSystem, TagCompound, ModSceneEffect, ModBiome,
    ModSurfaceBackgroundStyle, ModUndergroundBackgroundStyle, BackgroundTextureLoader,
    ModWaterStyle, ModWaterfallStyle, ModMenu,
    ModPlayer, ModBuff, ModMount, MountTextureType, ModTile, ModProjectile,
    ModCommand, CommandType, UsageException, CommandLoader, ModHair, ModCloud, CloudLoader, ModEmoteBubble, ModAchievement,
    ModNPC, NPCLoot, NPCSpawnInfo, SpawnPool, SpawnCondition, NPCShop, NPCHappiness, AffectionLevel, ModGore,
    GlobalItem, GlobalNPC, GlobalProjectile, GlobalLoot,
    ModPacket, NetWriter, NetReader,
    SoundStyle, SoundEngine, SoundLimitBehavior, MusicLoader, SceneEffectPriority,
});
