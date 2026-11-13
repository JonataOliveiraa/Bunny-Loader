#include "graphics/GlApi.h"
#include "core/Log.h"
#include <dlfcn.h>

namespace bl::graphics {

GlApi& gl() {
    static GlApi instance;
    return instance;
}

bool GlApi::load() {
    static int state = 0;
    if (state != 0) return state > 0;
    state = -1;

    void* gles = dlopen("libGLESv2.so", RTLD_NOW);
    void* egl = dlopen("libEGL.so", RTLD_NOW);
    if (!gles || !egl) {
        BL_ERROR("screen effects: libGLESv2/libEGL did not open; effects disabled");
        return false;
    }
#define BL_GL_BIND(name)                                                          \
    name = reinterpret_cast<decltype(name)>(dlsym(gles, "gl" #name));             \
    if (!name) {                                                                  \
        BL_ERROR("screen effects: gl" #name " missing; effects disabled");     \
        return false;                                                             \
    }
    BL_GL_FUNCS(BL_GL_BIND)
#undef BL_GL_BIND
    eglGetCurrentContext = reinterpret_cast<void* (*)()>(dlsym(egl, "eglGetCurrentContext"));
    if (!eglGetCurrentContext) return false;

    state = 1;
    return true;
}

}
