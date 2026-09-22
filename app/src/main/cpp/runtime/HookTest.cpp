#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "runtime/GameRefs.h"
#include <atomic>

// Teste do modo de hook que funciona sob houdini (MuMu): hook por ENDERECO de
// um metodo ja carregado, nao por nome pendente (esse precisa do linker x86).
// Confirmado: shadowhook_hook_func_addr instala em Projectile.SetDefaults.
//
// Aqui provamos tambem que o hook DISPARA, hookando Main.DoUpdate(GameTime) —
// o loop principal, que roda todo frame ja no menu (nao precisa jogar).

namespace bl::runtime {

namespace {

using DoUpdateFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
DoUpdateFn origDoUpdate = nullptr;
std::atomic<int> g_frames{0};

void hkDoUpdate(Il2CppObject* self, Il2CppObject* gameTime, const MethodInfo* m) {
    int n = g_frames.fetch_add(1);
    if (n == 0) {
        BL_INFO(">>> HOOK Main.DoUpdate DISPAROU (frame 0) — hook funciona sob houdini!");
    } else if (n == 300) {
        BL_INFO(">>> HOOK Main.DoUpdate: 300 frames interceptados, tudo estavel");
    }
    origDoUpdate(self, gameTime, m);
}

using SetDefaultsFn = void (*)(Il2CppObject*, int32_t, const MethodInfo*);
SetDefaultsFn origSetDefaults = nullptr;

void hkSetDefaults(Il2CppObject* self, int32_t type, const MethodInfo* m) {
    BL_INFO(">>> HOOK Projectile.SetDefaults DISPAROU: type=%d", type);
    origSetDefaults(self, type, m);
}

} // namespace

void installHookTest() {
    auto& g = game();
    auto& a = il2cpp::api();

    // 1. Projectile.SetDefaults (dispara ao atirar, no jogo).
    if (g.proj.setDefaults) {
        if (hook::install(g.proj.setDefaults, hkSetDefaults, &origSetDefaults))
            BL_INFO(">>> hooktest: Projectile.SetDefaults hookado (por endereco)");
        else
            BL_ERROR("hooktest: hook de Projectile.SetDefaults falhou");
    }

    // 2. Main.DoUpdate (dispara todo frame, ja no menu) — prova de disparo.
    Il2CppClass* mainCls = il2cpp::findClass({"Terraria", "Main", {}});
    const MethodInfo* doUpdate = mainCls
        ? a.class_get_method_from_name(mainCls, "DoUpdate", 1) : nullptr;
    if (doUpdate) {
        BL_INFO("hooktest: hookando Main.DoUpdate em %p", il2cpp::methodPointer(doUpdate));
        if (hook::install(doUpdate, hkDoUpdate, &origDoUpdate))
            BL_INFO(">>> hooktest: Main.DoUpdate hookado — aguardando disparo");
        else
            BL_ERROR("hooktest: hook de Main.DoUpdate falhou");
    } else {
        BL_ERROR("hooktest: Main.DoUpdate nao resolvido");
    }
}

} // namespace bl::runtime
