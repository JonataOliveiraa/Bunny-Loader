cmake_minimum_required(VERSION 3.22)
project(bunny CXX C ASM)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)

# --- ShadowHook (via prefab, vem do Gradle) ---
find_package(shadowhook REQUIRED CONFIG)

# --- QuickJS (fontes locais, ver third_party/README.md) ---
set(QUICKJS_DIR ${CMAKE_CURRENT_SOURCE_DIR}/third_party/quickjs)
set(BL_QUICKJS_SOURCES "")
# quickjs-ng v0.17: fontes sao quickjs.c/libregexp.c/libunicode.c/dtoa.c
# (nao ha mais cutils.c nem libbf). Precisa de _GNU_SOURCE.
if(EXISTS ${QUICKJS_DIR}/quickjs.c)
    # O quickjs.c entra pelo QuickJsExt.c, que o inclui e acrescenta o acesso
    # a pilha de frames do runtime (motor compartilhado entre threads).
    list(APPEND BL_QUICKJS_SOURCES
        ${CMAKE_CURRENT_SOURCE_DIR}/script/bridge/QuickJsExt.c
        ${QUICKJS_DIR}/libregexp.c
        ${QUICKJS_DIR}/libunicode.c
        ${QUICKJS_DIR}/dtoa.c)
    set_source_files_properties(${BL_QUICKJS_SOURCES} PROPERTIES
        COMPILE_DEFINITIONS "_GNU_SOURCE")
    add_compile_definitions(BL_HAVE_QUICKJS=1)
    message(STATUS "QuickJS (quickjs-ng) encontrado — motor de script habilitado.")
else()
    message(WARNING "QuickJS ausente em third_party/quickjs — ScriptEngine será stub. Ver third_party/README.md")
endif()

# --- Mods embutidos (gerados a partir do .js; fonte unica de verdade) ---
# Embute o JS do mod padrao dentro da libbunny para funcionar SEM adb/arquivos
# no celular. Le samples/HelloMod/content/main.js (raiz do repo: 4 niveis acima de cpp/).
set(BL_MINISHARK_JS ${CMAKE_CURRENT_SOURCE_DIR}/../../../../samples/HelloMod/content/main.js)
file(READ ${BL_MINISHARK_JS} BL_BUILTIN_MINISHARK_JS)
# file(READ) nao cria dependencia: sem isto o CMake nao reconfigura quando o
# .js muda e a lib segue embutindo a versao anterior — descoberto quando
# tl.log virou bl.log e o mod continuou chamando tl.
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS ${BL_MINISHARK_JS})
configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/mods/BuiltinMods.h.in
    ${CMAKE_CURRENT_BINARY_DIR}/gen/mods/BuiltinMods.h @ONLY)

