#include "il2cpp/Signature.h"
#include "il2cpp/Api.h"

#include <algorithm>
#include <cstring>

namespace bl::il2cpp {

namespace {

std::string trim(std::string_view s) {
    size_t a = s.find_first_not_of(" \t\r\n");
    if (a == std::string_view::npos) return {};
    size_t b = s.find_last_not_of(" \t\r\n");
    return std::string(s.substr(a, b - a + 1));
}

/**
 * Palavra-chave C# -> nome que o IL2CPP reporta.
 *
 * O modder escreve o que está no dump ("int", "float"); o
 * `il2cpp_type_get_name` devolve o nome do tipo do CLR ("System.Int32").
 */
const char* keywordToClr(const std::string& kw) {
    struct Pair { const char* cs; const char* clr; };
    static const Pair kMap[] = {
        {"void", "System.Void"},       {"bool", "System.Boolean"},
        {"byte", "System.Byte"},       {"sbyte", "System.SByte"},
        {"short", "System.Int16"},     {"ushort", "System.UInt16"},
        {"int", "System.Int32"},       {"uint", "System.UInt32"},
        {"long", "System.Int64"},      {"ulong", "System.UInt64"},
        {"float", "System.Single"},    {"double", "System.Double"},
        {"decimal", "System.Decimal"}, {"char", "System.Char"},
        {"string", "System.String"},   {"object", "System.Object"},
    };
    for (const auto& p : kMap) {
        if (kw == p.cs) return p.clr;
    }
    return nullptr;
}

/**
 * O tipo escrito pelo modder casa com o que o IL2CPP reporta?
 *
 * Três formas aceitas, da mais estrita para a mais frouxa:
 *   1. igual              ("System.Int32" == "System.Int32")
 *   2. palavra-chave C#   ("int"          -> "System.Int32")
 *   3. só o nome curto    ("ItemVariant"  -> "Terraria.ItemVariant")
 *
 * A terceira existe porque ninguém escreve o namespace inteiro ao ler o dump,
 * e é segura o bastante: o conjunto candidato já está limitado aos overloads
 * de um método de uma classe.
 */
bool typeMatches(const std::string& want, const char* got) {
    if (want.empty() || !got) return true;  // não especificado = não verifica
    if (want == got) return true;

    if (const char* clr = keywordToClr(want)) {
        if (std::strcmp(clr, got) == 0) return true;
    }

    // Sufixo após o último ponto, preservando "[]"/"&" que vierem junto.
    std::string g(got);
    size_t dot = g.rfind('.');
    if (dot != std::string::npos && g.substr(dot + 1) == want) return true;
    return false;
}

/** Nome do tipo (malloc do il2cpp) como std::string, já liberado. */
std::string typeName(const Il2CppType* t) {
    if (!t) return {};
    auto& a = api();
    char* raw = a.type_get_name(t);
    if (!raw) return {};
    std::string s(raw);
    a.il2cpp_free(raw);
    return s;
}

/** Separa por vírgula no nível 0, para não quebrar List<int, string>. */
std::vector<std::string> splitParams(std::string_view inner) {
    std::vector<std::string> out;
    int depth = 0;
    size_t start = 0;
    for (size_t i = 0; i <= inner.size(); ++i) {
        if (i == inner.size() || (inner[i] == ',' && depth == 0)) {
            std::string part = trim(inner.substr(start, i - start));
            if (!part.empty()) out.push_back(part);
            start = i + 1;
        } else if (inner[i] == '<' || inner[i] == '[') {
            ++depth;
        } else if (inner[i] == '>' || inner[i] == ']') {
            // "int[]" fecha logo depois de abrir; depth nunca fica negativo.
            if (depth > 0) --depth;
        }
    }
    return out;
}

/**
 * "int Type" -> "int". O NOME do parâmetro é decorativo: o modder copia do
 * dump e nós casamos só por tipo.
 */
std::string paramTypeOf(const std::string& decl) {
    // Corta um valor default, se o modder esqueceu de tirar ("int a = 0").
    std::string d = decl;
    size_t eq = d.find('=');
    if (eq != std::string::npos) d = trim(d.substr(0, eq));

    size_t sp = d.find_last_of(" \t");
    if (sp == std::string::npos) return d;  // só o tipo, sem nome
    return trim(d.substr(0, sp));
}

} // namespace

Signature parseSignature(std::string_view text) {
    Signature sig;
    size_t open = text.find('(');
    size_t close = text.rfind(')');
    if (open == std::string_view::npos || close == std::string_view::npos || close < open) {
        return sig;
    }

    std::string head = trim(text.substr(0, open));
    size_t sp = head.find_last_of(" \t");
    if (sp == std::string::npos) {
        sig.name = head;  // "Nome(...)" sem tipo de retorno
    } else {
        sig.returnType = trim(head.substr(0, sp));
        sig.name = trim(head.substr(sp + 1));
    }
    if (sig.name.empty()) return sig;

    for (const auto& p : splitParams(text.substr(open + 1, close - open - 1))) {
        sig.paramTypes.push_back(paramTypeOf(p));
    }
    sig.valid = true;
    return sig;
}

const MethodInfo* findMethodBySignature(Il2CppClass* cls, const Signature& sig,
                                        bool* ambiguous) {
    if (ambiguous) *ambiguous = false;
    if (!cls || !sig.valid) return nullptr;
    auto& a = api();

    const MethodInfo* found = nullptr;
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        void* iter = nullptr;
        while (const MethodInfo* m = a.class_get_methods(c, &iter)) {
            if (sig.name != a.method_get_name(m)) continue;
            if (a.method_get_param_count(m) != sig.paramTypes.size()) continue;
            if (!typeMatches(sig.returnType, typeName(a.method_get_return_type(m)).c_str())) {
                continue;
            }
            bool ok = true;
            for (size_t i = 0; i < sig.paramTypes.size(); ++i) {
                if (!typeMatches(sig.paramTypes[i], typeName(a.method_get_param(m, i)).c_str())) {
                    ok = false;
                    break;
                }
            }
            if (!ok) continue;
            if (found && found != m) {
                if (ambiguous) *ambiguous = true;
                return nullptr;
            }
            found = m;
        }
        if (found) break;  // classe derivada ganha da base
    }
    return found;
}

std::vector<std::string> listOverloads(Il2CppClass* cls, std::string_view name) {
    std::vector<std::string> out;
    if (!cls) return out;
    auto& a = api();
    std::string n(name);
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        void* iter = nullptr;
        while (const MethodInfo* m = a.class_get_methods(c, &iter)) {
            if (n == a.method_get_name(m)) out.push_back(describeMethod(m));
        }
        if (!out.empty()) break;
    }
    return out;
}

std::string describeMethod(const MethodInfo* m) {
    if (!m) return "(null)";
    auto& a = api();
    std::string s = typeName(a.method_get_return_type(m));
    s += " ";
    s += a.method_get_name(m);
    s += "(";
    uint32_t n = a.method_get_param_count(m);
    for (uint32_t i = 0; i < n; ++i) {
        if (i) s += ", ";
        s += typeName(a.method_get_param(m, i));
    }
    s += ")";
    return s;
}

} // namespace bl::il2cpp
