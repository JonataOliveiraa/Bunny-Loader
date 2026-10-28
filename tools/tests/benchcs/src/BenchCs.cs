using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

// Os mesmos casos do tools/tests/benchjs, com os mesmos nomes. Mede no quadro
// 120 dentro do mundo (o benchjs no 400): 3 rodadas de cada caso, mediana,
// menos o laco vazio. Loga "bench cs <caso>: <valor> <unidade>".
public sealed unsafe class BenchCs : Mod
{
    static ILog log = null!;

    const int Frame = 120, Rounds = 3, N = 20000;
    static int frames;
    static bool done;
    static long sink;
    static float fsink;

    public override void Load()
    {
        log = Logger;
        On_Player.Update += OnUpdate;
    }

    static void OnUpdate(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (done || i != Main.myPlayer || Main.gameMenu || ++frames != Frame) return;
        done = true;
        Main.NewText("Medindo C#... (fique parado uns segundos)", 255, 240, 120);
        try { Run(self); }
        catch (Exception e) { log.Error($"bench cs error: {e}"); }
        log.Info("bench cs FIM");
    }

    static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;

    // ns por iteracao: `loop(n)` roda o caso n vezes por dentro (uma chamada
    // de delegate por rodada, nao por iteracao: no MuMu ela custa ~1 us).
    // Mediana das rodadas, menos o laco vazio.
    static double PerOp(Func<int, long> loop, int n, double empty)
    {
        sink += loop(16);
        var runs = new double[Rounds];
        for (int r = 0; r < Rounds; r++)
        {
            long t0 = Stopwatch.GetTimestamp();
            sink += loop(n);
            runs[r] = Seconds(Stopwatch.GetTimestamp() - t0) * 1e9 / n;
        }
        Array.Sort(runs);
        return Math.Max(0, runs[Rounds / 2] - empty);
    }

    // Tempo total de um trabalho inteiro (calculo puro), em microssegundos.
    static double Total(Action work)
    {
        work();
        var runs = new double[Rounds];
        for (int r = 0; r < Rounds; r++)
        {
            long t0 = Stopwatch.GetTimestamp();
            work();
            runs[r] = Seconds(Stopwatch.GetTimestamp() - t0) * 1e6;
        }
        Array.Sort(runs);
        return runs[Rounds / 2];
    }

    static void Report(string name, double value, string unit) => log.Info($"bench cs {name}: {value:0.##} {unit}");

    static int Fib(int n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);

    sealed class Small
    {
        public int A, B;
    }

