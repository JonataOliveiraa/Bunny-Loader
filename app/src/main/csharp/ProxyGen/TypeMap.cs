using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ProxyGen;

// Como um tipo do jogo viaja pela ABI.
enum AbiClass { Void, Int, F32, F64, Composite }

enum HfaKind { None, F32, F64 }

// Um tipo do jogo visto do C# gerado: o texto do tipo, como ele viaja e como
// converter de/para o registrador (classe Int).
sealed class TypeUse
{
    public string Cs = "";
    public string Sig = "";           // como o dump escreve (a assinatura)
    public AbiClass Abi;
    public int Size, Align;
    public HfaKind Hfa;
    public int HfaCount;
    public bool CheckSize;            // struct que o runtime confere (checkLayout)
    public bool IsString;             // string por valor (conversao com alocacao)
    public Func<string, string> ToRaw = e => e;     // expressao C# -> long
    public Func<string, string> FromRaw = e => e;   // long -> expressao C#
    public List<Type> Refs = new();
}

// Como um tipo e gerado: completo (membros e hooks), so o ponteiro, so o
// layout (struct) ou enum.
enum GenMode { Full, Opaque, Mirror, Enum }

sealed class StructLayout
{
    public int Size, Align;
    public HfaKind Hfa;
    public int HfaCount;
    public List<(FieldInfo Field, int Offset, TypeUse Use)> Fields = new();
}

sealed class TypeMap
{
    public const string RT = "global::BunnyLoader.Interop";

    public readonly Dictionary<string, (Type Type, GenMode Mode)> Generated = new();
    readonly Dictionary<string, StructLayout?> layouts = new();
    readonly Dictionary<string, bool> emitted = new();
    readonly HashSet<string> full;
    readonly Func<Type, bool> isGame;

    public TypeMap(HashSet<string> full, Func<Type, bool> isGame)
    {
        this.full = full;
        this.isGame = isGame;
    }

    // So os tipos do Assembly-CSharp ganham proxy: um System.Exception do
    // jogo gerado aqui esconderia o do .NET no proprio codigo gerado.
    public bool IsGame(Type t) => isGame(t);

    static readonly HashSet<string> Handwritten = new()
    {
        "ReLogic.Content.AssetRequestMode",
        "ReLogic.Content.AssetState",
    };

    public static string Key(Type t) => t.FullName ?? t.Name;

    // O nome C# da proxy: global::Ns.Externo.Interno.
    public static string ProxyName(Type t)
    {
        var parts = new List<string>();
        Type? cur = t;
        while (cur != null)
        {
            parts.Insert(0, Ident(cur.Name));
            if (!cur.IsNested) break;
            cur = cur.DeclaringType;
        }
        string ns = Outermost(t).Namespace ?? "";
        return "global::" + (ns.Length > 0 ? ns + "." : "") + string.Join(".", parts);
    }

    public static Type Outermost(Type t)
    {
        while (t.IsNested) t = t.DeclaringType!;
        return t;
    }

    // Nome como o dump escreve: curto, aninhado com ponto.
    public static string DumpName(Type t)
    {
        if (t.IsByRef) return DumpName(t.GetElementType()!);
        if (t.IsArray)
        {
            int rank = t.GetArrayRank();
            return DumpName(t.GetElementType()!) + "[" + new string(',', rank - 1) + "]";
        }
        if (t.IsPointer) return DumpName(t.GetElementType()!) + "*";
        string? kw = Keyword(t);
        if (kw != null) return kw;
        if (t.IsGenericType && !t.IsGenericTypeDefinition)
        {
            string baseName = t.GetGenericTypeDefinition().Name;
            int tick = baseName.IndexOf('`');
            if (tick >= 0) baseName = baseName[..tick];
            string outer = t.IsNested ? DumpName(t.DeclaringType!) + "." : "";
            return outer + baseName + "<" + string.Join(", ", t.GetGenericArguments().Select(DumpName)) + ">";
        }
        string name = t.Name;
        int bt = name.IndexOf('`');
        if (bt >= 0) name = name[..bt];
        return t.IsNested ? DumpName(t.DeclaringType!) + "." + name : name;
    }

