using Microsoft.Xna.Framework;

namespace Terraria.UI;

public class UIEvent
{
    public readonly UIElement Target;

    public UIEvent(UIElement target) => Target = target;
}

public class UIMouseEvent : UIEvent
{
    public readonly Vector2 MousePosition;

    public UIMouseEvent(UIElement target, Vector2 mousePosition) : base(target) => MousePosition = mousePosition;
}

public class UIScrollWheelEvent : UIMouseEvent
{
    public readonly int ScrollWheelValue;

    public UIScrollWheelEvent(UIElement target, Vector2 mousePosition, int scrollWheelValue) : base(target, mousePosition) =>
        ScrollWheelValue = scrollWheelValue;
}
