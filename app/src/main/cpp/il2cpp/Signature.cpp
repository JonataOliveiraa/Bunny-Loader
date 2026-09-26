#include "il2cpp/Signature.h"
#include "il2cpp/Api.h"
#include "core/Log.h"

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

std::vector<std::string> splitParams(std::string_view inner);

/**
 * Um nome de tipo genérico, separado: "System.Nullable`1<System.Single>" ->
 * base "System.Nullable", argumentos {"System.Single"}. A aridade (`1) sai
 * da base porque ninguém a escreve ao ler o dump, e o TL a escreve sem os
 * argumentos. Aceita <...> e [...] (os dois formatos do runtime); "[]" de
 * array não é genérico.
 */
struct Generic {
    std::string base;
    std::vector<std::string> args;
    bool hasArgs = false;
};

Generic splitGeneric(const std::string& name) {
    Generic g;
    size_t open = std::string::npos;
    for (size_t i = 0; i < name.size(); ++i) {
        if (name[i] == '<' || (name[i] == '[' && i + 1 < name.size() && name[i + 1] != ']')) {
            open = i;
            break;
        }
    }
    std::string base = open == std::string::npos ? name : name.substr(0, open);
    size_t tick = base.find('`');
    if (tick != std::string::npos) base.erase(tick);
    g.base = base;
    if (open != std::string::npos) {
        size_t close = name.find_last_of(name[open] == '<' ? '>' : ']');
        if (close != std::string::npos && close > open) {
            g.args = splitParams(std::string_view(name).substr(open + 1, close - open - 1));
            g.hasArgs = true;
        }
    }
    return g;
}

/** "SpriteFont.Glyph" casa com "Microsoft.Xna.Framework.Graphics.SpriteFont/Glyph". */
bool nameEndsWith(std::string got, const std::string& want) {
    for (char& c : got) if (c == '/' || c == '+') c = '.';
    if (got == want) return true;
    return got.size() > want.size() && got[got.size() - want.size() - 1] == '.' &&
           got.compare(got.size() - want.size(), want.size(), want) == 0;
}

bool typeMatches(const std::string& want, const char* got);

/**
 * Tipo genérico: `Nullable\`1` (como o TL escreve), `Nullable<float>` e
 * `float?` casam com o Nullable<Single> do runtime. Sem argumentos, a base
 * basta; com argumentos, cada um é conferido.
 */
bool genericMatches(const std::string& want, const std::string& got) {
    Generic g = splitGeneric(got);
    if (!g.hasArgs) return false;
    Generic w;
    if (want.size() > 1 && want.back() == '?') {
        w.base = "Nullable";
        w.args = {want.substr(0, want.size() - 1)};
        w.hasArgs = true;
    } else {
        w = splitGeneric(want);
    }
    if (w.base.empty() || !nameEndsWith(g.base, w.base)) return false;
    if (!w.hasArgs) return true;
    if (w.args.size() != g.args.size()) return false;
    for (size_t i = 0; i < w.args.size(); ++i) {
        if (!typeMatches(w.args[i], g.args[i].c_str())) return false;
    }
    return true;
}

/**
 * O tipo escrito pelo modder casa com o que o IL2CPP reporta?
 *
 * Três formas aceitas, da mais estrita para a mais frouxa:
 *   1. igual              ("System.Int32" == "System.Int32")
 *   2. palavra-chave C#   ("int"          -> "System.Int32")
 *   3. só o nome curto    ("ItemVariant"  -> "Terraria.ItemVariant")
 *   4. genérico           ("Nullable`1", "Nullable<float>", "float?"
 *                          -> Nullable<Single>; ver genericMatches)
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

    if (genericMatches(want, got)) return true;

    // Sufixo após o último ponto, preservando "[]"/"&" que vierem junto — e
    // tipo aninhado, que o runtime separa com '/'.
    std::string g(got);
    if (nameEndsWith(g, want)) return true;

    // Apelido DENTRO de um array ou de um ref: `byte[]` tem de casar com
    // `System.Byte[]`, e não casava — a tabela só conhecia o nome cru. Quem
    // escrevesse `int[]` numa assinatura não achava o método e não recebia
    // pista do porquê.
    size_t fim = want.size();
    while (fim > 0 && (want[fim - 1] == '&' || want[fim - 1] == ']' || want[fim - 1] == '[')) {
        --fim;
    }
    if (fim == want.size() || fim == 0) return false;
    const char* clr = keywordToClr(want.substr(0, fim));
    if (!clr) return false;
    return (std::string(clr) + want.substr(fim)) == got;
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

struct ParamDecl {
    std::string type;
    std::string name;  // "" = so o tipo
};

/**
 * "int Type" -> {"int", "Type"}. O nome tem de ser o do jogo, letra por
 * letra: e ele que diz que o modder leu o metodo certo.
 */
ParamDecl paramOf(const std::string& decl) {
    // Corta um valor default, se o modder esqueceu de tirar ("int a = 0").
    std::string d = decl;
    size_t eq = d.find('=');
    if (eq != std::string::npos) d = trim(d.substr(0, eq));

    // `ref int x` / `out Vector2 pos` / `in T x`: o runtime chama de "Int32&".
    std::string suffix;
    for (const char* kw : {"ref ", "out ", "in "}) {
        const size_t n = std::strlen(kw);
        if (d.compare(0, n, kw) == 0) {
            d = trim(d.substr(n));
            suffix = "&";
            break;
        }
    }

    size_t sp = d.find_last_of(" \t");
    if (sp == std::string::npos) return {d + suffix, {}};  // só o tipo, sem nome
    return {trim(d.substr(0, sp)) + suffix, trim(d.substr(sp + 1))};
}

