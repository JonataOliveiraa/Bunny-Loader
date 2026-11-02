namespace Terraria.UI;

public struct StyleDimension
{
    public static StyleDimension Fill = new(0f, 1f);
    public static StyleDimension Empty = new(0f, 0f);
    public float Pixels;
    public float Precent;

    public StyleDimension(float pixels, float precent)
    {
        Pixels = pixels;
        Precent = precent;
    }

    public float Percent
    {
        readonly get => Precent;
        set => Precent = value;
    }

    public void Set(float pixels, float precent)
    {
        Pixels = pixels;
        Precent = precent;
    }

    public readonly float GetValue(float containerSize) => Pixels + Precent * containerSize;

    public static StyleDimension FromPixels(float pixels) => new(pixels, 0f);

    public static StyleDimension FromPercent(float percent) => new(0f, percent);

    public static StyleDimension FromPixelsAndPercent(float pixels, float percent) => new(pixels, percent);
}
