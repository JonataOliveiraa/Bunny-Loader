# PlayerDrawLayer

Classes exported in `Content/`, `Common/` or in the mod's entry file can
extend `PlayerDrawLayer`. Registration happens automatically, after the items
and equipment and before `Mod.Load`. Each class has its own template,
reachable through `ModContent.GetInstance(Class)`.

```js
export class MyLayer extends PlayerDrawLayer {
    GetDefaultPosition() {
        return PlayerDrawLayer.AfterParent(PlayerDrawLayers.Wings);
    }

    GetDefaultVisibility(drawInfo) {
        const player = drawInfo.drawPlayer;
        return !player.dead && !player.invis && player.wings > 0;
    }

    Draw(drawInfo) {
        const drawData = Terraria.DataStructures.DrawData.new();
        drawData['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
            Terraria.GameContent.TextureAssets.MagicPixel.Value,
            Vector2.new(drawInfo.Position.X - Terraria.Main.screenPosition.X, drawInfo.Position.Y - Terraria.Main.screenPosition.Y),
            Rectangle.new(0, 0, 1, 1), Color.new(255, 255, 255, 255),
            0, Vector2.new(0, 0), 8, drawInfo.playerEffect, 0);
        drawData.shader = drawInfo.cWings;
        ModPlayer.AddDrawData(drawInfo, drawData);
    }
}
```

## Methods and state

| Member | Contract |
|---|---|
| `GetDefaultPosition()` | Required. Returns `BeforeParent(parent)` or `AfterParent(parent)`. Asked when the plan is built, with the result cached. |
| `GetDefaultVisibility(drawInfo)` | Default `true`. Asked on every draw. `false` suppresses the layer and its children. |
| `Draw(drawInfo)` | Required. Runs at the native or custom parent's point. Use the cache to draw. |
| `SetStaticDefaults()` | Runs once with content ready. A late registration runs it before the first draw. |
| `Hide()` | Suppresses the layer and its children in the current draw. |
| `Visible` | Getter for the layer's own visibility. A hidden parent also prevents it from running. |
| `Name` | By default, the class name. `super('Name')` allows another name. |
| `FullName` | `mod-uuid/Name`, or just the name for native descriptors. |
| `Mod` | The mod responsible for the registration. |
| `BeforeParent(parent)` / `AfterParent(parent)` | Creates a position before/after the parent. |
| `register(Class)` | Manual registration. Validates the required methods, rejects duplicates and returns the template. |

The parent can be a `PlayerDrawLayers` descriptor, a registered custom class,
its instance or its `FullName`. Simple names are accepted when they identify a
layer without ambiguity. Layers from the same mod need distinct names. Classes
with `static Autoload = false` are not registered automatically and can call
`PlayerDrawLayer.register`.

Children are drawn in registration order: the earlier children, the parent,
the later children. The same mechanism accepts children of custom layers and
of nested native points, like `FrontAccFront`. Hiding the parent suppresses the
whole tree. Visibilities are restored at the end. The renderer closes access
to the layer hooks outside that draw. Nested calls restore the previous draw's
filters.

`Draw` can be called for afterimages. Use `drawInfo.shadow === 0` in the
visibility when the effect should only appear on the main player. Callback
failures are reported once and isolated. Valid data already added before a
failure stays in the cache. Unknown parents, invalid positions and cycles
disable the affected branches with a warning.

## Per-player ordering

`ModPlayer.ModifyDrawLayerOrdering(player, positions)` receives a `Map`
initialized with the custom layers' positions. Use descriptors, classes,
instances or names as keys. The positions belong to the current draw;
changing them does not change the defaults or another player's draw.

```js
ModifyDrawLayerOrdering(player, positions) {
    positions.set(MyLayer, PlayerDrawLayer.BeforeParent(PlayerDrawLayers.Head));
}

HideDrawLayers(player, drawInfo) {
    ModContent.GetInstance(MyLayer).Hide();
}
```

Removing an instance's position with `positions.delete(instance)` suppresses
its tree in that draw. When a native layer is reordered, its custom children
move along with the data it produced. Relations between native layers and
custom children are resolved through the tree's native parent, keeping the
whole group.

## Wings glowmask

The [WingsGlowmask example](../../samples/ExampleMod/content/Common/Players/WingsGlowmask.js)
implements a texture and color per slot, a position next to the wings, the
animation frame and the `cWings` shader. Register the data with content
ready, for example in the mod's `PostSetupContent`:

```js
import { WingsGlowmask } from './Common/Players/WingsGlowmask.js';
import { ExampleWings } from './Content/Items/Accessories/ExampleWings.js';

PostSetupContent() {
    WingsGlowmask.RegisterData(ModContent.GetInstance(ExampleWings).Item.wingSlot, {
        Texture: ModContent.Request('Content/Items/Accessories/ExampleWings_Wings_Glow').Value,
        Color: Color.new(255, 255, 255, 255),
        Frames: 4
    });
}
```

Provide your own `_Wings_Glow` texture; it is not included in ExampleMod.
`Frames` has to match the animation. The default offset `{ X: -9, Y: 2 }`
reproduces the example's geometry and can be replaced through `Offset`. Wings
with special drawing may need another offset. `RegisterData` keeps the first
configuration of each slot.

The data inserted must be the `DrawData` built for the texture, not the `Ref`
holding the configuration. `ModPlayer.AddDrawData` updates the native counter.
The renderer applies transformations, order and shaders after assembling the
layers. Calling `DrawData.Draw(Main.spriteBatch)` directly draws early and
skips that flow.

## Compatibility and cost

The behavior was checked against the local tModLoader copy, in
`PlayerDrawLayer.cs`, `PlayerDrawLayer.Positions.cs` and
`ExamplePlayerDrawLayer.cs`. The
[public source](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/PlayerDrawLayer.cs)
describes the same children and cache contract. Bunny Loader uses mobile's
`DrawData[]` with a counter, instead of `List<DrawData>`.

The default plan is rebuilt when new layers are registered. Visibility and
dynamic ordering are evaluated per draw. Only the native hooks that need to
run children, hide a layer or record data for reordering enter JavaScript. A
layer attached only to the wings activates the wings hook. Invisible layers do
not activate their parents' hooks. The individual filters are only updated
when the set of active layers changes. Outside the renderer, the native
`whileIn` condition keeps the hooks out of JavaScript even when their filters
are cached. Inserting into the cache uses the existing array. Native
reordering reuses the block copy and only clones the active section when the
effective order changes.

This implementation covers `BeforeParent`, `AfterParent`, visibility,
drawing, registration and the ordering shared with ModPlayer. `Between`,
`Multiple`, `Transformation`, `IsHeadLayer` and drawing the minimap icon are
not implemented.

Tests and results: [PlayerDrawLayer suite](../../tools/tests/playerdrawlayers/README.md).
