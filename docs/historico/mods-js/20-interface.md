# Interface: panels, buttons and resource bars

tModLoader's UI works in Bunny Loader with the same names: `UIState`,
`UserInterface`, `UIElement`, `UIPanel`, `UIText`, `UITextPanel`, `UIImage`,
`UIImageButton`, `UIImageFramed`, `UIList`, `UIScrollbar`, and
`ModSystem.ModifyInterfaceLayers` with `LegacyGameInterfaceLayer`. On top of
the interface there is `ModResourceOverlay` for the hearts and mana stars.

The elements are JavaScript classes you extend (override `DrawSelf`,
`Update`, `LeftClick`...). Panels and texts are drawn by the game's own
`UIPanel` and `UIText`, so they look exactly like the game's.

## A screen in four pieces

```js
// 1. The screen: build the elements in OnInitialize
export class MyState extends UIState {
    OnInitialize() {
        const panel = new UIPanel();
        panel.Left.Set(400, 0);
        panel.Top.Set(100, 0);
        panel.Width.Set(170, 0);
        panel.Height.Set(70, 0);

        const close = new UIImageButton(ModContent.Request('Common/UI/Close'));
        close.Left.Set(140, 0);
        close.Top.Set(10, 0);
        close.OnLeftClick.Add((evt, element) => ModContent.GetInstance(MyUISystem).Hide());
        panel.Append(close);

        panel.Append(new UIText('Hello', 1));
        this.Append(panel);
    }
}

// 2-4. The system: create, update, draw
export class MyUISystem extends ModSystem {
    ui = null;
    state = null;

    PostSetupContent() {
        this.ui = new UserInterface();
        this.state = new MyState();
        this.state.Activate();
    }

    Show() { this.ui.SetState(this.state); }
    Hide() { this.ui.SetState(null); }

    UpdateUI(gameTime) {
        if (this.ui?.CurrentState) this.ui.Update(gameTime);
    }

    ModifyInterfaceLayers(layers) {
        const index = layers.FindIndex((layer) => layer.Name === 'Vanilla: Mouse Text');
        if (index === -1) return;
        layers.Insert(index, new LegacyGameInterfaceLayer('MyMod: My Screen', () => {
            if (this.ui?.CurrentState) this.ui.Draw(Main.spriteBatch, null);
            return true;
        }, InterfaceScaleType.UI));
    }
}
```

Build the screen in `PostSetupContent` (or later), not in `Load`: textures
and fonts only exist once the game is running.

## Coordinates

Everything is in **UI units**, the same as `Main.mouseX`/`Main.mouseY`: the
phone screen divided by about 1.57 (1019×573 on a 1600×900 screen).
`UIState` fills the whole UI screen, so percentages (`Width.Set(0, 0.5)`) are
fractions of it.

## Touch

A touch reaches your elements as in tModLoader (`LeftMouseDown`,
`LeftMouseUp`, `LeftClick`, `LeftDoubleClick`, `MouseOver`, `MouseOut`), and
touching your panel does **not** use the item in the player's hand or move
the player. Inside `UserInterface.Update`, `Main.mouseX`, `Main.mouseY`,
`Main.mouseLeft` and `Main.MouseScreen` hold the touch, so code that drags a
panel with `Main.mouseX` works unchanged.

What is different on a phone:

- There is no hover without a finger: `IsMouseHovering` and `MouseOver` only
  happen while the finger is down (and on the frame it lifts).
- A screen catches touches on the area of its **top-level** elements (the
  children of the `UIState`). Keep buttons inside their panel.
- The game's own controls (joystick, hotbar, action buttons) win over your
  screen where they overlap.
- `UIList` scrolls by dragging the finger. A drag that scrolled is not a
  click on the item under it.
- `RightClick` and the scroll wheel do not exist on a phone.

## Events

C#'s `element.OnLeftClick += handler` is `element.OnLeftClick.Add(handler)`;
`-=` is `Remove(handler)`. Assigning (`element.OnLeftClick = handler`)
replaces every handler. The handlers receive `(evt, listeningElement)`;
`evt.Target` is the element under the finger and `evt.MousePosition` the touch.

## The elements

