#include "script/api/Texture.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "mods/ModLoader.h"
#include "script/bridge/Bridge.h"
#include "boot/Boot.h"
#include "content/common/ContentAssets.h"
#include "script/bridge/Invoke.h"

#include <unistd.h>

#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

namespace bl::script {

namespace {

/**
 * Este build do Terraria NAO e XNA de prateleira: e uma reimplementacao sobre a
 * Unity, e o `Texture2D` do jogo EMBRULHA um `UnityEngine.Texture2D`. Nao ha
 * `Texture2D.FromStream`. O caminho que existe e:
 *
 *   1. UnityEngine.Texture2D(2, 2)            textura vazia, o tamanho e
 *                                             corrigido pelo passo 2
 *   2. ImageConversion.LoadImage(tex, bytes)  decodifica o PNG/JPG
 *   3. Terraria Texture2D(.ctor(Texture2D))   embrulha para o jogo desenhar
 *
 * O passo 2 e da Unity, nao do jogo, entao aceita o que a Unity aceita: PNG e
 * JPG. Nao passa por atlas nem por AssetBundle.
 */
struct Refs {
    bool tried = false;
    bool ok = false;
    Il2CppClass* byteCls = nullptr;
    Il2CppClass* unityTex = nullptr;
    Il2CppClass* gameTex = nullptr;
    const MethodInfo* unityCtor = nullptr;
    const MethodInfo* loadImage = nullptr;
    const MethodInfo* gameCtor = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;

    r.byteCls  = il2cpp::findClass({"System", "Byte", {}});
    r.unityTex = il2cpp::findClass({"UnityEngine", "Texture2D", {}});
    r.gameTex  = il2cpp::findClass({"Microsoft.Xna.Framework.Graphics", "Texture2D", {}});
    Il2CppClass* conv = il2cpp::findClass({"UnityEngine", "ImageConversion", {}});
    if (!r.byteCls || !r.unityTex || !r.gameTex || !conv) {
        BL_ERROR("loadTexture: classe faltando (Byte=%p UnityTexture2D=%p GameTexture2D=%p "
                 "ImageConversion=%p)", (void*)r.byteCls, (void*)r.unityTex,
                 (void*)r.gameTex, (void*)conv);
        return r;
    }

    r.unityCtor = il2cpp::findMethodBySignature(
        r.unityTex, il2cpp::parseSignature("void .ctor(int width, int height)"));
    r.loadImage = il2cpp::findMethodBySignature(
        conv, il2cpp::parseSignature(
            "bool LoadImage(Texture2D tex, byte[] data, bool markNonReadable)"));
    // O jogo tem varios .ctor(Texture2D ...); este e o de um argumento so.
    r.gameCtor = il2cpp::findMethodBySignature(
        r.gameTex, il2cpp::parseSignature("void .ctor(Texture2D texture)"));

