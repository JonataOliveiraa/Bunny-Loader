# Interface

Mod screens are written as in tModLoader: a `UIState` with elements, shown by
a `UserInterface`, updated in `ModSystem.UpdateUI` and drawn in a layer added
by `ModSystem.ModifyInterfaceLayers`.

```csharp
[Autoload(Side = ModSide.Client)]
public class MyUISystem : ModSystem
{
    UserInterface ui;
    MyState state;

    public override void PostSetupContent()
    {
        ui = new UserInterface();
        state = new MyState();
        state.Activate();
        ui.SetState(state);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (ui?.CurrentState != null) ui.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index != -1)
            layers.Insert(index, new LegacyGameInterfaceLayer("MyMod: My UI", delegate
            {
                if (ui?.CurrentState != null) ui.Draw(Main.spriteBatch, new GameTime());
                return true;
            }, InterfaceScaleType.UI));
    }
}
```

Build the screen in `PostSetupContent` or later: in `Load` the game has no
screen yet and textures only load on the game thread.

## Elements

`UIElement`, `UIState`, `UserInterface`, `StyleDimension`, `CalculatedStyle`,
the events (`OnLeftClick`, `OnMouseOver`...), `OverflowHidden`, and
`UIPanel`, `UIText`, `UITextPanel<T>`, `UIImage`, `UIImageButton`,
`UIImageFramed`, `UIList`, `UIScrollbar`, plus `UICommon.TooltipMouseText`.
The game's layers keep their PC names (`Vanilla: Mouse Text`,
`Vanilla: Inventory`, `Vanilla: Resource Bars`...); a layer can be turned off
with `Active = false`.

## On the phone

- **Units.** The interface is drawn in UI units: the phone's screen divided by
  the interface scale (about 1019×573 on a 1600×900 screen).
  `UserInterface.GetDimensions()` and `Main.mouseX`/`mouseY` inside `Update`
  use them.
- **Touch.** There is no hover: an element is under the finger only while it
  is touching. A touch lasts one or two frames, so `UserInterface.Update`
  replays every change of the finger since the last update, and the click
  arrives even when the update skipped that frame.
- **Touch regions.** The game only gives a touch to the interface inside a
  registered region; each `UserInterface` drawn in a frame registers the area
  of its top-level elements for the next one. Elements outside those areas get
  no touch, and the touch keeps going to the world.
- **Lists.** `UIList` scrolls by dragging the finger; a drag is not a click on
  an item.
- **Images.** Some PC images do not exist on the phone (`Images/UI/ButtonPlay`,
  `ButtonDelete`, `Scrollbar`, `ScrollbarInner`). `UIScrollbar` draws plain
  rectangles without them; a mod that needs the others ships its own copy.

Example: `samples/ExampleModCSharp/Common/UI/ExampleCoinsUI` (the coins per
minute panel, opened with `/coins`). Test pack: `tools/tests/csui`.
