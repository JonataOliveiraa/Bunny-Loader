#include "content/common/ModNet.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <atomic>
#include <cstring>
#include <mutex>

namespace bl::runtime {

namespace {

constexpr int32_t kRequestWorld = 6;
constexpr int32_t kClientUuid = 68;
constexpr const char* kHelloPrefix = "bunnyloader-cs:";

struct Refs {
    bool ok = false;
    Il2CppClass* packet = nullptr;
    Il2CppClass* byteCls = nullptr;
    const MethodInfo* packetCtor = nullptr;
    const MethodInfo* packetWriter = nullptr;
    const MethodInfo* packetShrink = nullptr;
    const MethodInfo* sendToServer = nullptr;
    const MethodInfo* sendToClient = nullptr;
    const MethodInfo* broadcast = nullptr;
    const MethodInfo* read = nullptr;
    const MethodInfo* sendData = nullptr;
    const MethodInfo* processData = nullptr;
    const MethodInfo* bootPlayer = nullptr;
    const MethodInfo* fromLiteral = nullptr;
    FieldInfo* managerInstance = nullptr;
    const MethodInfo* managerGetter = nullptr;
    FieldInfo* netMode = nullptr;
    FieldInfo* clientUuid = nullptr;
    int32_t whoAmI = -1;
};

Refs g_refs;
std::atomic<ManagedPacketHandler> g_handler{nullptr};
std::mutex g_helloMx;
std::string g_hello;
content::CultureNames g_refusal;

const MethodInfo* sig(Il2CppClass* cls, const char* text) {
    return cls ? il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(text)) : nullptr;
}

bool resolve() {
    static bool tried = false;
    if (tried) return g_refs.ok;
    tried = true;
    auto& a = il2cpp::api();
    Refs& r = g_refs;
    r.packet = il2cpp::findClass({"Terraria.Net", "NetPacket", {}});
    Il2CppClass* manager = il2cpp::findClass({"Terraria.Net", "NetManager", {}});
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    Il2CppClass* message = il2cpp::findClass({"Terraria", "NetMessage", {}});
    Il2CppClass* buffer = il2cpp::findClass({"Terraria", "MessageBuffer", {}});
    Il2CppClass* networkText = il2cpp::findClass({"Terraria.Localization", "NetworkText", {}});
    r.byteCls = il2cpp::findClass({"System", "Byte", {}});
    r.packetCtor = sig(r.packet, "void .ctor(ushort id, int size)");
    r.packetWriter = r.packet ? a.class_get_method_from_name(r.packet, "get_Writer", 0) : nullptr;
    r.packetShrink = sig(r.packet, "void ShrinkToFit()");
    r.sendToServer = sig(manager, "void SendToServer(NetPacket packet)");
    r.sendToClient = sig(manager, "void SendToClient(NetPacket packet, int clientId)");
    r.broadcast = sig(manager, "void Broadcast(NetPacket packet, int ignoreClient)");
    r.read = sig(manager, "void Read(BinaryReader reader, int userId, int readLength)");
    r.sendData = sig(message, "void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)");
    r.processData = sig(buffer, "void ProcessData(byte[] messageData, int length, out int messageType)");
    r.bootPlayer = sig(message, "void BootPlayer(int plr, NetworkText msg)");
    r.fromLiteral = sig(networkText, "NetworkText FromLiteral(string text)");
    r.managerInstance = manager ? il2cpp::findField(manager, "Instance") : nullptr;
    r.managerGetter = manager && !r.managerInstance ? a.class_get_method_from_name(manager, "get_Instance", 0) : nullptr;
    r.netMode = main ? il2cpp::findField(main, "netMode") : nullptr;
    r.clientUuid = main ? il2cpp::findField(main, "clientUUID") : nullptr;
    r.whoAmI = buffer ? il2cpp::fieldOffset(buffer, "whoAmI") : -1;
    r.ok = r.packet && r.byteCls && r.packetCtor && r.packetWriter && r.packetShrink && r.sendToServer && r.sendToClient &&
           r.broadcast && r.read && (r.managerInstance || r.managerGetter) && r.netMode;
    if (!r.ok) {
        BL_ERROR("C# net: game refs missing (NetPacket=%p ctor=%p Writer=%p Read=%p SendToServer=%p netMode=%p)",
                 (void*)r.packet, (void*)r.packetCtor, (void*)r.packetWriter, (void*)r.read, (void*)r.sendToServer, (void*)r.netMode);
    }
    return r.ok;
}

bool call(const MethodInfo* m, void* self, void** args, const char* what, Il2CppObject** result = nullptr) {
    if (!m) return false;
    Il2CppObject* exc = nullptr;
    Il2CppObject* value = il2cpp::api().runtime_invoke(m, self, args, &exc);
    if (exc) {
        BL_ERROR("C# net: %s threw", what);
        return false;
    }
    if (result) *result = value;
    return true;
}

Il2CppObject* invoke(const MethodInfo* m, void* self, void** args, const char* what) {
    Il2CppObject* result = nullptr;
    return call(m, self, args, what, &result) ? result : nullptr;
}

const MethodInfo* methodOf(Il2CppObject* object, const char* name, int argc) {
    auto& a = il2cpp::api();
    return object ? a.class_get_method_from_name(a.object_get_class(object), name, argc) : nullptr;
}

template <typename T>
T unbox(Il2CppObject* boxed) {
    T value{};
    if (boxed) std::memcpy(&value, reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject), sizeof(T));
    return value;
}