/** O nome do parametro `i` de `m` no jogo; "" se o runtime nao diz. */
std::string paramName(const MethodInfo* m, uint32_t i) {
    auto& a = api();
    const char* n = a.method_get_param_name ? a.method_get_param_name(m, i) : nullptr;
    return n ? n : "";
}

/** Os nomes escritos sao os do jogo, exatos? */
bool namesMatch(const MethodInfo* m, const Signature& sig) {
    // Sem il2cpp_method_get_param_name nao ha com o que comparar: fica o tipo.
    if (!api().method_get_param_name) return true;
    for (size_t i = 0; i < sig.paramNames.size(); ++i) {
        if (sig.paramNames[i] != paramName(m, static_cast<uint32_t>(i))) return false;
    }
    return true;
}

/** "System.Int32" -> "int", "Terraria.DataStructures.PlayerDrawSet" -> "PlayerDrawSet". */
std::string displayType(std::string t) {
    std::string prefix, suffix;
    if (!t.empty() && t.back() == '&') {
        t.pop_back();
        prefix = "ref ";
    }
    while (t.size() > 2 && t.compare(t.size() - 2, 2, "[]") == 0) {
        suffix += "[]";
        t.resize(t.size() - 2);
    }
    // Generico fica inteiro: o nome completo casa, e encurtar os argumentos
    // um a um nao vale o risco de escrever algo que nao casa.
    if (t.find_first_of("<[`") == std::string::npos) {
        static const struct { const char* clr; const char* cs; } kMap[] = {
            {"System.Void", "void"},       {"System.Boolean", "bool"},  {"System.Byte", "byte"},
            {"System.SByte", "sbyte"},     {"System.Int16", "short"},   {"System.UInt16", "ushort"},
            {"System.Int32", "int"},       {"System.UInt32", "uint"},   {"System.Int64", "long"},
            {"System.UInt64", "ulong"},    {"System.Single", "float"},  {"System.Double", "double"},
            {"System.Decimal", "decimal"}, {"System.Char", "char"},     {"System.String", "string"},
            {"System.Object", "object"},
        };
        bool keyword = false;
        for (const auto& k : kMap) {
            if (t == k.clr) { t = k.cs; keyword = true; break; }
        }
        if (!keyword) {
            // Aninhado: "Microsoft.Xna.Framework.Graphics.SpriteFont/Glyph" -> "SpriteFont.Glyph".
            const size_t slash = t.find('/');
            const size_t dot = t.rfind('.', slash == std::string::npos ? std::string::npos : slash);
            if (dot != std::string::npos) t.erase(0, dot + 1);
            for (char& c : t) if (c == '/') c = '.';
        }
    }
    return prefix + t + suffix;
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
        ParamDecl d = paramOf(p);
        sig.paramTypes.push_back(std::move(d.type));
        sig.paramNames.push_back(std::move(d.name));
    }
    sig.valid = true;
    return sig;
}

const MethodInfo* findMethodBySignature(Il2CppClass* cls, const Signature& sig,
                                        bool* ambiguous, const MethodInfo** nameMismatch) {
    if (ambiguous) *ambiguous = false;
    if (nameMismatch) *nameMismatch = nullptr;
    if (!cls || !sig.valid) return nullptr;
    auto& a = api();

    const MethodInfo* found = nullptr;
    const MethodInfo* almost = nullptr;
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
            // Os tipos casaram; o nome de cada parametro tambem tem de casar.
            // Nao desempata nada (dois overloads nunca tem os mesmos tipos):
            // so recusa o que foi escrito diferente do jogo.
            if (!namesMatch(m, sig)) {
                if (!almost) almost = m;
                continue;
            }
            if (found && found != m) {
                if (ambiguous) *ambiguous = true;
                return nullptr;
            }
            found = m;
        }
        if (found) break;  // classe derivada ganha da base
    }
    if (!found && almost) {
        if (nameMismatch) {
            *nameMismatch = almost;
        } else {
            // Busca interna (C++): quem chamou so ve nullptr, entao o motivo
            // vai ao log — e o que muda quando o jogo renomeia um parametro.
            BL_WARN("assinatura '%s': %s", sig.name.c_str(), explainParamNames(almost, sig).c_str());
        }
    }
    return found;
}

std::string explainParamNames(const MethodInfo* m, const Signature& sig) {
    std::string out;
    for (size_t i = 0; i < sig.paramNames.size(); ++i) {
        const std::string real = paramName(m, static_cast<uint32_t>(i));
        const std::string& wrote = sig.paramNames[i];
        if (wrote == real) continue;
        if (!out.empty()) out += "; ";
        out += "o parametro " + std::to_string(i + 1);
        out += wrote.empty() ? " esta sem nome (e '" + real + "')"
                             : " se chama '" + real + "', nao '" + wrote + "'";
    }
    return out + ". No jogo: " + describeMethod(m);
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
    std::string s = displayType(typeName(a.method_get_return_type(m)));
    s += " ";
    s += a.method_get_name(m);
    s += "(";
    uint32_t n = a.method_get_param_count(m);
    for (uint32_t i = 0; i < n; ++i) {
        if (i) s += ", ";
        s += displayType(typeName(a.method_get_param(m, i)));
        const std::string pn = paramName(m, i);
        if (!pn.empty()) s += " " + pn;
    }
    s += ")";
    return s;
}

} // namespace bl::il2cpp
