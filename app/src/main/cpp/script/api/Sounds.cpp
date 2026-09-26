#include "script/api/Sounds.h"

#if BL_HAVE_QUICKJS
#include "content/sounds/AndroidAudio.h"
#include "core/Log.h"
#include "script/api/Texture.h"

#include <sys/stat.h>

#include <string>

namespace bl::script {

namespace {

bool argInt(JSContext* ctx, int argc, JSValueConst* argv, int i, int32_t* out) {
    return argc > i && JS_ToInt32(ctx, out, argv[i]) == 0;
}

bool argFloat(JSContext* ctx, int argc, JSValueConst* argv, int i, float* out) {
    double d = 0;
    if (argc <= i || JS_ToFloat64(ctx, &d, argv[i]) != 0) return false;
    *out = static_cast<float>(d);
    return true;
}

JSValue sounds_load(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsString(argv[0])) return JS_ThrowTypeError(ctx, "bl.sounds.load(caminho) espera um texto");
    const char* cs = JS_ToCString(ctx, argv[0]);
    if (!cs) return JS_EXCEPTION;
    const std::string path = resolveModPath(ctx, cs);
    JS_FreeCString(ctx, cs);
    struct stat st{};
    if (stat(path.c_str(), &st) != 0 || !S_ISREG(st.st_mode)) {
        return JS_ThrowReferenceError(ctx, "bl.sounds.load: nao achei %s", path.c_str());
    }
    const int id = runtime::content::loadSound(path);
    if (id <= 0) return JS_ThrowInternalError(ctx, "bl.sounds.load: o Android recusou %s (ver o log)", path.c_str());
    BL_DEBUG("sons: carregando %s (id %d)", path.c_str(), id);
    return JS_NewInt32(ctx, id);
}

JSValue sounds_state(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t id = 0;
    if (!argInt(ctx, argc, argv, 0, &id)) return JS_ThrowTypeError(ctx, "bl.sounds.state(id)");
    return JS_NewInt32(ctx, runtime::content::soundState(id));
}

JSValue sounds_play(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t id = 0;
    float left = 1, right = 1, rate = 1;
    if (!argInt(ctx, argc, argv, 0, &id)) return JS_ThrowTypeError(ctx, "bl.sounds.play(id, esquerda, direita, velocidade)");
    if (argc > 1 && !argFloat(ctx, argc, argv, 1, &left)) return JS_EXCEPTION;
    if (argc > 2 && !argFloat(ctx, argc, argv, 2, &right)) return JS_EXCEPTION;
    if (argc > 3 && !argFloat(ctx, argc, argv, 3, &rate)) return JS_EXCEPTION;
    auto clamp = [](float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; };
    return JS_NewInt32(ctx, runtime::content::playSound(id, clamp(left, 0, 1), clamp(right, 0, 1),
                                                       clamp(rate, 0.5f, 2.0f)));
}

JSValue sounds_stop(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t stream = 0;
    if (!argInt(ctx, argc, argv, 0, &stream)) return JS_ThrowTypeError(ctx, "bl.sounds.stop(stream)");
    runtime::content::stopSound(stream);
    return JS_UNDEFINED;
}

JSValue sounds_duration(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t id = 0;
    if (!argInt(ctx, argc, argv, 0, &id)) return JS_ThrowTypeError(ctx, "bl.sounds.duration(id)");
    return JS_NewInt32(ctx, runtime::content::soundDuration(id));
}

JSValue music_register(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsString(argv[0])) return JS_ThrowTypeError(ctx, "bl.music.register(caminho) espera um texto");
    const char* cs = JS_ToCString(ctx, argv[0]);
    if (!cs) return JS_EXCEPTION;
    const std::string path = resolveModPath(ctx, cs);
    JS_FreeCString(ctx, cs);
    struct stat st{};
    if (stat(path.c_str(), &st) != 0 || !S_ISREG(st.st_mode)) {
        return JS_ThrowReferenceError(ctx, "bl.music.register: nao achei %s", path.c_str());
    }
    const int id = runtime::content::registerMusic(path);
    if (id <= 0) return JS_ThrowInternalError(ctx, "bl.music.register: o Android recusou %s (ver o log)", path.c_str());
    BL_DEBUG("musica: %s (id %d)", path.c_str(), id);
    return JS_NewInt32(ctx, id);
}

JSValue music_setVolume(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t id = 0;
    float volume = 0;
    if (!argInt(ctx, argc, argv, 0, &id) || !argFloat(ctx, argc, argv, 1, &volume)) {
        return JS_ThrowTypeError(ctx, "bl.music.setVolume(id, volume)");
    }
    runtime::content::setMusicVolume(id, volume < 0 ? 0 : volume > 1 ? 1 : volume);
    return JS_UNDEFINED;
}

JSValue music_state(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    int32_t id = 0;
    if (!argInt(ctx, argc, argv, 0, &id)) return JS_ThrowTypeError(ctx, "bl.music.state(id)");
    return JS_NewInt32(ctx, runtime::content::musicState(id));
}

} // namespace

void installSoundsApi(JSContext* ctx, JSValue bl) {
    static const JSCFunctionListEntry soundFns[] = {
        JS_CFUNC_DEF("load", 1, sounds_load),
        JS_CFUNC_DEF("state", 1, sounds_state),
        JS_CFUNC_DEF("duration", 1, sounds_duration),
        JS_CFUNC_DEF("play", 4, sounds_play),
        JS_CFUNC_DEF("stop", 1, sounds_stop),
    };
    static const JSCFunctionListEntry musicFns[] = {
        JS_CFUNC_DEF("register", 1, music_register),
        JS_CFUNC_DEF("setVolume", 2, music_setVolume),
        JS_CFUNC_DEF("state", 1, music_state),
    };
    JSValue sounds = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, sounds, soundFns, sizeof soundFns / sizeof soundFns[0]);
    JS_SetPropertyStr(ctx, bl, "sounds", sounds);
    JSValue music = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, music, musicFns, sizeof musicFns / sizeof musicFns[0]);
    JS_SetPropertyStr(ctx, bl, "music", music);
}

} // namespace bl::script
#endif