    static void Run(Player p)
    {
        double empty = PerOp(n => { long s = 0; for (int k = 0; k < n; k++) s += k; return s; }, N, 0);
        Report("empty loop", empty, "ns/op");

        // ---- calculo puro ----
        Report("compute: int loop 1M", Total(() =>
        {
            long s = 0;
            for (int k = 0; k < 1_000_000; k++) s += (k * 7) ^ (k >> 3);
            sink += s;
        }), "us");
        Report("compute: sin+sqrt 100k", Total(() =>
        {
            double s = 0;
            for (int k = 0; k < 100_000; k++) s += Math.Sin(k * 0.001) + Math.Sqrt(k);
            sink += (long)s;
        }), "us");
        Report("compute: fib(20)", Total(() => sink += Fib(20)), "us");
        Report("compute: sort 10k ints", Total(() =>
        {
            var arr = new int[10_000];
            uint x = 12345;
            for (int k = 0; k < arr.Length; k++) { x = x * 1103515245 + 12345; arr[k] = (int)(x >> 8); }
            Array.Sort(arr);
            sink += arr[5000];
        }), "us");
        Report("compute: map 10k set+get", Total(() =>
        {
            var map = new Dictionary<int, int>();
            for (int k = 0; k < 10_000; k++) map[k * 31] = k;
            long s = 0;
            for (int k = 0; k < 10_000; k++) s += map[k * 31];
            sink += s;
        }), "us");
        Report("compute: string build 1k", Total(() =>
        {
            var sb = new StringBuilder();
            for (int k = 0; k < 1000; k++) sb.Append("item").Append(k).Append(',');
            sink += sb.Length;
        }), "us");
        Report("compute: allocate 100k objects", Total(() =>
        {
            Small? last = null;
            for (int k = 0; k < 100_000; k++) last = new Small { A = k, B = k + 1 };
            sink += last!.B;
        }), "us");

        // ---- campos e estaticos ----
        Report("field: p.head", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += p.head; } return s + (long)f; }, N, empty), "ns/op");
        Report("field: p.position.X", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += p.position.X; } return s + (long)f; }, N, empty), "ns/op");
        Report("field write: p.statLife = p.statLife", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { p.statLife = p.statLife; } return s + (long)f; }, N, empty), "ns/op");
        Report("static: Main.myPlayer", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += Main.myPlayer; } return s + (long)f; }, N, empty), "ns/op");
        Report("static: Main.GameUpdateCount", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += Main.GameUpdateCount; } return s + (long)f; }, N, empty), "ns/op");
        Report("array: Main.player[myPlayer]", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += (long)Main.player[Main.myPlayer].Pointer; } return s + (long)f; }, N, empty), "ns/op");
        Report("array: p.inventory[p.selectedItem].type", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += p.inventory[p.selectedItem].type; } return s + (long)f; }, N, empty), "ns/op");
        Report("array: ItemID.Sets.Deprecated[5]", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += ItemID.Sets.Deprecated[5] ? 1 : 0; } return s + (long)f; }, N, empty), "ns/op");

        // ---- chamadas ----
        Vector2 a = new(1, 2), b = new(4, 6);
        Matrix m1 = Matrix.CreateScale(2f), m2 = Matrix.CreateScale(3f);
        Report("call: WorldGen.InWorld(int, int, int)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += WorldGen.InWorld(100 + (k & 7), 100, 0) ? 1 : 0; } return s + (long)f; }, N, empty), "ns/op");
        Report("call: MathHelper.Clamp(float x3)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += MathHelper.Clamp(k * 0.5f, 0, 10); } return s + (long)f; }, N, empty), "ns/op");
        Report("call: Vector2.Distance(Vector2, Vector2)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += Vector2.Distance(a, b); } return s + (long)f; }, N, empty), "ns/op");
        Report("call: p.CountBuffs()", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += p.CountBuffs(); } return s + (long)f; }, N, empty), "ns/op");
        Report("call: p.Center (Vector2 return)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += p.Center.X; } return s + (long)f; }, N, empty), "ns/op");
        Report("call: Matrix.Add(Matrix, Matrix) (64-byte return)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += Matrix.Add(m1, m2).M11; } return s + (long)f; }, N, empty), "ns/op");
        Report("call: Lang.GetItemNameValue(98) (string)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += Lang.GetItemNameValue(98)!.Length; } return s + (long)f; }, N / 4, empty), "ns/op");
        Report("struct: new Vector2(1, 2)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += new Vector2(k, 2).X; } return s + (long)f; }, N, empty), "ns/op");
        Report("struct: new Color(1, 2, 3)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += new Color(k & 255, 2, 3).R; } return s + (long)f; }, N, empty), "ns/op");

        // A mesma chamada sem o CallFrame e sem a guarda de excecao, direto
        // pelo ponteiro: o custo da protecao.
        MethodRef inWorld = default;
        GameInterop.Method(ref inWorld, WorldGen.Class, "bool InWorld(int x, int y, int fluff)", new[] { -1, -1, -1, -1 });
        var fn = (delegate* unmanaged<int, int, int, nint, byte>)inWorld.Fn;
        nint info = inWorld.Info;
        Report("call: InWorld direct (no guard, C# only)", PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { s += fn(100 + (k & 7), 100, 0, info); } return s + (long)f; }, N, empty), "ns/op");

        // A mesma chamada sem a transicao de GC do Mono (a thread segue
        // "rodando C#" dentro do jogo) e uma funcao nativa vazia: separam o
        // custo da transicao do custo do metodo.
        var fnFast = (delegate* unmanaged[SuppressGCTransition]<int, int, int, nint, byte>)inWorld.Fn;
        Report("call: InWorld direct, no GC transition (C# only)", PerOp(n => { long s = 0; for (int k = 0; k < n; k++) { s += fnFast(100 + (k & 7), 100, 0, info); } return s; }, N, empty), "ns/op");
        Report("native: empty native call (C# only)", PerOp(n => { long s = 0; for (int k = 0; k < n; k++) { s += BunnyLoader.Interop.GameInterop.NativeNoOp(); } return s; }, N, empty), "ns/op");

        // ---- hook: o custo de passar por um hook que so chama o orig ----
        double plain = PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += MathHelper.Max(k, 3f); } return s + (long)f; }, N, empty);
        On_MathHelper.hook_Max pass = (orig, x, y) => orig(x, y);
        On_MathHelper.Max += pass;
        double hooked = PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { f += MathHelper.Max(k, 3f); } return s + (long)f; }, N, empty);
        On_MathHelper.Max -= pass;
        Report("hook: call without hook", plain, "ns/op");
        Report("hook: call through pass-through hook", hooked, "ns/op");
        Report("hook: overhead per call", Math.Max(0, hooked - plain), "ns/op");

        // ref/out: o mesmo, num metodo com ref e out (so C#: o benchjs nao tem).
        var q1 = new Quaternion { X = 1, Y = 2, Z = 3, W = 4 };
        var q2 = new Quaternion { X = 5, Y = 6, Z = 7, W = 8 };
        double refPlain = PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { Quaternion.Dot(ref q1, ref q2, out float d); f += d; } return s + (long)f; }, N, empty);
        On_Quaternion.hook_Dot_refQuaternion_refQuaternion_outfloat refPass = (orig, ref x, ref y, out r) => orig(ref x, ref y, out r);
        On_Quaternion.Dot_refQuaternion_refQuaternion_outfloat += refPass;
        double refHooked = PerOp(n => { long s = 0; float f = 0; for (int k = 0; k < n; k++) { Quaternion.Dot(ref q1, ref q2, out float d); f += d; } return s + (long)f; }, N, empty);
        On_Quaternion.Dot_refQuaternion_refQuaternion_outfloat -= refPass;
        Report("hook ref/out: overhead per call (Quaternion.Dot, C# only)", Math.Max(0, refHooked - refPlain), "ns/op");

        log.Info($"bench cs sink {sink} {fsink}");
    }
}
