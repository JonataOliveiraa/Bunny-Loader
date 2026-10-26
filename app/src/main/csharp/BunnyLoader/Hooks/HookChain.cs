using System;
using System.Collections.Generic;
using System.Threading;

namespace BunnyLoader.Hooks;

// Os hooks de UM metodo do jogo, no modelo do On_ do tModLoader: cada um
// recebe o `orig` e decide se e quando chama-lo. O ultimo adicionado roda por
// fora. A cadeia e montada quando a lista muda, nunca por chamada.
//
// O codigo gerado (On_<Tipo>) entrega tres pecas por metodo: `wrap`, que
// encaixa um hook na frente do proximo; `native`, o `orig` mais de dentro
// (chama o metodo do jogo); e `install`, que poe o trampolim nativo no metodo
// na primeira vez que alguem se inscreve.
public sealed class HookChain<THook, TOrig> where THook : System.Delegate where TOrig : System.Delegate
{
    readonly object gate = new();
    readonly List<THook> hooks = new();
    readonly Func<THook, TOrig, TOrig> wrap;
    readonly TOrig native;
    readonly Action install;
    bool installed;
    TOrig? chain;

    public HookChain(Func<THook, TOrig, TOrig> wrap, TOrig native, Action install)
    {
        this.wrap = wrap;
        this.native = native;
        this.install = install;
    }

    // A cadeia atual; null sem nenhum hook (o trampolim chama o jogo direto).
    public TOrig? Chain => Volatile.Read(ref chain);

    public int Count => hooks.Count;

    public void Add(THook hook)
    {
        lock (gate)
        {
            if (!installed)
            {
                install();
                installed = true;
            }
            hooks.Add(hook);
            Rebuild();
        }
    }

    public void Remove(THook hook)
    {
        lock (gate)
        {
            if (hooks.Remove(hook)) Rebuild();
        }
    }

    void Rebuild()
    {
        if (hooks.Count == 0)
        {
            Volatile.Write(ref chain, null);
            return;
        }
        TOrig next = native;
        foreach (THook hook in hooks) next = wrap(hook, next);
        Volatile.Write(ref chain, next);
    }
}

// Erro que escapou de um hook: vai ao log de erro (o painel dentro do jogo) e
// o metodo do jogo segue, como no hook JS.
public static class HookErrors
{
    public static void Report(string method, Exception error) => Log.Error($"hook {method}: {error}");
}
