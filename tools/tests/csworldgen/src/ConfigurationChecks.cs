using System;
using System.Runtime.CompilerServices;
using BunnyLoader.Interop;
using Terraria.IO;

namespace CsWorldGen;

static unsafe class ConfigurationChecks
{
    static readonly GameClass ObjectClass = new("Newtonsoft.Json.Linq", "JObject");

    static MethodRef parse;

    static readonly int[] ParseLayout = { -1, -1 };

    internal static void Run()
    {
        try
        {
            ref readonly MethodRef method = ref GameInterop.Method(ref parse, ObjectClass, "JObject Parse(string json)", ParseLayout);
            CallFrame frame;
            Unsafe.SkipInit(out frame);
            frame.X[0] = (long)GameString.New("{\"Integer\":7,\"Number\":2.5,\"Text\":\"ore\"}");
            frame.X[1] = (long)method.Info;
            frame.Ret = RetKind.Int;
            GameInterop.Call(method.Fn, &frame);
            using var root = new GameHandle((IntPtr)frame.Out[0]);
            GameConfiguration configuration = GameConfiguration.New(new GameObject(root.Target));
            using var handle = new GameHandle(configuration.Pointer);
            Checks.Check("configuration integer", configuration.Get<int>("Integer") == 7);
            Checks.Check("configuration number", configuration.Get<double>("Number") == 2.5);
            Checks.Check("configuration text", configuration.Get<string>("Text") == "ore");
        }
        catch (Exception error)
        {
            Checks.Check("configuration values", false);
            Checks.Log("csworldgen configuration error: " + error);
        }
    }
}
