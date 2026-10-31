namespace Terraria;

public static partial class Utils
{
    public static void Swap<T>(ref T t1, ref T t2) => (t1, t2) = (t2, t1);

    public static T Clamp<T>(T value, T min, T max) where T : System.IComparable<T>
    {
        if (value.CompareTo(max) > 0) return max;
        if (value.CompareTo(min) < 0) return min;
        return value;
    }
}
