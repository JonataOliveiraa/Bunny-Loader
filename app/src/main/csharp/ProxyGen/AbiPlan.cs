using System.Collections.Generic;

namespace ProxyGen;

enum ArgMode { Value, Ref, Out, In, This, ThisRef, MethodInfo }

enum Placement { X, D, HfaD, CompositeX, ByRefCopy, Pointer }

// Um argumento de uma chamada nativa e onde ele mora (AAPCS64). Casas X de 8
// a 15 sao a pilha (CallFrame.X, script/dotnet/DotnetCall.h).
sealed class ArgPlan
{
    public string Name = "";
    public TypeUse Use = null!;
    public ArgMode Mode;
    public Placement Place;
    public int X = -1, XCount;
    public int D = -1, DCount;
    // Os parametros crus do trampolim ([UnmanagedCallersOnly]): tipo e nome.
    public List<(string Type, string Name)> Raw = new();
}

sealed class RetPlan
{
    public TypeUse Use = null!;
    public string Kind = "Int";       // RetKind
    public string RawType = "void";   // retorno do trampolim
    public bool Indirect;
    public bool ByRef;                // `ref T` (o metodo devolve um ponteiro)
}

sealed class MethodPlan
{
    public List<ArgPlan> Args = new();
    public RetPlan Ret = null!;
    // Os parametros crus do trampolim, em ordem, inclusive enchimentos.
    public List<(string Type, string Name)> Raw = new();
    public string? Error;
}

static class Aapcs
{
    // Distribui argumentos e retorno. Recusa (Error) o que o trampolim nao
    // consegue repetir com tipos primitivos: float que vaza para a pilha e
    // struct com alinhamento de 16.
    public static MethodPlan Plan(List<ArgPlan> args, RetPlan ret)
    {
        var plan = new MethodPlan { Args = args, Ret = ret };
        int ngrn = 0, nsrn = 0, nsaa = 0, pad = 0, raw = 0;

        bool TakeInt(ArgPlan a, int count)
        {
            if (ngrn + count <= 8)
            {
                a.X = ngrn;
                a.XCount = count;
                ngrn += count;
                for (int i = 0; i < count; i++) a.Raw.Add(("long", $"r{raw++}"));
                return true;
            }
            // Nao cabe: os x que sobram ficam vazios (o C faz NGRN = 8) e o
            // argumento vai inteiro para a pilha.
            while (ngrn < 8)
            {
                plan.Raw.Add(("long", $"pad{pad++}"));
                ngrn++;
            }
            if (nsaa + count > 8) return false;
            a.X = 8 + nsaa;
            a.XCount = count;
            nsaa += count;
            for (int i = 0; i < count; i++) a.Raw.Add(("long", $"r{raw++}"));
            return true;
        }

        foreach (ArgPlan a in args)
        {
            bool ok;
            TypeUse u = a.Use;
            if (a.Mode is ArgMode.Ref or ArgMode.Out or ArgMode.In or ArgMode.ThisRef)
            {
                a.Place = Placement.Pointer;
                ok = TakeInt(a, 1);
            }
            else if (u.Abi == AbiClass.Int)
            {
                a.Place = Placement.X;
                ok = TakeInt(a, 1);
            }
            else if (u.Abi is AbiClass.F32 or AbiClass.F64)
            {
                a.Place = Placement.D;
                ok = nsrn < 8;
                if (ok)
                {
                    a.D = nsrn++;
                    a.DCount = 1;
                    a.Raw.Add((u.Abi == AbiClass.F32 ? "float" : "double", $"r{raw++}"));
                }
            }
            else if (u.Abi == AbiClass.Composite)
            {
                if (u.Size > 16)
                {
                    a.Place = Placement.ByRefCopy;
                    ok = TakeInt(a, 1);
                }
                else if (u.Hfa != HfaKind.None)
                {
                    a.Place = Placement.HfaD;
                    ok = nsrn + u.HfaCount <= 8;
                    if (ok)
                    {
                        a.D = nsrn;
                        a.DCount = u.HfaCount;
                        nsrn += u.HfaCount;
                        for (int i = 0; i < u.HfaCount; i++) a.Raw.Add((u.Hfa == HfaKind.F32 ? "float" : "double", $"r{raw++}"));
                    }
                }
                else
                {
                    a.Place = Placement.CompositeX;
                    ok = u.Align <= 8 && TakeInt(a, (u.Size + 7) / 8);
                }
            }
            else
            {
                ok = false;
            }
            if (!ok)
            {
                plan.Error = $"argument '{a.Name}' does not fit the supported registers";
                return plan;
            }
            // Os crus do argumento entram depois de qualquer enchimento.
            plan.Raw.AddRange(a.Raw);
        }

        TypeUse r = ret.Use;
        if (ret.ByRef)
        {
            ret.Kind = "Int";
            ret.RawType = "long";
        }
        else switch (r.Abi)
        {
            case AbiClass.Void: ret.Kind = "Int"; ret.RawType = "void"; break;
            case AbiClass.Int: ret.Kind = "Int"; ret.RawType = "long"; break;
            case AbiClass.F32: ret.Kind = "F32"; ret.RawType = "float"; break;
            case AbiClass.F64: ret.Kind = "F64"; ret.RawType = "double"; break;
            case AbiClass.Composite:
                if (r.Size > 16)
                {
                    ret.Kind = "Indirect";
                    ret.Indirect = true;
                    ret.RawType = $"global::BunnyLoader.Generated.RetBuf{r.Size}";
                }
                else if (r.Hfa == HfaKind.F32)
                {
                    ret.Kind = $"H{r.HfaCount}F";
                    ret.RawType = $"{TypeMap.RT}.RetH{r.HfaCount}F";
                }
                else if (r.Hfa == HfaKind.F64)
                {
                    ret.Kind = $"H{r.HfaCount}D";
                    ret.RawType = $"{TypeMap.RT}.RetH{r.HfaCount}D";
                }
                else
                {
                    ret.Kind = "S16";
                    ret.RawType = $"{TypeMap.RT}.RetS16";
                }
                break;
        }
        return plan;
    }
}