# As classes base dos mods (Mod, ModItem, ModNPC...), em script/js/mod/: uma
# classe por arquivo (Core/ o nucleo, Loaders/ os hooks de cada conteudo,
# Enums/, e na raiz a API dos mods), juntados nesta ordem dentro de uma funcao
# (um escopo so, privado) e embutidos como um texto. A ordem importa: a base
# antes do `extends`, e as classes que um inicializador estatico usa antes dele.
set(BL_MOD_CLASSES_PARTS
    Core/Constants Core/Safe Core/Hooks Core/SharedHooks Core/Ready Core/Entities Core/DrawBatches Core/Templates
    Core/ModFiles Core/Lang Core/ModRegistry
    Enums/AffectionLevel Enums/SoundLimitBehavior Enums/SceneEffectPriority Enums/EquipType Enums/PrefixCategory
    Enums/CommandType Enums/TooltipNames Enums/TreeTypes
    Mod ModLoader
    Loaders/LocalizationLoader ModLocalization Condition
    StatModifier HitModifiers HitInfo HurtModifiers HurtInfo StatInheritanceData DamageClass Loaders/DamageClassLoader
    Loaders/RecipeLoader ModRecipe
    Loaders/ItemLoader Loaders/ItemDrawLoader Loaders/ItemDataLoader Loaders/ItemNetworkHooks Loaders/TileEntityItemHooks Loaders/EquipLoader Loaders/ArmorSetLoader Loaders/WingLoader Loaders/ItemPickupLoader
    Loaders/TooltipLoader Loaders/VanillaTooltips TooltipLine DrawableTooltipLine Loaders/RarityLoader ModRarity
    EquipTexture ItemLoot ModItem
    Loaders/PrefixLoader ModPrefix
    TagCompound GenPass Loaders/SystemUpdateHooks Loaders/SystemDrawHooks Loaders/SystemWorldHooks Loaders/SystemNetworkHooks Loaders/SystemLoader ModSystem
    Loaders/CombatLoader Loaders/PvpItemNetwork Loaders/PlayerItemHooks Loaders/ItemCombatHooks Loaders/ItemUseHooks Loaders/ItemHealingHooks Loaders/PlayerUpdateHooks Loaders/PlayerJumpHooks
    Loaders/PlayerDrawHooks Loaders/PlayerWorldHooks Loaders/PlayerNetworkHooks Loaders/PlayerLoader ModPlayer
    Loaders/TouchButtonLayouts Loaders/TouchButtonHooks Loaders/TouchButtonLoader ModTouchButton Loaders/KeybindLoader
    Loaders/BackgroundTextureLoader Loaders/SurfaceBackgroundLoader Loaders/UndergroundBackgroundLoader
    ModSurfaceBackgroundStyle ModUndergroundBackgroundStyle
    Loaders/WaterfallStyleLoader Loaders/WaterStyleLoader ModWaterfallStyle ModWaterStyle
    Loaders/MapBackgroundLoader Loaders/SceneEffectLoader Loaders/BiomeLoader Loaders/BiomeShoppingLoader
    Loaders/BiomeBestiaryLoader ModSceneEffect ModBiome
    Loaders/BuffLoader ModBuff
    Loaders/MountLoader ModMount
    Loaders/CommandLoader ModCommand Loaders/HairLoader ModHair Loaders/CloudLoader ModCloud
    Loaders/EmoteBubbleLoader ModEmoteBubble Loaders/AchievementLoader ModAchievement Loaders/AnglerQuestLoader
    Loaders/TileLoader Loaders/TileDrawLoader Loaders/TileUseLoader Loaders/TileWorldLoader Loaders/ChestNetworkHooks
    ModTile ModBannerTile TileRestingInfo Loaders/TileEntityLoader ModTileEntity TEModdedPylon Loaders/TreeLoader ModTree
    Loaders/PylonLoader ModPylon
    Loaders/WallLoader ModWall
    Loaders/ProjectileLoader ModProjectile
    Loaders/BossBarLoader ModBossBar
    NPCLoot NPCSpawnInfo SpawnPool SpawnCondition Loaders/SpawnLoader Loaders/NPCLoader Loaders/BestiaryLoader ModNPC Loaders/BannerLoader
    NPCHappiness NPCShop Loaders/TownNPCLoader
    Loaders/GoreLoader ModGore
    Loaders/DustLoader ModDust
    UI/StyleDimension UI/CalculatedStyle UI/UIEvent UI/UIMouseEvent UI/UIScrollWheelEvent UI/UIElementEvent
    UI/UIElement UI/UIState UI/UserInterface UI/UINative UI/UIPanel UI/UIText UI/UITextPanel
    UI/UIImage UI/UIImageButton UI/UIImageFramed UI/UIScrollbar UI/UIListInner UI/UIList
    UI/UICommon UI/InterfaceScaleType UI/GameInterfaceLayer UI/LegacyGameInterfaceLayer UI/InterfaceLayerList Loaders/InterfaceLayerLoader
    UI/PlayerStatsSnapshot UI/ResourceOverlayDrawContext Loaders/ResourceOverlayLoader ModResourceOverlay
    Core/GlobalType Core/GlobalRegistry GlobalItem GlobalNPC GlobalProjectile Core/GlobalRegistries
    Loaders/GlobalItemLoader Loaders/GlobalNPCLoader Loaders/GlobalProjectileLoader
    GlobalLoot Loaders/GlobalLootLoader
    Core/GlobalList GlobalTile GlobalBuff GlobalBossBar GlobalRecipe Core/GlobalLists Loaders/GlobalTileLoader
    NetWriter NetReader ModPacket Core/ModNet
    Loaders/SoundLoader SoundStyle SoundEngine Loaders/VanillaMusic Loaders/ModMusic MusicLoader
    Loaders/MenuLoader ModMenu Loaders/ConfigLoader ModConfig Loaders/ModConfigMenu Loaders/MenuThemeSetting Loaders/HostSettingsMenu Loaders/ServerSyncLoader Loaders/ServerSyncMenu
    Effects/ScreenSpace Effects/Easing Effects/ColorMath Effects/ScreenShader Effects/ScreenTarget Effects/ScreenTexture
    Effects/ScreenPasses Effects/ScreenMask Effects/ScreenTween Effects/ScreenCurve
    Effects/ScreenGraphics Effects/ScreenUtils Effects/ScreenFrame Effects/ScreenCommands Effects/DistortionMap
    Effects/ScreenEffects Effects/ModScreenEffect
    Effects/Presets/Twirl Effects/Presets/Shockwave Effects/Presets/Blur Effects/Presets/Bloom Effects/Presets/Echo
    Effects/Presets/BlackHole Effects/Presets/HeatHaze Effects/Presets/Tint Effects/Presets/ColorGrade
    Effects/Presets/Vignette Effects/Presets/Ripple Effects/Presets/ForceField Effects/Presets/ChromaticAberration
    Effects/Presets/Pixelate Effects/Presets/Flash Effects/Presets/Glitch Effects/Presets/ZoomBlur Effects/Presets/Lens
    Effects/ScreenPresets Effects/ScreenDebug Effects/ScreenFxCommand Loaders/ScreenEffectsSetting
    Core/ContentLookup ModContent Core/ContentAutoload
    Exports)
