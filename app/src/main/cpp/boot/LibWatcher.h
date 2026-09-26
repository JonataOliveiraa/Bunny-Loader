#pragma once

namespace bl::loader {

// Registra um hook PENDENTE em il2cpp_init. O ShadowHook aplica sozinho
// quando a libil2cpp.so for carregada pela Unity.
bool installWatcher();

} // namespace bl::loader
