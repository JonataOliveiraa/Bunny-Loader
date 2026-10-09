#include "mods/LoadOrder.h"

#include <algorithm>

namespace bl::mods {

namespace {
constexpr size_t kMissing = static_cast<size_t>(-1);
constexpr size_t kAmbiguous = static_cast<size_t>(-2);

enum class Mark { None, Visiting, Done };

struct Sorter {
    const std::vector<OrderEntry>& mods;
    OrderResult result;
    std::vector<Mark> marks;
    std::vector<size_t> stack;

    explicit Sorter(const std::vector<OrderEntry>& list)
        : mods(list), marks(list.size(), Mark::None) {
        result.errors.resize(list.size());
        result.needs.resize(list.size());
    }

    std::string nameOf(size_t i) const {
        return mods[i].id.empty() ? mods[i].uid : mods[i].id;
    }

    // Pelo uid primeiro (e unico); pelo id so se um mod so o usa, como o
    // ModLoader.TryGetMod.
    size_t resolve(const std::string& ref) const {
        for (size_t i = 0; i < mods.size(); ++i) {
            if (mods[i].uid == ref) return i;
        }
        size_t found = kMissing;
        for (size_t i = 0; i < mods.size(); ++i) {
            if (mods[i].id != ref) continue;
            if (found != kMissing) return kAmbiguous;
            found = i;
        }
        return found;
    }

    void fail(size_t i, std::string reason) {
        if (result.errors[i].empty()) result.errors[i] = std::move(reason);
    }

    void visit(size_t t) {
        if (marks[t] != Mark::None) return;
        marks[t] = Mark::Visiting;
        stack.push_back(t);

        for (const std::string& ref : mods[t].dependencies) {
            const size_t d = resolve(ref);
            if (d == kMissing) {
                fail(t, "requires '" + ref + "', which is not enabled");
                continue;
            }
            if (d == kAmbiguous) {
                fail(t, "requires '" + ref + "', but more than one enabled mod has that id; list it by uid");
                continue;
            }
            if (std::find(result.needs[t].begin(), result.needs[t].end(), d) != result.needs[t].end()) {
                continue;
            }
            result.needs[t].push_back(d);
            if (marks[d] == Mark::Visiting) {
                markCycle(d);
                continue;
            }
            visit(d);
            if (!result.errors[d].empty() || !mods[d].usable) {
                fail(t, "requires '" + nameOf(d) + "', which could not be loaded");
            }
        }

        stack.pop_back();
        marks[t] = Mark::Done;
        result.order.push_back(t);
    }

    // Todo mod do ciclo cai, com o ciclo inteiro no motivo: a -> b -> a.
    void markCycle(size_t d) {
        const auto from = std::find(stack.begin(), stack.end(), d);
        std::string path;
        for (auto it = from; it != stack.end(); ++it) path += nameOf(*it) + " -> ";
        path += nameOf(d);
        for (auto it = from; it != stack.end(); ++it) fail(*it, "dependency cycle: " + path);
    }
};
}

OrderResult sortByDependencies(const std::vector<OrderEntry>& mods) {
    Sorter sorter(mods);
    for (size_t i = 0; i < mods.size(); ++i) sorter.visit(i);
    return std::move(sorter.result);
}

} // namespace bl::mods
