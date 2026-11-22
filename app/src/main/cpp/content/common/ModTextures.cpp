#include "content/common/ModTextures.h"
#include "content/common/ContentAssets.h"
#include "content/common/PngDecode.h"
#include "core/Log.h"
#include "il2cpp/Api.h"

#include <pthread.h>
#include <sys/resource.h>
#include <sys/system_properties.h>
#include <unistd.h>

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdio>
#include <deque>
#include <fstream>
#include <mutex>
#include <thread>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

namespace bl::runtime::content {

namespace {

enum class State { Waiting, Queued, Decoding, Ready, Uploading, Failed, Done };

struct Entry {
    std::string name;
    State state = State::Waiting;
    bool listed = false;
    bool unityTried = false;
    int width = 0, height = 0;
    std::vector<uint8_t> pixels;
    std::string error;
    uint32_t texture = 0;
};

struct Stats {
    int decodedAside = 0, decodedHere = 0, uploadedInBudget = 0, uploadedOnDemand = 0, viaUnity = 0, failed = 0;
    int shared = 0;
    double decodeMs = 0, uploadMs = 0;
    uint64_t bytes = 0;
};

constexpr int kWorkers = 4;
constexpr size_t kReadyLimit = size_t(96) << 20;
constexpr size_t kWarmLimit = 8000;

using Clock = std::chrono::steady_clock;

std::mutex g_mutex;
std::condition_variable g_work, g_decoded;
std::unordered_map<std::string, Entry> g_entries;
std::unordered_map<std::string, std::string> g_files;
std::unordered_map<std::string, uint32_t> g_assets;
std::deque<std::string> g_queue, g_ready;
size_t g_readyBytes = 0;
int g_decoding = 0;
std::atomic<bool> g_started{false};
std::once_flag g_startOnce;
Stats g_stats;
bool g_statsDirty = false;

bool g_warmRecording = true;
std::string g_warmPath, g_warmKey;
std::vector<std::pair<std::string, std::string>> g_warmSeen;
std::unordered_set<std::string> g_warmFiles;
size_t g_warmLoaded = 0;

double msSince(Clock::time_point t) { return std::chrono::duration<double, std::milli>(Clock::now() - t).count(); }

bool checkEnabled() {
    static const bool enabled = [] {
        char value[PROP_VALUE_MAX] = {};
        return __system_property_get("debug.bl.texcheck", value) > 0 && value[0] == '1';
    }();
    return enabled;
}

void finishDecode(Entry& e, const std::string& file, bool ok, int w, int h, std::vector<uint8_t>& px,
                  std::string& error, bool aside) {
    if (ok) {
        e.width = w;
        e.height = h;
        e.pixels = std::move(px);
        e.state = State::Ready;
        if (aside) {
            g_readyBytes += e.pixels.size();
            g_ready.push_back(file);
            e.listed = true;
            ++g_stats.decodedAside;
        } else {
            ++g_stats.decodedHere;
        }
    } else {
        e.error = std::move(error);
        e.state = State::Failed;
    }
    g_statsDirty = true;
}

void worker() {
    pthread_setname_np(pthread_self(), "bl-texdecode");
    setpriority(PRIO_PROCESS, gettid(), 10);
    std::unique_lock<std::mutex> lock(g_mutex);
    for (;;) {
        g_work.wait(lock, [] { return !g_queue.empty() && g_readyBytes < kReadyLimit; });
        std::string file = std::move(g_queue.front());
        g_queue.pop_front();
        auto it = g_entries.find(file);
        if (it == g_entries.end() || it->second.state != State::Queued) continue;
        Entry& e = it->second;
        e.state = State::Decoding;
        ++g_decoding;
        lock.unlock();
        int w = 0, h = 0;
        std::vector<uint8_t> px;
        std::string error;
        const Clock::time_point t = Clock::now();
        const bool ok = decodePngFile(file, true, &w, &h, px, &error);
        const double ms = msSince(t);
        lock.lock();
        --g_decoding;
        g_stats.decodeMs += ms;
        finishDecode(e, file, ok, w, h, px, error, true);
        g_decoded.notify_all();
    }
}

void startWorkers() {
    std::call_once(g_startOnce, [] {
        for (int i = 0; i < kWorkers; ++i) std::thread(worker).detach();
        g_started.store(true, std::memory_order_release);
    });
}

void unlist(Entry& e, const std::string& file) {
    if (!e.listed) return;
    e.listed = false;
    g_readyBytes -= e.pixels.size();
    for (auto it = g_ready.begin(); it != g_ready.end(); ++it) {
        if (*it == file) {
            g_ready.erase(it);
            break;
        }
    }
}

Il2CppObject* makeTexture(const std::string& file, const std::string& name, const std::vector<uint8_t>& px, int w,
                          int h, bool* viaUnity, int* outW, int* outH) {
    *viaUnity = false;
    *outW = w;
    *outH = h;
    if (!px.empty()) {
        if (checkEnabled()) checkTextureAgainstUnity(file, px.data(), w, h, name);
        if (Il2CppObject* texture = gameTextureFromPixels(px.data(), w, h, name)) return texture;
    }
    *viaUnity = true;
    return loadGameTexture(file, nullptr, 0, name, 0, outW, outH);
}

void logStats() {
    static int times = 0, lastAside = -1;
    const Stats& s = g_stats;
    if (s.decodedAside == lastAside) return;
    lastAside = s.decodedAside;
    const int level = times++ == 0 ? ANDROID_LOG_INFO : ANDROID_LOG_DEBUG;
    bl::log::write(level, "mod textures: %d decoded off the game thread and %d on it (%.0f ms); %d uploaded in the frame "
            "budget, %d on demand (%.1f MB, %.0f ms); %d shared by another name, %d through Unity, %d failed",
            s.decodedAside, s.decodedHere, s.decodeMs, s.uploadedInBudget, s.uploadedOnDemand,
            static_cast<double>(s.bytes) / (1024.0 * 1024.0), s.uploadMs, s.shared, s.viaUnity, s.failed);
}

void queueLocked(const std::string& file, const std::string& name) {
    Entry& e = g_entries[file];
    if (e.name.empty()) e.name = name;
    if (e.state != State::Waiting) return;
    e.state = State::Queued;
    g_queue.push_back(file);
}

Il2CppObject* liveTexture(Entry& e) {
    if (e.state != State::Done) return nullptr;
    if (Il2CppObject* texture = il2cpp::api().gchandle_get_target(e.texture)) return texture;
    e.state = State::Waiting;
    return nullptr;
}

Il2CppObject* assetFor(std::unique_lock<std::mutex>& lock, Il2CppObject* texture, const std::string& name) {
    auto& a = il2cpp::api();
    lock.unlock();
    Il2CppObject* asset = wrapTextureAsset(texture, name);
    lock.lock();
    if (!asset) return nullptr;
    auto it = g_assets.find(name);
    if (it != g_assets.end()) {
        if (Il2CppObject* existing = a.gchandle_get_target(it->second)) return existing;
        a.gchandle_free(it->second);
    }
    g_assets[name] = a.gchandle_new(asset, false);
    return asset;
}

}

void prefetchModTexture(const std::string& file, const std::string& assetName) {
    if (file.empty() || assetName.empty() || isVanillaTexturePath(file)) return;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        g_files.emplace(assetName, file);
        queueLocked(file, assetName);
    }
    startWorkers();
    g_work.notify_one();
}

