// A API que os mods enxergam; o resto fica no escopo privado.
Object.assign(globalThis, {
    Mod, ModLoader, ModContent, ModLocalization, ModConfig,
    ModItem, ItemLoot, ModRecipe, Condition, DamageClass, DamageClassLoader, StatModifier, StatInheritanceData, HitModifiers, HitInfo, HurtModifiers, HurtInfo, TooltipLine, TooltipNames, DrawableTooltipLine, ModRarity, RarityLoader, EquipType, EquipLoader, EquipTexture,
    ModPrefix, PrefixCategory, PrefixLoader,
    ModSystem, GenPass, PassLegacy, TagCompound, ModSceneEffect, ModBiome,
    ModSurfaceBackgroundStyle, ModUndergroundBackgroundStyle, BackgroundTextureLoader,
    ModWaterStyle, ModWaterfallStyle, ModMenu,
    ModTouchButton, TouchButtonLoader, KeybindLoader, ModKeybind: ModTouchButton,
    ModPlayer, ExtraJump, PlayerDrawLayer, PlayerDrawLayers, ModBuff, ModMount, MountTextureType, ModTile, ModBannerTile, ModTileEntity, TEModdedPylon, ModPylon, ModTree, TreeTypes, ModWall, ModProjectile,
    ModCommand, CommandType, UsageException, CommandLoader, ModHair, ModCloud, CloudLoader, ModEmoteBubble, ModAchievement,
    ModNPC, ModBossBar, NPCLoot, NPCSpawnInfo, SpawnPool, SpawnCondition, NPCShop, NPCHappiness, AffectionLevel, ModGore, ModDust, DustLoader,
    GlobalItem, GlobalNPC, GlobalProjectile, GlobalLoot, GlobalTile, GlobalBuff, GlobalBossBar, GlobalRecipe,
    ModPacket, NetWriter, NetReader,
    SoundStyle, SoundEngine, SoundLimitBehavior, MusicLoader, SceneEffectPriority,
    ModScreenEffect, ScreenEffects, ScreenPresets, ScreenGraphics, ScreenUtils,
    UIElement, UIState, UserInterface, StyleDimension, CalculatedStyle, UIEvent, UIMouseEvent, UIScrollWheelEvent,
    UIPanel, UIText, UITextPanel, UIImage, UIImageButton, UIImageFramed, UIList, UIScrollbar,
    GameInterfaceLayer, LegacyGameInterfaceLayer, InterfaceScaleType, UICommon,
    ModResourceOverlay, ResourceOverlayDrawContext, PlayerStatsSnapshot,
});

ArmorSetLoader.InstallHairSettings();
// Item.ResearchUnlockCount, a quantidade que o modo Jornada pede para pesquisar.
ItemLoader.InstallResearch();

// Sempre ligado, com ou sem mod: o "Config. dos Mods" do menu de pausa.
ModConfigMenu.Install();
// As opções do Bunny Loader na tela de Host do multijogador.
HostSettingsMenu.Install();
// Os efeitos de tela: a linha das Configurações e o /fx.
ScreenEffectsSetting.Install();
ScreenFxCommand.Install();
// A conferência dos mods e das texturas ao entrar num servidor.
ServerSyncLoader.Install();
ServerSyncMenu.Install();
