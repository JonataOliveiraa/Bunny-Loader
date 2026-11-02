# Dust: your own particles

`ModDust` is tModLoader's `ModDust`: a new dust type with its own texture
(or a frame of the game's dust sheet) and, optionally, its own behavior and
drawing. Spawn it with the game's `Dust.NewDust` / `NewDustDirect` /
`NewDustPerfect`, passing `ModContent.DustType(YourDust)`.

```js
// Content/Dusts/Sparkle.js (+ Sparkle.png next to it)
const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];

export class Sparkle extends ModDust {
    OnSpawn(dust) {
        dust.velocity.X *= 0.4;
        dust.velocity.Y *= 0.4;
        dust.noGravity = true;
        dust.noLight = true;
        dust.scale *= 1.5;
    }

    Update(dust) {
        dust.position.X += dust.velocity.X;
        dust.position.Y += dust.velocity.Y;
        dust.rotation += dust.velocity.X * 0.15;
        dust.scale *= 0.99;
        AddLight(dust.position, 0.35 * dust.scale, 0.35 * dust.scale, 0.35 * dust.scale);
        if (dust.scale < 0.5) dust.active = false;
        return false;   // false: the game does not move the dust itself
    }
}

// Anywhere: spawn
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
NewDust(player.position, player.width, player.height, ModContent.DustType(Sparkle), 0, 0, 0, Color.White, 1);
```

Tile and wall `DustType`, `MountData.spawnDust` and anything else that takes
a dust type accept a mod dust too.

## The texture

Like every content class, the texture is the PNG with the class name next to
the file (`Content/Dusts/Sparkle.png`), or `get Texture() { return 'path'; }`.
The usual sheet is three 8×8 frames stacked 10 px apart (10×30); on spawn the
frame is set to one of them (`frame.X = 0`, `frame.Y` 0, 10 or 20), as in
tModLoader. Set `dust.frame` in `OnSpawn` for other sizes.

`get Texture() { return null; }` (or `Texture = null`) uses the **game's dust
sheet**. Pick the look in `OnSpawn` with `dust.HackFrame(DustID.Electric)`.

`this.Texture2D` is the `Asset<Texture2D>` the dust draws with (the game's
sheet for a `null` texture), for `PreDraw`.

## The hooks

| Hook | When | Return |
|---|---|---|
| `SetStaticDefaults()` | Once, when the content is ready. Set `UpdateType` here. | |
| `OnSpawn(dust)` | Right after `Dust.NewDust` creates it. | |
| `Update(dust)` | Every update while it is active. | `false`: the game does not update it this tick (the usual with your own movement). `true` (default): the game's default update runs too. |
| `MidUpdate(dust)` | After the game's update. | `true`: undoes the game's horizontal slow-down (`velocity.X *= 0.99`). |
| `GetAlpha(dust, lightColor)` | When the game asks for the color to draw. | A `Color`, or `null` for the game's. `dust.fullBright` wins over it. |
| `PreDraw(dust)` | Before the mod dusts are drawn, with `Main.spriteBatch` open (draw trails here). | `false`: the game does not draw this one. |

`UpdateType = DustID.X` copies the behavior of a game dust: during the game's
update the dust is of that type, and it goes back to yours right after.

```js
export class ExampleSolutionDust extends ModDust {
    SetStaticDefaults() { this.UpdateType = Terraria.ID.DustID.PureSpray; }
}
```

## Killing a dust

`dust.active = false` works, as in tModLoader. Terraria Mobile keeps free
dusts in a stack that only `Dust.SetNotActive(dust)` refills; writing
`active` directly would lose the slot forever and, after a while, no dust
would spawn at all. Bunny Loader returns the slot right away for dusts killed
inside `Update`, `MidUpdate` and `PreDraw`, and checks the stack every frame
for the rest (the log warns when it had to rebuild it). `Dust.SetNotActive`
is still the cleanest way outside those hooks.

In total darkness the game deletes a dust whose drawing color comes out black
(the same rule as on PC): use `dust.noLight`, `dust.fullBright` or `GetAlpha`
for dusts that must survive in the dark.

## Cost

Measured on the emulator with 1500 dusts at once: dusts without JS hooks cost
about the same as the game's (each texture adds one extra drawing pass, about
0.2 ms). An `Update` in JS costs about 0.8 µs per dust when empty and about
3 µs when it reads and writes several fields (each field goes through the
bridge). A few hundred dusts with `Update` are fine; thousands of them every
frame are not.

## Notes

- The calls into JS are batched: one call per frame for all `Update`s, one
  for all `PreDraw`s. `OnSpawn` and `GetAlpha` are per dust, only for the
  types that override them.
- Mod dusts are drawn after the game's dusts in the same frame.
- Armor shaders on dusts (`dust.shader`, dyes) are not supported for mod dusts.
- The Example Mod has the five dusts of tModLoader's ExampleMod in
  `Content/Dusts`, used by its furniture, wall, torch, tools, yoyo, spear,
  drill, sentry, ExamplePerson, mount and water.
