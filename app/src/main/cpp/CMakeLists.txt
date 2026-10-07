cmake_minimum_required(VERSION 3.22)
project(bunny CXX C)

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
    Core/Constants Core/Safe Core/Hooks Core/Ready Core/Entities Core/Templates
    Core/ModFiles Core/Lang Core/ModRegistry
    Enums/AffectionLevel Enums/SoundLimitBehavior Enums/SceneEffectPriority Enums/EquipType Enums/PrefixCategory
    Enums/CommandType
    Mod ModLoader
    Loaders/LocalizationLoader ModLocalization
    StatModifier StatInheritanceData DamageClass Loaders/DamageClassLoader
    Loaders/RecipeLoader ModRecipe
    Loaders/ItemLoader Loaders/ItemDataLoader Loaders/ItemNetworkHooks Loaders/EquipLoader Loaders/ArmorSetLoader Loaders/WingLoader
    Loaders/TooltipLoader Loaders/VanillaTooltips TooltipLine DrawableTooltipLine Loaders/RarityLoader ModRarity
    EquipTexture ItemLoot ModItem
    Loaders/PrefixLoader ModPrefix
    TagCompound GenPass Loaders/SystemUpdateHooks Loaders/SystemDrawHooks Loaders/SystemWorldHooks Loaders/SystemNetworkHooks Loaders/SystemLoader ModSystem
    Loaders/PlayerCombatHooks Loaders/PlayerItemHooks Loaders/ItemCombatHooks Loaders/ItemUseHooks Loaders/ItemHealingHooks Loaders/PlayerUpdateHooks Loaders/PlayerJumpHooks
    Loaders/PlayerDrawHooks Loaders/PlayerWorldHooks Loaders/PlayerNetworkHooks Loaders/PlayerLoader ModPlayer
    Loaders/BackgroundTextureLoader Loaders/SurfaceBackgroundLoader Loaders/UndergroundBackgroundLoader
    ModSurfaceBackgroundStyle ModUndergroundBackgroundStyle
    Loaders/WaterfallStyleLoader Loaders/WaterStyleLoader ModWaterfallStyle ModWaterStyle
    Loaders/MapBackgroundLoader Loaders/SceneEffectLoader Loaders/BiomeLoader Loaders/BiomeShoppingLoader
    Loaders/BiomeBestiaryLoader ModSceneEffect ModBiome
    Loaders/BuffLoader ModBuff
    Loaders/MountLoader ModMount
    Loaders/CommandLoader ModCommand Loaders/HairLoader ModHair Loaders/CloudLoader ModCloud
    Loaders/EmoteBubbleLoader ModEmoteBubble Loaders/AchievementLoader ModAchievement Loaders/AnglerQuestLoader
    Loaders/TileLoader Loaders/TileDrawLoader Loaders/TileUseLoader Loaders/TileWorldLoader
    ModTile TileRestingInfo
    Loaders/WallLoader ModWall
    Loaders/ProjectileLoader ModProjectile
    NPCLoot NPCSpawnInfo SpawnPool SpawnCondition Loaders/SpawnLoader Loaders/NPCLoader Loaders/BestiaryLoader ModNPC
    NPCHappiness NPCShop Loaders/TownNPCLoader
    Loaders/GoreLoader ModGore
    Core/GlobalType Core/GlobalRegistry GlobalItem GlobalNPC GlobalProjectile Core/GlobalRegistries
    Loaders/HitLoader Loaders/GlobalItemLoader Loaders/GlobalNPCLoader Loaders/GlobalProjectileLoader
    GlobalLoot Loaders/GlobalLootLoader
    NetWriter NetReader ModPacket Core/ModNet
    Loaders/SoundLoader SoundStyle SoundEngine Loaders/VanillaMusic Loaders/ModMusic MusicLoader
    Loaders/MenuLoader ModMenu Loaders/ConfigLoader ModConfig Loaders/ModConfigMenu
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

add_library(bunny SHARED ${BL_SOURCES} ${BL_QUICKJS_SOURCES})

target_include_directories(bunny PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_BINARY_DIR}/gen)
if(EXISTS ${QUICKJS_DIR}/quickjs.h)
    target_include_directories(bunny PRIVATE ${QUICKJS_DIR})
endif()

target_compile_options(bunny PRIVATE -O2 -fno-exceptions -fno-rtti)

# Abi.cpp e o UNICO que entra no codigo do jogo por chamada direta, e uma
# excecao gerida pelo IL2CPP e uma excecao C++ de verdade. Sem tabela de
# desenrolar aqui, ela passaria por um frame -fno-exceptions e chamaria
# std::terminate. -fexceptions so neste arquivo custa as tabelas dele e nada
# mais; -fno-rtti continua valendo (catch(...) nao precisa de RTTI).
# MapReveal.cpp pelo mesmo motivo: chama o WorldMap.UpdateLighting direto,
# milhoes de vezes, e ele lanca se o mapa nao tiver onde guardar o pedaco.
set_source_files_properties(script/bridge/Abi.cpp menu/MapReveal.cpp
    content/textures/TexturePacks.cpp
    PROPERTIES COMPILE_OPTIONS -fexceptions)
target_link_libraries(bunny shadowhook::shadowhook log dl)
