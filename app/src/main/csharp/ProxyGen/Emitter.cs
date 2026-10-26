using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProxyGen;

// Um metodo do jogo pronto para gerar: os tipos mapeados e a ABI decidida.
sealed class MethodModel
{
    public MethodBase Method = null!;
    public Type Owner = null!;          // a proxy onde a chamada e gerada
    public string Name = "";            // nome C# do membro
    public string Signature = "";       // como o dump escreve (Signature.h)
    public bool IsStatic, IsCtor, IsVirtual, OwnerIsStruct;
    // Chamada sem a transicao de GC (GameInterop.CallFast): so metodo folha.
    public bool Fast;
    public MethodPlan Plan = null!;
    public List<ArgPlan> Params = new();   // sem this e sem MethodInfo
    public int[] Layout = Array.Empty<int>();
    public List<string?> Defaults = new();
}

sealed class Emitter
{
    const string RT = TypeMap.RT;
    const string G = RT + ".GameInterop";
    readonly TypeMap map;
    public readonly SortedSet<int> RetBufSizes = new();
    public readonly List<string> Skipped = new();
    public int Methods, Hooks, Fields, Properties;
    int uid;

    public Emitter(TypeMap map) => this.map = map;

    // ---- modelo de metodo ----

    public MethodModel? Model(MethodBase m, Type owner, bool forHook)
    {
        if (m.IsGenericMethodDefinition || m.ContainsGenericParameters) return null;
        bool ctor = m is ConstructorInfo;
        if (ctor && m.IsStatic) return null;
        if (!ctor && !TypeMap.ValidName(m.Name)) return null;
        var model = new MethodModel
        {
            Method = m, Owner = owner, IsStatic = m.IsStatic, IsCtor = ctor,
            OwnerIsStruct = owner.IsValueType,
        };
        var args = new List<ArgPlan>();
        if (!m.IsStatic)
        {
            if (owner.IsValueType)
            {
                TypeUse? self = map.Map(owner, true);
                if (self == null) return null;
                args.Add(new ArgPlan { Name = "self", Use = self, Mode = ArgMode.ThisRef });
            }
            else
            {
                args.Add(new ArgPlan
                {
                    Name = "self", Mode = ArgMode.This,
                    Use = new TypeUse { Cs = TypeMap.ProxyName(owner), Abi = AbiClass.Int, Size = 8, Align = 8 },
                });
            }
        }
        var names = new HashSet<string> { "self", "orig" };
        var sigParts = new List<string>();
        int index = 0;
        foreach (ParameterInfo p in m.GetParameters())
        {
            Type pt = p.ParameterType;
            ArgMode mode = ArgMode.Value;
            string prefix = "";
            if (pt.IsByRef)
            {
                pt = pt.GetElementType()!;
                if (p.IsOut && !p.IsIn) { mode = ArgMode.Out; prefix = "out "; }
                else if (p.IsIn && !p.IsOut) { mode = ArgMode.In; prefix = "in "; }
                else { mode = ArgMode.Ref; prefix = "ref "; }
            }
            TypeUse? use = map.Map(pt, mode != ArgMode.Value);
            if (use == null || use.Abi == AbiClass.Void) return null;
            string name = p.Name != null && TypeMap.ValidName(p.Name) ? p.Name : $"arg{index}";
            string ident = TypeMap.Ident(name);
            while (!names.Add(ident)) ident += "_";
            sigParts.Add(prefix + use.Sig + " " + (p.Name ?? ""));
            var plan = new ArgPlan { Name = ident, Use = use, Mode = mode };
            args.Add(plan);
            model.Params.Add(plan);
            model.Defaults.Add(DefaultOf(p, use, mode));
            index++;
        }
        args.Add(new ArgPlan
        {
            Name = "method", Mode = ArgMode.MethodInfo,
            Use = new TypeUse { Cs = "nint", Abi = AbiClass.Int, Size = 8, Align = 8 },
        });

        var ret = new RetPlan();
        Type rt = ctor ? typeof(void) : ((MethodInfo)m).ReturnType;
        if (ctor)
        {
            ret.Use = new TypeUse { Cs = "void", Sig = "void", Abi = AbiClass.Void };
        }
        else
        {
            if (rt.IsByRef)
            {
                if (forHook) return null;
                ret.ByRef = true;
                rt = rt.GetElementType()!;
            }
            TypeUse? r = map.Map(rt, ret.ByRef);
            if (r == null) return null;
            ret.Use = r;
        }
        model.Plan = Aapcs.Plan(args, ret);
        if (model.Plan.Error != null)
        {
            Skipped.Add($"{owner.FullName}.{m.Name}: {model.Plan.Error}");
            return null;
        }
        string retSig = ctor ? "void" : ret.ByRef ? "" : ret.Use.Sig;
        string mname = ctor ? ".ctor" : m.Name;
        model.Signature = (retSig.Length > 0 ? retSig + " " : "") + mname + "(" + string.Join(", ", sigParts) + ")";
        model.Layout = model.Params.Select(a => a.Use.CheckSize ? a.Use.Size : -1)
            .Append(!ctor && !ret.ByRef && ret.Use.CheckSize ? ret.Use.Size : -1).ToArray();
        if (ret.Indirect) RetBufSizes.Add(ret.Use.Size);
        model.IsVirtual = !m.IsStatic && !ctor && m.IsVirtual && !m.IsFinal && !owner.IsValueType && !owner.IsSealed;
        model.Fast = !forHook && IsLeaf(m, model);
        return model;
    }