    r.ok = r.unityCtor && r.loadImage && r.gameCtor;
    if (!r.ok) {
        BL_ERROR("loadTexture: metodo faltando (ctor=%p LoadImage=%p wrap=%p)",
                 (void*)r.unityCtor, (void*)r.loadImage, (void*)r.gameCtor);
    }
    return r;
}

/**
 * O uid do mod dono de um modulo. O main.js tem o uid como nome; um arquivo
 * que ele importa, "<uid>/<caminho dentro do mod>" (ver o carregador de
 * modulos em ScriptEngine.cpp).
 */
std::string modIdOfModule(const char* name) {
    if (!name) return {};
    const char* slash = std::strchr(name, '/');
    return slash ? std::string(name, static_cast<size_t>(slash - name)) : std::string(name);
}

// Quantos frames da pilha JS olhar atras do mod. Chamadas que passam pelas
// classes do loader (ModItem.register -> bl.items.register) poem frames
// delas no meio.
constexpr int kCallerLevels = 8;

/**
 * Caminho relativo vale a partir da pasta do mod de QUEM CHAMOU.
 *
 * Nao da para usar so "o mod que esta carregando": o jeito recomendado de
 * carregar textura e dentro de um hook, na primeira chamada, e ai a carga ja
 * acabou ha muito. O QuickJS sabe de qual modulo veio a chamada, e o nome do
 * modulo e o uid do mod — que e a chave da pasta.
 */
std::string resolvePath(JSContext* ctx, const char* path) {
    std::string p = path ? path : "";
    if (p.empty() || p[0] == '/') return p;

    for (int level = 0; level < kCallerLevels; ++level) {
        JSAtom atom = JS_GetScriptOrModuleName(ctx, level);
        if (atom == JS_ATOM_NULL) continue;
        const char* name = JS_AtomToCString(ctx, atom);
        std::string base = name ? mods::dirOf(modIdOfModule(name)) : std::string();
        if (name) JS_FreeCString(ctx, name);
        JS_FreeAtom(ctx, atom);
        if (!base.empty()) return base + "/" + p;
    }
    // Durante a carga o modulo ainda nao esta no mapa; a pasta corrente serve.
    const std::string& base = mods::currentDir();
    return base.empty() ? p : base + "/" + p;
}

/** Le o arquivo direto para dentro de um byte[] do heap do jogo. */
Il2CppArray* readIntoByteArray(JSContext* ctx, const std::string& path) {
    auto& a = il2cpp::api();
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) {
        JS_ThrowInternalError(ctx, "loadTexture: nao abri '%s'", path.c_str());
        return nullptr;
    }
    std::fseek(f, 0, SEEK_END);
    long n = std::ftell(f);
    std::fseek(f, 0, SEEK_SET);
    if (n <= 0) {
        std::fclose(f);
        JS_ThrowInternalError(ctx, "loadTexture: '%s' esta vazio", path.c_str());
        return nullptr;
    }
    Il2CppArray* arr = a.array_new(refs().byteCls, static_cast<uintptr_t>(n));
    if (!arr) {
        std::fclose(f);
        JS_ThrowInternalError(ctx, "loadTexture: array_new falhou (%ld bytes)", n);
        return nullptr;
    }
    size_t bytesRead = std::fread(arrayData(arr), 1, static_cast<size_t>(n), f);
    std::fclose(f);
    if (bytesRead != static_cast<size_t>(n)) {
        JS_ThrowInternalError(ctx, "loadTexture: li %zu de %ld bytes", bytesRead, n);
        return nullptr;
    }
    return arr;
}

/** Chama um metodo e devolve false com a excecao ja posta no ctx. */
bool callMethod(JSContext* ctx, const MethodInfo* m, void* self, int argc, JSValueConst* argv,
            JSValue* out) {
    JSValue r = invokeMethod(ctx, m, self, argc, argv);
    if (JS_IsException(r)) return false;
    if (out) *out = r; else JS_FreeValue(ctx, r);
    return true;
}

} // namespace

std::string resolveModPath(JSContext* ctx, const std::string& path) {
    return resolvePath(ctx, path.c_str());
}

std::string callerModId(JSContext* ctx) {
    for (int level = 0; level < kCallerLevels; ++level) {
        JSAtom atom = JS_GetScriptOrModuleName(ctx, level);
        if (atom == JS_ATOM_NULL) continue;
        const char* name = JS_AtomToCString(ctx, atom);
        std::string id = name ? modIdOfModule(name) : std::string();
        if (!id.empty() && mods::dirOf(id).empty()) id.clear();
        if (name) JS_FreeCString(ctx, name);
        JS_FreeAtom(ctx, atom);
        if (!id.empty()) return id;
    }
    return mods::currentId();
}