Il2CppObject* modTexture(const std::string& file, const std::string& assetName) {
    auto& a = il2cpp::api();
    std::unique_lock<std::mutex> lock(g_mutex);
    g_files[assetName] = file;
    auto known = g_assets.find(assetName);
    if (known != g_assets.end()) {
        if (Il2CppObject* asset = a.gchandle_get_target(known->second)) return asset;
        a.gchandle_free(known->second);
        g_assets.erase(known);
    }
    Entry& e = g_entries[file];
    if (e.name.empty()) e.name = assetName;
    if (g_warmRecording && g_warmFiles.insert(file).second && g_warmSeen.size() < kWarmLimit) {
        g_warmSeen.emplace_back(file, assetName);
    }
    Il2CppObject* texture = liveTexture(e);
    if (texture) {
        if (e.name != assetName) ++g_stats.shared;
        return assetFor(lock, texture, assetName);
    }
    if (e.state == State::Failed && e.unityTried) return nullptr;
    if (e.state == State::Waiting || e.state == State::Queued || e.state == State::Uploading) {
        e.state = State::Decoding;
        ++g_decoding;
        lock.unlock();
        int w = 0, h = 0;
        std::vector<uint8_t> px;
        std::string error;
        const Clock::time_point t = Clock::now();
        const bool ok = decodePngFile(file, true, &w, &h, px, &error);
        const double ms = msSince(t);
        lock.lock();
        --g_decoding;
        g_stats.decodeMs += ms;
        finishDecode(e, file, ok, w, h, px, error, false);
        g_decoded.notify_all();
    }
    while (e.state == State::Decoding) g_decoded.wait(lock);
    if ((texture = liveTexture(e))) return assetFor(lock, texture, assetName);
    std::vector<uint8_t> px;
    if (e.state == State::Ready) {
        unlist(e, file);
        px = std::move(e.pixels);
    } else if (e.state == State::Failed && !e.error.empty()) {
        BL_DEBUG("mod textures: %s: %s; trying Unity", assetName.c_str(), e.error.c_str());
        e.error.clear();
    }
    e.state = State::Uploading;
    const int w = e.width, h = e.height;
    lock.unlock();
    g_work.notify_all();
    bool viaUnity = false;
    int outW = w, outH = h;
    const Clock::time_point t = Clock::now();
    texture = makeTexture(file, assetName, px, w, h, &viaUnity, &outW, &outH);
    const double ms = msSince(t);
    lock.lock();
    g_stats.uploadMs += ms;
    g_statsDirty = true;
    if (!texture) {
        e.state = State::Failed;
        e.unityTried = true;
        ++g_stats.failed;
        return nullptr;
    }
    e.texture = a.gchandle_new(texture, false);
    e.width = outW;
    e.height = outH;
    e.state = State::Done;
    ++g_stats.uploadedOnDemand;
    if (viaUnity) ++g_stats.viaUnity;
    g_stats.bytes += px.size();
    return assetFor(lock, texture, assetName);
}

