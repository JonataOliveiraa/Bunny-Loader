using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ProxyGen;

enum OpenKind { Plain, Param, Array, Asset, Void }

sealed class OpenParam
{
    public string Name = "";
    public ArgMode Mode;
    public OpenKind Kind;
    public int Index;
}

sealed class GenericInstance
{
    public MethodModel Closed = null!;
    public Type?[] Slots = Array.Empty<Type?>();
    public string[] ValueCs = Array.Empty<string>();
}

sealed class GenericModel
{
    public MethodInfo Definition = null!;
    public Type Owner = null!;
    public string Name = "";
    public string Key = "";
    public bool IsStatic;
    public string Signature = "";
    public string[] TypeParams = Array.Empty<string>();
    public bool[] NeedsGameObject = Array.Empty<bool>();
    public List<OpenParam> Params = new();
    public OpenParam Ret = new();
    public List<GenericInstance> Instances = new();
}

static class Generics
{
    static readonly Dictionary<string, List<string[]>> instances = new(StringComparer.Ordinal);

    public static void Load(string path)
    {
        foreach (string line in File.ReadAllLines(path))
        {
            string[] parts = line.Split('\t');
            if (parts.Length != 4) continue;
            string key = KeyOf(parts[0], parts[1], parts[2]);
            if (!instances.TryGetValue(key, out List<string[]>? list)) instances[key] = list = new List<string[]>();
            list.Add(parts[3].Split(','));
        }
    }

    static string KeyOf(string owner, string name, string parameters) => owner + "\t" + name + "\t" + parameters;

    public static GenericModel? Build(MethodInfo m, Type owner, Emitter emitter, TypeMap map, Func<string, Type?> resolve, bool handWritten)
    {
        string what = m.Name + "<>";
        if (m.IsAbstract) return null;
        if (handWritten)
        {
            emitter.Skip(owner, what, "generic method written by hand");
            return null;
        }
        if (m.IsVirtual && !m.IsFinal && !owner.IsSealed && !owner.IsValueType)
        {
            emitter.Skip(owner, what, "generic virtual methods are not generated yet");
            return null;
        }

        ParameterInfo[] ps = m.GetParameters();
        string key = KeyOf(TypeMap.Key(m.DeclaringType!), m.Name, string.Join(",", ps.Select(p => p.Name ?? "")));
        if (!instances.TryGetValue(key, out List<string[]>? rows))
        {
            emitter.Skip(owner, what, "generic method with no compiled instance in the game");
            return null;
        }

        Type[] typeParams = m.GetGenericArguments();
        var g = new GenericModel
        {
            Definition = m, Owner = owner, IsStatic = m.IsStatic,
            TypeParams = typeParams.Select(t => TypeMap.Ident(t.Name)).ToArray(),
            NeedsGameObject = new bool[typeParams.Length],
        };

        var sigParts = new List<string>();
        foreach (ParameterInfo p in ps)
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
            OpenParam? op = Classify(pt, mode, g);
            if (op == null)
            {
                emitter.Skip(owner, what, $"generic method: parameter '{p.Name}' of type {TypeMap.DumpName(pt)} is not supported");
                return null;
            }
            op.Name = TypeMap.Ident(p.Name != null && TypeMap.ValidName(p.Name) ? p.Name : $"arg{g.Params.Count}");
            g.Params.Add(op);
            sigParts.Add(prefix + TypeMap.DumpName(pt) + " " + (p.Name ?? ""));
        }

