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
#include <deque>
#include <mutex>
#include <thread>
#include <unordered_map>
#include <vector>

namespace bl::runtime::content {

namespace {

enum class State { Waiting, Queued, Decoding, Ready, Uploading, Failed, Done };

struct Entry {
    std::string file;
    State state = State::Waiting;
    bool listed = false;
    int width = 0, height = 0;
    std::vector<uint8_t> pixels;
    std::string error;
    uint32_t handle = 0;
};

struct Stats {
    int decodedAside = 0, decodedHere = 0, uploadedInBudget = 0, uploadedOnDemand = 0, viaUnity = 0, failed = 0;
    double decodeMs = 0, uploadMs = 0;
    uint64_t bytes = 0;
};

constexpr int kWorkers = 2;
constexpr size_t kReadyLimit = size_t(96) << 20;

using Clock = std::chrono::steady_clock;

std::mutex g_mutex;
std::condition_variable g_work, g_decoded;
std::unordered_map<std::string, Entry> g_entries;
std::deque<std::string> g_queue, g_ready;
size_t g_readyBytes = 0;
int g_decoding = 0;
std::atomic<bool> g_started{false};
std::once_flag g_startOnce;
Stats g_stats;
bool g_statsDirty = false;

double msSince(Clock::time_point t) { return std::chrono::duration<double, std::milli>(Clock::now() - t).count(); }

bool checkEnabled() {
    static const bool enabled = [] {
        char value[PROP_VALUE_MAX] = {};
        return __system_property_get("debug.bl.texcheck", value) > 0 && value[0] == '1';
    }();
    return enabled;
}

void finishDecode(Entry& e, const std::string& name, bool ok, int w, int h, std::vector<uint8_t>& px,
                  std::string& error, bool aside) {
    if (ok) {
        e.width = w;
        e.height = h;
        e.pixels = std::move(px);
        e.state = State::Ready;
        if (aside) {
            g_readyBytes += e.pixels.size();
            g_ready.push_back(name);
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
        std::string name = std::move(g_queue.front());
        g_queue.pop_front();
        auto it = g_entries.find(name);
        if (it == g_entries.end() || it->second.state != State::Queued) continue;
        Entry& e = it->second;
        e.state = State::Decoding;
        ++g_decoding;
        const std::string file = e.file;
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
        finishDecode(e, name, ok, w, h, px, error, true);
        g_decoded.notify_all();
    }
}

void startWorkers() {
    std::call_once(g_startOnce, [] {
        for (int i = 0; i < kWorkers; ++i) std::thread(worker).detach();
        g_started.store(true, std::memory_order_release);
    });
}

void unlist(Entry& e, const std::string& name) {
    if (!e.listed) return;
    e.listed = false;
    g_readyBytes -= e.pixels.size();
    for (auto it = g_ready.begin(); it != g_ready.end(); ++it) {
        if (*it == name) {
            g_ready.erase(it);
            break;
        }
    }
}

Il2CppObject* upload(const std::string& name, const std::string& file, int w, int h, const std::vector<uint8_t>& px,
                     bool* viaUnity) {
    *viaUnity = false;
    if (!px.empty()) {
        if (checkEnabled()) checkTextureAgainstUnity(file, px.data(), w, h, name);
        if (Il2CppObject* asset = textureAssetFromPixels(px.data(), w, h, name)) return asset;
    }
    *viaUnity = true;
    int ow = 0, oh = 0;
    return loadTextureAsset(file, nullptr, 0, name, &ow, &oh);
}

void logStats() {
    static int times = 0, lastAside = -1;
    const Stats& s = g_stats;
    if (s.decodedAside == lastAside) return;
    lastAside = s.decodedAside;
    const int level = times++ == 0 ? ANDROID_LOG_INFO : ANDROID_LOG_DEBUG;
    bl::log::write(level,"mod textures: %d decoded off the game thread and %d on it (%.0f ms); %d uploaded in the frame budget, "
            "%d on demand (%.1f MB, %.0f ms); %d through Unity, %d failed",
            s.decodedAside, s.decodedHere, s.decodeMs, s.uploadedInBudget, s.uploadedOnDemand,
            static_cast<double>(s.bytes) / (1024.0 * 1024.0), s.uploadMs, s.viaUnity, s.failed);
}

}

void prefetchModTexture(const std::string& file, const std::string& assetName) {
    if (file.empty() || assetName.empty() || isVanillaTexturePath(file)) return;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        Entry& e = g_entries[assetName];
        if (e.state != State::Waiting) return;
        e.file = file;
        e.state = State::Queued;
        g_queue.push_back(assetName);
    }
    startWorkers();
    g_work.notify_one();
}

Il2CppObject* modTexture(const std::string& file, const std::string& assetName) {
    auto& a = il2cpp::api();
    std::unique_lock<std::mutex> lock(g_mutex);
    Entry& e = g_entries[assetName];
    if (e.file.empty()) e.file = file;
    if (e.state == State::Done) {
        if (Il2CppObject* asset = a.gchandle_get_target(e.handle)) return asset;
        e.state = State::Waiting;
    }
    if (e.state == State::Waiting || e.state == State::Queued || e.state == State::Uploading) {
        e.state = State::Decoding;
        ++g_decoding;
        const std::string path = e.file;
        lock.unlock();
        int w = 0, h = 0;
        std::vector<uint8_t> px;
        std::string error;
        const Clock::time_point t = Clock::now();
        const bool ok = !isVanillaTexturePath(path) && decodePngFile(path, true, &w, &h, px, &error);
        const double ms = msSince(t);
        lock.lock();
        --g_decoding;
        g_stats.decodeMs += ms;
        finishDecode(e, assetName, ok, w, h, px, error, false);
        g_decoded.notify_all();
    }
    while (e.state == State::Decoding) g_decoded.wait(lock);
    std::vector<uint8_t> px;
    if (e.state == State::Ready) {
        unlist(e, assetName);
        px = std::move(e.pixels);
    } else if (e.state == State::Failed && !e.error.empty()) {
        BL_DEBUG("mod textures: %s: %s; trying Unity", assetName.c_str(), e.error.c_str());
        e.error.clear();
    }
    e.state = State::Uploading;
    const std::string path = e.file;
    const int w = e.width, h = e.height;
    lock.unlock();
    g_work.notify_all();
    bool viaUnity = false;
    const Clock::time_point t = Clock::now();
    Il2CppObject* asset = upload(assetName, path, w, h, px, &viaUnity);
    const double ms = msSince(t);
    lock.lock();
    g_stats.uploadMs += ms;
    g_statsDirty = true;
    if (!asset) {
        e.state = State::Failed;
        ++g_stats.failed;
        return nullptr;
    }
    e.handle = a.gchandle_new(asset, false);
    e.state = State::Done;
    ++g_stats.uploadedOnDemand;
    if (viaUnity) ++g_stats.viaUnity;
    g_stats.bytes += px.size();
    return asset;
}

Il2CppObject* loadedModTexture(const std::string& assetName) {
    std::lock_guard<std::mutex> lock(g_mutex);
    auto it = g_entries.find(assetName);
    if (it == g_entries.end() || it->second.state != State::Done) return nullptr;
    return il2cpp::api().gchandle_get_target(it->second.handle);
}

void pumpModTextures(int budgetMicros) {
    if (!g_started.load(std::memory_order_acquire)) return;
    auto& a = il2cpp::api();
    const Clock::time_point start = Clock::now();
    for (;;) {
        std::string name, file;
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
            name = std::move(g_ready.front());
            g_ready.pop_front();
            Entry& e = g_entries[name];
            if (!e.listed || e.state != State::Ready) continue;
            e.listed = false;
            g_readyBytes -= e.pixels.size();
            px = std::move(e.pixels);
            e.state = State::Uploading;
            file = e.file;
            w = e.width;
            h = e.height;
        }
        g_work.notify_all();
        bool viaUnity = false;
        const Clock::time_point t = Clock::now();
        Il2CppObject* asset = upload(name, file, w, h, px, &viaUnity);
        const double ms = msSince(t);
        {
            std::lock_guard<std::mutex> lock(g_mutex);
            Entry& e = g_entries[name];
            g_stats.uploadMs += ms;
            g_statsDirty = true;
            if (e.state == State::Uploading) {
                if (asset) {
                    e.handle = a.gchandle_new(asset, false);
                    e.state = State::Done;
                    ++g_stats.uploadedInBudget;
                    if (viaUnity) ++g_stats.viaUnity;
                    g_stats.bytes += px.size();
                } else {
                    e.state = State::Failed;
                    ++g_stats.failed;
                }
            }
        }
        if (msSince(start) * 1000.0 >= budgetMicros) return;
    }
}

}
