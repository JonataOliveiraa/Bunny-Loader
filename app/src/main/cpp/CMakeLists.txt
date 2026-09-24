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
    list(APPEND BL_QUICKJS_SOURCES
        ${QUICKJS_DIR}/quickjs.c
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
set_source_files_properties(script/Abi.cpp runtime/MapReveal.cpp
    PROPERTIES COMPILE_OPTIONS -fexceptions)
target_link_libraries(bunny shadowhook::shadowhook log dl)
