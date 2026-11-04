cmake_minimum_required(VERSION 3.22)
project(bunny CXX C ASM)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)

# --- ShadowHook (via prefab, vem do Gradle) ---
find_package(shadowhook REQUIRED CONFIG)

# Nossos fontes C++ (recursivo).
file(GLOB_RECURSE BL_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.cpp)
list(FILTER BL_SOURCES EXCLUDE REGEX "/compile/")

add_library(blcompile SHARED compile/CompilerHost.cpp)
target_link_libraries(blcompile log dl)

# O trampolim da chamada guardada (il2cpp/GuardedCall.h), em assembly.
file(GLOB_RECURSE BL_ASM_SOURCES CONFIGURE_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/*.S)

add_library(bunny SHARED ${BL_SOURCES} ${BL_ASM_SOURCES})

target_include_directories(bunny PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_BINARY_DIR}/gen)

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
