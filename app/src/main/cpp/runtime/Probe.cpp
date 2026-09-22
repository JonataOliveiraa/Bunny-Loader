#include "runtime/Probe.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "runtime/GameRefs.h"

namespace bl::runtime { void installHookTest(); }
#include "script/ScriptEngine.h"

#include <chrono>
#include <dlfcn.h>
#include <thread>

namespace bl::runtime {

namespace {

using DomainGetFn = Il2CppDomain* (*)();

// IMPORTANTE: chamar il2cpp_domain_get() durante a janela de inicializacao do
// il2cpp crasha (SIGSEGV) — o caminho do hook so tocaria a API DEPOIS do
// il2cpp_init. Sem poder hookar (houdini), a sonda espera o jogo assentar por
// um tempo fixo antes de tocar em qualquer coisa do il2cpp, e nunca faz poll
// apertado.
constexpr int kSettleMs = 10000;  // tempo ate o jogo chegar ao menu
constexpr int kRetries = 12;
constexpr int kRetryGapMs = 2500;

bool il2cppReady() {
    void* lib = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW);
    if (!lib) return false;
    auto domain_get = reinterpret_cast<DomainGetFn>(dlsym(lib, "il2cpp_domain_get"));
    return domain_get && domain_get() != nullptr;
}

void probeThread() {
    BL_INFO("sonda: aguardando o jogo assentar (%d ms)...", kSettleMs);
    std::this_thread::sleep_for(std::chrono::milliseconds(kSettleMs));

    for (int i = 0; i < kRetries; ++i) {
        if (il2cppReady()) {
            BL_INFO("sonda: il2cpp pronto; carregando API");
            auto& a = il2cpp::api();
            if (!a.load()) {
                BL_ERROR("sonda: Api::load() falhou");
                return;
            }
            a.thread_attach(a.domain_get());
            if (resolveGameRefs()) {
                BL_INFO("sonda: RESOLUCAO OK — camada de bind validada no processo do jogo");
                // Testa o hook por endereco (deve funcionar sob houdini, ao
                // contrario do hook pendente por nome).
                installHookTest();

                // Fase 4: sobe o QuickJS dentro do jogo e roda um script.
                if (script::engine().init()) {
                    BL_INFO("sonda: QuickJS iniciado; rodando script de teste");
                    script::engine().eval(
                        "const ItemID = new NativeClass('Terraria.ID', 'ItemID');\n"
                        "tl.log('Minishark id = ' + ItemID.getStaticInt('Minishark') + ' (esperado 98)');\n"
                        "\n"
                        "// escrita de estatico: muda e restaura netMode\n"
                        "const Main = new NativeClass('Terraria', 'Main');\n"
                        "tl.log('netMode antes = ' + Main.getStaticInt('netMode'));\n"
                        "Main.setStaticInt('netMode', 2);\n"
                        "tl.log('netMode depois de set(2) = ' + Main.getStaticInt('netMode'));\n"
                        "Main.setStaticInt('netMode', 0);\n"
                        "\n"
                        "// instancia: cria um Item e escreve/le campos\n"
                        "const Item = new NativeClass('Terraria', 'Item');\n"
                        "const it = Item.new();\n"
                        "it.setInt('type', 42);\n"
                        "it.setInt('useTime', 4);\n"
                        "it.setFloat('shootSpeed', 10.5);\n"
                        "tl.log('item.type=' + it.getInt('type') + ' useTime=' + it.getInt('useTime')"
                        " + ' shootSpeed=' + it.getFloat('shootSpeed') + ' (esp 42/4/10.5)');\n"
                        "\n"
                        "// HOOK via JS: intercepta Projectile.SetDefaults (dispara no boot)\n"
                        "const Projectile = new NativeClass('Terraria', 'Projectile');\n"
                        "const SetDefaults = Projectile.method('SetDefaults', 1);\n"
                        "let n = 0;\n"
                        "SetDefaults.hook((original, self, type) => {\n"
                        "  original(self, type);\n"
                        "  if (n < 3) { tl.log('JS hook SetDefaults: type=' + type +"
                        " ' whoAmI=' + self.getInt('whoAmI')); n++; }\n"
                        "});\n"
                        "tl.log('hook JS instalado; aguardando disparos...');\n",
                        "teste");
                } else {
                    BL_ERROR("sonda: QuickJS nao iniciou");
                }
            } else {
                BL_ERROR("sonda: resolveGameRefs falhou");
            }
            return;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(kRetryGapMs));
    }
    BL_ERROR("sonda: il2cpp nao ficou pronto a tempo");
}

} // namespace

void startResolutionProbe() {
    std::thread(probeThread).detach();
}

} // namespace bl::runtime
