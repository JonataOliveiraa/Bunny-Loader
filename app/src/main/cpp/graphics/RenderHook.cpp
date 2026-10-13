#include "graphics/RenderHook.h"
#include "graphics/GlHooks.h"
#include "graphics/PocTwirl.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/GuardedCall.h"
#include "il2cpp/Resolver.h"

#include <chrono>
#include <cstring>
#include <initializer_list>
#include <mutex>

namespace bl::graphics {

namespace {

using FireFn = void (*)(Il2CppObject* camera, const MethodInfo*);

FireFn g_origFire = nullptr;

FieldInfo* g_runnerInstance = nullptr;   // XNAUnityRunner._instance
int32_t g_worldSettingsOff = -1, g_uiSettingsOff = -1, g_worldCameraOff = -1, g_uiCameraOff = -1;
const MethodInfo* g_getTargetTexture = nullptr;
const MethodInfo* g_getClearFlags = nullptr;
const MethodInfo* g_getDepth = nullptr;
bool g_camerasLogged = false;

std::mutex g_pocMutex;
PocParams g_poc;

std::mutex g_statsMutex;
RenderStats g_stats;

// O marcador (ver kMarkerWidth): objetos da Unity seguros por gchandle (o
// coletor do IL2CPP nao move objetos: os ponteiros ficam validos).
struct Marker {
    bool tried = false, ready = false;
    uint32_t handles[3] = {0, 0, 0};
    Il2CppObject* target = nullptr;
    Il2CppObject* source = nullptr;
    Il2CppObject* material = nullptr;
    const MethodInfo* blit = nullptr;   // Graphics.Internal_BlitMaterial5
};
Marker g_marker;

Il2CppObject* readRef(Il2CppObject* obj, int32_t offset) {
    if (!obj || offset < 0) return nullptr;
    return *reinterpret_cast<Il2CppObject**>(reinterpret_cast<char*>(obj) + offset);
}

Il2CppObject* runner() {
    Il2CppObject* r = nullptr;
    il2cpp::api().field_static_get_value(g_runnerInstance, &r);
    return r;
}

Il2CppObject* invoke(const MethodInfo* method, Il2CppObject* self) {
    if (!method || !self) return nullptr;
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(method, self, nullptr, &exc);
    return exc ? nullptr : r;
}

template <typename T>
T unbox(Il2CppObject* boxed) {
    return boxed ? *reinterpret_cast<T*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject)) : T{};
}

void logCamera(const char* label, Il2CppObject* camera) {
    BL_INFO("efeitos de tela: camera %s %p: alvo %p, clearFlags %d, depth %.1f", label, camera,
            invoke(g_getTargetTexture, camera), unbox<int32_t>(invoke(g_getClearFlags, camera)),
            unbox<float>(invoke(g_getDepth, camera)));
}

// O construtor pelo tipo de cada parametro ("System.Int32", "UnityEngine.Material"):
// pela contagem so, o Material(Material) viraria o Material(Shader), o primeiro
// de um parametro, e o jogo congelava.
const MethodInfo* findCtor(Il2CppClass* cls, std::initializer_list<const char*> types) {
    auto& a = il2cpp::api();
    void* iter = nullptr;
    while (const MethodInfo* m = a.class_get_methods(cls, &iter)) {
        if (std::strcmp(a.method_get_name(m), ".ctor") != 0) continue;
        if (a.method_get_param_count(m) != types.size()) continue;
        bool same = true;
        uint32_t i = 0;
        for (const char* type : types) {
            char* name = a.type_get_name(a.method_get_param(m, i++));
            same = same && name && std::strcmp(name, type) == 0;
            a.il2cpp_free(name);
        }
        if (same) return m;
    }
    return nullptr;
}

Il2CppObject* construct(Il2CppClass* cls, std::initializer_list<const char*> types, void** args) {
    auto& a = il2cpp::api();
    const MethodInfo* ctor = cls ? findCtor(cls, types) : nullptr;
    if (!ctor) return nullptr;
    Il2CppObject* obj = a.object_new(cls);
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(ctor, obj, args, &exc);
    return exc ? nullptr : obj;
}