Il2CppObject* netManager() {
    Il2CppObject* manager = nullptr;
    if (g_refs.managerInstance) il2cpp::api().field_static_get_value(g_refs.managerInstance, &manager);
    else if (g_refs.managerGetter) manager = invoke(g_refs.managerGetter, nullptr, nullptr, "NetManager.Instance");
    return manager;
}

using ReadFn = void (*)(Il2CppObject*, Il2CppObject*, int32_t, int32_t, const MethodInfo*);
ReadFn g_origRead = nullptr;

void hkRead(Il2CppObject* self, Il2CppObject* reader, int32_t userId, int32_t readLength, const MethodInfo* method) {
    ManagedPacketHandler handler = g_handler.load(std::memory_order_acquire);
    if (!handler || !reader || readLength < 2) return g_origRead(self, reader, userId, readLength, method);
    Il2CppObject* stream = invoke(methodOf(reader, "get_BaseStream", 0), reader, nullptr, "BinaryReader.BaseStream");
    const MethodInfo* getPosition = methodOf(stream, "get_Position", 0);
    const MethodInfo* setPosition = methodOf(stream, "set_Position", 1);
    const MethodInfo* readUInt16 = methodOf(reader, "ReadUInt16", 0);
    if (!stream || !getPosition || !setPosition || !readUInt16) return g_origRead(self, reader, userId, readLength, method);
    Il2CppObject* position = nullptr;
    if (!call(getPosition, stream, nullptr, "Stream.Position", &position) || !position)
        return g_origRead(self, reader, userId, readLength, method);
    int64_t start = unbox<int64_t>(position);
    Il2CppObject* idBox = nullptr;
    const bool readId = call(readUInt16, reader, nullptr, "BinaryReader.ReadUInt16", &idBox) && idBox;
    if (!readId || unbox<uint16_t>(idBox) != kManagedNetModule) {
        void* back[1] = {&start};
        call(setPosition, stream, back, "Stream.Position");
        return g_origRead(self, reader, userId, readLength, method);
    }
    int32_t count = readLength - 2;
    void* args[1] = {&count};
    auto* bytes = reinterpret_cast<Il2CppArray*>(invoke(methodOf(reader, "ReadBytes", 1), reader, args, "BinaryReader.ReadBytes"));
    if (!bytes) return;
    const int32_t from = gameNetMode() == 1 ? 256 : userId;
    handler(static_cast<const uint8_t*>(arrayData(bytes)), static_cast<int32_t>(bytes->length), from);
}

using SendDataFn = void (*)(int32_t, int32_t, int32_t, Il2CppObject*, int32_t, float, float, float, int32_t, int32_t, int32_t,
                            const MethodInfo*);
SendDataFn g_origSendData = nullptr;

void hkSendData(int32_t msgType, int32_t remote, int32_t ignore, Il2CppObject* text, int32_t number, float n2, float n3, float n4,
                int32_t n5, int32_t n6, int32_t n7, const MethodInfo* method) {
    if (msgType == kRequestWorld && gameNetMode() == 1 && g_refs.clientUuid) {
        std::string hello;
        {
            std::lock_guard<std::mutex> l(g_helloMx);
            hello = g_hello;
        }
        if (!hello.empty()) {
            auto& a = il2cpp::api();
            Il2CppObject* previous = nullptr;
            a.field_static_get_value(g_refs.clientUuid, &previous);
            Il2CppString* ours = a.string_new((std::string(kHelloPrefix) + hello).c_str());
            a.field_static_set_value(g_refs.clientUuid, ours);
            g_origSendData(kClientUuid, -1, -1, nullptr, 0, 0, 0, 0, 0, 0, 0, method);
            a.field_static_set_value(g_refs.clientUuid, previous);
        }
    }
    g_origSendData(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7, method);
}

bool readHello(const Il2CppArray* data, int32_t length, std::string* out) {
    const auto* bytes = static_cast<const uint8_t*>(arrayData(const_cast<Il2CppArray*>(data)));
    int32_t size = 0, shift = 0, i = 1;
    for (; i < length && shift < 35; shift += 7) {
        const uint8_t b = bytes[i++];
        size |= (b & 0x7F) << shift;
        if (!(b & 0x80)) break;
    }
    const size_t prefix = std::strlen(kHelloPrefix);
    if (size < static_cast<int32_t>(prefix) || i + size > length) return false;
    if (std::memcmp(bytes + i, kHelloPrefix, prefix) != 0) return false;
    out->assign(reinterpret_cast<const char*>(bytes + i + prefix), static_cast<size_t>(size) - prefix);
    return true;
}

