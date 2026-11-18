using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;
using System.Runtime.InteropServices;
using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class PreJit
{
    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    const int FailuresShown = 8;

    static readonly OpCode[] OneByte = new OpCode[256];
    static readonly OpCode[] TwoByte = new OpCode[256];
    static readonly Assembly Loader = typeof(Mod).Assembly;

    static IEnumerator<MethodBase>? cursor;
    static readonly Queue<MethodBase> Callees = new();
    static readonly HashSet<IntPtr> Seen = new();
    static readonly List<string> Failures = new();
    static int compiled, calleesCompiled, failed, frames;
    static long ticks;
    static TimeSpan jitAtStart;

    static PreJit()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode op) continue;
            ushort value = (ushort)op.Value;
            if (value < 0x100) OneByte[value] = op;
            else if ((value & 0xFF00) == 0xFE00) TwoByte[value & 0xFF] = op;
        }
    }

    internal static void Start(IReadOnlyList<Mod> mods)
    {
        if (cursor != null || !mods.Any(m => m.Code != Loader)) return;
        cursor = Methods(mods).GetEnumerator();
        jitAtStart = JitInfo.GetCompilationTime(false);
        Host.Native()->OnFrame(&Frame);
    }

    [UnmanagedCallersOnly]
    static int Frame(int budgetMicros)
    {
        try { return Step(budgetMicros) ? 1 : 0; }
        catch (Exception e)
        {
            Log.Error($"pre-JIT stopped: {e}");
            cursor = null;
            return 0;
        }
    }

    static bool Step(int budgetMicros)
    {
        if (cursor == null) return false;
        long start = Stopwatch.GetTimestamp(), limit = budgetMicros * Stopwatch.Frequency / 1_000_000;
        frames++;
        bool more = true;
        do
        {
            if (Callees.TryDequeue(out MethodBase? callee))
            {
                if (Compile(callee)) calleesCompiled++;
            }
            else if (cursor.MoveNext())
            {
                MethodBase method = cursor.Current;
                if (Compile(method))
                {
                    compiled++;
                    Scan(method);
                }
            }
            else
            {
                more = false;
            }
        } while (more && Stopwatch.GetTimestamp() - start < limit);
        ticks += Stopwatch.GetTimestamp() - start;
        if (!more) Finish();
        return more;
    }

    static void Finish()
    {
        double jit = (JitInfo.GetCompilationTime(false) - jitAtStart).TotalMilliseconds;
        double spent = ticks * 1000.0 / Stopwatch.Frequency;
        Log.Info($"pre-JIT: {compiled} mod method(s) and {calleesCompiled} loader method(s) they call, in {frames} frame(s) ({spent:F0} ms in the frame budget, {jit:F0} ms of JIT meanwhile){(failed > 0 ? $"; {failed} failed" : "")}");
        foreach (string failure in Failures) Log.Warn($"pre-JIT: {failure}");
        if (failed > Failures.Count) Log.Warn($"pre-JIT: {failed - Failures.Count} more failure(s) not shown");
        cursor = null;
        Callees.Clear();
        Seen.Clear();
        Failures.Clear();
    }

    static bool Compile(MethodBase method)
    {
        try
        {
            method.MethodHandle.GetFunctionPointer();
            return true;
        }
        catch (Exception e)
        {
            Fail(method.DeclaringType, method.Name, e);
            return false;
        }
    }

    static void Fail(Type? type, string member, Exception e)
    {
        failed++;
        if (Failures.Count < FailuresShown) Failures.Add($"{type?.FullName}.{member}: {e.GetType().Name}: {e.Message}");
    }

    static IEnumerable<MethodBase> Methods(IReadOnlyList<Mod> mods)
    {
        var rest = new List<MethodBase>();
        foreach (Mod mod in mods)
        {
            Assembly code = mod.Code;
            if (code == Loader) continue;
            foreach (Type type in LoadableTypes(code))
            {
                foreach (MethodBase method in Collect(type, mod.PreJITFilter))
                {
                    if (Overrides(method, code)) yield return method;
                    else rest.Add(method);
                }
            }
        }
        foreach (MethodBase method in rest) yield return method;
    }

    static Type[] LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.OfType<Type>().ToArray(); }
    }

    static bool ShouldJit(Type type, PreJITFilter filter) =>
        filter.ShouldJIT(type) && (type.DeclaringType is null || ShouldJit(type.DeclaringType, filter));

    static List<MethodBase> Collect(Type type, PreJITFilter filter)
    {
        var list = new List<MethodBase>();
        try
        {
            if (type.ContainsGenericParameters || !ShouldJit(type, filter)) return list;
            foreach (MethodInfo method in type.GetMethods(Declared))
                if (!method.IsSpecialName && Compilable(method) && filter.ShouldJIT(method)) list.Add(method);
            foreach (ConstructorInfo constructor in type.GetConstructors(Declared))
                if (!constructor.IsStatic && Compilable(constructor) && filter.ShouldJIT(constructor)) list.Add(constructor);
            foreach (PropertyInfo property in type.GetProperties(Declared))
            {
                if (!filter.ShouldJIT(property)) continue;
                foreach (MethodInfo accessor in property.GetAccessors(true))
                    if (accessor.DeclaringType == type && Compilable(accessor) && filter.ShouldJIT(accessor)) list.Add(accessor);
            }
        }
        catch (Exception e)
        {
            Fail(type, "(members)", e);
        }
        return list;
    }

    static bool Compilable(MethodBase method) =>
        !method.IsAbstract && !method.ContainsGenericParameters &&
        (method.Attributes & MethodAttributes.PinvokeImpl) == 0 &&
        (method.MethodImplementationFlags & MethodImplAttributes.InternalCall) == 0 &&
        !method.IsDefined(typeof(UnmanagedCallersOnlyAttribute), false);

    static bool Overrides(MethodBase method, Assembly code) =>
        method is MethodInfo info && info.IsVirtual && info.DeclaringType is { IsInterface: false } &&
        info.GetBaseDefinition().DeclaringType?.Assembly != code;

    static void Scan(MethodBase method)
    {
        byte[]? il;
        try { il = method.GetMethodBody()?.GetILAsByteArray(); }
        catch { return; }
        if (il == null) return;
        Module module = method.Module;
        for (int i = 0; i < il.Length;)
        {
            OpCode op;
            if (il[i] == 0xFE)
            {
                if (i + 1 >= il.Length) return;
                op = TwoByte[il[i + 1]];
            }
            else
            {
                op = OneByte[il[i]];
            }
            if (op.Size == 0) return;
            i += op.Size;
            if (op.OperandType == OperandType.InlineMethod && i + 4 <= il.Length) Want(module, BitConverter.ToInt32(il, i));
            i += OperandSize(op.OperandType, il, i);
        }
    }

    static int OperandSize(OperandType type, byte[] il, int at) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => at + 4 <= il.Length ? 4 + 4 * Math.Max(0, BitConverter.ToInt32(il, at)) : il.Length,
        _ => 4,
    };

    static void Want(Module module, int token)
    {
        MethodBase? callee;
        try { callee = module.ResolveMethod(token); }
        catch { return; }
        if (callee == null || callee.Module.Assembly != Loader || !Compilable(callee)) return;
        if (Seen.Add(callee.MethodHandle.Value)) Callees.Enqueue(callee);
    }
}
