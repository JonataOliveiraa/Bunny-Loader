#include "mods/ModLoader.h"
#include "mods/LoadOrder.h"
#include "mods/ManifestText.h"
#include "core/Log.h"
#include "content/textures/FontPacks.h"
#include "content/textures/TexturePacks.h"
#include "script/dotnet/DotnetHost.h"
#include <algorithm>
#include <cstdio>
#include <map>

#include <dirent.h>
#include <sys/stat.h>

namespace bl::mods {

namespace {
std::vector<LoadedMod>& registry() {
    static std::vector<LoadedMod> list;
    return list;
}

std::map<std::string, std::string>& dirsById() {
    static std::map<std::string, std::string> m;
    return m;
}

bool fileExists(const std::string& path) {
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return false;
    std::fclose(f);
    return true;
}

void readManifest(LoadedMod* mod) {
    FILE* f = nullptr;
    for (const char* name : {"manifest.json", "mod.json"}) {
        const std::string path = mod->dir + "/" + name;
        struct stat st{};
        if (stat(path.c_str(), &st) == 0 && S_ISREG(st.st_mode)) {
            f = std::fopen(path.c_str(), "rb");
            break;
        }
    }
    std::string txt;
    if (f) {
        char buf[1024];
        size_t n;
        while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0) txt.append(buf, n);
        std::fclose(f);
    }
    mod->internalName = manifestString(txt, "id");
    mod->displayName = manifestString(txt, "name");
    mod->version = manifestString(txt, "version");
    mod->dependencies = manifestStrings(txt, "dependencies");
    if (mod->displayName.empty()) mod->displayName = mod->id;
}
}

void loadAll(const std::string& modsDir, const std::vector<ModSpec>& enabled) {
    runtime::configureTexturePacks(modsDir, enabled);
    runtime::configureFontPacks(modsDir, enabled);
    registry().clear();
    dirsById().clear();
    BL_INFO("carregando mods de %s (%zu habilitados)", modsDir.c_str(), enabled.size());

    std::vector<LoadedMod> listed;
    for (const auto& spec : enabled) {
        if (spec.entry == "@texture" || spec.entry == "@font") continue;
        const std::string& id = spec.id;
        LoadedMod mod;
        mod.id = id;
        mod.dir = modsDir + "/" + id;
        readManifest(&mod);

        const std::string& name = spec.entry;
        const std::string path = mod.dir + "/" + name;
        if (name.empty() || !fileExists(path)) {
            BL_ERROR("mod %s: entry file %s not found", id.c_str(), name.c_str());
            mod.enabled = false;
        } else {
            mod.entry = path.substr(mod.dir.size() + 1);
            dirsById()[id] = path.substr(0, path.rfind('/'));
        }
        listed.push_back(std::move(mod));
    }

    std::vector<OrderEntry> entries;
    entries.reserve(listed.size());
    for (const LoadedMod& m : listed) entries.push_back({m.id, m.internalName, m.dependencies, m.enabled});
    const OrderResult sorted = sortByDependencies(entries);
    std::vector<size_t> position(listed.size());
    for (size_t k = 0; k < sorted.order.size(); ++k) {
        const size_t i = sorted.order[k];
        position[i] = k;
        LoadedMod& mod = listed[i];
        if (mod.enabled && !sorted.errors[i].empty()) {
            BL_ERROR("mod %s (%s) disabled: %s", mod.displayName.c_str(), mod.id.c_str(), sorted.errors[i].c_str());
            mod.enabled = false;
        }
        registry().push_back(std::move(mod));
    }
    std::vector<std::vector<size_t>> needs(registry().size());
    for (size_t i = 0; i < listed.size(); ++i) {
        for (size_t d : sorted.needs[i]) needs[position[i]].push_back(position[d]);
    }

    for (size_t i = 0; i < registry().size(); ++i) {
        if (!registry()[i].enabled) continue;
        const auto broken = std::find_if(needs[i].begin(), needs[i].end(),
            [](size_t d) { return !registry()[d].loaded; });
        if (broken != needs[i].end()) {
            const LoadedMod& dep = registry()[*broken];
            BL_ERROR("mod %s (%s) disabled: requires '%s', which failed to load",
                registry()[i].displayName.c_str(), registry()[i].id.c_str(), dep.displayName.c_str());
            registry()[i].enabled = false;
            continue;
        }
        const std::string id = registry()[i].id;
        const std::string path = registry()[i].dir + "/" + registry()[i].entry;
        const std::string dir = dirsById()[id];
        if (!dotnet::isCSharpEntry(path)) {
            BL_WARN("mod %s (%s) skipped: JS mods are no longer supported", registry()[i].displayName.c_str(), id.c_str());
            registry()[i].enabled = false;
            continue;
        }
        const bool ok = dotnet::host().loadMod(id, path);
        if (!ok) {
            BL_ERROR("mod %s falhou ao carregar (%s)", id.c_str(), path.c_str());
            registry()[i].enabled = false;
        } else {
            registry()[i].loaded = true;
        }
    }
}

std::string rootOf(const std::string& id) {
    for (const LoadedMod& m : registry()) if (m.id == id) return m.dir;
    return {};
}

std::string displayName(const std::string& id) {
    const LoadedMod* m = find(id);
    return m ? m->displayName : id;
}

const LoadedMod* find(const std::string& id) {
    for (const LoadedMod& m : registry()) if (m.id == id) return &m;
    return nullptr;
}

size_t loadedCount() { return registry().size(); }

LoadedMod* get(uint16_t index) {
    if (index >= registry().size()) return nullptr;
    return &registry()[index];
}

}