Il2CppObject* modTextureAsset(const std::string& file, const unsigned char* data, size_t size,
                              const std::string& assetName, int* width, int* height) {
    if (data || file.empty() || isVanillaTexturePath(file)) return loadTextureAsset(file, data, size, assetName, width, height);
    Il2CppObject* asset = modTexture(file, assetName);
    if (!asset) return nullptr;
    std::lock_guard<std::mutex> lock(g_mutex);
    const Entry& e = g_entries[file];
    if (width) *width = e.width;
    if (height) *height = e.height;
    return asset;
}

Il2CppObject* loadedModTexture(const std::string& assetName) {
    auto& a = il2cpp::api();
    std::unique_lock<std::mutex> lock(g_mutex);
    auto known = g_assets.find(assetName);
    if (known != g_assets.end()) {
        if (Il2CppObject* asset = a.gchandle_get_target(known->second)) return asset;
    }
    auto file = g_files.find(assetName);
    if (file == g_files.end()) return nullptr;
    auto it = g_entries.find(file->second);
    if (it == g_entries.end()) return nullptr;
    Il2CppObject* texture = liveTexture(it->second);
    return texture ? assetFor(lock, texture, assetName) : nullptr;
}

void pumpModTextures(int budgetMicros) {
    if (!g_started.load(std::memory_order_acquire)) return;
    auto& a = il2cpp::api();
    const Clock::time_point start = Clock::now();
    for (;;) {
        std::string file, name;
        int w = 0, h = 0;
        std::vector<uint8_t> px;
        {
            std::lock_guard<std::mutex> lock(g_mutex);
            if (g_ready.empty()) {
                if (g_statsDirty && g_queue.empty() && g_decoding == 0) {
                    g_statsDirty = false;
                    logStats();
                }
                return;
            }
            file = std::move(g_ready.front());
            g_ready.pop_front();
            Entry& e = g_entries[file];
            if (!e.listed || e.state != State::Ready) continue;
            e.listed = false;
            g_readyBytes -= e.pixels.size();
            px = std::move(e.pixels);
            e.state = State::Uploading;
            name = e.name;
            w = e.width;
            h = e.height;
        }
        g_work.notify_all();
        bool viaUnity = false;
        int outW = w, outH = h;
        const Clock::time_point t = Clock::now();
        Il2CppObject* texture = makeTexture(file, name, px, w, h, &viaUnity, &outW, &outH);
        const double ms = msSince(t);
        {
            std::lock_guard<std::mutex> lock(g_mutex);
            Entry& e = g_entries[file];
            g_stats.uploadMs += ms;
            g_statsDirty = true;
            if (e.state == State::Uploading) {
                if (texture) {
                    e.texture = a.gchandle_new(texture, false);
                    e.width = outW;
                    e.height = outH;
                    e.state = State::Done;
                    ++g_stats.uploadedInBudget;
                    if (viaUnity) ++g_stats.viaUnity;
                    g_stats.bytes += px.size();
                } else {
                    e.state = State::Failed;
                    e.unityTried = true;
                    ++g_stats.failed;
                }
            }
        }
        if (msSince(start) * 1000.0 >= budgetMicros) return;
    }
}

