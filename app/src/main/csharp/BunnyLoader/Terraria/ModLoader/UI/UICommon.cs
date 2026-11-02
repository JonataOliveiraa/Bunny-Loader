namespace Terraria.ModLoader.UI;

public static class UICommon
{
    public static void TooltipMouseText(string text)
    {
        Main.hoverItemName = text;
        Main.LocalPlayer.mouseInterface = true;
    }
}
