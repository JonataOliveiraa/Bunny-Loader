#pragma once
#include <string>

namespace bl::dotnet {

/**
 * Os mods C#: um runtime .NET (Mono) no processo do jogo, ao lado do IL2CPP e
 * do QuickJS.
 *
 * Sobe so quando ha um mod C# ligado (o primeiro loadMod), na thread da sonda.
 * O C# fala com o jogo por uma tabela de ponteiros nativos (NativeApi, em
 * DotnetHost.cpp, espelhada em app/src/main/csharp/BunnyLoader/NativeApi.cs), e o
 * nativo chama o C# pelos [UnmanagedCallersOnly] do Host: nada de
 * mono_runtime_invoke no caminho quente.
 */
class DotnetHost {
public:
    /** Carrega um mod C# (o .dll de entrada). Sobe o runtime se preciso. */
    bool loadMod(const std::string& id, const std::string& path);

    /**
     * Fim da carga dos mods: a thread da sonda sai do registro do Mono antes
     * de morrer (registrada e morta, o coletor dele varreria uma pilha que
     * nao existe mais).
     */
    void finishLoading();

    bool started() const { return started_; }

    /**
     * Codigo C# do console do Editor. O C# compila fora da thread do jogo e
     * guarda o resultado; false se o runtime nao subiu (nenhum mod C#).
     */
    bool consoleSubmit(const std::string& code);

    /** Thread do jogo: roda o codigo do console que ja compilou. */
    void consoleTick();

private:
    bool start();

    bool started_ = false;
    bool failed_ = false;
    int (*loadModFn_)(const char* id, const char* path) = nullptr;
    void (*consoleSubmitFn_)(const char* code) = nullptr;
    void (*consoleTickFn_)() = nullptr;
};

DotnetHost& host();

/** O arquivo e um assembly .NET (`.dll`)? */
bool isAssemblyEntry(const std::string& entry);

/** O mod e C#: um `.dll` pronto ou um `.cs` (o Host compila as fontes se o bin/ nao bater). */
bool isCSharpEntry(const std::string& entry);

} // namespace bl::dotnet