    /**
     * Metodo folha: nao bloqueia nem roda codigo de mod por dentro (a nao ser
     * um hook nele mesmo, que o Mono aceita). Sao a matematica do XNA (os
     * structs e o MathHelper) e os getters de propriedade sem indice. So eles
     * pulam a transicao de GC (GameInterop.CallFast).
     */
    static bool IsLeaf(MethodBase m, MethodModel model)
    {
        Type? declaring = m.DeclaringType;
        if (declaring == null) return false;
        string ns = TypeMap.Outermost(declaring).Namespace ?? "";
        bool xna = ns.StartsWith("Microsoft.Xna.Framework", StringComparison.Ordinal) &&
                   (declaring.IsValueType || declaring.Name == "MathHelper");
        bool getter = m.IsSpecialName && m.Name.StartsWith("get_", StringComparison.Ordinal) && model.Params.Count == 0;
        return xna || getter;
    }

    static string? DefaultOf(ParameterInfo p, TypeUse use, ArgMode mode)
    {
        if (mode != ArgMode.Value || !p.HasDefaultValue) return null;
        object? v;
        try { v = p.RawDefaultValue; }
        catch { return null; }
        if (v == null || v == DBNull.Value || v is Missing)
        {
            if (use.IsString) return "null";
            if (use.Abi == AbiClass.Composite || use.Cs.StartsWith(RT) || use.Cs.StartsWith("global::")) return "default";
            return null;
        }
        string? lit = Literal(v);
        if (lit == null) return null;
        if (use.Abi == AbiClass.Int && use.Cs.StartsWith("global::") && !use.IsString) return $"({use.Cs})({lit})";
        return use.Cs switch
        {
            "bool" or "int" or "uint" or "long" or "ulong" or "short" or "ushort" or "byte" or "sbyte" or "char"
                or "float" or "double" or "string?" or "nint" => $"({use.Cs.TrimEnd('?')})({lit})",
            _ => null,
        };
    }

    public static string? Literal(object v) => v switch
    {
        bool b => b ? "true" : "false",
        string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\0", "\\0") + "\"",
        char c => ((int)c).ToString(CultureInfo.InvariantCulture),
        float f => float.IsNaN(f) ? "float.NaN" : float.IsPositiveInfinity(f) ? "float.PositiveInfinity" : float.IsNegativeInfinity(f) ? "float.NegativeInfinity" : f.ToString("R", CultureInfo.InvariantCulture) + "f",
        double d => double.IsNaN(d) ? "double.NaN" : double.IsPositiveInfinity(d) ? "double.PositiveInfinity" : double.IsNegativeInfinity(d) ? "double.NegativeInfinity" : d.ToString("R", CultureInfo.InvariantCulture) + "d",
        byte or sbyte or short or ushort or int => Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        uint u => u.ToString(CultureInfo.InvariantCulture) + "u",
        long l => l == long.MinValue ? "long.MinValue" : l.ToString(CultureInfo.InvariantCulture) + "L",
        ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
        _ => null,
    };

    // ---- corpo de uma chamada ao jogo ----