    static string? Keyword(Type t) => t.FullName switch
    {
        "System.Void" => "void",
        "System.Boolean" => "bool",
        "System.Byte" => "byte",
        "System.SByte" => "sbyte",
        "System.Int16" => "short",
        "System.UInt16" => "ushort",
        "System.Int32" => "int",
        "System.UInt32" => "uint",
        "System.Int64" => "long",
        "System.UInt64" => "ulong",
        "System.Single" => "float",
        "System.Double" => "double",
        "System.Char" => "char",
        "System.String" => "string",
        "System.Object" => "object",
        "System.Decimal" => "decimal",
        _ => null,
    };

    static readonly HashSet<string> CsKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
        "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
        "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    public static bool ValidName(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    public static string Ident(string name)
    {
        int bt = name.IndexOf('`');
        if (bt >= 0) name = name[..bt];
        return CsKeywords.Contains(name) ? "@" + name : name;
    }

    // O tipo e gerado inteiro? (listado em types.txt, ou aninhado num listado)
    public bool IsFull(Type t)
    {
        for (Type? cur = t; cur != null; cur = cur.IsNested ? cur.DeclaringType : null)
        {
            if (full.Contains(Key(cur))) return true;
            if (!cur.IsNested) break;
        }
        return false;
    }

    void Register(Type t, GenMode mode)
    {
        if (!IsGame(t) || Handwritten.Contains(Key(t))) return;
        string key = Key(t);
        if (Generated.TryGetValue(key, out var have))
        {
            if (have.Mode == GenMode.Full || mode != GenMode.Full) return;
        }
        Generated[key] = (t, ModeFor(t, IsFull(t) ? GenMode.Full : mode));
        // A proxy de um aninhado mora dentro da do externo.
        if (t.IsNested && !Generated.ContainsKey(Key(t.DeclaringType!)))
        {
            Type outer = t.DeclaringType!;
            if (!outer.IsGenericTypeDefinition)
                Register(outer, outer.IsEnum ? GenMode.Enum : outer.IsValueType ? GenMode.Mirror : GenMode.Opaque);
        }
        // As bases geradas viram conversoes implicitas.
        if (!t.IsValueType && t.BaseType != null && IsGame(t.BaseType) && Supported(t.BaseType))
            Register(t.BaseType, GenMode.Opaque);
    }

    // Enum e enum, e delegate so tem o ponteiro, mesmo dentro de um tipo completo.
    static GenMode ModeFor(Type t, GenMode wanted)
    {
        if (t.IsEnum) return GenMode.Enum;
        if (t.BaseType?.FullName == "System.MulticastDelegate") return GenMode.Opaque;
        return wanted;
    }