set(BL_MOD_CLASSES_JS "(() => {\n'use strict';\n")
foreach(part ${BL_MOD_CLASSES_PARTS})
    set(part_file ${CMAKE_CURRENT_SOURCE_DIR}/script/js/mod/${part}.js)
    file(READ ${part_file} part_text)
    string(APPEND BL_MOD_CLASSES_JS "// ---- mod/${part}.js ----\n${part_text}\n")
    set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS ${part_file})
endforeach()
string(APPEND BL_MOD_CLASSES_JS "})();\n")
configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/script/js/ModClassesJs.h.in
    ${CMAKE_CURRENT_BINARY_DIR}/gen/script/js/ModClassesJs.h @ONLY)

# Os ajudantes (Vector2, MathHelper, Rand, Color, ItemRarityID...), avaliados
# antes das classes.
set(BL_MOD_HELPERS_FILE ${CMAKE_CURRENT_SOURCE_DIR}/script/js/ModHelpers.js)
file(READ ${BL_MOD_HELPERS_FILE} BL_MOD_HELPERS_JS)
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS ${BL_MOD_HELPERS_FILE})
configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/script/js/ModHelpersJs.h.in
    ${CMAKE_CURRENT_BINARY_DIR}/gen/script/js/ModHelpersJs.h @ONLY)

# Nossos fontes C++ (recursivo).
file(GLOB_RECURSE BL_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.cpp)

# O trampolim da chamada guardada (il2cpp/GuardedCall.h), em assembly.
file(GLOB_RECURSE BL_ASM_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.S)

add_library(bunny SHARED ${BL_SOURCES} ${BL_ASM_SOURCES} ${BL_QUICKJS_SOURCES})

target_include_directories(bunny PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_BINARY_DIR}/gen)
if(EXISTS ${QUICKJS_DIR}/quickjs.h)
    target_include_directories(bunny PRIVATE ${QUICKJS_DIR})
endif()

target_compile_options(bunny PRIVATE -O2 -fno-exceptions -fno-rtti)

# Sem -fexceptions em arquivo nenhum. Uma excecao do jogo vem do __cxa_throw
# do libil2cpp, com o desenrolador do libgcc; um `catch` daqui, ou um frame
# daqui com destrutor pendente, usaria a personalidade da libbunny, que le o
# contexto como o do LLVM libunwind e derruba o processo. Quem chama codigo do
# jogo e pode ver uma excecao usa o il2cpp::guardedCall (il2cpp/GuardedCall.S),
# que nao depende de desenrolador. Um hook nativo no meio da pilha do jogo (o
# loadHook do TexturePacks.cpp) nao precisa de nada: sem personalidade, o frame
# dele e so atravessado (um destrutor pendente ali fica sem rodar).
target_link_libraries(bunny shadowhook::shadowhook log dl)
