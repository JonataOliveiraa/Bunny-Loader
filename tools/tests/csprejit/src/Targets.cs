using Terraria;
using Terraria.ModLoader;

namespace CsPreJit;

public static class Targets
{
    public static int Compiled()
    {
        int x = Holder.Value;
        x = x * 31 + 1;
        x = x * 31 + 2;
        x = x * 31 + 3;
        x = x * 31 + 4;
        x = x * 31 + 5;
        x = x * 31 + 6;
        x = x * 31 + 7;
        x = x * 31 + 8;
        x = x * 31 + 9;
        x = x * 31 + 10;
        x = x * 31 + 11;
        x = x * 31 + 12;
        x = x * 31 + 13;
        x = x * 31 + 14;
        x = x * 31 + 15;
        x = x * 31 + 16;
        x = x * 31 + 17;
        x = x * 31 + 18;
        x = x * 31 + 19;
        x = x * 31 + 20;
        x = x * 31 + 21;
        x = x * 31 + 22;
        x = x * 31 + 23;
        x = x * 31 + 24;
        x = x * 31 + 25;
        x = x * 31 + 26;
        x = x * 31 + 27;
        x = x * 31 + 28;
        x = x * 31 + 29;
        x = x * 31 + 30;
        x = x * 31 + 31;
        x = x * 31 + 32;
        x = x * 31 + 33;
        x = x * 31 + 34;
        x = x * 31 + 35;
        x = x * 31 + 36;
        x = x * 31 + 37;
        x = x * 31 + 38;
        x = x * 31 + 39;
        x = x * 31 + 40;
        x = x * 31 + 41;
        x = x * 31 + 42;
        x = x * 31 + 43;
        x = x * 31 + 44;
        x = x * 31 + 45;
        x = x * 31 + 46;
        x = x * 31 + 47;
        x = x * 31 + 48;
        x = x * 31 + 49;
        x = x * 31 + 50;
        x = x * 31 + 51;
        x = x * 31 + 52;
        x = x * 31 + 53;
        x = x * 31 + 54;
        x = x * 31 + 55;
        x = x * 31 + 56;
        x = x * 31 + 57;
        x = x * 31 + 58;
        x = x * 31 + 59;
        x = x * 31 + 60;
        return x;
    }

    [NoJIT]
    public static int Skipped()
    {
        int x = Holder.Value;
        x = x * 31 + 1;
        x = x * 31 + 2;
        x = x * 31 + 3;
        x = x * 31 + 4;
        x = x * 31 + 5;
        x = x * 31 + 6;
        x = x * 31 + 7;
        x = x * 31 + 8;
        x = x * 31 + 9;
        x = x * 31 + 10;
        x = x * 31 + 11;
        x = x * 31 + 12;
        x = x * 31 + 13;
        x = x * 31 + 14;
        x = x * 31 + 15;
        x = x * 31 + 16;
        x = x * 31 + 17;
        x = x * 31 + 18;
        x = x * 31 + 19;
        x = x * 31 + 20;
        x = x * 31 + 21;
        x = x * 31 + 22;
        x = x * 31 + 23;
        x = x * 31 + 24;
        x = x * 31 + 25;
        x = x * 31 + 26;
        x = x * 31 + 27;
        x = x * 31 + 28;
        x = x * 31 + 29;
        x = x * 31 + 30;
        x = x * 31 + 31;
        x = x * 31 + 32;
        x = x * 31 + 33;
        x = x * 31 + 34;
        x = x * 31 + 35;
        x = x * 31 + 36;
        x = x * 31 + 37;
        x = x * 31 + 38;
        x = x * 31 + 39;
        x = x * 31 + 40;
        x = x * 31 + 41;
        x = x * 31 + 42;
        x = x * 31 + 43;
        x = x * 31 + 44;
        x = x * 31 + 45;
        x = x * 31 + 46;
        x = x * 31 + 47;
        x = x * 31 + 48;
        x = x * 31 + 49;
        x = x * 31 + 50;
        x = x * 31 + 51;
        x = x * 31 + 52;
        x = x * 31 + 53;
        x = x * 31 + 54;
        x = x * 31 + 55;
        x = x * 31 + 56;
        x = x * 31 + 57;
        x = x * 31 + 58;
        x = x * 31 + 59;
        x = x * 31 + 60;
        return x;
    }