    public void RegisterFull(Type t)
    {
        Generated[Key(t)] = (t, ModeFor(t, GenMode.Full));
        if (t.IsNested && !Generated.ContainsKey(Key(t.DeclaringType!)))
        {
            Type outer = t.DeclaringType!;
            if (!outer.IsGenericTypeDefinition)
                Register(outer, outer.IsEnum ? GenMode.Enum : outer.IsValueType ? GenMode.Mirror : GenMode.Opaque);
        }
        foreach (Type n in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!ValidName(n.Name) || n.IsGenericTypeDefinition) continue;
            if (n.BaseType?.FullName == "System.MulticastDelegate") continue;
            RegisterFull(n);
        }
        if (!t.IsValueType && t.BaseType != null && IsGame(t.BaseType) && Supported(t.BaseType))
            Register(t.BaseType, GenMode.Opaque);
    }

    // Classes que da para ter proxy propria: nao generica, nome valido.
    static bool Supported(Type t) =>
        !t.IsGenericType && !t.IsGenericParameter && ValidName(t.Name.Split('`')[0]) && t.Name.IndexOf('`') < 0 &&
        (!t.IsNested || Supported(t.DeclaringType!));

    // ---- mapeamento ----

    // `memory`: o tipo como mora na memoria (campo, elemento, alvo de ref):
    // string vira GameString, sem conversao.
    public TypeUse? Map(Type t, bool memory)
    {
        if (t.IsByRef || t.IsGenericParameter || t.ContainsGenericParameters) return null;
        string sig = DumpName(t);
        TypeUse? Prim(string cs, AbiClass abi, int size, Func<string, string>? to = null, Func<string, string>? from = null) =>
            new TypeUse
            {
                Cs = cs, Sig = sig, Abi = abi, Size = size, Align = size,
                ToRaw = to ?? (e => $"(long)({e})"),
                FromRaw = from ?? (e => $"({cs})({e})"),
            };

        switch (t.FullName)
        {
            case "System.Void": return new TypeUse { Cs = "void", Sig = "void", Abi = AbiClass.Void };
            case "System.Boolean": return Prim("bool", AbiClass.Int, 1, e => $"(({e}) ? 1L : 0L)", e => $"((byte)({e}) != 0)");
            case "System.Byte": return Prim("byte", AbiClass.Int, 1);
            case "System.SByte": return Prim("sbyte", AbiClass.Int, 1);
            case "System.Int16": return Prim("short", AbiClass.Int, 2);
            case "System.UInt16": return Prim("ushort", AbiClass.Int, 2);
            case "System.Char": return Prim("char", AbiClass.Int, 2);
            case "System.Int32": return Prim("int", AbiClass.Int, 4);
            case "System.UInt32": return Prim("uint", AbiClass.Int, 4);
            case "System.Int64": return Prim("long", AbiClass.Int, 8);
            case "System.UInt64": return Prim("ulong", AbiClass.Int, 8);
            case "System.IntPtr": return Prim("nint", AbiClass.Int, 8);
            case "System.UIntPtr": return Prim("nuint", AbiClass.Int, 8);
            case "System.Single": return new TypeUse { Cs = "float", Sig = sig, Abi = AbiClass.F32, Size = 4, Align = 4 };
            case "System.Double": return new TypeUse { Cs = "double", Sig = sig, Abi = AbiClass.F64, Size = 8, Align = 8 };
            case "System.String":
                if (memory) return Ref($"{RT}.GameString", sig);
                return new TypeUse
                {
                    Cs = "string?", Sig = sig, Abi = AbiClass.Int, Size = 8, Align = 8, IsString = true,
                    ToRaw = e => $"(long){RT}.GameString.New({e})",
                    FromRaw = e => $"{RT}.GameString.Read((nint)({e}))",
                };
            case "System.Object": return Ref($"{RT}.GameObject", sig);
        }
        if (t.IsPointer) return Prim("nint", AbiClass.Int, 8);
        if (!t.IsArray && !t.IsGenericType && !IsGame(t))
        {
            if (t.IsEnum) return Map(t.GetEnumUnderlyingType(), memory);
            return t.IsValueType ? null : Ref($"{RT}.GameObject", sig);
        }
        if (t.IsEnum)
        {
            if (!Supported(t)) return null;
            TypeUse? under = Map(t.GetEnumUnderlyingType(), true);
            if (under == null) return null;
            Register(t, GenMode.Enum);
            string cs = ProxyName(t);
            string u = under.Cs;
            return new TypeUse
            {
                Cs = cs, Sig = sig, Abi = AbiClass.Int, Size = under.Size, Align = under.Align,
                ToRaw = e => $"(long)({u})({e})",
                FromRaw = e => $"({cs})({u})({e})",
                Refs = { t },
            };
        }
        if (t.IsArray)
        {
            TypeUse? elem = Map(t.GetElementType()!, true);
            if (elem == null || elem.Abi == AbiClass.Void) return Ref($"{RT}.GameObject", sig);
            int rank = t.GetArrayRank();
            if (rank == 1) return Ref($"{RT}.GameArray<{elem.Cs}>", sig, elem.Refs);
            if (rank == 2) return Ref($"{RT}.GameArray2D<{elem.Cs}>", sig, elem.Refs);
            return Ref($"{RT}.GameObject", sig);
        }
        if (t.IsGenericType)
        {
            if (t.GetGenericTypeDefinition().FullName == "System.Nullable`1")
            {
                Type inner = t.GetGenericArguments()[0];
                TypeUse? v = Map(inner, true);
                if (v == null || v.Abi == AbiClass.Void) return null;
                int align = Math.Max(1, v.Align);
                int size = Round(align + v.Size, align);
                return new TypeUse
                {
                    Cs = $"{RT}.GameNullable<{v.Cs}>", Sig = sig, Abi = AbiClass.Composite, Size = size, Align = align, CheckSize = true,
                    Refs = new List<Type>(v.Refs),
                };
            }
            if (t.IsValueType) return null;
            return Ref($"{RT}.GameObject", sig);
        }
        if (!t.IsValueType)
        {
            if (!Supported(t)) return Ref($"{RT}.GameObject", sig);
            Register(t, GenMode.Opaque);
            return Ref(ProxyName(t), sig, new[] { t });
        }
        // struct
        if (!Supported(t)) return null;
        StructLayout? layout = Layout(t);
        if (layout == null) return null;
        Register(t, GenMode.Mirror);
        return new TypeUse
        {
            Cs = ProxyName(t), Sig = sig, Abi = AbiClass.Composite, Size = layout.Size, Align = layout.Align,
            Hfa = layout.Hfa, HfaCount = layout.HfaCount, CheckSize = true,
            Refs = { t },
        };
    }

    static TypeUse Ref(string cs, string sig, IEnumerable<Type>? refs = null) => new()
    {
        Cs = cs, Sig = sig, Abi = AbiClass.Int, Size = 8, Align = 8,
        ToRaw = e => $"(long)({e}).Pointer",
        FromRaw = e => $"new {cs}((nint)({e}))",
        Refs = refs == null ? new List<Type>() : new List<Type>(refs),
    };

    public bool Emits(Type t)
    {
        string key = Key(t);
        if (emitted.TryGetValue(key, out bool have)) return have;
        if (Handwritten.Contains(key)) return true;
        emitted[key] = true;
        bool ok = Generated.ContainsKey(key) && (!t.IsNested || Emits(t.DeclaringType!));
        if (ok && t.IsValueType && !t.IsEnum) ok = Layout(t) != null;
        emitted[key] = ok;
        return ok;
    }

    public Type? Missing(TypeUse use) => use.Refs.FirstOrDefault(r => !Emits(r));

    public static int Round(int v, int a) => (v + a - 1) / a * a;

    // ---- layout dos structs ----

    public static int? FieldOffsetOf(FieldInfo f)
    {
        foreach (CustomAttributeData a in f.GetCustomAttributesData())
        {
            if (a.AttributeType.Name != "FieldOffsetAttribute") continue;
            foreach (CustomAttributeNamedArgument n in a.NamedArguments)
            {
                if (n.MemberName != "Offset" || n.TypedValue.Value is not string s) continue;
                s = s.Trim();
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
                if (int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return v;
            }
        }
        return null;
    }

    public StructLayout? Layout(Type t)
    {
        string key = Key(t);
        if (layouts.TryGetValue(key, out StructLayout? have)) return have;
        layouts[key] = null;  // ciclo = sem layout
        var layout = new StructLayout { Align = 1 };
        int end = 0;
        var leaves = new List<AbiClass>();
        foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            int? offset = FieldOffsetOf(f);
            TypeUse? use = Map(f.FieldType, true);
            if (offset == null || use == null || use.Abi == AbiClass.Void) return null;
            layout.Fields.Add((f, offset.Value, use));
            layout.Align = Math.Max(layout.Align, Math.Max(1, use.Align));
            end = Math.Max(end, offset.Value + use.Size);
            if (use.Abi == AbiClass.Composite)
            {
                if (use.Hfa == HfaKind.None) leaves.Add(AbiClass.Int);
                else for (int i = 0; i < use.HfaCount; i++) leaves.Add(use.Hfa == HfaKind.F32 ? AbiClass.F32 : AbiClass.F64);
            }
            else
            {
                leaves.Add(use.Abi);
            }
        }
        layout.Size = Math.Max(1, Round(end, layout.Align));
        if (leaves.Count is >= 1 and <= 4)
        {
            if (leaves.All(l => l == AbiClass.F32) && layout.Size == leaves.Count * 4) { layout.Hfa = HfaKind.F32; layout.HfaCount = leaves.Count; }
            else if (leaves.All(l => l == AbiClass.F64) && layout.Size == leaves.Count * 8) { layout.Hfa = HfaKind.F64; layout.HfaCount = leaves.Count; }
        }
        layouts[key] = layout;
        return layout;
    }
}