    // Lista de parametros C# (com ref/out/in). `defaults`: valores padrao
    // so no fim (o C# exige).
    static string ParamList(MethodModel m, bool withDefaults)
    {
        var parts = new List<string>();
        int firstDefault = m.Params.Count;
        if (withDefaults)
        {
            for (int i = m.Params.Count - 1; i >= 0 && m.Defaults[i] != null; i--) firstDefault = i;
        }
        for (int i = 0; i < m.Params.Count; i++)
        {
            ArgPlan a = m.Params[i];
            string mod = a.Mode switch { ArgMode.Ref => "ref ", ArgMode.Out => "out ", ArgMode.In => "in ", _ => "" };
            string def = withDefaults && i >= firstDefault ? " = " + m.Defaults[i] : "";
            parts.Add($"{mod}{a.Use.Cs} {a.Name}{def}");
        }
        return string.Join(", ", parts);
    }

    static string RetCs(MethodModel m) => m.Plan.Ret.ByRef ? "ref " + m.Plan.Ret.Use.Cs : m.Plan.Ret.Use.Cs;

    // O corpo que chama `fnExpr` com `infoExpr`. `selfExpr`: o ponteiro do
    // objeto (classe) ou do struct (ThisRef, ja fixado como `__self`).
    void EmitCall(StringBuilder sb, string ind, MethodModel m, string fnExpr, string infoExpr, string? selfExpr,
                  string? afterReturnValue = null)
    {
        RetPlan ret = m.Plan.Ret;
        bool hasRet = ret.Use.Abi != AbiClass.Void || ret.ByRef;
        sb.Append(ind).Append(RT).Append(".CallFrame __f;\n");
        sb.Append(ind).Append("global::System.Runtime.CompilerServices.Unsafe.SkipInit(out __f);\n");
        foreach (ArgPlan a in m.Params.Where(a => a.Mode == ArgMode.Out)) sb.Append(ind).Append($"{a.Name} = default;\n");
        if (ret.Indirect)
        {
            sb.Append(ind).Append($"{ret.Use.Cs} __ret;\n");
            sb.Append(ind).Append("global::System.Runtime.CompilerServices.Unsafe.SkipInit(out __ret);\n");
        }
        string inner = ind;
        int p = 0;
        foreach (ArgPlan a in m.Plan.Args)
        {
            if (a.Mode == ArgMode.ThisRef && selfExpr == "this")
            {
                sb.Append(inner).Append($"fixed ({a.Use.Cs}* __self = &this)\n");
            }
            else if (a.Mode == ArgMode.ThisRef && selfExpr == "__obj")
            {
                // O local do construtor de struct: o endereco ja e fixo.
            }
            else if (a.Mode == ArgMode.ThisRef)
            {
                sb.Append(inner).Append($"fixed ({a.Use.Cs}* __self = &{selfExpr})\n");
            }
            else if (a.Mode is ArgMode.Ref or ArgMode.Out or ArgMode.In)
            {
                sb.Append(inner).Append($"fixed ({a.Use.Cs}* __p{p++} = &{a.Name})\n");
            }
        }
        sb.Append(inner).Append("{\n");
        string b = inner + "    ";
        p = 0;
        foreach (ArgPlan a in m.Plan.Args)
        {
            switch (a.Mode)
            {
                case ArgMode.This:
                    sb.Append(b).Append($"__f.X[{a.X}] = (long)({selfExpr});\n");
                    continue;
                case ArgMode.ThisRef:
                    sb.Append(b).Append(selfExpr == "__obj" ? $"__f.X[{a.X}] = (long)&__obj;\n" : $"__f.X[{a.X}] = (long)__self;\n");
                    continue;
                case ArgMode.MethodInfo:
                    sb.Append(b).Append($"__f.X[{a.X}] = (long)({infoExpr});\n");
                    continue;
                case ArgMode.Ref or ArgMode.Out or ArgMode.In:
                    sb.Append(b).Append($"__f.X[{a.X}] = (long)__p{p++};\n");
                    continue;
            }
            switch (a.Place)
            {
                case Placement.X:
                    sb.Append(b).Append($"__f.X[{a.X}] = {a.Use.ToRaw(a.Name)};\n");
                    break;
                case Placement.D:
                    sb.Append(b).Append($"__f.D[{a.D}] = {RT}.Abi.{(a.Use.Abi == AbiClass.F32 ? "F32" : "F64")}({a.Name});\n");
                    break;
                case Placement.HfaD:
                {
                    string et = a.Use.Hfa == HfaKind.F32 ? "float" : "double";
                    string conv = a.Use.Hfa == HfaKind.F32 ? "F32" : "F64";
                    for (int i = 0; i < a.DCount; i++)
                        sb.Append(b).Append($"__f.D[{a.D + i}] = {RT}.Abi.{conv}((({et}*)&{a.Name})[{i}]);\n");
                    break;
                }
                case Placement.CompositeX:
                    sb.Append(b).Append($"*({a.Use.Cs}*)&__f.X[{a.X}] = {a.Name};\n");
                    break;
                case Placement.ByRefCopy:
                    sb.Append(b).Append($"__f.X[{a.X}] = (long)&{a.Name};\n");
                    break;
            }
        }
        sb.Append(b).Append($"__f.Ret = {RT}.RetKind.{ret.Kind};\n");
        if (ret.Indirect)
        {
            sb.Append(b).Append("__f.Indirect = (nint)(&__ret);\n");
            sb.Append(b).Append($"__f.RetSize = {ret.Use.Size};\n");
        }
        sb.Append(b).Append($"{G}.{(m.Fast ? "CallFast" : "Call")}({fnExpr}, &__f);\n");
        if (hasRet)
        {
            string value;
            if (ret.ByRef) value = $"ref *({ret.Use.Cs}*)(nint)__f.Out[0]";
            else value = ret.Use.Abi switch
            {
                AbiClass.Int => ret.Use.FromRaw("(long)__f.Out[0]"),
                AbiClass.F32 => $"{RT}.Abi.ToF32(__f.Out[0])",
                AbiClass.F64 => $"{RT}.Abi.ToF64(__f.Out[0])",
                _ => ret.Indirect ? "__ret" : $"*({ret.Use.Cs}*)__f.Out",
            };
            if (afterReturnValue != null && !ret.ByRef)
            {
                sb.Append(b).Append($"var __v = {value};\n");
                sb.Append(b).Append(afterReturnValue.Replace("$v", "__v")).Append('\n');
                sb.Append(b).Append("return __v;\n");
            }
            else
            {
                sb.Append(b).Append($"return {value};\n");
            }
        }
        sb.Append(inner).Append("}\n");
    }