    [JITWhenModsEnabled("NoSuchMod")]
    public static int Weak()
    {
        int x = Holder.Value;
        x = x * 31 + 1;
        x = x * 31 + 2;
        x = x * 31 + 3;
        x = x * 31 + 4;
        x = x * 31 + 5;
        x = x * 31 + 6;
        x = x * 31 + 7;
        x = x * 31 + 8;
        x = x * 31 + 9;
        x = x * 31 + 10;
        x = x * 31 + 11;
        x = x * 31 + 12;
        x = x * 31 + 13;
        x = x * 31 + 14;
        x = x * 31 + 15;
        x = x * 31 + 16;
        x = x * 31 + 17;
        x = x * 31 + 18;
        x = x * 31 + 19;
        x = x * 31 + 20;
        x = x * 31 + 21;
        x = x * 31 + 22;
        x = x * 31 + 23;
        x = x * 31 + 24;
        x = x * 31 + 25;
        x = x * 31 + 26;
        x = x * 31 + 27;
        x = x * 31 + 28;
        x = x * 31 + 29;
        x = x * 31 + 30;
        x = x * 31 + 31;
        x = x * 31 + 32;
        x = x * 31 + 33;
        x = x * 31 + 34;
        x = x * 31 + 35;
        x = x * 31 + 36;
        x = x * 31 + 37;
        x = x * 31 + 38;
        x = x * 31 + 39;
        x = x * 31 + 40;
        x = x * 31 + 41;
        x = x * 31 + 42;
        x = x * 31 + 43;
        x = x * 31 + 44;
        x = x * 31 + 45;
        x = x * 31 + 46;
        x = x * 31 + 47;
        x = x * 31 + 48;
        x = x * 31 + 49;
        x = x * 31 + 50;
        x = x * 31 + 51;
        x = x * 31 + 52;
        x = x * 31 + 53;
        x = x * 31 + 54;
        x = x * 31 + 55;
        x = x * 31 + 56;
        x = x * 31 + 57;
        x = x * 31 + 58;
        x = x * 31 + 59;
        x = x * 31 + 60;
        return x;
    }

    public static int Big
    {
        get
        {
            int x = Holder.Value;
            x = x * 17 + 1;
            x = x * 17 + 2;
            x = x * 17 + 3;
            x = x * 17 + 4;
            x = x * 17 + 5;
            x = x * 17 + 6;
            x = x * 17 + 7;
            x = x * 17 + 8;
            x = x * 17 + 9;
            x = x * 17 + 10;
            x = x * 17 + 11;
            x = x * 17 + 12;
            x = x * 17 + 13;
            x = x * 17 + 14;
            x = x * 17 + 15;
            x = x * 17 + 16;
            x = x * 17 + 17;
            x = x * 17 + 18;
            x = x * 17 + 19;
            x = x * 17 + 20;
            x = x * 17 + 21;
            x = x * 17 + 22;
            x = x * 17 + 23;
            x = x * 17 + 24;
            x = x * 17 + 25;
            x = x * 17 + 26;
            x = x * 17 + 27;
            x = x * 17 + 28;
            x = x * 17 + 29;
            x = x * 17 + 30;
            x = x * 17 + 31;
            x = x * 17 + 32;
            x = x * 17 + 33;
            x = x * 17 + 34;
            x = x * 17 + 35;
            x = x * 17 + 36;
            x = x * 17 + 37;
            x = x * 17 + 38;
            x = x * 17 + 39;
            x = x * 17 + 40;
            x = x * 17 + 41;
            x = x * 17 + 42;
            x = x * 17 + 43;
            x = x * 17 + 44;
            x = x * 17 + 45;
            x = x * 17 + 46;
            x = x * 17 + 47;
            x = x * 17 + 48;
            x = x * 17 + 49;
            x = x * 17 + 50;
            x = x * 17 + 51;
            x = x * 17 + 52;
            x = x * 17 + 53;
            x = x * 17 + 54;
            x = x * 17 + 55;
            x = x * 17 + 56;
            x = x * 17 + 57;
            x = x * 17 + 58;
            x = x * 17 + 59;
            x = x * 17 + 60;
            return x;
        }
    }

    public static float CallsLoader(float value) => Utils.Turn01ToCyclic010(value) + Utils.WrappedLerp(0f, 1f, value);
}

