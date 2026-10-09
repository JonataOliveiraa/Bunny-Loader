// Ordenacao por dependencias (mods/LoadOrder.cpp) e o "dependencies" do
// manifesto (mods/ManifestText.cpp), sem aparelho: ./run.sh
#include "mods/LoadOrder.h"
#include "mods/ManifestText.h"

#include <cstdio>
#include <string>
#include <vector>

using bl::mods::OrderEntry;
using bl::mods::sortByDependencies;

namespace {
int failures = 0;

void check(bool ok, const char* what) {
    std::printf("%s %s\n", ok ? "ok    " : "FALHOU", what);
    if (!ok) ++failures;
}

OrderEntry mod(const std::string& id, std::vector<std::string> deps = {}, bool usable = true) {
    return {"uid-" + id, id, std::move(deps), usable};
}

std::string orderOf(const std::vector<OrderEntry>& mods) {
    const auto r = sortByDependencies(mods);
    std::string out;
    for (size_t i : r.order) out += mods[i].id;
    return out;
}

std::string failedOf(const std::vector<OrderEntry>& mods) {
    const auto r = sortByDependencies(mods);
    std::string out;
    for (size_t i = 0; i < mods.size(); ++i) if (!r.errors[i].empty()) out += mods[i].id;
    return out;
}
}

int main() {
    check(orderOf({mod("a"), mod("b"), mod("c"), mod("d")}) == "abcd", "sem dependencias: a ordem do launcher");
    check(orderOf({mod("d"), mod("c"), mod("b"), mod("a")}) == "dcba", "sem dependencias: invertida tambem fica");
    check(orderOf({mod("a", {"c"}), mod("b"), mod("c")}) == "cab", "dependencia sobe para antes de quem a pede");
    check(orderOf({mod("a", {"uid-c"}), mod("b"), mod("c")}) == "cab", "dependencia pelo uid");
    check(orderOf({mod("a", {"b"}), mod("b", {"c"}), mod("c")}) == "cba", "dependencia transitiva");
    check(orderOf({mod("x"), mod("a", {"c", "b"}), mod("b"), mod("c")}) == "xcba",
          "varias dependencias: na ordem em que o manifesto as lista");

    // Idempotente: a ordem que o servidor manda (ja ordenada) chega igual.
    {
        const std::vector<OrderEntry> first = {mod("e", {"b"}), mod("a", {"d"}), mod("b"), mod("c"), mod("d", {"c"})};
        const auto r = sortByDependencies(first);
        std::vector<OrderEntry> again;
        for (size_t i : r.order) again.push_back(first[i]);
        std::string a, b;
        for (const auto& m : again) a += m.id;
        b = orderOf(again);
        check(a == b, "reordenar a lista ja ordenada nao muda nada");
    }

    check(failedOf({mod("a", {"z"}), mod("b")}) == "a", "dependencia ausente: so o mod que pede cai");
    check(failedOf({mod("a", {"b"}), mod("b", {"z"}), mod("c")}) == "ab", "ausente mais abaixo: a cadeia inteira cai");
    check(failedOf({mod("a", {"b"}), mod("b", {"a"}), mod("c")}) == "ab", "ciclo: os dois caem, o resto carrega");
    check(failedOf({mod("a", {"a"}), mod("b")}) == "a", "depender de si mesmo e ciclo");
    check(failedOf({mod("a", {"b"}), mod("b", {"c"}), mod("c", {"b"}), mod("d")}) == "abc",
          "quem depende de um ciclo cai junto");
    check(failedOf({mod("a", {"b"}), mod("b", {}, false)}) == "a", "dependencia sem arquivo de entrada derruba quem pede");
    {
        std::vector<OrderEntry> mods = {mod("a", {"dup"}), mod("dup"), mod("dup")};
        mods[2].uid = "uid-dup2";
        check(failedOf(mods) == "a", "id repetido entre dois mods: ambiguo, pede o uid");
        mods[0].dependencies = {"uid-dup2"};
        check(failedOf(mods).empty() && orderOf(mods) == "dupadup", "id repetido, pedido pelo uid: resolve");
    }
    {
        const auto r = sortByDependencies({mod("a", {"b"}), mod("b", {"a"})});
        check(r.errors[0].find("dependency cycle: ") == 0 && r.errors[0].find(" -> ") != std::string::npos,
              "o motivo do ciclo mostra o caminho");
        check(r.order.size() == 2, "mod que cai continua na lista (fica no registro, desligado)");
    }
    {
        const auto r = sortByDependencies({mod("a", {"b", "b", "uid-b"}), mod("b")});
        check(r.needs[0].size() == 1 && r.errors[0].empty(), "dependencia repetida conta uma vez");
    }

    // O "dependencies" lido do manifesto (mods/ManifestText.cpp).
    using bl::mods::manifestString;
    using bl::mods::manifestStrings;
    using List = std::vector<std::string>;
    check(manifestStrings(R"({"id":"a","dependencies":["b", "uid-c"]})", "dependencies") == List{"b", "uid-c"},
          "manifesto: lista de textos");
    check(manifestStrings(R"({"dependencies": [ ]})", "dependencies").empty(), "manifesto: lista vazia");
    check(manifestStrings(R"({"id":"a"})", "dependencies").empty(), "manifesto: sem o campo");
    check(manifestStrings(R"({"dependencies":"b"})", "dependencies").empty(), "manifesto: texto solto nao e lista");
    check(manifestStrings(R"({"authors":[{"dependencies":["x"]}],"dependencies":["b"]})", "dependencies") == List{"b"},
          "manifesto: so a chave do objeto de fora");
    check(manifestStrings(R"({"dependencies":["a]b", 3, {"k":"x"}, ["y"], "", "c"]})", "dependencies") == List{"a]b", "c"},
          "manifesto: colchete dentro do texto, nao-textos e vazios ficam de fora");
    check(manifestStrings(R"({"dependencies":["a", "b")", "dependencies") == List{"a", "b"},
          "manifesto: arquivo cortado nao trava");
    check(manifestString(R"({"authors":[{"name":"Fulano"}],"name":"Meu Mod","dependencies":["x"]})", "name") == "Meu Mod",
          "manifesto: o name continua o de fora");

    std::printf("%s\n", failures ? "FALHOU" : "tudo ok");
    return failures ? 1 : 0;
}
