#include "graphics/GlApi.h"
#include "core/Log.h"
#include <dlfcn.h>

namespace bl::graphics {

GlApi& gl() {
    static GlApi instance;
    return instance;
}

bool GlApi::load() {
    static int state = 0;   // 0 nao tentou, 1 ok, -1 falhou
    if (state != 0) return state > 0;
    state = -1;

    void* gles = dlopen("libGLESv2.so", RTLD_NOW);
    void* egl = dlopen("libEGL.so", RTLD_NOW);
    if (!gles || !egl) {
        BL_ERROR("efeitos de tela: libGLESv2/libEGL nao abriram; efeitos desligados");
        return false;
    }
#define BL_GL_BIND(name)                                                          \
    name = reinterpret_cast<decltype(name)>(dlsym(gles, "gl" #name));             \
    if (!name) {                                                                  \
        BL_ERROR("efeitos de tela: gl" #name " ausente; efeitos desligados");     \
        return false;                                                             \
    }
    BL_GL_FUNCS(BL_GL_BIND)
#undef BL_GL_BIND
    eglGetCurrentContext = reinterpret_cast<void* (*)()>(dlsym(egl, "eglGetCurrentContext"));
    if (!eglGetCurrentContext) return false;

    state = 1;
    return true;
}

} // namespace bl::graphics