public sealed class Box
{
    public readonly int Value;

    public Box(int seed)
    {
        int x = seed;
        x = x * 13 + 1;
        x = x * 13 + 2;
        x = x * 13 + 3;
        x = x * 13 + 4;
        x = x * 13 + 5;
        x = x * 13 + 6;
        x = x * 13 + 7;
        x = x * 13 + 8;
        x = x * 13 + 9;
        x = x * 13 + 10;
        x = x * 13 + 11;
        x = x * 13 + 12;
        x = x * 13 + 13;
        x = x * 13 + 14;
        x = x * 13 + 15;
        x = x * 13 + 16;
        x = x * 13 + 17;
        x = x * 13 + 18;
        x = x * 13 + 19;
        x = x * 13 + 20;
        x = x * 13 + 21;
        x = x * 13 + 22;
        x = x * 13 + 23;
        x = x * 13 + 24;
        x = x * 13 + 25;
        x = x * 13 + 26;
        x = x * 13 + 27;
        x = x * 13 + 28;
        x = x * 13 + 29;
        x = x * 13 + 30;
        x = x * 13 + 31;
        x = x * 13 + 32;
        x = x * 13 + 33;
        x = x * 13 + 34;
        x = x * 13 + 35;
        x = x * 13 + 36;
        x = x * 13 + 37;
        x = x * 13 + 38;
        x = x * 13 + 39;
        x = x * 13 + 40;
        x = x * 13 + 41;
        x = x * 13 + 42;
        x = x * 13 + 43;
        x = x * 13 + 44;
        x = x * 13 + 45;
        x = x * 13 + 46;
        x = x * 13 + 47;
        x = x * 13 + 48;
        x = x * 13 + 49;
        x = x * 13 + 50;
        x = x * 13 + 51;
        x = x * 13 + 52;
        x = x * 13 + 53;
        x = x * 13 + 54;
        x = x * 13 + 55;
        x = x * 13 + 56;
        x = x * 13 + 57;
        x = x * 13 + 58;
        x = x * 13 + 59;
        x = x * 13 + 60;
        Value = x;
    }
}

[NoJIT]
public static class SkippedClass
{
    public static int Run()
    {
        int x = Holder.Value;
        x = x * 7 + 1;
        x = x * 7 + 2;
        x = x * 7 + 3;
        x = x * 7 + 4;
        x = x * 7 + 5;
        x = x * 7 + 6;
        x = x * 7 + 7;
        x = x * 7 + 8;
        x = x * 7 + 9;
        x = x * 7 + 10;
        x = x * 7 + 11;
        x = x * 7 + 12;
        x = x * 7 + 13;
        x = x * 7 + 14;
        x = x * 7 + 15;
        x = x * 7 + 16;
        x = x * 7 + 17;
        x = x * 7 + 18;
        x = x * 7 + 19;
        x = x * 7 + 20;
        x = x * 7 + 21;
        x = x * 7 + 22;
        x = x * 7 + 23;
        x = x * 7 + 24;
        x = x * 7 + 25;
        x = x * 7 + 26;
        x = x * 7 + 27;
        x = x * 7 + 28;
        x = x * 7 + 29;
        x = x * 7 + 30;
        x = x * 7 + 31;
        x = x * 7 + 32;
        x = x * 7 + 33;
        x = x * 7 + 34;
        x = x * 7 + 35;
        x = x * 7 + 36;
        x = x * 7 + 37;
        x = x * 7 + 38;
        x = x * 7 + 39;
        x = x * 7 + 40;
        x = x * 7 + 41;
        x = x * 7 + 42;
        x = x * 7 + 43;
        x = x * 7 + 44;
        x = x * 7 + 45;
        x = x * 7 + 46;
        x = x * 7 + 47;
        x = x * 7 + 48;
        x = x * 7 + 49;
        x = x * 7 + 50;
        x = x * 7 + 51;
        x = x * 7 + 52;
        x = x * 7 + 53;
        x = x * 7 + 54;
        x = x * 7 + 55;
        x = x * 7 + 56;
        x = x * 7 + 57;
        x = x * 7 + 58;
        x = x * 7 + 59;
        x = x * 7 + 60;
        return x;
    }
}

static class Holder
{
    public static readonly int Value = Main.maxTilesX > 0 ? 3 : 1;
}
