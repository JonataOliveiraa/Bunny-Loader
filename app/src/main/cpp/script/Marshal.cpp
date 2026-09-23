#include "script/Marshal.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "script/Bridge.h"
#include "script/Value.h"

#include <cstring>
#include <memory>

namespace bl::script {

std::string stringToUtf8(Il2CppString* s) {
    if (!s || s->length <= 0) return {};
    // UTF-16 -> UTF-8 na mão: a libil2cpp não exporta conversor, e o texto
    // aqui é nome de item/diálogo, não um parser completo de Unicode.
    std::string out;
    out.reserve(static_cast<size_t>(s->length));
    for (int32_t i = 0; i < s->length; ++i) {
        char32_t c = s->chars[i];
        // Par substituto: junta antes de codificar.
        if (c >= 0xD800 && c <= 0xDBFF && i + 1 < s->length) {
            char32_t lo = s->chars[i + 1];
            if (lo >= 0xDC00 && lo <= 0xDFFF) {
                c = 0x10000 + ((c - 0xD800) << 10) + (lo - 0xDC00);
                ++i;
            }
        }
        if (c < 0x80) {
            out += static_cast<char>(c);
        } else if (c < 0x800) {
            out += static_cast<char>(0xC0 | (c >> 6));
            out += static_cast<char>(0x80 | (c & 0x3F));
        } else if (c < 0x10000) {
            out += static_cast<char>(0xE0 | (c >> 12));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        } else {
            out += static_cast<char>(0xF0 | (c >> 18));
            out += static_cast<char>(0x80 | ((c >> 12) & 0x3F));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        }
    }
    return out;
}

ArgPack::~ArgPack() {
    auto& a = il2cpp::api();
    if (!a.gchandle_free) return;
    for (uint32_t h : handles_) a.gchandle_free(h);
}

void* ArgPack::reserve(size_t bytes) {
    size_t alinhado = (usado_ + 15u) & ~static_cast<size_t>(15u);
    if (alinhado + bytes <= kArena) {
        usado_ = alinhado + bytes;
        void* p = arena_ + alinhado;
        std::memset(p, 0, bytes);
        return p;
    }
    grandes_.push_back(std::make_unique<uint8_t[]>(bytes));
    std::memset(grandes_.back().get(), 0, bytes);
    return grandes_.back().get();
}

bool ArgPack::build(JSContext* ctx, const MethodInfo* m, int argc, JSValueConst* argv) {
    auto& a = il2cpp::api();
    uint32_t n = a.method_get_param_count(m);
    if (n > static_cast<uint32_t>(kMaxArgs)) {
        JS_ThrowTypeError(ctx, "metodo com %u parametros; o limite da ponte e %d", n, kMaxArgs);
        return false;
    }

    for (uint32_t i = 0; i < n; ++i) {
        const TypeDesc& d = describe(a.method_get_param(m, i));
        JSValueConst v = (static_cast<int>(i) < argc) ? argv[i] : JS_UNDEFINED;

        if (d.byRef) {
            // `ref`/`out` quer o ENDERECO de uma variavel do chamador, e o JS
            // nao tem isso. Recusa em vez de mandar um ponteiro qualquer.
            JS_ThrowTypeError(ctx, "argumento %u e ref/out (%s) — ainda nao suportado",
                              i + 1, d.name.c_str());
            return false;
        }

        // O buffer tem no minimo 8 bytes: um tipo por referencia escreve o
        // ponteiro aqui antes de a gente extrai-lo.
        size_t sz = d.size < sizeof(void*) ? sizeof(void*) : d.size;
        void* buf = reserve(sz);
        if (writeAt(ctx, buf, d, v) < 0) return false;

        // A convencao do runtime_invoke: tipo por VALOR entra pelo endereco do
        // valor; tipo por REFERENCIA entra pelo proprio ponteiro do objeto.
        // Isto vale inclusive para struct — passar a CAIXA faz o metodo ler o
        // cabecalho como se fosse o primeiro campo, sem erro e com lixo.
        void* slot = d.byValue ? buf : *reinterpret_cast<void**>(buf);
        slots_[count_++] = slot;
        if (d.prim == Prim::String && slot && a.gchandle_new) {
            handles_.push_back(a.gchandle_new(static_cast<Il2CppObject*>(slot), false));
        }
    }
    return true;
}

JSValue fromReturn(JSContext* ctx, const MethodInfo* m, Il2CppObject* ret) {
    const TypeDesc& d = describe(il2cpp::api().method_get_return_type(m));
    if (d.prim == Prim::Void) return JS_UNDEFINED;

    // Referencia: `ret` JA e o objeto; o leitor espera o endereco de onde ler
    // o ponteiro, entao passamos o endereco da variavel local.
    if (!d.byValue) return readAt(ctx, &ret, d, JS_UNDEFINED);

    if (!ret) return JS_NULL;
    // Valor volta ENCAIXOTADO: o dado fica logo depois do cabecalho.
    void* data = reinterpret_cast<char*>(ret) + sizeof(Il2CppObject);
    // A caixa e do coletor do jogo e ninguem mais aponta pra ela; copiamos em
    // vez de virar uma vista que pode ser recolhida a qualquer momento.
    if (d.prim == Prim::Struct) return makeStructCopy(ctx, d.cls, data, d.size);
    return readAt(ctx, data, d, JS_UNDEFINED);
}

} // namespace bl::script
#endif