    // ---- membros de uma proxy ----

    public void EmitMethod(StringBuilder sb, string ind, MethodModel m, string classExpr, string name, string access = "public")
    {
        int id = uid++;
        Methods++;
        sb.Append(ind).Append($"static {RT}.MethodRef __m{id};\n");
        sb.Append(ind).Append($"static readonly int[] __L{id} = {{ {string.Join(", ", m.Layout)} }};\n");
        if (m.IsVirtual) sb.Append(ind).Append($"static {RT}.VirtualCache? __v{id};\n");
        string sig = Emitter.Literal(m.Signature)!;
        string isStatic = m.IsStatic || m.IsCtor ? "static " : "";
        if (m.IsCtor)
        {
            // Classe: Tipo.New(...) aloca e roda o construtor do jogo.
            string self = TypeMap.ProxyName(m.Owner);
            sb.Append(ind).Append($"{access} static {self} {name}({ParamList(m, true)})\n");
            sb.Append(ind).Append("{\n");
            string b0 = ind + "    ";
            sb.Append(b0).Append($"ref readonly var __m = ref {G}.Method(ref __m{id}, {classExpr}, {sig}, __L{id});\n");
            if (m.OwnerIsStruct)
            {
                // Struct: um valor zerado que o construtor do jogo preenche.
                sb.Append(b0).Append($"{self} __obj = default;\n");
                EmitCall(sb, b0, m, "__m.Fn", "__m.Info", "__obj");
                sb.Append(b0).Append("return __obj;\n");
            }
            else
            {
                sb.Append(b0).Append($"nint __obj = {G}.NewObject({classExpr});\n");
                EmitCall(sb, b0, m, "__m.Fn", "__m.Info", "__obj");
                sb.Append(b0).Append($"return new {self}(__obj);\n");
            }
            sb.Append(ind).Append("}\n");
            return;
        }
        string unsafeRet = RetCs(m);
        sb.Append(ind).Append($"{access} {isStatic}{unsafeRet} {name}({ParamList(m, true)})\n");
        sb.Append(ind).Append("{\n");
        string bb = ind + "    ";
        sb.Append(bb).Append($"ref readonly var __m = ref {G}.Method(ref __m{id}, {classExpr}, {sig}, __L{id});\n");
        string fn = "__m.Fn", info = "__m.Info";
        string? self2 = null;
        if (!m.IsStatic)
        {
            if (m.OwnerIsStruct)
            {
                self2 = "this";
            }
            else
            {
                self2 = "Pointer";
                sb.Append(bb).Append($"if (Pointer == 0) throw new global::System.NullReferenceException(\"{m.Owner.Name}.{m.Method.Name}: game object is null\");\n");
                if (m.IsVirtual)
                {
                    sb.Append(bb).Append($"var __vc = {G}.Virtual(ref __v{id}, in __m, Pointer);\n");
                    fn = "__vc.Fn";
                    info = "__vc.Info";
                }
            }
        }
        EmitCall(sb, bb, m, fn, info, self2);
        sb.Append(ind).Append("}\n");
    }