JSValue loadTexture(JSContext* ctx, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsString(argv[0])) {
        return JS_ThrowTypeError(ctx, "bl.loadTexture(caminho) espera um texto");
    }
    // A Unity aborta o processo inteiro se uma textura nascer fora da thread do
    // jogo — nao lanca excecao, chama abort() dentro da libunity, e o log so
    // mostra frames sem nome.
    //
    // A recusa e FECHADA: so passa quando sabemos estar na thread certa. Na
    // carga dos mods o DoUpdate ainda nao rodou uma vez sequer, entao a thread
    // do jogo e desconhecida — e deixar passar "na duvida" e exatamente o caso
    // que derruba o processo.
    const int gameThread = runtime::gameThreadId();
    if (gameThread == 0 || static_cast<int>(gettid()) != gameThread) {
        return JS_ThrowInternalError(
            ctx, "bl.loadTexture so vale na thread do jogo, com o jogo ja rodando "
                 "— e mods carregam antes disso, noutra thread. Carregue dentro "
                 "de um hook, na primeira chamada, em vez de no topo do arquivo.");
    }

    Refs& r = refs();
    if (!r.ok) {
        return JS_ThrowInternalError(
            ctx, "bl.loadTexture: o runtime do jogo nao tem as classes de textura "
                 "esperadas; ver o log");
    }

    const char* cs = JS_ToCString(ctx, argv[0]);
    if (!cs) return JS_EXCEPTION;
    std::string path = resolvePath(ctx, cs);
    JS_FreeCString(ctx, cs);

    auto& a = il2cpp::api();
    Il2CppArray* bytes = readIntoByteArray(ctx, path);
    if (!bytes) return JS_EXCEPTION;

    // 1. textura vazia da Unity. O 2x2 e provisorio: o LoadImage redimensiona
    //    para o tamanho real do arquivo.
    Il2CppObject* ut = a.object_new(r.unityTex);
    if (!ut) return JS_ThrowInternalError(ctx, "loadTexture: object_new falhou");
    JSValue jsUt = makeNativeObject(ctx, ut);
    JSValue jsArr = makeGameArray(ctx, bytes);

    JSValue two[2] = {JS_NewInt32(ctx, 2), JS_NewInt32(ctx, 2)};
    bool ok = callMethod(ctx, r.unityCtor, ut, 2, two, nullptr);
    JS_FreeValue(ctx, two[0]);
    JS_FreeValue(ctx, two[1]);

    // 2. decodifica o PNG/JPG dentro dela.
    JSValue res = JS_UNDEFINED;
    if (ok) {
        JSValue args[3] = {jsUt, jsArr, JS_FALSE};
        ok = callMethod(ctx, r.loadImage, nullptr, 3, args, &res);
    }
    bool decoded = ok && JS_ToBool(ctx, res) > 0;
    JS_FreeValue(ctx, res);
    if (ok && !decoded) {
        JS_ThrowInternalError(ctx, "loadTexture: '%s' nao e um PNG/JPG que a Unity leia",
                              path.c_str());
        ok = false;
    }

    // 3. embrulha no Texture2D do jogo, que e o que o SpriteBatch aceita.
    Il2CppObject* gt = nullptr;
    if (ok) {
        gt = a.object_new(r.gameTex);
        ok = gt && callMethod(ctx, r.gameCtor, gt, 1, &jsUt, nullptr);
    }

    JS_FreeValue(ctx, jsUt);
    JS_FreeValue(ctx, jsArr);
    if (!ok) return JS_EXCEPTION;

    BL_DEBUG("loadTexture: %s", path.c_str());
    return makeNativeObject(ctx, gt);
}

JSValue loadTextureAsset(JSContext* ctx, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsString(argv[0])) {
        return JS_ThrowTypeError(ctx, "bl.loadTextureAsset(caminho) espera um texto");
    }
    // A mesma trava do loadTexture: textura da Unity so nasce na thread do jogo.
    const int gameThread = runtime::gameThreadId();
    if (gameThread == 0 || static_cast<int>(gettid()) != gameThread) {
        return JS_ThrowInternalError(ctx, "bl.loadTextureAsset so vale na thread do jogo, com o jogo ja "
                                          "rodando (num hook, ou no bl.onContentReady)");
    }
    const char* cs = JS_ToCString(ctx, argv[0]);
    if (!cs) return JS_EXCEPTION;
    const std::string path = resolvePath(ctx, cs);
    JS_FreeCString(ctx, cs);
    int w = 0, h = 0;
    Il2CppObject* asset = runtime::content::loadTextureAsset(path, nullptr, 0, callerModId(ctx) + ":" + path, &w, &h);
    if (!asset) return JS_ThrowReferenceError(ctx, "bl.loadTextureAsset: nao consegui carregar %s", path.c_str());
    return makeNativeObject(ctx, asset);
}

} // namespace bl::script
#endif
