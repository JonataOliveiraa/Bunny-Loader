#include "boot/QuickStart.h"

#include "content/common/GameRefs.h"
#include "content/common/ModContent.h"
#include "content/textures/TexturePacks.h"
#include "core/Config.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "menu/Cheats.h"

#include <time.h>
#include <unistd.h>

#include <algorithm>
#include <atomic>
#include <condition_variable>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>

namespace bl::runtime {

namespace {

// O splash acaba no quadro 620 do cronometro dele (10,3 s a 60 quadros por
// segundo). Adiantar 11 s passa do fim de qualquer ponto em que ele esteja.
constexpr int kSkipSeconds = 11;
constexpr double kSplashSeconds = 620 / 60.0;
// O fim do splash poe Main.fadeCounter = 120: o titulo sai do preto em 2 s.
// Com o inicio rapido, um quarto disso.
constexpr int32_t kTitleFadeFrames = 30;
constexpr int64_t kHoldFrames = 558;
constexpr int64_t kQuickSplashHoldFrames = 120;
constexpr double kHoldLimitSeconds = 300;
constexpr uint32_t kWhite = 0xFFFFFFFF;
constexpr uint32_t kBlack = 0xFF000000;
// Janela de popup do jogo por cima do titulo (menuMode fora de 0): espera
// ela fechar ate este tempo antes de desistir de entrar sozinho.
constexpr double kTitleWaitSeconds = 30;

// ------------------------------------------------------------------ tempo

/** Quando o processo do jogo nasceu (o toque em Jogar), em s desde o boot. */
double processStartSeconds() {
    // Campo 22 de /proc/self/stat, em tiques desde o boot do aparelho. O
    // nome (campo 2) pode ter espaco, entao a contagem comeca depois do ')'.
    FILE* f = fopen("/proc/self/stat", "r");
    if (!f) return -1;
    char buf[1024];
    const size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    fclose(f);
    buf[n] = '\0';
    const char* p = strrchr(buf, ')');
    if (!p || p[1] != ' ') return -1;
    p += 2;   // campo 3
    for (int field = 3; field < 22; ++field) {
        p = strchr(p, ' ');
        if (!p) return -1;
        ++p;
    }
    return strtod(p, nullptr) / static_cast<double>(sysconf(_SC_CLK_TCK));
}

/** Segundos desde o toque em Jogar: e a conta que o dev faz de cabeca. */
double sinceStart() {
    static const double start = processStartSeconds();
    if (start < 0) return 0;
    timespec ts{};
    clock_gettime(CLOCK_BOOTTIME, &ts);
    return static_cast<double>(ts.tv_sec) + ts.tv_nsec / 1e9 - start;
}

// ----------------------------------------------------------------- estado

std::mutex g_loadedMutex;
std::condition_variable g_loadedCv;
bool g_gameLoaded = false;              // Main._isAsyncLoadComplete ja foi visto
std::atomic<bool> g_coreReady{false};   // a sonda carregou os mods e o nucleo
std::atomic<bool> g_probeFinished{false};
std::atomic<bool> g_splashHooked{false};
std::atomic<bool> g_splashOver{false};

// Main e o cronometro do splash. Resolvido no install.
struct IntroRefs {
    FieldInfo* asyncLoadComplete = nullptr;   // Main._isAsyncLoadComplete
    FieldInfo* showSplash = nullptr;          // Main.showSplash
    FieldInfo* splashTimer = nullptr;         // Main.splashTimer (Stopwatch)
    FieldInfo* fadeCounter = nullptr;         // Main.fadeCounter
    FieldInfo* spriteBatch = nullptr;
    const MethodInfo* loadingFlower = nullptr;
    int32_t quickSplash = -1;
    int32_t splashFrameCount = -1;
} g_intro;

bool staticBool(FieldInfo* f) {
    uint8_t v = 0;
    il2cpp::api().field_static_get_value(f, &v);
    return v != 0;
}

// ------------------------------------------------------------- o splash

using DrawSplashFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
DrawSplashFn g_origDrawSplash = nullptr;

// Thread do jogo (so o DrawSplash mexe).
bool g_splashCut = false;
bool g_titleHeld = false;
bool g_titleLogged = false;

struct SplashClock {
    Il2CppObject* timer = nullptr;
    int32_t elapsedOffset = -1;
    int64_t frequency = 0;
    int64_t ms = 0;
};

bool readSplashClock(SplashClock& clock) {
    auto& a = il2cpp::api();
    a.field_static_get_value(g_intro.splashTimer, &clock.timer);
    if (!clock.timer) return false;
    static FieldInfo* const frequencyField = il2cpp::findField(clock.timer->klass, "Frequency");
    static const int32_t elapsedOffset = il2cpp::fieldOffset(clock.timer->klass, "elapsed");
    static const MethodInfo* const elapsedMs =
        a.class_get_method_from_name(clock.timer->klass, "get_ElapsedMilliseconds", 0);
    if (!frequencyField || elapsedOffset < 0 || !elapsedMs) return false;
    clock.elapsedOffset = elapsedOffset;
    a.field_static_get_value(frequencyField, &clock.frequency);
    if (clock.frequency <= 0) return false;
    Il2CppObject* exc = nullptr;
    Il2CppObject* boxed = a.runtime_invoke(elapsedMs, clock.timer, nullptr, &exc);
    if (exc || !boxed) return false;
    clock.ms = *reinterpret_cast<int64_t*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
    return true;
}

/**
 * Adianta o cronometro do splash para depois do fim dele.
 *
 * O DrawSplash do celular recalcula o quadro a cada chamada pelo tempo
 * (splashCounter = ms * 60 / 1000, conferido na disassembly) e, passado o
 * quadro 620 com o jogo carregado, roda o fim de sempre: LoadSettings,
 * Initialize_AlmostEverything, PostContentLoadInitialize, showSplash = false.
 * Mexer no relogio e o jeito de chegar nesse fim sem reescrever nada dele.
 *
 * O Stopwatch do Mono guarda o acumulado em `elapsed`, na unidade de
 * Stopwatch.Frequency (tiques por segundo).
 *
 * @return os segundos que o splash ainda ia durar, ou -1 se nao deu.
 */
double advanceSplashTimer() {
    // Quanto ja correu, pela conta do proprio jogo: e o que diz quanto o
    // splash ainda ia durar.
    SplashClock clock;
    if (!readSplashClock(clock)) return -1;
    field<int64_t>(clock.timer, clock.elapsedOffset) += clock.frequency * kSkipSeconds;
    return std::max(0.0, kSplashSeconds - static_cast<double>(clock.ms) / 1000.0);
}

bool holdSplashTimer(Il2CppObject* self) {
    SplashClock clock;
    if (!readSplashClock(clock)) return false;
    const bool quick = g_intro.quickSplash >= 0 && field<uint8_t>(self, g_intro.quickSplash) != 0;
    const int64_t limit = (quick ? kQuickSplashHoldFrames : kHoldFrames) * 1000 / 60;
    if (clock.ms < limit) return false;
    field<int64_t>(clock.timer, clock.elapsedOffset) -= (clock.ms - limit) * clock.frequency / 1000;
    return true;
}

bool invokeDraw(const MethodInfo* method, void* target, void** args) {
    Il2CppObject* exc = nullptr;
    il2cpp::api().runtime_invoke(method, target, args, &exc);
    return !exc;
}

void drawLoadingFlower(Il2CppObject* self) {
    auto& a = il2cpp::api();
    Il2CppObject* batch = nullptr;
    a.field_static_get_value(g_intro.spriteBatch, &batch);
    if (!batch) return;
    static const MethodInfo* const begin = a.class_get_method_from_name(batch->klass, "Begin", 2);
    static const MethodInfo* const end = a.class_get_method_from_name(batch->klass, "End", 0);
    static bool broken = false;
    if (broken || !begin || !end) return;
    int32_t sortMode = 0;
    uint8_t deferred = 1;
    void* beginArgs[2] = {&sortMode, &deferred};
    uint32_t color = kWhite;
    void* flowerArgs[1] = {&color};
    broken = !invokeDraw(begin, batch, beginArgs) ||
             !invokeDraw(g_intro.loadingFlower, self, flowerArgs) ||
             !invokeDraw(end, batch, nullptr);
    if (broken) BL_ERROR("startup: the loading flower could not be drawn");
}

void drawLoadingScreen(Il2CppObject* self) {
    auto& a = il2cpp::api();
    static const MethodInfo* const getDevice = a.class_get_method_from_name(self->klass, "get_GraphicsDevice", 0);
    if (getDevice) {
        Il2CppObject* exc = nullptr;
        Il2CppObject* device = a.runtime_invoke(getDevice, self, nullptr, &exc);
        static const MethodInfo* const clear = device ? a.class_get_method_from_name(device->klass, "Clear", 1) : nullptr;
        if (!exc && device && clear) {
            uint32_t black = kBlack;
            void* args[1] = {&black};
            invokeDraw(clear, device, args);
        }
    }
    drawLoadingFlower(self);
    if (g_intro.splashFrameCount >= 0) {
        float& frame = field<float>(self, g_intro.splashFrameCount);
        frame += 1;
        if (frame >= 70) frame = 0;
    }
}

void showTitle() {
    uint8_t off = 0;
    il2cpp::api().field_static_set_value(g_intro.showSplash, &off);
    g_titleHeld = false;
    g_titleLogged = true;
    if (g_splashCut) {
        int32_t fade = kTitleFadeFrames;
        il2cpp::api().field_static_set_value(g_intro.fadeCounter, &fade);
    }
    BL_INFO("startup: title on screen at %.1f s", sinceStart());
}

void hkDrawSplash(Il2CppObject* self, Il2CppObject* gameTime, const MethodInfo* m) {
    const bool expired = sinceStart() > kHoldLimitSeconds;
    if (expired && (g_titleHeld || !g_probeFinished.load(std::memory_order_acquire))) {
        static bool warned = false;
        if (!warned) BL_WARN("startup: mods still loading after %.0f s; showing the title anyway", kHoldLimitSeconds);
        warned = true;
    }
    if (g_titleHeld) {
        drawLoadingScreen(self);
        if (runtime::contentFinished() || expired) showTitle();
        return;
    }

    // O jogo marca o fim da carga DENTRO do DrawSplash; aqui ve-se no quadro
    // seguinte. Com os mods ja carregados, o splash acaba agora.
    if (config().fastBoot && !g_splashCut && staticBool(g_intro.asyncLoadComplete) &&
        g_coreReady.load(std::memory_order_acquire) && !runtime::texturePacksBusy()) {
        const double left = advanceSplashTimer();
        g_splashCut = true;
        if (left >= 0) {
            BL_INFO("inicio rapido: abertura encerrada em %.1f s (a espera ia durar mais %.1f s)",
                    sinceStart(), left);
        } else {
            BL_WARN("inicio rapido: cronometro do splash nao encontrado; abertura normal");
        }
    }
    const bool loaded = expired ||
        (g_probeFinished.load(std::memory_order_acquire) && !runtime::texturePacksBusy());
    const bool held = !loaded && holdSplashTimer(self);

    g_origDrawSplash(self, gameTime, m);

    if (!g_gameLoaded && staticBool(g_intro.asyncLoadComplete)) {
        {
            std::lock_guard<std::mutex> lock(g_loadedMutex);
            g_gameLoaded = true;
        }
        g_loadedCv.notify_all();
        BL_INFO("startup: game content loaded at %.1f s", sinceStart());
    }
    if (held) drawLoadingFlower(self);
    if (!g_titleLogged && !staticBool(g_intro.showSplash)) {
        g_splashOver.store(true, std::memory_order_release);
        if (runtime::contentFinished() || expired) {
            showTitle();
        } else {
            uint8_t on = 1;
            il2cpp::api().field_static_set_value(g_intro.showSplash, &on);
            g_titleHeld = true;
            BL_INFO("startup: splash over at %.1f s; waiting for mod content", sinceStart());
        }
    }
}

// ------------------------------------------------------ entrada no mundo

bool g_entryDone = false;       // thread do jogo
bool g_entered = false;         // pedimos o playWorld: falta ver o mundo
double g_settledAt = -1;        // quando o conteudo ficou pronto (espera de popup)

std::u16string toUtf16(const std::string& s) {
    std::u16string out;
    for (size_t i = 0; i < s.size();) {
        const auto c = static_cast<unsigned char>(s[i]);
        uint32_t cp = c;
        int extra = 0;
        if (c >= 0xF0) { cp = c & 0x07; extra = 3; }
        else if (c >= 0xE0) { cp = c & 0x0F; extra = 2; }
        else if (c >= 0xC0) { cp = c & 0x1F; extra = 1; }
        ++i;
        for (int k = 0; k < extra && i < s.size(); ++k, ++i) {
            cp = (cp << 6) | (static_cast<unsigned char>(s[i]) & 0x3F);
        }
        if (cp >= 0x10000) {
            cp -= 0x10000;
            out += static_cast<char16_t>(0xD800 + (cp >> 10));
            out += static_cast<char16_t>(0xDC00 + (cp & 0x3FF));
        } else {
            out += static_cast<char16_t>(cp);
        }
    }
    return out;
}

/** O caminho termina em `/<file>`? A comparacao e pelo nome do arquivo. */
bool hasFileName(const Il2CppString* path, const std::u16string& file) {
    if (!path || file.empty()) return false;
    const auto n = static_cast<size_t>(path->length);
    const size_t m = file.size();
    if (n < m) return false;
    if (n > m && path->chars[n - m - 1] != u'/' && path->chars[n - m - 1] != u'\\') return false;
    return std::equal(file.begin(), file.end(), path->chars + (n - m));
}

/** O item de uma List<FileData> cujo arquivo e `file`. */
Il2CppObject* findByFile(Il2CppObject* list, const std::u16string& file, int32_t pathOffset) {
    if (!list) return nullptr;
    const int32_t itemsOffset = il2cpp::fieldOffset(list->klass, "_items");
    const int32_t sizeOffset = il2cpp::fieldOffset(list->klass, "_size");
    if (itemsOffset < 0 || sizeOffset < 0) return nullptr;
    auto* items = field<Il2CppArray*>(list, itemsOffset);
    const int32_t size = field<int32_t>(list, sizeOffset);
    if (!items || size < 0 || static_cast<uintptr_t>(size) > items->length) return nullptr;
    auto** data = static_cast<Il2CppObject**>(arrayData(items));
    for (int32_t i = 0; i < size; ++i) {
        if (data[i] && hasFileName(field<Il2CppString*>(data[i], pathOffset), file)) return data[i];
    }
    return nullptr;
}

bool invoke(const MethodInfo* m, void* self, void** args, const char* what,
            Il2CppObject** result = nullptr) {
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(m, self, args, &exc);
    if (exc) {
        BL_ERROR("inicio rapido: %s lancou excecao", what);
        return false;
    }
    if (result) *result = r;
    return true;
}

/**
 * O aviso "Gostaria de editar os controles agora?" que o Player.Spawn abre a
 * cada entrada no mundo enquanto os controles de toque nunca foram
 * configurados (Main.PerformedTouchInputConfig): o "Mais tarde" dele, para
 * esta sessao. O DisableCheck do aviso nao vai para o save; o
 * PerformedTouchInputConfig iria, e por isso fica como esta.
 */
void skipTouchControlsPrompt() {
    auto& a = il2cpp::api();
    Il2CppClass* gui = il2cpp::findClass({"", "GUIInstance", {}});
    FieldInfo* active = gui ? il2cpp::findField(gui, "Active") : nullptr;
    if (!active) return;
    Il2CppObject* instance = nullptr;
    a.field_static_get_value(active, &instance);
    const int32_t popupOffset = instance ? il2cpp::fieldOffset(gui, "GUIConfigureTouchControlsPopup") : -1;
    Il2CppObject* popup = popupOffset >= 0 ? field<Il2CppObject*>(instance, popupOffset) : nullptr;
    const int32_t disableOffset = popup ? il2cpp::fieldOffset(popup->klass, "DisableCheck") : -1;
    if (disableOffset < 0) return;
    field<uint8_t>(popup, disableOffset) = 1;
    BL_DEBUG("inicio rapido: aviso de controles de toque dispensado nesta sessao");
}

/**
 * O que os botoes "Jogar" das duas listas fazem, em sequencia (conferido na
 * disassembly do GUIPlayerSelectMenu e do GUIWorldSelectMenu):
 *
 *   Main.LoadPlayers()          a lista de personagens, lida do disco
 *   Main.SelectPlayer(dados)    ativo, myPlayer = 0, lista de mundos, menuMode 6
 *   mundo.SetAsActive()
 *   Main.menuMode = 10          a tela de "carregando"
 *   WorldGen.playWorld()        a carga, na thread do jogo para isso
 *
 * Os avisos que as listas dao no caminho (espaco em disco, memoria para
 * mundo grande, versao do save) ficam de fora: e ferramenta de dev.
 */
/**
 * Os dois primeiros passos: o personagem do arquivo `playerFile` ativo. O
 * Player dele, ou null (o motivo vai para o log).
 */
Il2CppObject* selectPlayerFile(const std::string& playerFile) {
    auto& a = il2cpp::api();
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
    Il2CppClass* fileData = il2cpp::findClass({"Terraria.IO", "FileData", {}});
    Il2CppClass* playerData = il2cpp::findClass({"Terraria.IO", "PlayerFileData", {}});
    if (!main || !player || !fileData || !playerData) {
        BL_ERROR("inicio rapido: classes do jogo nao encontradas");
        return nullptr;
    }
    const MethodInfo* loadPlayers = a.class_get_method_from_name(main, "LoadPlayers", 1);
    const MethodInfo* getPlayerList = a.class_get_method_from_name(main, "get_PlayerList", 0);
    const MethodInfo* selectPlayer = a.class_get_method_from_name(main, "SelectPlayer", 1);
    const MethodInfo* getPlayer = a.class_get_method_from_name(playerData, "get_Player", 0);
    const int32_t pathOffset = il2cpp::fieldOffset(fileData, "_path");
    const int32_t loadStatusOffset = il2cpp::fieldOffset(player, "loadStatus");
    if (!loadPlayers || !getPlayerList || !selectPlayer || !getPlayer || pathOffset < 0 ||
        loadStatusOffset < 0) {
        BL_ERROR("inicio rapido: metodos ou campos do personagem nao encontrados");
        return nullptr;
    }

    // A lista do titulo nasce vazia: quem a enche e a tela de personagens, ao
    // abrir.
    uint8_t fullRefresh = 0;
    void* loadArgs[1] = {&fullRefresh};
    Il2CppObject* players = nullptr;
    if (!invoke(loadPlayers, nullptr, loadArgs, "Main.LoadPlayers") ||
        !invoke(getPlayerList, nullptr, nullptr, "Main.PlayerList", &players)) return nullptr;
    Il2CppObject* data = findByFile(players, toUtf16(playerFile), pathOffset);
    if (!data) {
        BL_ERROR("inicio rapido: o personagem %s nao esta na pasta Players; o jogo fica no titulo",
                 playerFile.c_str());
        return nullptr;
    }
    Il2CppObject* who = nullptr;
    if (!invoke(getPlayer, data, nullptr, "PlayerFileData.Player", &who)) return nullptr;
    if (!who || field<int32_t>(who, loadStatusOffset) != 0) {
        BL_ERROR("inicio rapido: o personagem %s nao abriu (arquivo com defeito?); o jogo fica no titulo",
                 playerFile.c_str());
        return nullptr;
    }
    void* selectArgs[1] = {data};
    if (!invoke(selectPlayer, nullptr, selectArgs, "Main.SelectPlayer")) return nullptr;
    return who;
}

void enterWorld(const std::string& playerFile, const std::string& worldFile) {
    auto& a = il2cpp::api();
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    Il2CppClass* worldGen = il2cpp::findClass({"Terraria", "WorldGen", {}});
    Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
    Il2CppClass* fileData = il2cpp::findClass({"Terraria.IO", "FileData", {}});
    Il2CppClass* worldData = il2cpp::findClass({"Terraria.IO", "WorldFileData", {}});
    if (!main || !worldGen || !player || !fileData || !worldData) {
        BL_ERROR("inicio rapido: classes do jogo nao encontradas");
        return;
    }
    const MethodInfo* setMenuMode = a.class_get_method_from_name(main, "set_menuMode", 1);
    const MethodInfo* setActive = a.class_get_method_from_name(worldData, "SetAsActive", 0);
    const MethodInfo* playWorld = a.class_get_method_from_name(worldGen, "playWorld", 0);
    FieldInfo* worldList = il2cpp::findField(main, "WorldList");
    const int32_t pathOffset = il2cpp::fieldOffset(fileData, "_path");
    const int32_t difficultyOffset = il2cpp::fieldOffset(player, "difficulty");
    const int32_t gameModeOffset = il2cpp::fieldOffset(worldData, "GameMode");
    if (!setMenuMode || !setActive || !playWorld || !worldList || pathOffset < 0 ||
        difficultyOffset < 0 || gameModeOffset < 0) {
        BL_ERROR("inicio rapido: metodos ou campos do jogo nao encontrados");
        return;
    }

    Il2CppObject* who = selectPlayerFile(playerFile);
    if (!who) return;

    // O mundo. O SelectPlayer ja leu a lista; daqui em diante, qualquer
    // recusa deixa o jogo na lista de mundos, com o personagem escolhido.
    Il2CppObject* worlds = nullptr;
    a.field_static_get_value(worldList, &worlds);
    Il2CppObject* world = findByFile(worlds, toUtf16(worldFile), pathOffset);
    if (!world) {
        BL_ERROR("inicio rapido: o mundo %s nao esta na pasta Worlds; o jogo fica na lista de mundos",
                 worldFile.c_str());
        return;
    }
    // A regra da lista de mundos (UIWorldSelect.CanWorldBeJoinedByActivePlayer):
    // personagem de Jornada so entra em mundo de Jornada, e vice-versa.
    const bool journeyPlayer = field<uint8_t>(who, difficultyOffset) == 3;
    const bool journeyWorld = field<int32_t>(world, gameModeOffset) == 3;
    if (journeyPlayer != journeyWorld) {
        BL_ERROR("inicio rapido: %s %s de Jornada e %s %s; o jogo nao deixa entrar, fica a lista de mundos",
                 playerFile.c_str(), journeyPlayer ? "e" : "nao e",
                 worldFile.c_str(), journeyWorld ? "e" : "nao e");
        return;
    }
    if (!invoke(setActive, world, nullptr, "WorldFileData.SetAsActive")) return;
    skipTouchControlsPrompt();
    int32_t loading = 10;
    void* modeArgs[1] = {&loading};
    if (!invoke(setMenuMode, nullptr, modeArgs, "Main.menuMode")) return;
    if (!invoke(playWorld, nullptr, nullptr, "WorldGen.playWorld")) return;
    g_entered = true;
    BL_INFO("inicio rapido: carregando %s com %s (%.1f s)", worldFile.c_str(), playerFile.c_str(),
            sinceStart());
}

/**
 * A volta ao servidor depois da sincronizacao de mods: o personagem, e o que
 * o "Entrar" da tela de Multijogador faz (Main.netMode = 1, a tela de
 * "conectando", Netplay.SetRemoteIP, a porta, a senha e
 * Netplay.StartTcpClient). Senha errada ou servidor fora do ar caem nas telas
 * do proprio jogo.
 */
void joinServer(const Config& c) {
    auto& a = il2cpp::api();
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    Il2CppClass* netplay = il2cpp::findClass({"Terraria", "Netplay", {}});
    if (!main || !netplay) {
        BL_ERROR("voltar ao servidor: Main/Netplay nao encontrados");
        return;
    }
    const MethodInfo* setMenuMode = a.class_get_method_from_name(main, "set_menuMode", 1);
    FieldInfo* netMode = il2cpp::findField(main, "netMode");
    FieldInfo* listenPort = il2cpp::findField(netplay, "ListenPort");
    FieldInfo* password = il2cpp::findField(netplay, "ServerPassword");
    const MethodInfo* setRemoteIp = il2cpp::findMethodBySignature(
        netplay, il2cpp::parseSignature("bool SetRemoteIP(string remoteAddress)"));
    const MethodInfo* startClient = il2cpp::findMethodBySignature(
        netplay, il2cpp::parseSignature("void StartTcpClient(bool connectingToLocalServer)"));
    if (!setMenuMode || !netMode || !listenPort || !password || !setRemoteIp || !startClient) {
        BL_ERROR("voltar ao servidor: metodos ou campos do Netplay nao encontrados");
        return;
    }
    if (!selectPlayerFile(c.joinPlayer)) return;

    void* ipArgs[1] = {a.string_new(c.joinAddress.c_str())};
    Il2CppObject* ok = nullptr;
    if (!invoke(setRemoteIp, nullptr, ipArgs, "Netplay.SetRemoteIP", &ok)) return;
    if (!ok || *(reinterpret_cast<uint8_t*>(ok) + sizeof(Il2CppObject)) == 0) {
        BL_ERROR("voltar ao servidor: endereco %s invalido", c.joinAddress.c_str());
        return;
    }
    int32_t client = 1, port = c.joinPort > 0 ? c.joinPort : 7777, connecting = 14;
    a.field_static_set_value(netMode, &client);
    a.field_static_set_value(listenPort, &port);
    a.field_static_set_value(password, a.string_new(c.joinPassword.c_str()));
    void* modeArgs[1] = {&connecting};
    if (!invoke(setMenuMode, nullptr, modeArgs, "Main.menuMode")) return;
    skipTouchControlsPrompt();
    uint8_t local = 0;
    void* startArgs[1] = {&local};
    if (!invoke(startClient, nullptr, startArgs, "Netplay.StartTcpClient")) return;
    BL_INFO("voltar ao servidor: %s conectando em %s:%d (%.1f s)", c.joinPlayer.c_str(),
            c.joinAddress.c_str(), port, sinceStart());
}

int32_t menuMode() {
    static const MethodInfo* getter = [] {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        return main ? il2cpp::api().class_get_method_from_name(main, "get_menuMode", 0) : nullptr;
    }();
    if (!getter) return -1;
    Il2CppObject* exc = nullptr;
    Il2CppObject* boxed = il2cpp::api().runtime_invoke(getter, nullptr, nullptr, &exc);
    if (exc || !boxed) return -1;
    return *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
}

} // namespace

void installSplashHook() {
    static std::once_flag once;
    std::call_once(once, [] {
        auto& a = il2cpp::api();
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        if (!main) { BL_ERROR("startup: Terraria.Main not found"); return; }
        g_intro.asyncLoadComplete = il2cpp::findField(main, "_isAsyncLoadComplete");
        g_intro.showSplash = il2cpp::findField(main, "showSplash");
        g_intro.splashTimer = il2cpp::findField(main, "splashTimer");
        g_intro.fadeCounter = il2cpp::findField(main, "fadeCounter");
        g_intro.spriteBatch = il2cpp::findField(main, "spriteBatch");
        g_intro.loadingFlower = a.class_get_method_from_name(main, "DrawSplash_LoadingFlower", 1);
        g_intro.quickSplash = il2cpp::fieldOffset(main, "quickSplash");
        g_intro.splashFrameCount = il2cpp::fieldOffset(main, "_splashFrameCount");
        const MethodInfo* drawSplash = a.class_get_method_from_name(main, "DrawSplash", 1);
        if (!g_intro.asyncLoadComplete || !g_intro.showSplash || !g_intro.splashTimer ||
            !g_intro.fadeCounter || !g_intro.spriteBatch || !g_intro.loadingFlower || !drawSplash) {
            BL_ERROR("startup: splash fields not found; normal intro");
            return;
        }
        if (!hook::install(drawSplash, hkDrawSplash, &g_origDrawSplash)) {
            BL_ERROR("startup: Main.DrawSplash could not be hooked; normal intro");
            return;
        }
        g_splashHooked.store(true, std::memory_order_release);
        BL_DEBUG("startup: splash hook installed (%.1f s)", sinceStart());
    });
}

void markProbeFinished() { g_probeFinished.store(true, std::memory_order_release); }

bool gameInitialized() {
    return g_splashOver.load(std::memory_order_acquire) || !g_splashHooked.load(std::memory_order_acquire) ||
           !staticBool(g_intro.showSplash);
}

void waitForGameLoaded(std::chrono::milliseconds max) {
    std::unique_lock<std::mutex> lock(g_loadedMutex);
    g_loadedCv.wait_for(lock, max, [] { return g_gameLoaded; });
}

bool isCoreReady() { return g_coreReady.load(std::memory_order_acquire); }

void markCoreReady() {
    g_coreReady.store(true, std::memory_order_release);
    if (config().fastBoot) BL_INFO("inicio rapido: mods carregados em %.1f s", sinceStart());
}

void tickQuickStart() {
    if (g_entryDone) return;
    const Config& c = config();
    const bool join = !c.joinAddress.empty() && !c.joinPlayer.empty();
    if (!join && (!c.fastBoot || c.quickPlayer.empty())) { g_entryDone = true; return; }
    if (!g_coreReady.load(std::memory_order_acquire)) return;

    if (g_entered) {
        if (inWorld()) {
            g_entryDone = true;
            BL_INFO("inicio rapido: no mundo em %.1f s", sinceStart());
        }
        return;
    }
    // Alguem entrou na mao antes de nos: nao ha o que fazer.
    if (inWorld()) { g_entryDone = true; return; }
    if (g_intro.showSplash && staticBool(g_intro.showSplash)) return;
    if (!contentSettled() || !contentFinished()) return;

    const double now = sinceStart();
    if (g_settledAt < 0) g_settledAt = now;
    const int32_t mode = menuMode();
    if (mode != 0) {
        // Um popup do jogo por cima do titulo (menuMode de outra tela):
        // espera fechar em vez de entrar por baixo dele.
        if (now - g_settledAt > kTitleWaitSeconds) {
            g_entryDone = true;
            BL_WARN("inicio rapido: o titulo nao ficou livre (menuMode %d); entrada cancelada", mode);
        }
        return;
    }
    if (join) {
        // Uma tentativa: daqui o jogo segue nas telas dele (conectando,
        // senha, erro).
        joinServer(c);
        g_entryDone = true;
        return;
    }
    if (c.quickWorld.empty()) {
        // Sem mundo escolhido: so a abertura rapida.
        g_entryDone = true;
        return;
    }
    enterWorld(c.quickPlayer, c.quickWorld);
    if (!g_entered) g_entryDone = true;
}

} // namespace bl::runtime