void boot(int32_t player, const std::string& reason) {
    auto& a = il2cpp::api();
    if (!g_refs.bootPlayer || !g_refs.fromLiteral) return;
    void* textArgs[1] = {a.string_new(reason.c_str())};
    Il2CppObject* text = invoke(g_refs.fromLiteral, nullptr, textArgs, "NetworkText.FromLiteral");
    if (!text) return;
    void* bootArgs[2] = {&player, text};
    call(g_refs.bootPlayer, nullptr, bootArgs, "NetMessage.BootPlayer");
}

using ProcessDataFn = void (*)(Il2CppObject*, Il2CppArray*, int32_t, int32_t*, const MethodInfo*);
ProcessDataFn g_origProcessData = nullptr;

void hkProcessData(Il2CppObject* self, Il2CppArray* data, int32_t length, int32_t* messageType, const MethodInfo* method) {
    if (!data || length <= 2 || static_cast<uintptr_t>(length) > data->length ||
        static_cast<const uint8_t*>(arrayData(data))[0] != kClientUuid || gameNetMode() == 1) {
        return g_origProcessData(self, data, length, messageType, method);
    }
    std::string hello;
    if (!readHello(data, length, &hello)) return g_origProcessData(self, data, length, messageType, method);
    if (messageType) *messageType = kClientUuid;
    const int32_t from = self && g_refs.whoAmI >= 0 ? *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(self) + g_refs.whoAmI) : -1;
    std::string ours;
    content::CultureNames refusal;
    {
        std::lock_guard<std::mutex> l(g_helloMx);
        ours = g_hello;
        refusal = g_refusal;
    }
    if (from < 0 || from >= 255 || hello == ours) return;
    BL_WARN("C# net: player %d refused, other C# mods (%s; this server: %s)", from, hello.c_str(), ours.c_str());
    boot(from, content::textForCulture(refusal, "This server uses other C# mods."));
}

}

int32_t gameNetMode() {
    if (!resolve() || !g_refs.netMode) return 0;
    int32_t mode = 0;
    il2cpp::api().field_static_get_value(g_refs.netMode, &mode);
    return mode;
}

bool installManagedNet(ManagedPacketHandler handler) {
    static std::once_flag once;
    static bool installed = false;
    g_handler.store(handler, std::memory_order_release);
    std::call_once(once, [] {
        if (!resolve()) return;
        installed = hook::install(g_refs.read, hkRead, &g_origRead);
        if (!installed) BL_ERROR("C# net: no hook on NetManager.Read; C# mod packets are not received");
        if (g_refs.sendData && !hook::install(g_refs.sendData, hkSendData, &g_origSendData))
            BL_ERROR("C# net: no hook on NetMessage.SendData; servers do not see this client's C# mods");
        if (g_refs.processData && !hook::install(g_refs.processData, hkProcessData, &g_origProcessData))
            BL_ERROR("C# net: no hook on MessageBuffer.ProcessData; clients with other C# mods are not refused");
    });
    return installed;
}

void setManagedHello(const std::string& json, const content::CultureNames& refusal) {
    std::lock_guard<std::mutex> l(g_helloMx);
    g_hello = json;
    g_refusal = refusal;
}

bool sendManagedPacket(const uint8_t* data, int32_t length, int32_t toClient, int32_t ignoreClient) {
    if (!resolve() || !data || length < 0) return false;
    const int32_t mode = gameNetMode();
    if (mode == 0) return false;
    auto& a = il2cpp::api();
    Il2CppObject* boxed = a.object_new(g_refs.packet);
    if (!boxed) return false;
    void* packet = reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject);
    uint16_t id = kManagedNetModule;
    int32_t size = length;
    void* ctorArgs[2] = {&id, &size};
    if (!call(g_refs.packetCtor, packet, ctorArgs, "NetPacket.ctor")) return false;
    Il2CppObject* writer = invoke(g_refs.packetWriter, packet, nullptr, "NetPacket.Writer");
    const MethodInfo* write = writer ? sig(a.object_get_class(writer), "void Write(byte[] buffer)") : nullptr;
    Il2CppArray* bytes = a.array_new(g_refs.byteCls, static_cast<uintptr_t>(length));
    if (!write || !bytes) return false;
    std::memcpy(arrayData(bytes), data, static_cast<size_t>(length));
    void* writeArgs[1] = {bytes};
    if (!call(write, writer, writeArgs, "BinaryWriter.Write")) return false;
    if (!call(g_refs.packetShrink, packet, nullptr, "NetPacket.ShrinkToFit")) return false;
    Il2CppObject* manager = netManager();
    if (!manager) return false;
    if (mode == 1) {
        void* args[1] = {packet};
        return call(g_refs.sendToServer, manager, args, "NetManager.SendToServer");
    }
    if (toClient >= 0) {
        void* args[2] = {packet, &toClient};
        return call(g_refs.sendToClient, manager, args, "NetManager.SendToClient");
    }
    void* args[2] = {packet, &ignoreClient};
    return call(g_refs.broadcast, manager, args, "NetManager.Broadcast");
}

}
