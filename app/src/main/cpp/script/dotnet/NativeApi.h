#pragma once

namespace bl::dotnet {

/**
 * A tabela de funcoes nativas que o C# recebe no Host.Initialize. Layout em
 * NativeApi.cpp, espelhado em app/src/main/csharp/BunnyLoader/Interop/NativeApi.cs.
 */
const void* nativeApiTable();

} // namespace bl::dotnet