// Uma RenderTexture 17x3, a textura branca da Unity como origem e uma COPIA
// de um material do jogo (o Blit troca a _MainTex do material; no original,
// estragaria os sprites dele). Na thread do jogo, FORA do desenho das cameras:
// montado dentro do FireOnPostRender, o jogo congelou (render multithread).
void setupMarker() {
    g_marker.tried = true;
    auto& a = il2cpp::api();
    Il2CppClass* renderTexture = il2cpp::findClass({"UnityEngine", "RenderTexture", {}});
    Il2CppClass* texture2D = il2cpp::findClass({"UnityEngine", "Texture2D", {}});
    Il2CppClass* material = il2cpp::findClass({"UnityEngine", "Material", {}});
    Il2CppClass* graphics = il2cpp::findClass({"UnityEngine", "Graphics", {}});
    Il2CppClass* device = il2cpp::findClass({"Microsoft.Xna.Framework.Graphics", "GraphicsDevice", {}});
    if (!renderTexture || !texture2D || !material || !graphics || !device) return;

    BL_INFO("efeitos de tela: marcador: RenderTexture");
    int32_t w = kMarkerWidth, h = kMarkerHeight, depth = 0;
    void* rtArgs[3] = {&w, &h, &depth};
    Il2CppObject* target = construct(renderTexture, {"System.Int32", "System.Int32", "System.Int32"}, rtArgs);
    BL_INFO("efeitos de tela: marcador: textura branca");

    Il2CppObject* exc = nullptr;
    const MethodInfo* white = a.class_get_method_from_name(texture2D, "get_whiteTexture", 0);
    Il2CppObject* source = white ? a.runtime_invoke(white, nullptr, nullptr, &exc) : nullptr;

    BL_INFO("efeitos de tela: marcador: material do jogo");
    Il2CppObject* instance = nullptr;
    FieldInfo* instanceField = il2cpp::findField(device, "Instance");
    if (instanceField) a.field_static_get_value(instanceField, &instance);
    const MethodInfo* getMaterial = a.class_get_method_from_name(device, "GetMaterial", 1);
    int32_t offset = 0;
    void* matArgs[1] = {&offset};
    exc = nullptr;
    Il2CppObject* gameMaterial = instance && getMaterial ? a.runtime_invoke(getMaterial, instance, matArgs, &exc) : nullptr;
    BL_INFO("efeitos de tela: marcador: copia do material");
    void* copyArgs[1] = {gameMaterial};
    Il2CppObject* copy = gameMaterial && !exc ? construct(material, {"UnityEngine.Material"}, copyArgs) : nullptr;

    g_marker.blit = a.class_get_method_from_name(graphics, "Internal_BlitMaterial5", 5);
    if (!target || !source || !copy || !g_marker.blit) {
        BL_ERROR("efeitos de tela: marcador nao montado (alvo %p, origem %p, material %p, blit %p); "
                 "o efeito vai no fim do quadro", target, source, copy, g_marker.blit);
        return;
    }
    g_marker.handles[0] = a.gchandle_new(target, false);
    g_marker.handles[1] = a.gchandle_new(source, false);
    g_marker.handles[2] = a.gchandle_new(copy, false);
    g_marker.target = target;
    g_marker.source = source;
    g_marker.material = copy;
    g_marker.ready = true;
    BL_INFO("efeitos de tela: marcador montado");
}

// Chamada direta (o runtime_invoke custava ~0,1 ms por quadro no celular),
// guardada: uma excecao do Blit nao pode atravessar a libbunny.
bool issueMarker() {
    using BlitFn = void (*)(Il2CppObject*, Il2CppObject*, Il2CppObject*, int32_t, bool, const MethodInfo*);
    const auto blit = reinterpret_cast<BlitFn>(il2cpp::methodPointer(g_marker.blit));
    // setRT = true: com false a Unity nao liga o destino e desenha no alvo que
    // estiver ativo (o do jogo), sem trocar framebuffer nem viewport.
    auto call = [blit] {
        blit(g_marker.source, g_marker.target, g_marker.material, 0, true, g_marker.blit);
    };
    if (il2cpp::guarded(call)) return true;
    BL_ERROR("efeitos de tela: o Blit do marcador lancou; marcador desligado");
    g_marker.ready = false;
    return false;
}

