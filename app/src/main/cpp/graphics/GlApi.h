#pragma once
#include <GLES3/gl3.h>

namespace bl::graphics {

// As funcoes GL que os efeitos de tela usam, pegas por dlsym da libGLESv2 que
// a Unity ja carregou. Sem link direto: se faltar alguma (aparelho estranho,
// tradutor do emulador), os efeitos so ficam desligados.
#define BL_GL_FUNCS(X) \
    X(GetIntegerv) X(GetBooleanv) X(GetString) X(GetError) X(IsEnabled) X(Enable) X(Disable) \
    X(ColorMask) X(Viewport) X(UseProgram) X(ActiveTexture) X(BindTexture) \
    X(BindSampler) X(BindBuffer) X(BindVertexArray) X(BindFramebuffer) \
    X(GenTextures) X(TexImage2D) X(TexParameteri) X(GenFramebuffers) \
    X(FramebufferTexture2D) X(CheckFramebufferStatus) X(BlitFramebuffer) \
    X(CopyTexSubImage2D) X(GetFramebufferAttachmentParameteriv) \
    X(GenVertexArrays) X(GenBuffers) X(BufferData) X(VertexAttribPointer) \
    X(EnableVertexAttribArray) X(CreateShader) X(ShaderSource) X(CompileShader) \
    X(GetShaderiv) X(GetShaderInfoLog) X(DeleteShader) X(CreateProgram) \
    X(AttachShader) X(LinkProgram) X(GetProgramiv) X(GetProgramInfoLog) \
    X(GetUniformLocation) X(Uniform1i) X(Uniform1f) X(Uniform2f) X(Uniform3f) X(Uniform4f) \
    X(Uniform1fv) X(Uniform2fv) X(Uniform3fv) X(Uniform4fv) \
    X(DrawArrays) X(DrawElements) X(Clear) X(GenSamplers) X(SamplerParameteri) X(PixelStorei)

struct GlApi {
#define BL_GL_SLOT(name) decltype(&::gl##name) name = nullptr;
    BL_GL_FUNCS(BL_GL_SLOT)
#undef BL_GL_SLOT
    void* (*eglGetCurrentContext)() = nullptr;

    // Uma vez; depois so devolve o resultado.
    bool load();
};

GlApi& gl();

} // namespace bl::graphics
