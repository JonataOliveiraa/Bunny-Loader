namespace BunnyLoader;

internal static unsafe class Log
{
    public static void Info(string text) => Write(0, text);

    public static void Warn(string text) => Write(1, text);

    public static void Error(string text) => Write(2, text);

    static void Write(int level, string text)
    {
        byte* utf8 = Host.Utf8(text);
        try { Host.Native()->Log(level, utf8); }
        finally { Host.FreeUtf8(utf8); }
    }
}
