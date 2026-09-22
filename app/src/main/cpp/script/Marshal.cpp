#include "script/Marshal.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "script/Bridge.h"

#include <cstring>
#include <memory>

namespace bl::script {

namespace {

std::string typeNameOf(const Il2CppType* t) {
    if (!t) return {};
    auto& a = il2cpp::api();
    char* raw = a.type_get_name(t);
    if (!raw) return {};
    std::string s(raw);
    a.il2cpp_free(raw);
    return s;
}

/** Tamanho do tipo por valor, ou 0 se for referência. */
size_t valueSize(const std::string& t) {
    if (t == "System.Boolean" || t == "System.Byte" || t == "System.SByte") return 1;
    if (t == "System.Int16" || t == "System.UInt16" || t == "System.Char") return 2;
    if (t == "System.Int32" || t == "System.UInt32" || t == "System.Single") return 4;
    if (t == "System.Int64" || t == "System.UInt64" || t == "System.Double") return 8;
    return 0;
}

} // namespace

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

bool ArgPack::build(JSContext* ctx, const MethodInfo* m, int argc, JSValueConst* argv) {
    auto& a = il2cpp::api();
    uint32_t n = a.method_get_param_count(m);
    storage_.reserve(n);
    slots_.reserve(n);

    for (uint32_t i = 0; i < n; ++i) {
        const Il2CppType* pt = a.method_get_param(m, i);
        std::string t = typeNameOf(pt);
        JSValueConst v = (static_cast<int>(i) < argc) ? argv[i] : JS_UNDEFINED;

        size_t sz = valueSize(t);
        // Enum: o nome e o do enum, nao o do subjacente. Trata como int32 —
        // cobre praticamente todos os enums do Terraria.
        if (sz == 0 && a.class_from_il2cpp_type && a.class_is_enum) {
            if (Il2CppClass* pc = a.class_from_il2cpp_type(pt)) {
                if (a.class_is_enum(pc)) sz = 4;
            }
        }
        if (sz == 0) {
            // Referência: entra o ponteiro do objeto, não o endereço dele.
            if (JS_IsNull(v) || JS_IsUndefined(v)) {
                slots_.push_back(nullptr);
            } else if (t == "System.String") {
                const char* cs = JS_ToCString(ctx, v);
                if (!cs) return false;
                Il2CppString* s = a.string_new(cs);
                JS_FreeCString(ctx, cs);
                slots_.push_back(s);
            } else if (Il2CppObject* o = objectFromJS(v)) {
                slots_.push_back(o);
            } else {
                JS_ThrowTypeError(ctx, "argumento %u: esperava %s (passe um objeto do jogo ou null)",
                                  i + 1, t.c_str());
                return false;
            }
            continue;
        }

        auto buf = std::make_unique<uint8_t[]>(8);
        std::memset(buf.get(), 0, 8);
        if (t == "System.Single") {
            double d = 0; if (JS_ToFloat64(ctx, &d, v) < 0) return false;
            float f = static_cast<float>(d); std::memcpy(buf.get(), &f, 4);
        } else if (t == "System.Double") {
            double d = 0; if (JS_ToFloat64(ctx, &d, v) < 0) return false;
            std::memcpy(buf.get(), &d, 8);
        } else if (t == "System.Boolean") {
            buf[0] = JS_ToBool(ctx, v) ? 1 : 0;
        } else {
            int64_t iv = 0; if (JS_ToInt64(ctx, &iv, v) < 0) return false;
            std::memcpy(buf.get(), &iv, sz);
        }
        slots_.push_back(buf.get());
        storage_.push_back(std::move(buf));
    }
    return true;
}

JSValue fromReturn(JSContext* ctx, const MethodInfo* m, Il2CppObject* ret) {
    auto& a = il2cpp::api();
    std::string t = typeNameOf(a.method_get_return_type(m));
    if (t.empty() || t == "System.Void") return JS_UNDEFINED;
    if (!ret) return JS_NULL;

    // Valor volta BOXED: o dado fica logo depois do cabeçalho do objeto.
    auto* p = reinterpret_cast<uint8_t*>(ret) + sizeof(Il2CppObject);
    if (t == "System.Single")  { float f; std::memcpy(&f, p, 4); return JS_NewFloat64(ctx, f); }
    if (t == "System.Double")  { double d; std::memcpy(&d, p, 8); return JS_NewFloat64(ctx, d); }
    if (t == "System.Boolean") return JS_NewBool(ctx, *p != 0);
    if (t == "System.Byte")    return JS_NewInt32(ctx, *p);
    if (t == "System.SByte")   return JS_NewInt32(ctx, *reinterpret_cast<int8_t*>(p));
    if (t == "System.Int16")   { int16_t x; std::memcpy(&x, p, 2); return JS_NewInt32(ctx, x); }
    if (t == "System.UInt16" || t == "System.Char") {
        uint16_t x; std::memcpy(&x, p, 2); return JS_NewInt32(ctx, x);
    }
    if (t == "System.Int32" || t == "System.UInt32") {
        int32_t x; std::memcpy(&x, p, 4); return JS_NewInt32(ctx, x);
    }
    if (t == "System.Int64" || t == "System.UInt64") {
        int64_t x; std::memcpy(&x, p, 8); return JS_NewInt64(ctx, x);
    }
    if (t == "System.String") {
        return JS_NewString(ctx, stringToUtf8(reinterpret_cast<Il2CppString*>(ret)).c_str());
    }
    return makeNativeObject(ctx, ret);
}

} // namespace bl::script
#endif