void hkFireOnPostRender(Il2CppObject* camera, const MethodInfo* method) {
    g_origFire(camera, method);

    Il2CppObject* r = runner();
    Il2CppObject* world = readRef(readRef(r, g_worldSettingsOff), g_worldCameraOff);
    Il2CppObject* ui = readRef(readRef(r, g_uiSettingsOff), g_uiCameraOff);
    if (!g_camerasLogged && world && ui) {
        g_camerasLogged = true;
        logCamera("do mundo", world);
        logCamera("da interface", ui);
    }

    bool issued = false;
    int64_t markerNs = 0;
    if (camera == world && g_marker.ready && pocParams().enabled) {
        const auto started = std::chrono::steady_clock::now();
        issued = issueMarker();
        markerNs = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::steady_clock::now() - started).count();
    }

    std::lock_guard<std::mutex> lock(g_statsMutex);
    if (camera == world) g_stats.worldPostRender++;
    else if (camera == ui) g_stats.uiPostRender++;
    else g_stats.otherPostRender++;
    if (issued) g_stats.markersIssued++;
    g_stats.markerCpuNs += markerNs;
    g_stats.markerReady = g_marker.ready;
}

bool resolveCameras() {
    Il2CppClass* runnerClass = il2cpp::findClass({"", "XNAUnityRunner", {}});
    Il2CppClass* worldClass = il2cpp::findClass({"", "XNAWorldCameraSettings", {}});
    Il2CppClass* uiClass = il2cpp::findClass({"", "XNAUICameraSettings", {}});
    if (!runnerClass || !worldClass || !uiClass) return false;

    g_runnerInstance = il2cpp::findField(runnerClass, "_instance");
    g_worldSettingsOff = il2cpp::fieldOffset(runnerClass, "WorldCameraSettings");
    g_uiSettingsOff = il2cpp::fieldOffset(runnerClass, "UICameraSettings");
    g_worldCameraOff = il2cpp::fieldOffset(worldClass, "Camera");
    g_uiCameraOff = il2cpp::fieldOffset(uiClass, "Camera");
    return g_runnerInstance && g_worldSettingsOff >= 0 && g_uiSettingsOff >= 0
        && g_worldCameraOff >= 0 && g_uiCameraOff >= 0;
}

} // namespace

void installRenderHook() {
    auto& a = il2cpp::api();
    if (!resolveCameras()) {
        BL_ERROR("efeitos de tela: cameras do XNAUnityRunner nao encontradas");
        return;
    }

    Il2CppClass* camera = il2cpp::findClass({"UnityEngine", "Camera", {}});
    const MethodInfo* fire = camera ? a.class_get_method_from_name(camera, "FireOnPostRender", 1) : nullptr;
    if (camera) {
        g_getTargetTexture = a.class_get_method_from_name(camera, "get_targetTexture", 0);
        g_getClearFlags = a.class_get_method_from_name(camera, "get_clearFlags", 0);
        g_getDepth = a.class_get_method_from_name(camera, "get_depth", 0);
    }
    const bool hooked = fire && hook::install(fire, hkFireOnPostRender, &g_origFire);
    if (!hooked) BL_ERROR("efeitos de tela: Camera.FireOnPostRender nao hookado");
    {
        std::lock_guard<std::mutex> lock(g_statsMutex);
        g_stats.hookInstalled = hooked;
    }

    installGlHooks();
}

void noteMarkerHit() {
    std::lock_guard<std::mutex> lock(g_statsMutex);
    g_stats.markerHits++;
}

void noteSwapDraw() {
    std::lock_guard<std::mutex> lock(g_statsMutex);
    g_stats.swapDraws++;
}

void setPoc(const PocParams& params) {
    if (params.enabled && !g_marker.tried) setupMarker();
    std::lock_guard<std::mutex> lock(g_pocMutex);
    g_poc = params;
}

PocParams pocParams() {
    std::lock_guard<std::mutex> lock(g_pocMutex);
    return g_poc;
}

RenderStats renderStats() {
    RenderStats copy;
    {
        std::lock_guard<std::mutex> lock(g_statsMutex);
        copy = g_stats;
    }
    PocTwirl::FillStats(copy);
    fillGlHookStats(copy);
    return copy;
}

} // namespace bl::graphics
