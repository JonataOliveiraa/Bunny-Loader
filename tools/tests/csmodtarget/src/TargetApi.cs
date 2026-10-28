namespace CsModTarget;

// O que os mods que dependem deste usam sem Call, pelos tipos.
public static class TargetApi
{
    public static bool Loaded { get; internal set; }

    public static int Answer => 42;

    public static string Greet(string name) => $"hello, {name}";
}
