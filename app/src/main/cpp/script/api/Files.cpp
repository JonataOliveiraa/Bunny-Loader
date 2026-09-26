#include "script/api/Files.h"

#if BL_HAVE_QUICKJS
#include "core/Config.h"
#include "core/Log.h"
#include "mods/ModLoader.h"
#include "script/api/Texture.h"

#include <dirent.h>
#include <sys/stat.h>
#include <unistd.h>

#include <algorithm>
#include <cerrno>
#include <cstdio>
#include <string>
#include <vector>

namespace bl::script {

namespace {

bool argString(JSContext* ctx, JSValueConst v, std::string* out) {
    const char* s = JS_ToCString(ctx, v);
    if (!s) return false;
    *out = s;
    JS_FreeCString(ctx, s);
    return true;
}

/** O caminho do argumento `i`, ja resolvido contra a pasta do mod. */
bool pathArg(JSContext* ctx, int argc, JSValueConst* argv, int i, const char* api, std::string* out) {
    std::string p;
    if (argc <= i || !argString(ctx, argv[i], &p) || p.empty()) {
        JS_ThrowTypeError(ctx, "%s: falta o caminho", api);
        return false;
    }
    *out = resolveModPath(ctx, p);
    return true;
}

bool isFile(const std::string& p) {
    struct stat st{};
    return stat(p.c_str(), &st) == 0 && S_ISREG(st.st_mode);
}

bool isDir(const std::string& p) {
    struct stat st{};
    return stat(p.c_str(), &st) == 0 && S_ISDIR(st.st_mode);
}

/** mkdir -p. */
bool makeDirs(const std::string& p) {
    if (p.empty()) return false;
    for (size_t i = 1; i <= p.size(); ++i) {
        if (i == p.size() || p[i] == '/') {
            const std::string part = p.substr(0, i);
            if (mkdir(part.c_str(), 0777) != 0 && errno != EEXIST) return false;
        }
    }
    return isDir(p);
}

bool removeTree(const std::string& p) {
    if (!isDir(p)) return unlink(p.c_str()) == 0;
    if (DIR* d = opendir(p.c_str())) {
        while (dirent* e = readdir(d)) {
            const std::string name = e->d_name;
            if (name == "." || name == "..") continue;
            removeTree(p + "/" + name);
        }
        closedir(d);
    }
    return rmdir(p.c_str()) == 0;
}

std::string parentOf(const std::string& p) {
    const size_t slash = p.find_last_of('/');
    if (slash == std::string::npos) return "";
    return slash == 0 ? "/" : p.substr(0, slash);
}

std::string nameOf(const std::string& p) {
    std::string s = p;
    while (s.size() > 1 && s.back() == '/') s.pop_back();
    const size_t slash = s.find_last_of('/');
    return slash == std::string::npos ? s : s.substr(slash + 1);
}

/** a + "/" + b sem barra dobrada; `b` absoluto recomeca. */
std::string joinTwo(const std::string& a, const std::string& b) {
    if (b.empty()) return a;
    if (a.empty() || b[0] == '/') return b;
    return a.back() == '/' ? a + b : a + "/" + b;
}

// A pasta do app (Android/data/com.bunnyloader): a mae de bunny_packs, onde
// tambem moram Players/, Worlds/ e logs/.
std::string appDirectory() { return parentOf(config().modsDir); }

bool readAll(const std::string& path, std::string* out) {
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return false;
    char buf[8192];
    size_t n;
    while ((n = std::fread(buf, 1, sizeof buf, f)) > 0) out->append(buf, n);
    std::fclose(f);
    return true;
}

bool writeAll(const std::string& path, const uint8_t* data, size_t size, const char* mode) {
    makeDirs(parentOf(path));
    FILE* f = std::fopen(path.c_str(), mode);
    if (!f) return false;
    const bool ok = std::fwrite(data, 1, size, f) == size;
    return std::fclose(f) == 0 && ok;
}

// ---------------------------------- bl.file ----------------------------------

JSValue file_exists(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (!pathArg(ctx, argc, argv, 0, "bl.file.exists", &p)) return JS_EXCEPTION;
    return JS_NewBool(ctx, isFile(p));
}

// Texto (UTF-8), ou undefined se o arquivo nao existe.
JSValue file_read(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p, txt;
    if (!pathArg(ctx, argc, argv, 0, "bl.file.read", &p)) return JS_EXCEPTION;
    if (!readAll(p, &txt)) return JS_UNDEFINED;
    return JS_NewStringLen(ctx, txt.data(), txt.size());
}

JSValue file_readBytes(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p, data;
    if (!pathArg(ctx, argc, argv, 0, "bl.file.readBytes", &p)) return JS_EXCEPTION;
    if (!readAll(p, &data)) return JS_UNDEFINED;
    return JS_NewUint8ArrayCopy(ctx, reinterpret_cast<const uint8_t*>(data.data()), data.size());
}

/** write/append: texto, ou bytes (Uint8Array/ArrayBuffer). Cria as pastas. */
JSValue writeImpl(JSContext* ctx, int argc, JSValueConst* argv, const char* api, const char* mode) {
    std::string p;
    if (!pathArg(ctx, argc, argv, 0, api, &p)) return JS_EXCEPTION;
    if (argc < 2) return JS_ThrowTypeError(ctx, "%s(caminho, dados)", api);
    size_t size = 0;
    const uint8_t* bytes = JS_IsString(argv[1]) ? nullptr : JS_GetUint8Array(ctx, &size, argv[1]);
    if (!bytes && !JS_IsString(argv[1])) {
        JS_FreeValue(ctx, JS_GetException(ctx));
        bytes = JS_GetArrayBuffer(ctx, &size, argv[1]);
        if (!bytes) JS_FreeValue(ctx, JS_GetException(ctx));
    }
    bool ok;
    if (bytes) {
        ok = writeAll(p, bytes, size, mode);
    } else {
        std::string txt;
        if (!argString(ctx, argv[1], &txt)) return JS_EXCEPTION;
        ok = writeAll(p, reinterpret_cast<const uint8_t*>(txt.data()), txt.size(), mode);
    }
    if (!ok) return JS_ThrowInternalError(ctx, "%s: nao consegui escrever %s", api, p.c_str());
    return JS_TRUE;
}

JSValue file_write(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    return writeImpl(ctx, argc, argv, "bl.file.write", "wb");
}

JSValue file_append(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    return writeImpl(ctx, argc, argv, "bl.file.append", "ab");
}

JSValue file_delete(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (!pathArg(ctx, argc, argv, 0, "bl.file.delete", &p)) return JS_EXCEPTION;
    return JS_NewBool(ctx, isFile(p) && unlink(p.c_str()) == 0);
}

// -------------------------------- bl.directory --------------------------------

JSValue dir_create(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (!pathArg(ctx, argc, argv, 0, "bl.directory.create", &p)) return JS_EXCEPTION;
    return JS_NewBool(ctx, makeDirs(p));
}

// Apaga a pasta e tudo dentro.
JSValue dir_delete(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (!pathArg(ctx, argc, argv, 0, "bl.directory.delete", &p)) return JS_EXCEPTION;
    return JS_NewBool(ctx, isDir(p) && removeTree(p));
}

JSValue dir_exists(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (!pathArg(ctx, argc, argv, 0, "bl.directory.exists", &p)) return JS_EXCEPTION;
    return JS_NewBool(ctx, isDir(p));
}

/**
 * Os arquivos (ou pastas) de `caminho`, em ordem, JA emendados ao caminho
 * como foi passado: listFiles('Textures/Bg') -> ['Textures/Bg/a.png', ...].
 */
JSValue listImpl(JSContext* ctx, int argc, JSValueConst* argv, bool dirs, const char* api) {
    std::string given, p;
    if (argc < 1 || !argString(ctx, argv[0], &given) || given.empty()) {
        return JS_ThrowTypeError(ctx, "%s: falta o caminho", api);
    }
    p = resolveModPath(ctx, given);
    std::vector<std::string> names;
    if (DIR* d = opendir(p.c_str())) {
        while (dirent* e = readdir(d)) {
            const std::string name = e->d_name;
            if (name == "." || name == "..") continue;
            if ((dirs ? isDir(p + "/" + name) : isFile(p + "/" + name))) names.push_back(name);
        }
        closedir(d);
    }
    std::sort(names.begin(), names.end());
    JSValue arr = JS_NewArray(ctx);
    for (size_t i = 0; i < names.size(); ++i) {
        const std::string full = joinTwo(given, names[i]);
        JS_SetPropertyUint32(ctx, arr, static_cast<uint32_t>(i), JS_NewString(ctx, full.c_str()));
    }
    return arr;
}

JSValue dir_listFiles(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    return listImpl(ctx, argc, argv, false, "bl.directory.listFiles");
}

JSValue dir_listDirectories(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    return listImpl(ctx, argc, argv, true, "bl.directory.listDirectories");
}

// ---------------------------------- bl.path ----------------------------------

JSValue path_join(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string out;
    for (int i = 0; i < argc; ++i) {
        std::string part;
        if (!argString(ctx, argv[i], &part)) return JS_EXCEPTION;
        out = joinTwo(out, part);
    }
    return JS_NewString(ctx, out.c_str());
}

JSValue path_getName(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (argc < 1 || !argString(ctx, argv[0], &p)) return JS_ThrowTypeError(ctx, "bl.path.getName(caminho)");
    return JS_NewString(ctx, nameOf(p).c_str());
}

JSValue path_getParentPath(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (argc < 1 || !argString(ctx, argv[0], &p)) return JS_ThrowTypeError(ctx, "bl.path.getParentPath(caminho)");
    return JS_NewString(ctx, parentOf(p).c_str());
}

// Com o ponto: '.png'; '' se nao ha.
JSValue path_getExtension(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string p;
    if (argc < 1 || !argString(ctx, argv[0], &p)) return JS_ThrowTypeError(ctx, "bl.path.getExtension(caminho)");
    const std::string name = nameOf(p);
    const size_t dot = name.find_last_of('.');
    return JS_NewString(ctx, dot == std::string::npos || dot == 0 ? "" : name.substr(dot).c_str());
}

// ----------------------------------- bl.mod -----------------------------------

void setStr(JSContext* ctx, JSValue obj, const char* key, const std::string& v) {
    JS_SetPropertyStr(ctx, obj, key, JS_NewString(ctx, v.c_str()));
}

// O `bl.mod` e o `ModLoader` moram no ModClasses.js (o objeto Mod de cada
// pacote e JS: a classe que o mod registra vira ele). Daqui saem so os dados.

/**
 * bl.__mods(): todos os mods do registro, na ordem de carga — uuid, id (do
 * manifesto), name, version, path (pasta do main.js), root (a do pacote) e
 * state: 'pending' (o main.js ainda nao rodou), 'loaded' ou 'failed'.
 */
JSValue mods_list(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    JSValue arr = JS_NewArray(ctx);
    uint32_t n = 0;
    for (size_t i = 0; i < mods::loadedCount(); ++i) {
        const mods::LoadedMod* m = mods::get(static_cast<uint16_t>(i));
        JSValue o = JS_NewObject(ctx);
        setStr(ctx, o, "uuid", m->id);
        setStr(ctx, o, "id", m->internalName);
        setStr(ctx, o, "name", m->displayName);
        setStr(ctx, o, "version", m->version);
        setStr(ctx, o, "path", mods::dirOf(m->id));
        setStr(ctx, o, "root", m->dir);
        setStr(ctx, o, "state", !m->enabled ? "failed" : m->loaded ? "loaded" : "pending");
        JS_SetPropertyUint32(ctx, arr, n++, o);
    }
    return arr;
}

/** bl.__callerMod(): o uuid do mod de QUEM CHAMA (o bl e um so para todos). */
JSValue mods_caller(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    const std::string id = callerModId(ctx);
    return id.empty() ? JS_UNDEFINED : JS_NewString(ctx, id.c_str());
}

/**
 * bl.__modDataDirectory(uuid): `Android/data/com.bunnyloader/mod_data/<uuid>`,
 * criada aqui. So para mod do registro: o uuid vira nome de pasta.
 */
JSValue mods_dataDirectory(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string id;
    if (argc < 1 || !argString(ctx, argv[0], &id) || !mods::find(id)) return JS_UNDEFINED;
    const std::string data = joinTwo(appDirectory(), "mod_data/" + id);
    makeDirs(data);
    return JS_NewString(ctx, data.c_str());
}

} // namespace

void installFilesApi(JSContext* ctx, JSValue bl) {
    static const JSCFunctionListEntry fileFns[] = {
        JS_CFUNC_DEF("exists", 1, file_exists),
        JS_CFUNC_DEF("read", 1, file_read),
        JS_CFUNC_DEF("readBytes", 1, file_readBytes),
        JS_CFUNC_DEF("write", 2, file_write),
        JS_CFUNC_DEF("append", 2, file_append),
        JS_CFUNC_DEF("delete", 1, file_delete),
    };
    static const JSCFunctionListEntry dirFns[] = {
        JS_CFUNC_DEF("create", 1, dir_create),
        JS_CFUNC_DEF("delete", 1, dir_delete),
        JS_CFUNC_DEF("exists", 1, dir_exists),
        JS_CFUNC_DEF("listFiles", 1, dir_listFiles),
        JS_CFUNC_DEF("listDirectories", 1, dir_listDirectories),
    };
    static const JSCFunctionListEntry pathFns[] = {
        JS_CFUNC_DEF("join", 2, path_join),
        JS_CFUNC_DEF("getName", 1, path_getName),
        JS_CFUNC_DEF("getParentPath", 1, path_getParentPath),
        JS_CFUNC_DEF("getExtension", 1, path_getExtension),
    };
    auto group = [&](const char* name, const JSCFunctionListEntry* fns, int n) {
        JSValue o = JS_NewObject(ctx);
        JS_SetPropertyFunctionList(ctx, o, fns, n);
        JS_SetPropertyStr(ctx, bl, name, o);
    };
    group("file", fileFns, sizeof fileFns / sizeof fileFns[0]);
    group("directory", dirFns, sizeof dirFns / sizeof dirFns[0]);
    group("path", pathFns, sizeof pathFns / sizeof pathFns[0]);

    static const JSCFunctionListEntry modFns[] = {
        JS_CFUNC_DEF("__mods", 0, mods_list),
        JS_CFUNC_DEF("__callerMod", 0, mods_caller),
        JS_CFUNC_DEF("__modDataDirectory", 1, mods_dataDirectory),
    };
    JS_SetPropertyFunctionList(ctx, bl, modFns, sizeof modFns / sizeof modFns[0]);

    JSValue info = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, info, "terrariaVersionCode",
                      JS_NewInt64(ctx, static_cast<int64_t>(config().gameVersion)));
    setStr(ctx, info, "appDirectory", appDirectory());
    setStr(ctx, info, "logsDirectory", joinTwo(appDirectory(), "logs"));
    JS_SetPropertyStr(ctx, bl, "info", info);
}

} // namespace bl::script
#endif
