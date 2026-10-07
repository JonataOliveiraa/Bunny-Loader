// A API que os mods enxergam; o resto fica no escopo privado.
Object.assign(globalThis, {
    Mod, ModLoader, ModContent, ModLocalization, ModConfig,
    ModItem, ItemLoot, ModRecipe, DamageClass, DamageClassLoader, StatModifier, StatInheritanceData, HitModifiers, HitInfo, HurtModifiers, HurtInfo, TooltipLine, TooltipNames, DrawableTooltipLine, ModRarity, RarityLoader, EquipType, EquipLoader, EquipTexture,
    ModPrefix, PrefixCategory, PrefixLoader,
    ModSystem, GenPass, PassLegacy, TagCompound, ModSceneEffect, ModBiome,
    ModSurfaceBackgroundStyle, ModUndergroundBackgroundStyle, BackgroundTextureLoader,
    ModWaterStyle, ModWaterfallStyle, ModMenu,
    ModPlayer, ExtraJump, PlayerDrawLayer, PlayerDrawLayers, ModBuff, ModMount, MountTextureType, ModTile, ModWall, ModProjectile,
    ModCommand, CommandType, UsageException, CommandLoader, ModHair, ModCloud, CloudLoader, ModEmoteBubble, ModAchievement,
    ModNPC, NPCLoot, NPCSpawnInfo, SpawnPool, SpawnCondition, NPCShop, NPCHappiness, AffectionLevel, ModGore,
    GlobalItem, GlobalNPC, GlobalProjectile, GlobalLoot,
    ModPacket, NetWriter, NetReader,
    SoundStyle, SoundEngine, SoundLimitBehavior, MusicLoader, SceneEffectPriority,
});

ArmorSetLoader.InstallHairSettings();

// Sempre ligado, com ou sem mod: o "Config. dos Mods" do menu de pausa.
ModConfigMenu.Install();
// As opções do Bunny Loader na tela de Host do multijogador.
HostSettingsMenu.Install();
// A conferência dos mods e das texturas ao entrar num servidor.
ServerSyncLoader.Install();
ServerSyncMenu.Install();
