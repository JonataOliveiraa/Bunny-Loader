#include "runtime/NetRequests.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "runtime/Cheats.h"
#include "runtime/GameRefs.h"
#include "runtime/ModItems.h"
#include "runtime/ModNpcs.h"
#include "runtime/Powers.h"
#include <cstdio>
#include <deque>
#include <mutex>

namespace bl::runtime {

namespace {

// "/bunny npc 697": com a barra, um servidor sem Bunny Loader trata como
// comando desconhecido e mostra o texto; legivel para quem ve.
constexpr char kPrefix[] = "/bunny ";

FieldInfo* g_netMode = nullptr;
const MethodInfo* g_sendData = nullptr;
Il2CppClass* g_chatMessage = nullptr;
const MethodInfo* g_chatMessageCtor = nullptr;   // ChatMessage(string)
const MethodInfo* g_sendFromClient = nullptr;    // ChatHelper.SendChatMessageFromClient
int32_t g_textOffset = -1;                       // ChatMessage.<Text>k__BackingField

using ProcessFn = void (*)(Il2CppObject*, Il2CppObject*, int32_t, const MethodInfo*);
ProcessFn g_origProcess = nullptr;

struct Request {
    int client;
    std::string command;
};
std::mutex g_mx;
std::deque<Request> g_queue;

std::string asciiOf(Il2CppString* s) {
    std::string out;
    if (!s) return out;
    out.reserve(static_cast<size_t>(s->length));
    for (int32_t i = 0; i < s->length; ++i) {
        const char16_t c = s->chars[i];
        out.push_back(c < 0x80 ? static_cast<char>(c) : '?');
    }
    return out;
}

/**
 * Chat que chega ao servidor, com o indice de quem mandou. So o prefixo nosso
 * e engolido; o resto segue para o jogo como sempre.
 */
void hkProcessIncomingMessage(Il2CppObject* self, Il2CppObject* message, int32_t clientId,
                              const MethodInfo* m) {
    if (message && g_textOffset >= 0 && isNetHost()) {
        const std::string text = asciiOf(field<Il2CppString*>(message, g_textOffset));
        if (text.rfind(kPrefix, 0) == 0) {
            std::lock_guard<std::mutex> l(g_mx);
            // Teto: um cliente mandando sem parar nao enche a memoria.
            if (g_queue.size() < 64) g_queue.push_back({clientId, text.substr(sizeof(kPrefix) - 1)});
            return;
        }
    }
    g_origProcess(self, message, clientId, m);
}

/**
 * Executa um pedido. O tipo vem da rede, e este IL2CPP nao confere limite de
 * array: tipo fora da tabela leria e escreveria alem do fim. Entao tudo e
 * conferido contra a contagem real (jogo + mods) antes de chegar ao jogo.
 */
void run(const Request& r) {
    int type = 0, amount = 0;
    char verb[16] = {};
    if (std::sscanf(r.command.c_str(), "%15s %d %d", verb, &type, &amount) < 2) {
        BL_WARN("rede: pedido mal formado do jogador %d: '%s'", r.client, r.command.c_str());
        return;
    }
    const std::string v = verb;
    if (v == "npc") {
        if (type <= 0 || type >= npcTypeCount()) {
            BL_WARN("rede: jogador %d pediu NPC %d, fora da tabela", r.client, type);
            return;
        }
        const int count = amount < 1 ? 1 : (amount > 10 ? 10 : amount);
        for (int i = 0; i < count; ++i) spawnNpc(type, r.client);
    } else if (v == "item") {
        if (type <= 0 || type >= itemTypeCount()) {
            BL_WARN("rede: jogador %d pediu item %d, fora da tabela", r.client, type);
            return;
        }
        giveItem(type, amount < 1 ? 1 : amount, r.client);
    } else if (v == "power") {
        // "power <id> <nivel>": so os de mundo (setWorldPowerFromNet confere).
        if (!setWorldPowerFromNet(type, amount)) {
            BL_WARN("rede: jogador %d pediu poder %d, que nao e de mundo", r.client, type);
        }
    } else if (v == "time") {
        setTimeOfDay(type);   // "time <0..3>": confere a faixa sozinho
    } else {
        BL_WARN("rede: pedido desconhecido do jogador %d: '%s'", r.client, r.command.c_str());
    }
}

} // namespace

int netMode() {
    int32_t mode = 0;
    if (g_netMode) il2cpp::api().field_static_get_value(g_netMode, &mode);
    return mode;
}

void sendData(int msgType, int number) {
    if (!g_sendData) return;
    int type = msgType, remote = -1, ignore = -1, n = number, n5 = 0, n6 = 0, n7 = 0;
    float n2 = 0, n3 = 0, n4 = 0;
    void* args[11] = { &type, &remote, &ignore, nullptr, &n, &n2, &n3, &n4, &n5, &n6, &n7 };
    Il2CppObject* exc = nullptr;
    il2cpp::api().runtime_invoke(g_sendData, nullptr, args, &exc);
    if (exc) BL_ERROR("rede: SendData(%d, %d) lancou excecao", msgType, number);
}

bool sendToServer(const std::string& command) {
    if (!g_chatMessageCtor || !g_sendFromClient) {
        BL_ERROR("rede: sem refs do chat; pedido '%s' perdido", command.c_str());
        return false;
    }
    auto& a = il2cpp::api();
    const std::string text = kPrefix + command;
    Il2CppObject* msg = a.object_new(g_chatMessage);
    void* ctorArgs[1] = { a.string_new(text.c_str()) };
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(g_chatMessageCtor, msg, ctorArgs, &exc);
    if (exc) { BL_ERROR("rede: ChatMessage(...) lancou excecao"); return false; }
    void* args[1] = { msg };
    a.runtime_invoke(g_sendFromClient, nullptr, args, &exc);
    if (exc) { BL_ERROR("rede: SendChatMessageFromClient lancou excecao"); return false; }
    BL_INFO("rede: pedido ao servidor: %s", command.c_str());
    return true;
}

void tickNetRequests() {
    std::deque<Request> batch;
    {
        std::lock_guard<std::mutex> l(g_mx);
        batch.swap(g_queue);
    }
    for (const Request& r : batch) {
        BL_INFO("rede: pedido do jogador %d: %s", r.client, r.command.c_str());
        run(r);
    }
}

void installNetRequests() {
    using namespace il2cpp;
    auto& a = api();
    if (Il2CppClass* main = findClass({"Terraria", "Main", {}})) {
        g_netMode = a.class_get_field_from_name(main, "netMode");
    }
    if (Il2CppClass* net = findClass({"Terraria", "NetMessage", {}})) {
        g_sendData = findMethodBySignature(net, parseSignature(
            "void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, "
            "int number, float number2, float number3, float number4, int number5, "
            "int number6, int number7)"));
    }
    g_chatMessage = findClass({"Terraria.Chat", "ChatMessage", {}});
    if (g_chatMessage) {
        g_chatMessageCtor = findMethodBySignature(g_chatMessage, parseSignature(
            "void .ctor(string message)"));
        g_textOffset = fieldOffset(g_chatMessage, "<Text>k__BackingField");
    }
    if (Il2CppClass* helper = findClass({"Terraria.Chat", "ChatHelper", {}})) {
        g_sendFromClient = a.class_get_method_from_name(helper, "SendChatMessageFromClient", 1);
    }
    const MethodInfo* process = nullptr;
    if (Il2CppClass* proc = findClass({"Terraria.Chat", "ChatCommandProcessor", {}})) {
        process = a.class_get_method_from_name(proc, "ProcessIncomingMessage", 2);
    }
    const bool hooked = process && g_textOffset >= 0 &&
                        hook::install(process, hkProcessIncomingMessage, &g_origProcess);
    BL_INFO("rede: netMode=%p SendData=%p chat(ctor=%p envio=%p texto=%d) servidor=%s",
            (void*)g_netMode, (const void*)g_sendData, (const void*)g_chatMessageCtor,
            (const void*)g_sendFromClient, g_textOffset, hooked ? "ok" : "SEM GANCHO");
}

} // namespace bl::runtime
