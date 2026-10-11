#pragma once
#include "il2cpp/Api.h"

#include <string>

namespace bl::il2cpp {

/** O que uma chamada guardada pegou. */
struct Caught {
    bool threw = false;
    // A excecao C# (um Il2CppException), quando quem lancou foi o IL2CPP.
    Il2CppObject* exception = nullptr;
    // Senao, o nome (mangled) do tipo C++ lancado, se deu para ler.
    const char* cppType = nullptr;
};

/**
 * Roda `body(arg)` e para ali qualquer excecao que venha de dentro dele.
 *
 * Uma excecao C# no IL2CPP e uma excecao C++ de verdade
 * (`throw Il2CppExceptionWrapper`), lancada pelo __cxa_throw DO libil2cpp,
 * que traz o desenrolador do libgcc. A libbunny traz o LLVM libunwind
 * (estatico, do NDK). Um `catch` compilado aqui chama a personalidade da
 * libbunny, que entrega o contexto do libgcc ao libunwind dela, que o le como
 * um `unw_cursor_t`: a vtable e lixo e o processo cai (pc 0x827). Nao e coisa
 * do emulador, e o mesmo no celular.
 *
 * Aqui nao ha `catch`. Um trampolim em assembly (GuardedCall.S) declara uma
 * personalidade NOSSA que nunca toca no contexto: na fase de busca diz "o
 * tratador e este", e na fase de limpeza (quando os frames do jogo abaixo ja
 * rodaram os `finally` deles) salta de volta para o trampolim, como um
 * longjmp. Funciona com qualquer desenrolador que tenha lancado.
 *
 * `body` NAO pode ter destrutor pendente entre ele e o codigo do jogo: o salto
 * descarta esses frames sem limpar. Na pratica, o corpo so monta a chamada e
 * guarda o resultado.
 *
 * @return true se `body` voltou normalmente; false se lancou (e `caught`,
 *         se dado, diz o que).
 */
bool guardedCall(void (*body)(void*), void* arg, Caught* caught = nullptr);

/** O mesmo, com uma lambda (ou qualquer chamavel sem argumento). */
template <class F>
bool guarded(F& fn, Caught* caught = nullptr) {
    return guardedCall([](void* p) { (*static_cast<F*>(p))(); }, &fn, caught);
}

/**
 * Texto de uma excecao pega: "System.IndexOutOfRangeException: Index was
 * outside the bounds of the array." (il2cpp_format_exception), ou o tipo C++.
 */
std::string describeCaught(const Caught& c);

/** O mesmo para uma excecao que o runtime_invoke devolveu. */
std::string describeException(Il2CppObject* exception);

} // namespace bl::il2cpp