    // ---- hooks (On_<Tipo>) ----

    public void EmitHook(StringBuilder sb, string ind, MethodModel m, string classExpr, string eventName, string label)
    {
        int id = uid++;
        Hooks++;
        RetPlan ret = m.Plan.Ret;
        bool isVoid = ret.Use.Abi == AbiClass.Void;
        string retCs = ret.Use.Cs;
        string selfParam = "";
        string selfArg = "";
        if (!m.IsStatic)
        {
            string owner = TypeMap.ProxyName(m.Owner);
            selfParam = m.OwnerIsStruct ? $"ref {owner} self" : $"{owner} self";
            selfArg = m.OwnerIsStruct ? "ref self" : "self";
        }
        string plist = ParamList(m, false);
        string fullParams = string.Join(", ", new[] { selfParam, plist }.Where(s => s.Length > 0));
        string argNames = string.Join(", ", new[] { selfArg }.Concat(m.Params.Select(a =>
            (a.Mode switch { ArgMode.Ref => "ref ", ArgMode.Out => "out ", ArgMode.In => "in ", _ => "" }) + a.Name)).Where(s => s.Length > 0));
        string orig = $"orig_{eventName}", hook = $"hook_{eventName}";
        string sig = Emitter.Literal(m.Signature)!;

        sb.Append(ind).Append($"public delegate {retCs} {orig}({fullParams});\n");
        sb.Append(ind).Append($"public delegate {retCs} {hook}({orig} orig{(fullParams.Length > 0 ? ", " + fullParams : "")});\n");
        sb.Append(ind).Append($"public static event {hook} {eventName}\n");
        sb.Append(ind).Append("{\n");
        sb.Append(ind).Append($"    add => __h{id}.Add(value);\n");
        sb.Append(ind).Append($"    remove => __h{id}.Remove(value);\n");
        sb.Append(ind).Append("}\n");
        sb.Append(ind).Append($"static {RT}.MethodRef __m{id};\n");
        sb.Append(ind).Append($"static readonly int[] __L{id} = {{ {string.Join(", ", m.Layout)} }};\n");
        sb.Append(ind).Append($"static nint* __s{id};\n");
        sb.Append(ind).Append($"[global::System.ThreadStatic] static int __r{id};\n");
        if (!isVoid) sb.Append(ind).Append($"[global::System.ThreadStatic] static {retCs} __l{id};\n");
        sb.Append(ind).Append($"static readonly global::BunnyLoader.Hooks.HookChain<{hook}, {orig}> __h{id} = new(\n");
        sb.Append(ind).Append($"    (__hk, __nx) => ({fullParams}) => __hk(__nx{(argNames.Length > 0 ? ", " + argNames : "")}),\n");
        sb.Append(ind).Append($"    __o{id}, __i{id});\n");

        // instala o trampolim
        string rawTypes = string.Join(", ", m.Plan.Raw.Select(r => r.Type).Append(ret.RawType));
        sb.Append(ind).Append($"static void __i{id}()\n");
        sb.Append(ind).Append("{\n");
        sb.Append(ind).Append($"    ref readonly var __m = ref {G}.Method(ref __m{id}, {classExpr}, {sig}, __L{id});\n");
        sb.Append(ind).Append($"    __s{id} = (nint*)global::BunnyLoader.Hook.Install(__m.Info, (nint)(delegate* unmanaged<{rawTypes}>)&__n{id}).OriginalSlot;\n");
        sb.Append(ind).Append("}\n");

        // o orig mais de dentro: o proximo elo nativo (o jogo, ou outro hook)
        sb.Append(ind).Append($"static {retCs} __o{id}({fullParams})\n");
        sb.Append(ind).Append("{\n");
        sb.Append(ind).Append($"    __r{id}++;\n");
        string after = isVoid ? null! : $"__l{id} = $v;";
        EmitCall(sb, ind + "    ", m, $"*__s{id}", $"__m{id}.Info", m.IsStatic ? null : (m.OwnerIsStruct ? "self" : "self.Pointer"),
                 isVoid ? null : after);
        sb.Append(ind).Append("}\n");

        // o trampolim: registradores -> valores tipados -> cadeia
        string rawParams = string.Join(", ", m.Plan.Raw.Select(r => $"{r.Type} {r.Name}"));
        var callArgs = new List<string>();
        foreach (ArgPlan a in m.Plan.Args)
        {
            switch (a.Mode)
            {
                case ArgMode.MethodInfo:
                    continue;
                case ArgMode.This:
                    callArgs.Add($"new {a.Use.Cs}((nint){a.Raw[0].Name})");
                    continue;
                case ArgMode.ThisRef:
                    callArgs.Add($"ref *({a.Use.Cs}*){a.Raw[0].Name}");
                    continue;
                case ArgMode.Ref:
                    callArgs.Add($"ref *({a.Use.Cs}*){a.Raw[0].Name}");
                    continue;
                case ArgMode.Out:
                    callArgs.Add($"out *({a.Use.Cs}*){a.Raw[0].Name}");
                    continue;
                case ArgMode.In:
                    callArgs.Add($"in *({a.Use.Cs}*){a.Raw[0].Name}");
                    continue;
            }
            string[] rn = a.Raw.Select(r => r.Name).ToArray();
            callArgs.Add(a.Place switch
            {
                Placement.X => a.Use.FromRaw(rn[0]),
                Placement.D => rn[0],
                Placement.HfaD => $"{RT}.Abi.{(a.Use.Hfa == HfaKind.F32 ? "FromF" : "FromD")}<{a.Use.Cs}>({string.Join(", ", rn)})",
                Placement.CompositeX => $"{RT}.Abi.FromX<{a.Use.Cs}>({string.Join(", ", rn)})",
                Placement.ByRefCopy => $"*({a.Use.Cs}*){rn[0]}",
                _ => throw new InvalidOperationException(),
            });
        }
        string args = string.Join(", ", callArgs);
        string Conv(string e) => ret.Use.Abi switch
        {
            AbiClass.Void => e,
            AbiClass.Int => ret.Use.ToRaw(e),
            AbiClass.F32 or AbiClass.F64 => e,
            _ => $"{RT}.Abi.As<{ret.Use.Cs}, {ret.RawType}>({e})",
        };
        string rep = $"global::BunnyLoader.Hooks.HookErrors.Report(\"{label}\", ";
        sb.Append(ind).Append("[global::System.Runtime.InteropServices.UnmanagedCallersOnly]\n");
        sb.Append(ind).Append($"static {ret.RawType} __n{id}({rawParams})\n");
        sb.Append(ind).Append("{\n");
        string t = ind + "    ";
        sb.Append(t).Append("try\n").Append(t).Append("{\n");
        string u = t + "    ";
        sb.Append(u).Append($"var __c = __h{id}.Chain;\n");
        if (isVoid)
        {
            sb.Append(u).Append($"if (__c == null) {{ __o{id}({args}); return; }}\n");
            sb.Append(u).Append($"int __runs = __r{id};\n");
            sb.Append(u).Append($"try {{ __c({args}); }}\n");
            sb.Append(u).Append($"catch (global::System.Exception __e) {{ {rep}__e); if (__r{id} == __runs) __o{id}({args}); }}\n");
            sb.Append(t).Append("}\n");
            sb.Append(t).Append($"catch (global::System.Exception __e2) {{ {rep}__e2); }}\n");
        }
        else
        {
            sb.Append(u).Append($"if (__c == null) return {Conv($"__o{id}({args})")};\n");
            sb.Append(u).Append($"int __runs = __r{id};\n");
            sb.Append(u).Append($"try {{ return {Conv($"__c({args})")}; }}\n");
            sb.Append(u).Append($"catch (global::System.Exception __e) {{ {rep}__e); if (__r{id} == __runs) return {Conv($"__o{id}({args})")}; return {Conv($"__l{id}")}; }}\n");
            sb.Append(t).Append("}\n");
            sb.Append(t).Append($"catch (global::System.Exception __e2) {{ {rep}__e2); }}\n");
            sb.Append(t).Append("return default;\n");
        }
        sb.Append(ind).Append("}\n");
    }
}
