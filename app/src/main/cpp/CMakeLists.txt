cmake_minimum_required(VERSION 3.22)
project(bunny CXX C ASM)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)

find_package(shadowhook REQUIRED CONFIG)

file(GLOB_RECURSE BL_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.cpp)
list(FILTER BL_SOURCES EXCLUDE REGEX "/compile/")

set(MOJOSHADER_DIR ${CMAKE_CURRENT_SOURCE_DIR}/third_party/mojoshader)
add_library(mojoshader STATIC
    ${MOJOSHADER_DIR}/mojoshader.c
    ${MOJOSHADER_DIR}/mojoshader_common.c
    ${MOJOSHADER_DIR}/mojoshader_effects.c
    ${MOJOSHADER_DIR}/profiles/mojoshader_profile_common.c
    ${MOJOSHADER_DIR}/profiles/mojoshader_profile_glsl.c)
target_include_directories(mojoshader PUBLIC ${MOJOSHADER_DIR})
target_compile_definitions(mojoshader PUBLIC
    MOJOSHADER_NO_VERSION_INCLUDE
    MOJOSHADER_EFFECT_SUPPORT
    SUPPORT_PROFILE_D3D=0
    SUPPORT_PROFILE_BYTECODE=0
    SUPPORT_PROFILE_HLSL=0
    SUPPORT_PROFILE_ARB1=0
    SUPPORT_PROFILE_ARB1_NV=0
    SUPPORT_PROFILE_METAL=0
    SUPPORT_PROFILE_SPIRV=0
    SUPPORT_PROFILE_GLSPIRV=0)
set_target_properties(mojoshader PROPERTIES C_STANDARD 99 C_VISIBILITY_PRESET hidden POSITION_INDEPENDENT_CODE ON)
target_compile_definitions(mojoshader PRIVATE NDEBUG)
target_compile_options(mojoshader PRIVATE -O2 -w)

add_library(blcompile SHARED compile/CompilerHost.cpp)
target_link_libraries(blcompile log dl)

file(GLOB_RECURSE BL_ASM_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.S)

add_library(bunny SHARED ${BL_SOURCES} ${BL_ASM_SOURCES})

target_include_directories(bunny PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_BINARY_DIR}/gen)

target_compile_options(bunny PRIVATE -O2 -fno-exceptions -fno-rtti)

target_link_libraries(bunny shadowhook::shadowhook mojoshader log dl z m)
