cmake_minimum_required(VERSION 3.22.1)
project(blcompiler CXX)

# O Mono do compilador dos mods C# (CompilerRuntime.kt). Nada liga contra a
# libmonosgen: os símbolos vêm por dlsym, como no libbunny do app.
add_library(blcompiler SHARED CompilerHost.cpp)
target_compile_options(blcompiler PRIVATE -Wall -Wextra -fvisibility=hidden)
target_link_libraries(blcompiler PRIVATE log dl)