void prefetchWarmTextures(const std::string& path, const std::string& key) {
    std::vector<std::pair<std::string, std::string>> list;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        g_warmPath = path;
        g_warmKey = key;
    }
    std::ifstream in(path);
    std::string line;
    if (!in || !std::getline(in, line) || line != "key\t" + key) return;
    while (std::getline(in, line) && list.size() < kWarmLimit) {
        const size_t tab = line.find('\t');
        if (tab == std::string::npos || tab == 0 || tab + 1 >= line.size()) continue;
        list.emplace_back(line.substr(0, tab), line.substr(tab + 1));
    }
    if (list.empty()) return;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        for (const auto& [file, name] : list) {
            if (isVanillaTexturePath(file)) continue;
            g_files.emplace(name, file);
            queueLocked(file, name);
        }
        g_warmLoaded = list.size();
    }
    startWorkers();
    g_work.notify_all();
    BL_DEBUG("mod textures: %zu texture(s) the last start asked for are decoding ahead", list.size());
}

void finishTextureWarmList() {
    std::vector<std::pair<std::string, std::string>> seen;
    std::string path, key;
    size_t loaded = 0;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_warmRecording) return;
        g_warmRecording = false;
        seen.swap(g_warmSeen);
        g_warmFiles.clear();
        path = g_warmPath;
        key = g_warmKey;
        loaded = g_warmLoaded;
    }
    if (path.empty() || key.empty() || (seen.empty() && loaded == 0)) return;
    const std::string tmp = path + ".tmp";
    {
        std::ofstream out(tmp, std::ios::trunc);
        if (!out) return;
        out << "key\t" << key << '\n';
        for (const auto& [file, name] : seen) {
            if (file.find('\n') != std::string::npos || name.find('\n') != std::string::npos) continue;
            if (file.find('\t') != std::string::npos) continue;
            out << file << '\t' << name << '\n';
        }
        if (!out) {
            std::remove(tmp.c_str());
            return;
        }
    }
    if (std::rename(tmp.c_str(), path.c_str()) != 0) std::remove(tmp.c_str());
    else BL_DEBUG("mod textures: %zu texture(s) asked for while loading, kept for the next start", seen.size());
}

}
