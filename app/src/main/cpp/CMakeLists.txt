cmake_minimum_required(VERSION 3.22)
project(bunny CXX C ASM)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)

find_package(shadowhook REQUIRED CONFIG)

file(GLOB_RECURSE BL_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.cpp)
list(FILTER BL_SOURCES EXCLUDE REGEX "/compile/")

add_library(blcompile SHARED compile/CompilerHost.cpp)
target_link_libraries(blcompile log dl)

file(GLOB_RECURSE BL_ASM_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.S)

add_library(bunny SHARED ${BL_SOURCES} ${BL_ASM_SOURCES})

target_include_directories(bunny PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_BINARY_DIR}/gen)

target_compile_options(bunny PRIVATE -O2 -fno-exceptions -fno-rtti)

target_link_libraries(bunny shadowhook::shadowhook log dl)
