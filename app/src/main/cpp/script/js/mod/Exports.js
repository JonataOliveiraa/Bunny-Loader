// A API que os mods enxergam; o resto fica no escopo privado.
Object.assign(globalThis, {
    Mod, ModLoader, ModContent, ModLocalization,
    ModItem, ModRecipe, TooltipLine,
    ModSystem, TagCompound,
    ModPlayer, ModBuff, ModTile, ModProjectile,
    ModNPC, NPCLoot, NPCSpawnInfo, NPCShop, NPCHappiness, AffectionLevel, ModGore,
    GlobalItem, GlobalNPC, GlobalProjectile, GlobalLoot,
    ModPacket, NetWriter, NetReader,
    SoundStyle, SoundEngine, SoundLimitBehavior, MusicLoader, SceneEffectPriority,
});
