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
if(EXISTS ${QUICKJS_DIR}/quickjs.c)
    list(APPEND BL_QUICKJS_SOURCES
        ${QUICKJS_DIR}/quickjs.c
        ${QUICKJS_DIR}/libregexp.c
        ${QUICKJS_DIR}/libunicode.c
        ${QUICKJS_DIR}/cutils.c)
    add_compile_definitions(BL_HAVE_QUICKJS=1)
    message(STATUS "QuickJS encontrado — motor de script habilitado.")
else()
    message(WARNING "QuickJS ausente em third_party/quickjs — ScriptEngine será stub. Ver third_party/README.md")
endif()

# Nossos fontes C++ (recursivo).
file(GLOB_RECURSE BL_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.cpp)

add_library(bunny SHARED ${BL_SOURCES} ${BL_QUICKJS_SOURCES})

target_include_directories(bunny PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
if(EXISTS ${QUICKJS_DIR}/quickjs.h)
    target_include_directories(bunny PRIVATE ${QUICKJS_DIR})
endif()

target_compile_options(bunny PRIVATE -O2 -fno-exceptions -fno-rtti)
target_link_libraries(bunny shadowhook::shadowhook log dl)