| Element | Notes |
|---|---|
| `UIElement` | Layout like the game: `Left`/`Top`/`Width`/`Height` (`StyleDimension`: pixels + percent of the parent), `Min*`/`Max*`, padding, margin, `HAlign`/`VAlign`, `OverflowHidden` (clips the children), `IgnoresMouseInteraction`. `StyleDimension` is a value: assigning copies it. |
| `UIPanel` | The game's blue panel. `BackgroundColor`, `BorderColor`; the constructor takes custom background and border textures. Padding 12. |
| `UIText` | `new UIText(text, scale, large)`, `SetText`, `TextColor`, `TextOriginX/Y`, `IsWrapped`. Its minimum size follows the text. |
| `UITextPanel` | A panel sized to its text; `TextHAlign`, `DrawPanel`. |
| `UIImage` | An `Asset<Texture2D>` or `Texture2D`; `ImageScale`, `ScaleToFit`, `Color`, `Rotation`, `NormalizedOrigin`. |
| `UIImageButton` | Dim (0.4) until touched (1), `SetHoverImage`, `SetVisibility`, the game's tick sound. |
| `UIImageFramed` | One frame (`Rectangle`) of a texture. |
| `UIList` / `UIScrollbar` | A clipped vertical list (`Add`, `Remove`, `Clear`, `ListPadding`, `SetScrollbar`, `ViewPosition`). |
| `UICommon.TooltipMouseText(text)` | The hover text next to the finger. |

## Textures of the game

`ModContent.Request('Terraria/Images/UI/...')` loads an image of the game, as
in tModLoader. Some PC images do not exist on the phone (`ButtonPlay`,
`ButtonDelete`, the scrollbar): the call throws `does not exist on mobile`;
`UIScrollbar` draws itself without them.

For text in the mod's own font, `ModContent.Request('Assets/Fonts/MyFont').Value`
is a `SpriteFont` for `DrawString` and `Utils.DrawBorderString*`
([guide 21](21-pacotes-de-fonte.md#fonts-for-a-mods-own-ui)).

## Interface layers

`ModifyInterfaceLayers(layers)` runs every frame with the game's layers in
drawing order (`'Vanilla: Resource Bars'`, `'Vanilla: Inventory'`,
`'Vanilla: Mouse Text'`...). `layers` has the names of C#'s `List<>`:
`FindIndex`, `Insert`, `RemoveAt`, `Add`, `Count`. `layer.Active = false`
hides a game layer for that frame. A `LegacyGameInterfaceLayer` draws with
the scale of its `InterfaceScaleType`: `UI` for screens (UI units), `Game`
for things over the world.

## A resource bar

The Example Mod's `ExampleResourceBar` is a `UIState` drawn in a layer
inserted before `'Vanilla: Resource Bars'`: a frame (`UIImage`), a gradient
fill drawn in `DrawSelf`, and a `UIText`. The resource itself is a
`ModPlayer` (`ExampleResourcePlayer`), spent by `ExampleCustomResourceWeapon`
and raised by `ExampleResourceAccessory`. The bar only draws while the weapon
is held (its `Draw` checks it).

## Hearts and stars: ModResourceOverlay

`ModResourceOverlay` draws over (or instead of) the hearts and mana stars.
Each piece the game draws (a heart, its frame, a star) goes through
`PreDrawResource(context)` and `PostDrawResource(context)`:

| Field of `context` | |
|---|---|
| `texture` | The game's `Asset<Texture2D>` being drawn (`Images/UI/PlayerResourceSets/FancyClassic/Heart_Fill`...). Compare with `ModContent.Request('Terraria/...')` using `bl.addressOf(a) === bl.addressOf(b)`. |
| `resourceNumber` | The heart or star index (0 is the first). |
| `drawingLife` | `true` for hearts, `false` for stars. |
| `snapshot` | `PlayerStatsSnapshot`: `Life`, `LifeMax`, `Mana`, `ManaMax`, `AmountOfLifeHearts`, `AmountOfManaStars`, `LifePerSegment`... |
| `DisplaySet` | `{ Name: 'Classic' \| 'Fancy' \| 'HorizontalBars' }`, the style chosen in the game's settings. |
| `position`, `source`, `color`, `rotation`, `origin`, `scale` | The drawing; change them and call `context.Draw()` to draw again. |

`PreDrawResource` returning `false` skips that piece. `PreDrawResourceDisplay`
/ `PostDrawResourceDisplay(snapshot, displaySet, drawingLife, textColor,
drawText)` wrap the whole bar; `drawText.value = false` hides the life text.

The Example Mod's `VanillaLifeOverlay` and `VanillaManaOverlay` draw over the
hearts and stars of its `ExampleLifeFruit` and `ExampleManaCrystal` (+10 max
life or mana each, through `ModPlayer.ModifyMaxStats`).

On the phone, life and mana are the game's `GUIHearts`/`GUIMana`: new display
sets (tModLoader's `ModResourceDisplaySet`) are not supported.

## Examples

In the Example Mod: `Common/UI/ExampleCoinsUI` (the `/coins` screen: a
draggable panel, image buttons with hover text and a coin counter),
`Common/UI/ExampleResourceUI` and `Common/UI/ResourceOverlay`.