        Type rt = m.ReturnType;
        if (rt.IsByRef)
        {
            emitter.Skip(owner, what, "generic method: returns by reference");
            return null;
        }
        OpenParam? ret = rt.FullName == "System.Void" ? new OpenParam { Kind = OpenKind.Void } : Classify(rt, ArgMode.Value, g);
        if (ret == null)
        {
            emitter.Skip(owner, what, $"generic method: return type {TypeMap.DumpName(rt)} is not supported");
            return null;
        }
        g.Ret = ret;
        g.Signature = TypeMap.DumpName(rt) + " " + m.Name + "(" + string.Join(", ", sigParts) + ")";

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] row in rows)
        {
            if (row.Length != typeParams.Length || !seen.Add(string.Join(",", row))) continue;
            var slots = new Type?[row.Length];
            var closedArgs = new Type[row.Length];
            var valueCs = new string[row.Length];
            bool ok = true;
            for (int k = 0; k < row.Length && ok; k++)
            {
                if (row[k] == "System.Object")
                {
                    closedArgs[k] = resolve("System.Object")!;
                    continue;
                }
                if (row[k].StartsWith("System.", StringComparison.Ordinal) && row[k].EndsWith("Enum", StringComparison.Ordinal))
                {
                    emitter.Skip(owner, what, $"generic instance <{string.Join(", ", row)}>: shared enum instances are not generated yet");
                    ok = false;
                    continue;
                }
                Type? t = row[k].StartsWith('?') ? null : resolve(row[k]);
                TypeUse? use = t != null && t.IsValueType ? map.Map(t, true) : null;
                if (t == null || use == null || use.Abi == AbiClass.Void)
                {
                    emitter.Skip(owner, what, $"generic instance <{string.Join(", ", row)}>: type {row[k]} has no C# mapping");
                    ok = false;
                    continue;
                }
                slots[k] = t;
                closedArgs[k] = t;
                valueCs[k] = use.Cs;
            }
            if (!ok) continue;

            MethodInfo closed;
            try { closed = m.MakeGenericMethod(closedArgs); }
            catch (Exception e)
            {
                emitter.Skip(owner, what, $"generic instance <{string.Join(", ", row)}>: {e.Message}");
                continue;
            }
            MethodModel? cm = emitter.Model(closed, owner, false);
            if (cm == null) continue;
            cm.Fast = false;
            cm.IsVirtual = false;
            g.Instances.Add(new GenericInstance { Closed = cm, Slots = slots, ValueCs = valueCs });
        }

        if (g.Instances.Count == 0)
        {
            emitter.Skip(owner, what, "generic method: none of its compiled instances maps to C#");
            return null;
        }
        g.Instances.Sort((a, b) => a.Slots.Count(s => s == null).CompareTo(b.Slots.Count(s => s == null)));
        g.Key = m.Name + "`" + typeParams.Length + "(" + string.Join(",", g.Params.Select((p, i) =>
            p.Mode + " " + (p.Kind == OpenKind.Plain ? g.Instances[0].Closed.Params[i].Use.Cs : p.Kind + p.Index.ToString()))) + ")";
        return g;
    }

    static OpenParam? Classify(Type t, ArgMode mode, GenericModel g)
    {
        if (t.IsGenericParameter)
        {
            if (t.DeclaringMethod == null) return null;
            return new OpenParam { Mode = mode, Kind = OpenKind.Param, Index = t.GenericParameterPosition };
        }
        if (!t.ContainsGenericParameters) return new OpenParam { Mode = mode, Kind = OpenKind.Plain };
        if (mode != ArgMode.Value) return null;
        if (t.IsArray && t.GetArrayRank() == 1 && t.GetElementType()!.IsGenericParameter && t.GetElementType()!.DeclaringMethod != null)
            return new OpenParam { Mode = mode, Kind = OpenKind.Array, Index = t.GetElementType()!.GenericParameterPosition };
        if (t.IsGenericType && t.GetGenericTypeDefinition().FullName == "ReLogic.Content.Asset`1")
        {
            Type arg = t.GetGenericArguments()[0];
            if (!arg.IsGenericParameter || arg.DeclaringMethod == null) return null;
            g.NeedsGameObject[arg.GenericParameterPosition] = true;
            return new OpenParam { Mode = mode, Kind = OpenKind.Asset, Index = arg.GenericParameterPosition };
        }
        return null;
    }
}
